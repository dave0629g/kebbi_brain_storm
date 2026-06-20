// 輔導室 Sim demo(整檔 #if !UNITY):跑一段「等待陪伴 → 探索 → 🟢開放聊 → 🟡黃燈交接 → 🔴紅線呼叫真人」
// 的腳本對話,展示確定性安全閘、逐句記錄、交接卡、通知。`dotnet run -- --counselor`。
#if !UNITY
using System;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App.Counselor
{
    public static class CounselorDemo
    {
        // 溫暖、單句、台灣中文的「開放聊」回應(Sim 用,Real 換成真 ILlm)。
        private sealed class GentleLlm : ILlm
        {
            private static readonly string[] R =
            { "嗯,聽起來今天還算平順,有想多聊一點嗎?", "謝謝你跟我說這些,我都在聽。", "這樣啊,那後來呢?", "嗯嗯,我懂,你想說的我都記得。" };
            private int _i;
            public Task<string> AskAsync(string system, string user) => Task.FromResult(R[_i++ % R.Length]);
        }

        public static async Task RunAsync()
        {
            Console.WriteLine("\n========== 輔導室陪伴機器人 Demo(Sim,免金鑰)==========");
            Console.WriteLine("流程:登入告知 → 沉默觸發探索 → 🟢開放聊 → 🟡黃燈出交接卡 → 🔴紅線呼叫真人\n");

            var gate = new SimSafetyGate();
            var log = new SimConversationLog("DEMO");
            var notify = new SimNotifyHuman(Console.WriteLine);
            var planner = new SimExplorationPlanner();
            var sess = new CounselorSession(null, null, new GentleLlm(), gate, log, notify, planner, Console.WriteLine);

            sess.Start("學生 小安", ConvMode.Voice);

            string[] script =
            {
                "",                                   // 沉默 → 探索拋題
                "還可以啦,今天考完試輕鬆一點",        // 🟢
                "",                                   // 沉默 → 換話題探索
                "其實…最近家裡氣氛很差,爸媽一直在吵架,我都不太想回家", // 🟡
                "我撐不下去了,有時候覺得活著好累,好想消失",            // 🔴
            };

            foreach (var line in script)
            {
                if (!string.IsNullOrEmpty(line)) Console.WriteLine("👦 學生: " + line);
                else Console.WriteLine("👦 學生:(沉默…)");
                var layer = await sess.StepAsync(line);
                Console.WriteLine("      [安全閘判定: " + (layer == Layer.Red ? "🔴 紅線" : layer == Layer.Yellow ? "🟡 黃燈" : "🟢 開放") + "]\n");
                if (sess.Ended) break;
            }

            var card = sess.End();
            Console.WriteLine("\n--- 逐句記錄(append-only)---");
            foreach (var t in log.GetTurns())
                Console.WriteLine($"  #{t.TurnIndex} [{t.Timestamp:HH:mm:ss}] {(t.Speaker == Speaker.Student ? "學生" : "凱比")}/" +
                                  $"{(t.Layer == Layer.Red ? "🔴" : t.Layer == Layer.Yellow ? "🟡" : "🟢")}" +
                                  $"{(t.Event != LogEvent.None ? " <" + t.Event + ">" : "")}: {t.Text}");

            Console.WriteLine("\n--- 交接卡(結構化 JSON,safety_flag 第一欄)---");
            Console.WriteLine("  " + card.ToJson());
            Console.WriteLine("\n========== Demo 結束:安全閘為確定性硬規則、紅線即時呼叫真人、全程逐句記錄 ==========\n");
        }
    }
}
#endif
