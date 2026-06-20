using System;
using System.Text;

namespace KebbiBrain.App
{
    // 從 server 收到的一則 Live 訊息解析結果。
    public struct LiveServerMsg
    {
        public string AudioBase64;     // serverContent.modelTurn.parts[].inlineData.data(24kHz PCM,可能空)
        public string InputText;        // serverContent.inputTranscription.text(使用者說的,可能空)
        public string OutputText;       // serverContent.outputTranscription.text(Kebbi 回的,可能空)
        public bool TurnComplete;       // 這一輪講完了
        public bool Interrupted;        // 被使用者打斷(barge-in)→ 該停掉正在播的音
        public bool SetupComplete;      // setupComplete:握手完成
        public bool GoAway;             // 連線即將結束(該重連)
    }

    // Gemini Live API(即時語音對話)協定層。純 C#(無 UnityEngine、無第三方 JSON lib)→ 主控台可單測、Unity 也直接用。
    //   wss://generativelanguage.googleapis.com/ws/...BidiGenerateContent?key=KEY
    //   流程:連線 → 送 setup(model+AUDIO+systemInstruction+逐字稿)→ 收 setupComplete →
    //         每 100ms 送 realtimeInput.audio(16-bit PCM 16kHz mono LE,base64)→
    //         收 serverContent(24kHz PCM 音訊 + 原文/譯文逐字稿 + turnComplete/interrupted)。
    //   turn-taking / VAD / barge-in 由模型原生處理(automaticActivityDetection 預設開)。
    // 模型:gemini-3.1-flash-live-preview(audio-to-audio;只支援 AUDIO 輸出,不支援 TEXT)。
    // 來源:ai.google.dev/gemini-api/docs/live-api、/api/live。
    public static class GeminiLiveProtocol
    {
        public const string DefaultModel = "gemini-3.1-flash-live-preview";
        public const string AudioMimeIn = "audio/pcm;rate=16000";  // 上行 MIME
        public const int InputRate = 16000;                         // 上行取樣率
        public const int OutputRate = 24000;                        // 下行取樣率(模型回的音訊)

        public static string WsUrl(string apiKey)
            => "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key=" + (apiKey ?? "");

        // setup 訊息:對話用(非翻譯)→ 一個 persona system instruction + AUDIO 輸出 + 原文/譯文逐字稿。
        public static string BuildSetupJson(string model, string systemInstruction)
        {
            string m = string.IsNullOrEmpty(model) ? DefaultModel : model;
            if (!m.StartsWith("models/")) m = "models/" + m;
            var sb = new StringBuilder();
            sb.Append("{\"setup\":{");
            sb.Append("\"model\":\"").Append(m).Append("\",");
            sb.Append("\"generationConfig\":{\"responseModalities\":[\"AUDIO\"]},");
            if (!string.IsNullOrEmpty(systemInstruction))
                sb.Append("\"systemInstruction\":{\"parts\":[{\"text\":\"").Append(Esc(systemInstruction)).Append("\"}]},");
            sb.Append("\"inputAudioTranscription\":{},");
            sb.Append("\"outputAudioTranscription\":{}");
            sb.Append("}}");
            return sb.ToString();
        }

        // 上行麥克風音訊(realtimeInput.audio;mediaChunks 已 deprecated 不用)。pcm16=16-bit LE PCM 16kHz mono。
        public static string BuildAudioChunkJson(byte[] pcm16, int len)
        {
            string b64 = Convert.ToBase64String(pcm16, 0, len);
            return "{\"realtimeInput\":{\"audio\":{\"data\":\"" + b64 + "\",\"mimeType\":\"" + AudioMimeIn + "\"}}}";
        }
        public static string BuildAudioChunkJson(byte[] pcm16) => BuildAudioChunkJson(pcm16, pcm16 == null ? 0 : pcm16.Length);

        // 用文字當一輪輸入(測試/不用麥克風時)。
        public static string BuildTextTurnJson(string text)
            => "{\"clientContent\":{\"turns\":[{\"role\":\"user\",\"parts\":[{\"text\":\"" + Esc(text) + "\"}]}],\"turnComplete\":true}}";

        // 告知本輪麥克風串流結束(可選)。
        public const string AudioStreamEndJson = "{\"realtimeInput\":{\"audioStreamEnd\":true}}";

