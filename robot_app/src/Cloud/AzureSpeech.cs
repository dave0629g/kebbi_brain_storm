// Azure AI Speech 印尼語 STT/TTS（REST）。主控台可跑，免機器人。
#if !UNITY
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KebbiBrain.Cloud
{
    public sealed class AzureSpeech
    {
        private readonly string _key;
        private readonly string _region;

        public AzureSpeech(string key, string region) { _key = key; _region = region; }

        // 文字 → 印尼語語音（回傳 16k/mono/16bit WAV bytes，可直接存檔播放，也可直接餵給 STT）
        public async Task<byte[]> SynthesizeWavAsync(string text, string lang, string voice)
        {
            string url = "https://" + _region + ".tts.speech.microsoft.com/cognitiveservices/v1";
            string ssml = "<speak version='1.0' xml:lang='" + lang + "'>" +
                          "<voice xml:lang='" + lang + "' name='" + voice + "'>" + XmlEscape(text) + "</voice></speak>";
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Ocp-Apim-Subscription-Key", _key);
            req.Headers.Add("X-Microsoft-OutputFormat", "riff-16khz-16bit-mono-pcm");
            req.Headers.Add("User-Agent", "kebbi-brain");
            req.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");
            var resp = await Http.Client.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                throw new Exception("Azure TTS 失敗 " + (int)resp.StatusCode + "：" + await resp.Content.ReadAsStringAsync());
            return await resp.Content.ReadAsByteArrayAsync();
        }

        // 印尼語語音(WAV bytes) → 文字
        public async Task<string> RecognizeWavAsync(byte[] wav, string lang)
        {
            string url = "https://" + _region + ".stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language=" + lang;
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Ocp-Apim-Subscription-Key", _key);
            req.Headers.Add("Accept", "application/json");
            var content = new ByteArrayContent(wav);
            // .NET 的 MediaTypeHeaderValue.Parse 會拒絕 codecs=audio/pcm(含斜線);直接塞原字串給 Azure。
            content.Headers.TryAddWithoutValidation("Content-Type", "audio/wav; codecs=audio/pcm; samplerate=16000");
            req.Content = content;
            var resp = await Http.Client.SendAsync(req);
            string json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception("Azure STT 失敗 " + (int)resp.StatusCode + "：" + json);
            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("DisplayText", out var dt)) return dt.GetString() ?? "";
                return "";
            }
        }

        private static string XmlEscape(string s)
        {
            return (s ?? "")
                .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("'", "&apos;");
        }
    }
}
#endif
