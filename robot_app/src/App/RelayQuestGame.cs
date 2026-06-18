using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // G1《雙機接力闖關》核心邏輯：把一段指令序列直譯成「兩台 Kebbi 在地板上接力走位」（程式邏輯啟蒙）。
    // 指令：FWD(依朝向前進一格) / LEFT / RIGHT(轉向) / HANDOFF(交棒) / GOAL(終點)
    //       IF_OBSTACLE / IF_CLEAR … ENDIF(條件積木:依正前方是否為障礙決定是否執行區塊;非巢狀)
    // 關卡地圖(LevelMap)為可選:
    //   • 無地圖 → 舊行為(FWD 永遠成功,不判障礙;啟蒙最小版)。
    //   • 有地圖 → FWD 依朝向位移,撞障礙/出界=Crashed 闖關失敗;GOAL 需「已交棒給 B 且站在 G 且沒撞過」。
    //     → 演出手冊命脈:學生改指令 → 撞牆失敗 vs 繞行成功,「指令修改→物理結果改變」立即可見。
    // 平板免疫核心：指令 → 實體在真實地板位移 + 交棒第二台 + 終點同步勝利手勢（移動 + 多機 + 關節）。可重入(每次 RunProgram 自動重置)。
    public sealed class RelayQuestGame
    {
        private static readonly int[] DR = { -1, 0, 1, 0 }; // 朝向 0=N 1=E 2=S 3=W 的列位移
        private static readonly int[] DC = { 0, 1, 0, -1 };

        private readonly IKebbiBody _bodyA, _bodyB;
        private readonly IRobotLink _linkA, _linkB;
        private readonly LevelMap _map;          // null = 無地圖(舊行為)
        private readonly Action<string> _log;
        private bool _bReady;
        private int _r, _c, _heading;            // 目前格位與朝向(有地圖時有效)

        public int Steps { get; private set; }        // 本次走了幾格
        public bool ReachedGoal { get; private set; }  // 是否由 B 抵達終點
        public bool OnRobotB { get; private set; }     // 目前是否輪到 B
        public bool Crashed { get; private set; }      // 本次是否撞過障礙(有地圖時)
        public int Attempts { get; private set; }      // 累計嘗試次數(同一實例 RunProgram 幾次;不隨 Reset 歸零)
        public int TotalCrashes { get; private set; }  // 累計撞牆次數(跨嘗試;每次嘗試最多 +1)
        public int Stars { get; private set; }         // 本次效率星等(0~3:走越接近最短路徑越多星)

        public RelayQuestGame(IKebbiBody bodyA, IRobotLink linkA, IKebbiBody bodyB, IRobotLink linkB,
                              Action<string> log, LevelMap map = null)
        {
            _bodyA = bodyA; _linkA = linkA; _bodyB = bodyB; _linkB = linkB; _map = map; _log = log ?? Console.WriteLine;
            _linkB.OnMessage((from, t) => { if (t == "GO") _bReady = true; });
        }

        // 重置「本次」狀態(可重入:同一關卡可連跑多份程式;Attempts/TotalCrashes 累計不歸零,供結算)。
        public void Reset()
        {
            Steps = 0; ReachedGoal = false; OnRobotB = false; Crashed = false; Stars = 0; _bReady = false;
            if (_map != null) { _r = _map.StartR; _c = _map.StartC; _heading = 1; } // 起點、面向東(+col)
        }

        public async Task RunProgramAsync(List<string> program)
        {
            Attempts++; Reset();
            IKebbiBody active = _bodyA;
            string activeName = _linkA.RobotId;

            for (int i = 0; i < program.Count; i++)
            {
                string cmd = (program[i] ?? "").Trim().ToUpperInvariant();
                switch (cmd)
                {
                    case "FWD":
                        if (_map == null) { active.Move(0.1f); active.StopWheels(); Steps++; _log("   👣 " + activeName + " 前進一格（第 " + Steps + " 格）"); }
                        else
                        {
                            int nr = _r + DR[_heading], nc = _c + DC[_heading];
                            if (_map.IsObstacle(nr, nc)) { if (!Crashed) TotalCrashes++; Crashed = true; _log("   💥 " + activeName + " 撞牆/出界！闖關失敗，回去改積木"); }
                            else { active.Move(0.1f); active.StopWheels(); _r = nr; _c = nc; Steps++; _log("   👣 " + activeName + " 前進到 (" + _r + "," + _c + ")（第 " + Steps + " 格）" + (_map.At(_r, _c) == '*' ? " ✨撿到寶物" : "")); }
                        }
                        break;
                    case "LEFT":
                        _heading = (_heading + 3) % 4; active.Turn(-30f); _log("   ↩️ " + activeName + " 左轉");
                        break;
                    case "RIGHT":
                        _heading = (_heading + 1) % 4; active.Turn(30f); _log("   ↪️ " + activeName + " 右轉");
                        break;
                    case "IF_OBSTACLE":
                        if (!FrontIsObstacle()) i = SkipToEndif(program, i); // 正前方沒障礙 → 跳過區塊
                        break;
                    case "IF_CLEAR":
                        if (FrontIsObstacle()) i = SkipToEndif(program, i);  // 正前方有障礙 → 跳過區塊
                        break;
                    case "ENDIF":
                        break; // 區塊結束,no-op
                    case "HANDOFF":
                        _bReady = false;
                        await _linkA.SendAsync(_linkB.RobotId, "GO");
                        if (_bReady) { OnRobotB = true; active = _bodyB; activeName = _linkB.RobotId; _log("   🤝 " + _linkA.RobotId + " 交棒 → " + _linkB.RobotId + " 接力"); }
                        break;
                    case "GOAL":
                        bool ok = _map == null ? OnRobotB : (OnRobotB && !Crashed && _map.IsGoal(_r, _c));
                        if (ok)
                        {
                            ReachedGoal = true;
                            if (_map != null) { int sp = _map.ShortestSteps(); Stars = sp > 0 && Steps <= sp ? 3 : sp > 0 && Steps <= sp + 2 ? 2 : 1; }
                            _bodyA.SetMotor(KebbiMotor.RShoulderY, 100f); _bodyA.SetMotor(KebbiMotor.LShoulderY, 100f);
                            _bodyB.SetMotor(KebbiMotor.RShoulderY, 100f); _bodyB.SetMotor(KebbiMotor.LShoulderY, 100f);
                            _log("   🏁 抵達終點！兩機同步舉雙手勝利手勢 🎉");
                        }
                        else if (_map != null && Crashed) _log("   ⚠ 撞過牆，沒能到終點（闖關失敗）");
                        else if (_map != null && !_map.IsGoal(_r, _c)) _log("   ⚠ 還沒走到終點格就 GOAL（闖關失敗）");
                        else _log("   ⚠ 還沒交棒給 B 就到 GOAL，闖關失敗（回去改 Blockly）");
                        break;
                    default:
                        _log("   ❓ 未知指令：" + program[i]);
                        break;
                }
            }
        }

        // 正前方那格是否為障礙(無地圖→永遠 false)。
        private bool FrontIsObstacle()
            => _map != null && _map.IsObstacle(_r + DR[_heading], _c + DC[_heading]);

        // 從 IF 的索引找到對應 ENDIF 的索引(非巢狀);回傳 ENDIF 的索引(for 迴圈 i++ 後跳到其後)。
        private static int SkipToEndif(List<string> program, int ifIndex)
        {
            for (int j = ifIndex + 1; j < program.Count; j++)
                if ((program[j] ?? "").Trim().ToUpperInvariant() == "ENDIF") return j;
            return program.Count; // 沒 ENDIF → 跳到結尾
        }

        // 闖關結算報告:第幾次嘗試成功/失敗、本次走幾格、累計撞牆幾次、效率星等。
        public void PrintSummary()
        {
            string stars = ReachedGoal ? new string('★', Stars) + new string('☆', 3 - Stars) : "—";
            _log("=== 闖關結算：第 " + Attempts + " 次嘗試" + (ReachedGoal ? "成功 🎉" : "失敗")
                 + "、本次走 " + Steps + " 格、累計撞牆 " + TotalCrashes + " 次、效率 " + stars + " ===");
        }

        // 範例指令程式（無地圖版）：A 走 3 格＋轉彎 → 交棒 → B 走 2 格到終點
        public static List<string> MakeSampleProgram()
        {
            return new List<string> { "FWD", "FWD", "RIGHT", "FWD", "HANDOFF", "FWD", "LEFT", "FWD", "GOAL" };
        }
    }
}