        // 解析一則 server 訊息。手寫極簡掃描(不依賴 JSON lib,Unity/主控台一致)。
        public static LiveServerMsg TryParseServer(string json)
        {
            var r = new LiveServerMsg();
            if (string.IsNullOrEmpty(json)) return r;
            if (json.IndexOf("\"setupComplete\"", StringComparison.Ordinal) >= 0) r.SetupComplete = true;
            if (json.IndexOf("\"goAway\"", StringComparison.Ordinal) >= 0) r.GoAway = true;
            if (json.IndexOf("\"turnComplete\":true", StringComparison.Ordinal) >= 0) r.TurnComplete = true;
            if (json.IndexOf("\"interrupted\":true", StringComparison.Ordinal) >= 0) r.Interrupted = true;
            r.OutputText = ScanNestedText(json, "\"outputTranscription\"");
            r.InputText = ScanNestedText(json, "\"inputTranscription\"");
            r.AudioBase64 = ScanInlineAudio(json);
            return r;
        }

        // 找 <key>:{... "text":"..." ...} 裡的 text(用於 input/outputTranscription)。
        private static string ScanNestedText(string s, string key)
        {
            int k = s.IndexOf(key, StringComparison.Ordinal);
            if (k < 0) return null;
            int t = s.IndexOf("\"text\"", k, StringComparison.Ordinal);
            if (t < 0) return null;
            int colon = s.IndexOf(':', t);
            if (colon < 0) return null;
            return ReadString(s, colon + 1);
        }

        // 找 inlineData.data 的 base64(24kHz 音訊);base64 無引號/反斜線,讀到下一個 " 即止。
        private static string ScanInlineAudio(string s)
        {
            int i = s.IndexOf("\"inlineData\"", StringComparison.Ordinal);
            if (i < 0) i = s.IndexOf("\"inline_data\"", StringComparison.Ordinal);
            if (i < 0) return null;
            int d = s.IndexOf("\"data\"", i, StringComparison.Ordinal);
            if (d < 0) return null;
            int colon = s.IndexOf(':', d);
            if (colon < 0) return null;
            return ReadString(s, colon + 1);
        }

        // 從 idx 起跳過空白與開頭引號,讀一個 JSON 字串(處理 \" \\ \n 等跳脫)。
        private static string ReadString(string s, int idx)
        {
            int i = idx;
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n')) i++;
            if (i >= s.Length || s[i] != '"') return null;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[i + 1];
                    switch (n)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '/': sb.Append('/'); break;
                        default: sb.Append(n); break;
                    }
                    i += 2; continue;
                }
                if (c == '"') break;
                sb.Append(c); i++;
            }
            return sb.ToString();
        }

        // float[-1,1] → 16-bit LE PCM bytes(寫進 dst,回傳寫了幾 byte)。
        public static int FloatToPcm16(float[] samples, int count, byte[] dst)
        {
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                float f = samples[i]; if (f > 1f) f = 1f; else if (f < -1f) f = -1f;
                short v = (short)(f * 32767f);
                dst[n++] = (byte)(v & 0xff);
                dst[n++] = (byte)((v >> 8) & 0xff);
            }
            return n;
        }

        // 16-bit LE PCM bytes → float[-1,1](回傳 sample 數)。
        public static int Pcm16ToFloat(byte[] pcm, int len, float[] dst)
        {
            int n = 0;
            for (int i = 0; i + 1 < len; i += 2)
            {
                short v = (short)(pcm[i] | (pcm[i + 1] << 8));
                dst[n++] = v / 32768f;
            }
            return n;
        }

        // 線性重採樣(srcRate→dstRate,單聲道 float)。回傳輸出 sample 數。
        public static float[] Resample(float[] src, int srcCount, int srcRate, int dstRate)
        {
            if (srcRate == dstRate || srcCount == 0) { var c = new float[srcCount]; Array.Copy(src, c, srcCount); return c; }
            int dstCount = (int)((long)srcCount * dstRate / srcRate);
            var outp = new float[dstCount];
            double step = (double)srcRate / dstRate;
            for (int i = 0; i < dstCount; i++)
            {
                double pos = i * step;
                int i0 = (int)pos; double frac = pos - i0;
                int i1 = i0 + 1 < srcCount ? i0 + 1 : i0;
                outp[i] = (float)(src[i0] * (1 - frac) + src[i1] * frac);
            }
            return outp;
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
