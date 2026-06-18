using System.Threading.Tasks;
using System;

namespace KebbiBrain.Hardware
{
    // 掛在「被控」那台 Kebbi:訂閱 IRobotLink,收到機身命令(BC|…)就套到本機 body、語音命令(VC|…)就用本機 voice 說;
    // 其餘訊息(HANDOFF/CUE/READY…)轉交 alsoHandle(讓被控機同時參與遊戲交棒邏輯)。
    // 與 RemoteBodyProxy/RemoteVoiceProxy 搭配,即構成「中控驅動 + 被控執行(動 + 說)」的真機多台分散式控制。純 C#,可自測。
    // ⚠ IRobotLink.OnMessage 是「單一 handler、後者覆寫」語意 → 被控機所有訊息都必須走這條 handler,再經 alsoHandle 分流。
    //   分派鏈(前綴互斥):BC|… 機身 → VC|… 語音 → 其餘轉交。localVoice/ackVoiceDone 為新增可選參數,放最後以相容既有呼叫端。
    // ackVoiceDone=true:收到 VC|SAY 時「等本機 voice 播畢」再回 VC|DONE 給中控 → 配合 RemoteVoiceProxy 的等播畢模式做「說完才交棒」。
    public sealed class BodyCommandReceiver
    {
        public BodyCommandReceiver(IRobotLink link, IKebbiBody localBody,
                                   Action<string, string> alsoHandle = null, IVoice localVoice = null,
                                   bool ackVoiceDone = false)
        {
            link.OnMessage((from, text) =>
            {
                if (BodyCommand.TryApply(text, localBody)) return;   // BC|… 機身命令
                if (localVoice != null && VoiceCommand.TryParseSay(text, out var lang, out var say)) // VC|SAY 語音命令
                {
                    if (ackVoiceDone) _ = SpeakThenAckAsync(localVoice, link, from, say, lang); // 播畢回 VC|DONE
                    else _ = localVoice.SpeakAsync(say, lang);                                  // fire-and-forget
                    return;
                }
                alsoHandle?.Invoke(from, text);                      // 其餘訊息轉交
            });
        }

        // 等本機 voice 真的播畢,再回 VC|DONE 給中控(讓中控的 RemoteVoiceProxy 等播畢模式得以「說完才交棒」)。
        // 硬化:播放成敗都回 DONE(中控別白等逾時);最外層 try/catch 吞掉 link 斷線等例外,避免 fire-and-forget Task 留未觀察例外
        //      (UnobservedTaskException)。播放失敗時中控收到 DONE 照樣續行 → graceful。
        private static async Task SpeakThenAckAsync(IVoice voice, IRobotLink link, string director, string text, string lang)
        {
            try
            {
                try { await voice.SpeakAsync(text, lang); }
                finally { await link.SendAsync(director, VoiceCommand.Done()); }
            }
            catch { /* 連回 DONE 都失敗(link 斷):吞掉避免未觀察例外;中控會走逾時分支續行 */ }
        }
    }
}
