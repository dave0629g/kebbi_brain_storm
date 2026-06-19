// IVoice 的真雲端實作：說＝Azure 印尼語 TTS（存檔 + 嘗試用 afplay 播放）；聽＝腳本佇列（主控台 demo 無麥克風）。
#if !UNITY
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.Cloud
{
    public sealed class AzureVoice : IVoice
    {
        private readonly AzureSpeech _speech;
        private readonly string _voice;
        private readonly Action<string> _out;
        private readonly Queue<string> _heard = new Queue<string>();
        private readonly string _outDir;
        private int _seq;

        public AzureVoice(AzureSpeech speech, string voice, Action<string> output)
        {
            _speech = speech; _voice = voice; _out = output ?? Console.WriteLine;
            _outDir = Path.Combine(Directory.GetCurrentDirectory(), "cloud_audio_out");
            Directory.CreateDirectory(_outDir);
        }

        // 主控台 demo：預先排入「學生會說的話」
        public void EnqueueHeard(string text) => _heard.Enqueue(text);

        public async Task SpeakAsync(string text, string lang = "id-ID")
        {
            string voice = KebbiBrain.Config.VoiceForLang(lang, _voice);   // 依語言挑聲線(中文用中文聲、印尼語用印尼聲)
            byte[] wav = await _speech.SynthesizeWavAsync(text, lang, voice);
            string path = Path.Combine(_outDir, "say_" + (++_seq).ToString("00") + ".wav");
            File.WriteAllBytes(path, wav);
            _out("   🗣️  [Kebbi 說/" + lang + "/" + voice + "] 「" + text + "」 → " + path);
            TryPlay(path);
        }

        public Task<string> ListenAsync(string lang = "id-ID")
        {
            string heard = _heard.Count > 0 ? _heard.Dequeue() : "";
            _out("   🎧 [聽到(腳本)/" + lang + "] 「" + heard + "」");
            return Task.FromResult(heard);
        }

        // macOS 用 afplay 直接播；其他平台或失敗就略過（檔案已存好）
        private static void TryPlay(string path)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("afplay", "\"" + path + "\"") { UseShellExecute = false };
                var p = System.Diagnostics.Process.Start(psi);
                if (p != null) p.WaitForExit();
            }
            catch { /* 沒有 afplay 就只存檔 */ }
        }
    }
}
#endif
