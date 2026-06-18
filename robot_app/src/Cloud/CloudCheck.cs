// 真雲端自測：不需機器人。設好金鑰後 `dotnet run -- --cloud-test`。
//  1) Azure TTS 合成印尼語 → 存成可聽的 wav（並嘗試 afplay 播放）
//  2) Azure TTS→STT 來回：合成「Saya di kanan」再辨識，應辨識出含 "kanan"
//  3) (若有 LLM 金鑰) Claude 生成一句印尼語糾錯提示
#if !UNITY
using System;
using System.IO;
using System.Threading.Tasks;

namespace KebbiBrain.Cloud
{
    public static class CloudCheck
    {
        public static async Task<int> RunAsync(Action<string> log)
        {
            log("========== 真雲端自測（Azure 印尼語 + Claude） ==========");
            int fail = 0;

            if (string.IsNullOrEmpty(Config.SpeechKey) || string.IsNullOrEmpty(Config.SpeechRegion))
            {
                log("  ❌ 缺少 KEBBI_SPEECH_KEY 或 KEBBI_SPEECH_REGION（見 金鑰申請步驟.md）");
                return 1;
            }
            log("  ℹ 區域=" + Config.SpeechRegion + "  語音=" + Config.SpeechVoice);

            var speech = new AzureSpeech(Config.SpeechKey, Config.SpeechRegion);
            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "cloud_audio_out");
            Directory.CreateDirectory(outDir);

            // 1) TTS：合成一句鼓勵語，存檔讓老師聽聲音品質
            try
            {
                byte[] wav = await speech.SynthesizeWavAsync("Benar! Bagus sekali!", "id-ID", Config.SpeechVoice);
                string path = Path.Combine(outDir, "check_benar.wav");
                File.WriteAllBytes(path, wav);
                log("  ✅ TTS 合成成功（" + wav.Length + " bytes）→ " + path + "（請播放確認印尼語發音）");
                TryPlay(path);
            }
            catch (Exception e) { fail++; log("  ❌ TTS 失敗：" + e.Message); }

            // 2) TTS→STT 來回：合成「Saya di kanan」再辨識，應含 "kanan"
            try
            {
                byte[] wav = await speech.SynthesizeWavAsync("Saya di kanan.", "id-ID", Config.SpeechVoice);
                File.WriteAllBytes(Path.Combine(outDir, "check_saya.wav"), wav);
                string text = await speech.RecognizeWavAsync(wav, "id-ID");
                bool ok = text != null && text.ToLowerInvariant().Contains("kanan");
                log((ok ? "  ✅" : "  ⚠") + " STT 辨識結果：「" + text + "」" + (ok ? "（含 kanan）" : "（未含 kanan，請檢查）"));
                if (!ok) fail++;
            }
            catch (Exception e) { fail++; log("  ❌ STT 失敗：" + e.Message); }

            // 3) Claude 糾錯提示
            if (string.IsNullOrEmpty(Config.LlmKey))
            {
                log("  ⏭ 未設 KEBBI_LLM_KEY，略過 Claude 測試（遊戲可先用內建提示）");
            }
            else
            {
                try
                {
                    var llm = KebbiBrain.KebbiFactory.CreateLlm(log);
                    string tip = await llm.AskAsync(
                        "Kamu guru bahasa Indonesia yang ramah untuk anak SMA.",
                        "Murid bilang 'kanan' padahal sebenarnya di 'kiri'. Beri satu kalimat tip singkat dalam bahasa Indonesia.");
                    log("  ✅ LLM 提示：「" + tip + "」");
                }
                catch (Exception e) { fail++; log("  ❌ LLM 失敗：" + e.Message); }
            }

            log("");
            log(fail == 0 ? "結果：全部通過 ✅（雲端管線就緒）" : ("結果：" + fail + " 項失敗 ❌"));
            log("======================================================");
            return fail == 0 ? 0 : 1;
        }

        private static void TryPlay(string path)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("afplay", "\"" + path + "\"") { UseShellExecute = false };
                var p = System.Diagnostics.Process.Start(psi);
                if (p != null) p.WaitForExit();
            }
            catch { }
        }
    }
}
#endif
