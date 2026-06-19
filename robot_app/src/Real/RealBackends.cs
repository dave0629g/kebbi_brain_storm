// ============================================================================
//  實機（Unity + NuwaUnity SDK + 雲端）後端。整檔 #if UNITY：主控台不編譯。
//  ⚠️ 這台沒有 Unity，無法在此編譯/執行；以下依 Unity 官方 API 與「已在 --cloud-test
//     驗證過的 REST 形狀」撰寫，最終以 Unity 實機編譯為準。接入步驟見 UNITY_接入指南.md。
//  到 Unity：Player Settings → Scripting Define Symbols 加入 UNITY，Config.Target=Real。
//  依實測 SDK：轉頭用「讀 DOA + 自寫 NeckZ」(不用 turnToDOA)；語音走雲端(內建無印尼語)。
// ============================================================================
#if UNITY
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using KebbiBrain.Hardware;

namespace KebbiBrain.Real
{
    // ── UnityWebRequest → Task 小工具（免 UniTask；用 op.completed 在主執行緒回呼）──
    internal static class UnityHttp
    {
        public static Task SendAsync(UnityWebRequest req)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op = req.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }
    }

    // ── WAV(16-bit PCM mono) ↔ AudioClip ──
    internal static class WavUtil
    {
        public static AudioClip ToAudioClip(byte[] wav, string name = "tts")
        {
            int sampleRate = BitConverter.ToInt32(wav, 24);
            int dataOffset = 44, dataLen = wav.Length - 44;
            for (int i = 12; i < wav.Length - 8; i++)
                if (wav[i] == 'd' && wav[i + 1] == 'a' && wav[i + 2] == 't' && wav[i + 3] == 'a')
                { dataLen = BitConverter.ToInt32(wav, i + 4); dataOffset = i + 8; break; }
            int n = dataLen / 2;
            var f = new float[n];
            for (int i = 0; i < n; i++) f[i] = BitConverter.ToInt16(wav, dataOffset + i * 2) / 32768f;
            var clip = AudioClip.Create(name, n, 1, sampleRate <= 0 ? 16000 : sampleRate, false);
            clip.SetData(f, 0);
            return clip;
        }

        public static byte[] FromAudioClip(AudioClip clip)
        {
            int n = clip.samples;
            var f = new float[n * clip.channels];
            clip.GetData(f, 0);
            int sr = clip.frequency;
            using (var ms = new System.IO.MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms))
            {
                int dataBytes = n * 2; // 取單聲道
                bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + dataBytes);
                bw.Write(Encoding.ASCII.GetBytes("WAVE")); bw.Write(Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16); bw.Write((short)1); bw.Write((short)1);
                bw.Write(sr); bw.Write(sr * 2); bw.Write((short)2); bw.Write((short)16);
                bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(dataBytes);
                for (int i = 0; i < n; i++) bw.Write((short)(Mathf.Clamp(f[i * clip.channels], -1f, 1f) * 32767));
                bw.Flush();
                return ms.ToArray();
            }
        }
    }

    // ── 身體：NuwaRobotAPI（方法名已對「真 NuwaSDK 2.1.0.08 aar」反查校正，見下）──
    //   校正來源：用參考專案 ~/Projects/UnityKebbi 內附的 NuwaSDK aar 2.1.0.08 以 javap 反查實際 public 方法(2026-06-18)。
    //   修正：① 馬達設角無 setMotorPositionInDegree → 真 API 是 ctlMotor(motorId, degrees, speed)。
    //         ② 讀角拼字 Possition→Position：真 API 是 getMotorPresentPositionInDegree(int)。
    //   不變(反查確認存在)：getInst()、move(float)、turn(float)、getDirectionOfDOA()。
    //   馬達 ID：KebbiMotor enum 值 == SDK MOTOR_* 常數(NECK_Y=1…LEFT_ELBOW_Y=10)，已驗證一致(見 T_NuwaMotorIds)。
    //   ⚠ ctlMotor 的 (degrees,speed) 引數語意由 aar 簽章+degree 讀取器推得，仍以實機往復量測為準(必測⑥)。
    public sealed class UnityKebbiBody : IKebbiBody
    {
        private readonly AndroidJavaObject _api;

        public UnityKebbiBody()
        {
            using (var cls = new AndroidJavaClass("com.nuwarobotics.service.agent.NuwaRobotAPI"))
                _api = cls.CallStatic<AndroidJavaObject>("getInst");
        }

        public void SetMotor(KebbiMotor m, float degrees, float speed = 50f)
            => _api.Call("ctlMotor", (int)m, degrees, speed); // ctlMotor(int motorId, float degrees, float speed)
        public float GetMotor(KebbiMotor m)
            => _api.Call<float>("getMotorPresentPositionInDegree", (int)m);
        public float ReadDoaDegrees()
            => _api.Call<float>("getDirectionOfDOA"); // ⚠️解析度/360°/非語音未驗

        public bool CanMove { get; set; } = true; // H201 請設 false
        public void Move(float metersPerSec) => _api.Call("move", metersPerSec);
        public void Turn(float degPerSec) => _api.Call("turn", degPerSec);
        public void StopWheels() { _api.Call("move", 0f); _api.Call("turn", 0f); }

        // 官方 NUWA SDK 馬達表 neck_z = ±40°；上實機再以 ctlMotor+getMotorPresentPositionInDegree 往復核對零點/正負向。
        public float NeckZMinDeg => -40f;
        public float NeckZMaxDeg => 40f;
    }

    // ── 語音：Azure 印尼語 TTS/STT（UnityWebRequest）──
    public sealed class UnityVoice : IVoice
    {
        private readonly string _key, _region, _voice;
        private AudioSource _speaker;

        public UnityVoice(string provider, string speechKey, string region, string voice)
        { _key = speechKey; _region = region; _voice = string.IsNullOrEmpty(voice) ? "id-ID-GadisNeural" : voice; }

        private AudioSource Speaker()
        {
            if (_speaker == null)
            {
                var go = new GameObject("KebbiAudio");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _speaker = go.AddComponent<AudioSource>();
            }
            return _speaker;
        }

        public async Task SpeakAsync(string text, string lang = "id-ID")
        {
            string url = "https://" + _region + ".tts.speech.microsoft.com/cognitiveservices/v1";
            string voice = KebbiBrain.Config.VoiceForLang(lang, _voice);   // 依語言挑聲線(中文用中文聲、印尼語用印尼聲)
            string ssml = "<speak version='1.0' xml:lang='" + lang + "'><voice xml:lang='" + lang +
                          "' name='" + voice + "'>" + XmlEscape(text) + "</voice></speak>";
            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(ssml));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/ssml+xml");
                req.SetRequestHeader("Ocp-Apim-Subscription-Key", _key);
                req.SetRequestHeader("X-Microsoft-OutputFormat", "riff-16khz-16bit-mono-pcm");
                req.SetRequestHeader("User-Agent", "kebbi-brain");
                await UnityHttp.SendAsync(req);
                if (req.result != UnityWebRequest.Result.Success) { Debug.LogError("Azure TTS: " + req.error); return; }
                var clip = WavUtil.ToAudioClip(req.downloadHandler.data);
                var src = Speaker(); src.clip = clip; src.Play();
                // 等播畢才返回:多機對話/辯論的「說完才交棒」要精準——對方據此才知道「我停下來了」才開口。
                float dur = clip != null ? clip.length : 0f;
                if (dur > 0f) await System.Threading.Tasks.Task.Delay((int)(dur * 1000f) + 120);
            }
        }

        public async Task<string> ListenAsync(string lang = "id-ID")
        {
            // ⚠️ 先決:系統 wakeup 持麥時能否搶麥(見 UNITY_接入指南.md 必測①)。必要時先 Nuwa.stopListen()。
            string mic = (Microphone.devices != null && Microphone.devices.Length > 0) ? Microphone.devices[0] : null;
            if (mic == null) { Debug.LogError("無麥克風裝置"); return ""; }
            int seconds = 4;
            var clip = Microphone.Start(mic, false, seconds, 16000);
            await Task.Delay(seconds * 1000);
            Microphone.End(mic);
            byte[] wav = WavUtil.FromAudioClip(clip);

            string url = "https://" + _region + ".stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language=" + lang;
            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(wav);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "audio/wav; codecs=audio/pcm; samplerate=16000");
                req.SetRequestHeader("Ocp-Apim-Subscription-Key", _key);
                req.SetRequestHeader("Accept", "application/json");
                await UnityHttp.SendAsync(req);
                if (req.result != UnityWebRequest.Result.Success) { Debug.LogError("Azure STT: " + req.error); return ""; }
                var r = JsonUtility.FromJson<SttResp>(req.downloadHandler.text);
                return r != null ? (r.DisplayText ?? "") : "";
            }
        }

        [Serializable] private class SttResp { public string RecognitionStatus; public string DisplayText; }
        private static string XmlEscape(string s) => (s ?? "")
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
    }

    // ── LLM：依金鑰前綴自動選 OpenAI / Anthropic（UnityWebRequest）──
    public sealed class UnityLlm : ILlm
    {
        private readonly string _key, _provider;
        public UnityLlm(string provider, string key)
        {
            _key = key;
            _provider = !string.IsNullOrEmpty(provider) ? provider : (key != null && key.StartsWith("sk-ant") ? "anthropic" : "openai");
        }

        public async Task<string> AskAsync(string system, string user)
        {
            return _provider == "anthropic" ? await Anthropic(system, user) : await OpenAi(system, user);
        }

        private async Task<string> OpenAi(string system, string user)
        {
            var body = new OAReq { model = "gpt-4o-mini", max_tokens = 256, messages = new[] { new Msg { role = "system", content = system }, new Msg { role = "user", content = user } } };
            using (var req = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + _key);
                await UnityHttp.SendAsync(req);
                if (req.result != UnityWebRequest.Result.Success) { Debug.LogError("OpenAI: " + req.error + " " + req.downloadHandler.text); return ""; }
                var r = JsonUtility.FromJson<OAResp>(req.downloadHandler.text);
                return (r != null && r.choices != null && r.choices.Length > 0) ? (r.choices[0].message.content ?? "").Trim() : "";
            }
        }

        private async Task<string> Anthropic(string system, string user)
        {
            var body = new AnReq { model = "claude-opus-4-8", max_tokens = 256, system = system, messages = new[] { new Msg { role = "user", content = user } } };
            using (var req = new UnityWebRequest("https://api.anthropic.com/v1/messages", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("x-api-key", _key);
                req.SetRequestHeader("anthropic-version", "2023-06-01");
                await UnityHttp.SendAsync(req);
                if (req.result != UnityWebRequest.Result.Success) { Debug.LogError("Anthropic: " + req.error + " " + req.downloadHandler.text); return ""; }
                var r = JsonUtility.FromJson<AnResp>(req.downloadHandler.text);
                if (r != null && r.content != null)
                    foreach (var b in r.content) if (b.type == "text") return (b.text ?? "").Trim();
                return "";
            }
        }

        [Serializable] private class Msg { public string role; public string content; }
        [Serializable] private class OAReq { public string model; public int max_tokens; public Msg[] messages; }
        [Serializable] private class OAMsg { public string content; }
        [Serializable] private class OAChoice { public OAMsg message; }
        [Serializable] private class OAResp { public OAChoice[] choices; }
        [Serializable] private class AnReq { public string model; public int max_tokens; public string system; public Msg[] messages; }
        [Serializable] private class AnBlock { public string type; public string text; }
        [Serializable] private class AnResp { public AnBlock[] content; public string stop_reason; }
    }
}
#endif
