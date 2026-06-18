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
    public sealed class GeometryRelayGame
    {
        private readonly IKebbiBody _guide;     // 甲機：會走位、手臂指認
        private readonly IRobotLink _guideLink;
        private readonly IRobotLink _reasonerLink;
        private readonly IVoice _reasonerVoice; // 乙機：宣告理由
        private readonly Action<string> _log;
        private string _lastDoneEdge;

        public int StepsDone { get; private set; }

        public sealed class Step
        {
            public string Reason;   // 例「因為 AB = AC（已知，等腰）」
            public string Edge;     // 例「AB,AC」
            public float ArmAngle;  // 手臂指向角度
            public Step(string reason, string edge, float armAngle) { Reason = reason; Edge = edge; ArmAngle = armAngle; }
        }

        public GeometryRelayGame(IKebbiBody guideBody, IRobotLink guideLink, IRobotLink reasonerLink, IVoice reasonerVoice, Action<string> log)
        {
            _guide = guideBody; _guideLink = guideLink; _reasonerLink = reasonerLink; _reasonerVoice = reasonerVoice; _log = log ?? Console.WriteLine;

            // 甲機收到 POINT|edge|angle → 走位 + 手臂指向 + 回 DONE
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

            // 乙機收到 DONE → 記錄完成的邊
            _reasonerLink.OnMessage((from, t) => { if (t.StartsWith("DONE")) _lastDoneEdge = t.Split('|')[1]; });
        }

        public async Task RunProofAsync(List<Step> steps)
        {
            foreach (var s in steps)
            {
                await _reasonerVoice.SpeakAsync(s.Reason, "zh-TW");   // 乙機宣告理由
                _lastDoneEdge = null;
                await _reasonerLink.SendAsync(_guideLink.RobotId, "POINT|" + s.Edge + "|" + s.ArmAngle.ToString(CultureInfo.InvariantCulture));
                if (_lastDoneEdge == s.Edge)
                {
                    StepsDone++;
                    _log("   ✔ 第 " + StepsDone + " 步完成：「" + s.Reason + "」→ 指認 " + s.Edge);
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
    }
}
