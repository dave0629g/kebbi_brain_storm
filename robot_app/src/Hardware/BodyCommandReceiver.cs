using System;

namespace KebbiBrain.Hardware
{
    // 掛在「被控」那台 Kebbi:訂閱 IRobotLink,收到機身命令(BC|…)就套到本機 body;
    // 非機身命令(HANDOFF/CUE/READY…)轉交 alsoHandle(讓被控機同時參與遊戲交棒邏輯)。
    // 與 RemoteBodyProxy 搭配,即構成「中控驅動 + 被控執行」的真機多台分散式控制。純 C#,可自測。
    public sealed class BodyCommandReceiver
    {
        public BodyCommandReceiver(IRobotLink link, IKebbiBody localBody, Action<string, string> alsoHandle = null)
        {
            link.OnMessage((from, text) =>
            {
                if (!BodyCommand.TryApply(text, localBody)) alsoHandle?.Invoke(from, text);
            });
        }
    }
}
