using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // G2《幾何證明接力站》核心邏輯：雙機「說—走—指—接棒」。
    // 乙機(reasoner)宣告每一步理由 → 用 IRobotLink 觸發甲機(guide)走位 + 手臂指向該邊/角 → 甲機回報 DONE → 乙機接下一步。
    // 平板免疫核心：移動到地面大圖對應位置 + 關節手臂指認 + 雙機接力(都是平板做不到的物理能力)。
    // 交棒等待用 LinkAwaiter(送 POINT → await 甲機回 DONE 帶逾時)→ Sim 同步遞送與真機 UDP 非同步遞送皆正確
    //   (舊版「送 POINT 後同步讀旗標」只在 Sim 成立);甲機逾時沒回(離線/卡住)→ 乙機降級口述、不卡死。可重入。
    public sealed class GeometryRelayGame
    {
        private readonly IKebbiBody _guide;     // 甲機：會走位、手臂指認
        private readonly IRobotLink _guideLink;
        private readonly IRobotLink _reasonerLink;
        private readonly IVoice _reasonerVoice; // 乙機：宣告理由
        private readonly LinkAwaiter _awaiter;  // 乙機在自己的 link 上等甲機回 DONE
        private readonly int _doneTimeoutMs;
        private readonly Action<string> _log;

        public int StepsDone { get; private set; }     // 完成的步數
        public int StepsSkipped { get; private set; }   // 甲機逾時沒回 → 降級跳過的步數
        public int Score { get; private set; }          // 學習單作答得分(每步答對邏輯層次 +1;Layer 為 null 的步不計)

        public sealed class Step
        {
            public string Reason;   // 例「因為 AB = AC（已知，等腰）」
            public string Edge;     // 例「AB,AC」
            public float ArmAngle;  // 手臂指向角度
            public string Layer;    // 學習單邏輯層次:已知/因為/所以(null=該步不出學習單題,維持舊行為)
            public Step(string reason, string edge, float armAngle, string layer = null)
            { Reason = reason; Edge = edge; ArmAngle = armAngle; Layer = layer; }
        }

        // doneTimeoutMs：等甲機回 DONE 的上限(過了算甲機離線/卡住 → 降級)。
        public GeometryRelayGame(IKebbiBody guideBody, IRobotLink guideLink, IRobotLink reasonerLink,
                                 IVoice reasonerVoice, Action<string> log, int doneTimeoutMs = 2000)
        {
            _guide = guideBody; _guideLink = guideLink; _reasonerLink = reasonerLink;
            _reasonerVoice = reasonerVoice; _log = log ?? Console.WriteLine; _doneTimeoutMs = doneTimeoutMs;
            _awaiter = new LinkAwaiter(_reasonerLink); // 乙機 link 收 DONE

            // 甲機收到 POINT|edge|angle → 走位 + 手臂指向 + 回 DONE|edge
            _guideLink.OnMessage((from, t) =>
            {
                if (!t.StartsWith("POINT")) return;
                var p = t.Split('|');
                string edge = p[1];
                float angle = float.Parse(p[2], CultureInfo.InvariantCulture);
                _guide.Move(0.1f);        // 走向地面大圖該邊(H201 不動;輪式會走;開迴路)
                _guide.StopWheels();
                _guide.SetMotor(KebbiMotor.RShoulderY, angle); // 手臂指認
                _log("   👉 甲機 走到並指向 " + edge);
                _guideLink.SendAsync(from, "DONE|" + edge);
            });
        }

        public async Task RunProofAsync(List<Step> steps)
        {
            StepsDone = 0; StepsSkipped = 0; Score = 0; // 可重入:每場重置
            foreach (var s in steps)
            {
                await _reasonerVoice.SpeakAsync(s.Reason, "zh-TW");   // 乙機宣告理由

                // 學習單作答(可選:Step.Layer 有值才出題):問學生這步是「已知/因為/所以」,答對計分、答錯念提示。
                if (s.Layer != null)
                {
                    await _reasonerVoice.SpeakAsync("這一步是『已知』、『因為』還是『所以』?", "zh-TW");
                    string heard = await _reasonerVoice.ListenAsync("zh-TW");
                    if (!string.IsNullOrEmpty(heard) && heard.Contains(s.Layer))
                    { Score++; _log("   ✅ 學習單答對：這步是「" + s.Layer + "」（得分 " + Score + "）"); }
                    else
                    {
                        await _reasonerVoice.SpeakAsync("再想想：這步是在『用已知條件』、『推論過程』還是『下結論』?", "zh-TW");
                        _log("   ✏ 學習單答錯（學生說「" + heard + "」）→ 正解是「" + s.Layer + "」，乙機提示後繼續");
                    }
                }

                // 鐵則:先註冊 DONE 等待者,再送 POINT(Sim 同步遞送下 DONE 會在 SendAsync 當下就回,先送會漏接)。
                var doneTask = _awaiter.WaitForAsync((f, t) => t == "DONE|" + s.Edge, _doneTimeoutMs);
                await _reasonerLink.SendAsync(_guideLink.RobotId, "POINT|" + s.Edge + "|" + s.ArmAngle.ToString(CultureInfo.InvariantCulture));
                if (await doneTask != null)
                {
                    StepsDone++;
                    _log("   ✔ 第 " + StepsDone + " 步完成：「" + s.Reason + "」→ 指認 " + s.Edge);
                }
                else
                {
                    StepsSkipped++;
                    _log("   ⚠ 甲機逾時沒回（離線/卡住）→ 乙機降級口述、不卡死，繼續下一步");
                }
            }
        }

        // 內建範例證明：等腰三角形「兩底角相等」的 3 步接力。
        public static List<Step> MakeIsoscelesProof()
        {
            return new List<Step>
            {
                new Step("因為 AB = AC（已知，等腰三角形）", "AB,AC", 60f),
                new Step("作頂角平分線 AD，∠BAD = ∠CAD", "AD", 30f),
                new Step("所以 △ABD ≅ △ACD（SAS），得 ∠B = ∠C", "△ABD,△ACD", 80f),
            };
        }

        // 學習單版:同一證明,但每步標上邏輯層次(已知/因為/所以)→ 乙機會問學生這步屬哪一層、答對計分。
        public static List<Step> MakeIsoscelesProofWorksheet()
        {
            return new List<Step>
            {
                new Step("因為 AB = AC（已知，等腰三角形）", "AB,AC", 60f, "已知"),
                new Step("作頂角平分線 AD，∠BAD = ∠CAD", "AD", 30f, "因為"),
                new Step("所以 △ABD ≅ △ACD（SAS），得 ∠B = ∠C", "△ABD,△ACD", 80f, "所以"),
            };
        }
    }
}
