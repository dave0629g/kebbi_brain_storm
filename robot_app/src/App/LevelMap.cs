using System.Collections.Generic;
using System.Text;

namespace KebbiBrain.App
{
    // G1《雙機接力闖關》的關卡地圖:純資料(格子陣列 + 起點/終點/障礙),供 RelayQuestGame 做避障判定與 ASCII 渲染。
    // 格子:S=起點 G=終點 #=障礙 *=寶物 .=空地。出界視為牆。純 C#,可自測。
    // 平板免疫核心藉此放大:學生改指令 → 機器人在真實地板撞牆/繞行,「指令修改→物理結果改變」的因果回饋立即可見。
    public sealed class LevelMap
    {
        private readonly string[] _g;
        public int Rows { get; }
        public int Cols { get; }
        public int StartR { get; }
        public int StartC { get; }
        public int GoalR { get; }
        public int GoalC { get; }
        public int HandoffR { get; } = -1;   // 交接點(H);-1=本關不設交接點 → 不啟用同步檢查(向後相容)
        public int HandoffC { get; } = -1;
        public bool HasHandoffPoint => HandoffR >= 0;
        public bool IsHandoffPoint(int r, int c) => HasHandoffPoint && r == HandoffR && c == HandoffC;

        public LevelMap(string[] rows)
        {
            _g = rows; Rows = rows.Length; Cols = rows[0].Length;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    if (_g[r][c] == 'S') { StartR = r; StartC = c; }
                    else if (_g[r][c] == 'G') { GoalR = r; GoalC = c; }
                    else if (_g[r][c] == 'H') { HandoffR = r; HandoffC = c; } // 交接點(可走的空地,IsObstacle 不算障礙)
                }
        }

        public bool InBounds(int r, int c) => r >= 0 && r < Rows && c >= 0 && c < Cols;
        public bool IsObstacle(int r, int c) => !InBounds(r, c) || _g[r][c] == '#'; // 出界也視為牆(撞牆)
        public bool IsGoal(int r, int c) => r == GoalR && c == GoalC;
        public char At(int r, int c) => InBounds(r, c) ? _g[r][c] : '#';

        // 起點→終點的最短步數(BFS,避開障礙;到不了回 -1)。用來給闖關效率評星等。
        public int ShortestSteps()
        {
            var dist = new int[Rows, Cols];
            for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++) dist[r, c] = -1;
            int[] dr = { -1, 1, 0, 0 }, dc = { 0, 0, -1, 1 };
            var q = new Queue<(int r, int c)>();
            dist[StartR, StartC] = 0; q.Enqueue((StartR, StartC));
            while (q.Count > 0)
            {
                var (r, c) = q.Dequeue();
                if (r == GoalR && c == GoalC) return dist[r, c];
                for (int k = 0; k < 4; k++)
                {
                    int nr = r + dr[k], nc = c + dc[k];
                    if (InBounds(nr, nc) && !IsObstacle(nr, nc) && dist[nr, nc] < 0)
                    { dist[nr, nc] = dist[r, c] + 1; q.Enqueue((nr, nc)); }
                }
            }
            return -1;
        }

        // 把目前機器人位置(who='A'/'B')疊到地圖上印成 ASCII。
        public string Render(int r, int c, char who)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Rows; i++)
            {
                sb.Append("      ");
                for (int j = 0; j < Cols; j++)
                    sb.Append((i == r && j == c) ? who : _g[i][j]).Append(' ');
                sb.Append('\n');
            }
            sb.Append("      (S起點 G終點 H交接點 #障礙 *寶物 .空地 ").Append(who).Append("=機器人)");
            return sb.ToString();
        }

        // ── 內建關卡1：一處障礙；直直走會撞 #，需「右轉下繞再左轉」才能到 G。──
        //   S . # .
        //   . . . .
        //   . . G *
        public static LevelMap Level1() => new LevelMap(new[] { "S.#.", "....", "..G*" });

        // 錯誤示範:直直往前 → 第 2 步撞牆,闖關失敗。
        public static List<string> CrashProgram() =>
            new List<string> { "FWD", "FWD", "FWD", "FWD", "GOAL" };

        // 修正示範:手動繞行(右轉下繞、交棒、續下走、左轉到 G)→ 成功。
        public static List<string> DetourProgram() =>
            new List<string> { "FWD", "RIGHT", "FWD", "HANDOFF", "FWD", "LEFT", "FWD", "GOAL" };

        // 進階示範:用條件積木 IF_OBSTACLE 自動偵測前方障礙就繞行(把判斷交給程式)。
        public static List<string> SmartProgram() =>
            new List<string> { "FWD", "IF_OBSTACLE", "RIGHT", "FWD", "LEFT", "ENDIF",
                               "HANDOFF", "RIGHT", "FWD", "LEFT", "FWD", "GOAL" };

        // ── 關卡2(難度↑:轉彎走廊 + 交接點 H)──
        //   S . . H        A 直走到 H(0,3) 站對交接點才能交棒;B 右轉下繞到 G。
        //   . # # .
        //   . . . G
        public static LevelMap Level2() => new LevelMap(new[] { "S..H", ".##.", "...G" });

        // 正解:A 走到交接點 H 才 HANDOFF → 交棒成功,B 下繞到 G。
        public static List<string> Level2DetourProgram() =>
            new List<string> { "FWD", "FWD", "FWD", "HANDOFF", "RIGHT", "FWD", "FWD", "GOAL" };

        // 反例:A 只走 1 格(沒到交接點)就 HANDOFF → 交棒失敗(B 不啟動),最後 GOAL 因沒交棒而失敗。
        public static List<string> Level2HandoffTooEarlyProgram() =>
            new List<string> { "FWD", "HANDOFF", "FWD", "GOAL" };

        // ── 關卡3(難度↑↑:多障礙 S 形強制路徑 + 交接點 H)──
        //   S . . .       上排往右會走到死路((1,3)=#);唯一路是先下、橫越中排到 H、再下到 G。
        //   . # # #
        //   . . . H       交接點 H(2,3)
        //   # # # .
        //   . . . G
        public static LevelMap Level3() => new LevelMap(new[] { "S...", ".###", "...H", "###.", "...G" });

        // 正解:下→橫越→到 H 交棒→下到 G(7 步,即最短)。
        public static List<string> Level3DetourProgram() =>
            new List<string> { "RIGHT", "FWD", "FWD", "LEFT", "FWD", "FWD", "FWD",
                               "HANDOFF", "RIGHT", "FWD", "FWD", "GOAL" };

        // 反例:沿上排直直往右,想下卻撞牆((1,3)=#)→ 闖關失敗。
        public static List<string> Level3CrashProgram() =>
            new List<string> { "FWD", "FWD", "FWD", "RIGHT", "FWD", "GOAL" };
    }
}
