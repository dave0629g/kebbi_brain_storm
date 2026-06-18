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

        public LevelMap(string[] rows)
        {
            _g = rows; Rows = rows.Length; Cols = rows[0].Length;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    if (_g[r][c] == 'S') { StartR = r; StartC = c; }
                    else if (_g[r][c] == 'G') { GoalR = r; GoalC = c; }
                }
        }

        public bool InBounds(int r, int c) => r >= 0 && r < Rows && c >= 0 && c < Cols;
        public bool IsObstacle(int r, int c) => !InBounds(r, c) || _g[r][c] == '#'; // 出界也視為牆(撞牆)
        public bool IsGoal(int r, int c) => r == GoalR && c == GoalC;
        public char At(int r, int c) => InBounds(r, c) ? _g[r][c] : '#';

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
            sb.Append("      (S起點 G終點 #障礙 *寶物 .空地 ").Append(who).Append("=機器人)");
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
    }
}
