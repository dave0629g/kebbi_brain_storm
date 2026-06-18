using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // G1《雙機接力闖關》核心邏輯：把一段指令序列直譯成「兩台 Kebbi 在地板上接力走位」（程式邏輯啟蒙）。
    // 註：全程 Unity 建置；指令可由任何簡易 UI 產生，不受 Blockly/CodeLab 功能侷限。
    // 平板免疫核心：指令 → 一個實體在你腳邊的真實地板上位移 + 交棒給第二台 + 終點同步勝利手勢（移動 + 多機 + 關節）。
    // 指令：FWD(前進一格) / LEFT / RIGHT / HANDOFF(交棒) / GOAL(終點)
    public sealed class RelayQuestGame
    {
        private readonly IKebbiBody _bodyA, _bodyB;
        private readonly IRobotLink _linkA, _linkB;
        private readonly Action<string> _log;
        private bool _bReady;

        public int Steps { get; private set; }       // 走了幾格
        public bool ReachedGoal { get; private set; } // 是否由 B 抵達終點
        public bool OnRobotB { get; private set; }    // 目前是否輪到 B

        public RelayQuestGame(IKebbiBody bodyA, IRobotLink linkA, IKebbiBody bodyB, IRobotLink linkB, Action<string> log)
        {
            _bodyA = bodyA; _linkA = linkA; _bodyB = bodyB; _linkB = linkB; _log = log ?? Console.WriteLine;
            _linkB.OnMessage((from, t) => { if (t == "GO") _bReady = true; });
        }

        public async Task RunProgramAsync(List<string> program)
        {
            IKebbiBody active = _bodyA;
            string activeName = _linkA.RobotId;

            foreach (var raw in program)
            {
                string cmd = (raw ?? "").Trim().ToUpperInvariant();
                switch (cmd)
                {
                    case "FWD":
                        active.Move(0.1f); active.StopWheels(); Steps++;
                        _log("   👣 " + activeName + " 前進一格（第 " + Steps + " 格）");
                        break;
                    case "LEFT":
                        active.Turn(-30f); _log("   ↩️ " + activeName + " 左轉");
                        break;
                    case "RIGHT":
                        active.Turn(30f); _log("   ↪️ " + activeName + " 右轉");
                        break;
                    case "HANDOFF":
                        _bReady = false;
                        await _linkA.SendAsync(_linkB.RobotId, "GO");
                        if (_bReady) { OnRobotB = true; active = _bodyB; activeName = _linkB.RobotId; _log("   🤝 " + _linkA.RobotId + " 交棒 → " + _linkB.RobotId + " 接力"); }
                        break;
                    case "GOAL":
                        if (OnRobotB)
                        {
                            ReachedGoal = true;
                            _bodyA.SetMotor(KebbiMotor.RShoulderY, 100f); _bodyA.SetMotor(KebbiMotor.LShoulderY, 100f);
                            _bodyB.SetMotor(KebbiMotor.RShoulderY, 100f); _bodyB.SetMotor(KebbiMotor.LShoulderY, 100f);
                            _log("   🏁 抵達終點！兩機同步舉雙手勝利手勢 🎉");
                        }
                        else _log("   ⚠ 還沒交棒給 B 就到 GOAL，闖關失敗（回去改 Blockly）");
                        break;
                    default:
                        _log("   ❓ 未知指令：" + raw);
                        break;
                }
            }
        }

        // 範例指令程式：A 走 3 格＋轉彎 → 交棒 → B 走 2 格到終點
        public static List<string> MakeSampleProgram()
        {
            return new List<string> { "FWD", "FWD", "RIGHT", "FWD", "HANDOFF", "FWD", "LEFT", "FWD", "GOAL" };
        }
    }
}
