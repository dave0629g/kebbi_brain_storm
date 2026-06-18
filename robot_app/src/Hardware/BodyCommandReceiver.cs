using System;

namespace KebbiBrain.Hardware
{
    // 掛在「被控」那台 Kebbi:訂閱 IRobotLink,收到機身命令(BC|…)就套到本機 body、語音命令(VC|…)就用本機 voice 說;
    // 其餘訊息(HANDOFF/CUE/READY…)轉交 alsoHandle(讓被控機同時參與遊戲交棒邏輯)。
    // 與 RemoteBodyProxy/RemoteVoiceProxy 搭配,即構成「中控驅動 + 被控執行(動 + 說)」的真機多台分散式控制。純 C#,可自測。
    // ⚠ IRobotLink.OnMessage 是「單一 handler、後者覆寫」語意 → 被控機所有訊息都必須走這條 handler,再經 alsoHandle 分流。
    //   分派鏈(前綴互斥):BC|… 機身 → VC|… 語音 → 其餘轉交。localVoice 為新增可選參數,放最後以相容既有呼叫端。
    public sealed class BodyCommandReceiver
    {
        public BodyCommandReceiver(IRobotLink link, IKebbiBody localBody,
                                   Action<string, string> alsoHandle = null, IVoice localVoice = null)
        {
            link.OnMessage((from, text) =>
            {
                if (BodyCommand.TryApply(text, localBody)) return;                       // BC|… 機身命令
                if (localVoice != null && VoiceCommand.TryApply(text, localVoice)) return; // VC|… 語音命令
                alsoHandle?.Invoke(from, text);                                          // 其餘訊息轉交
            });
        }
    }
}
