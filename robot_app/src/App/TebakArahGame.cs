using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // G4《Tebak Arah：聲源方位 × 印尼語方向詞定向遊戲》核心邏輯。
    // 完全建立在抽象介面上：同一份程式碼，Sim 後端可自測、Unity Real 後端可上實機。
    // 平板免疫核心：用 DOA（聲源真實方位）當「方位詞對錯的真值」，再用 NeckZ 轉頭面向說話者。
    public sealed class TebakArahGame
    {
        private readonly KebbiContext _ctx;
        private readonly List<Student> _students = new List<Student>();

        public int Score { get; private set; }
        public int Rounds { get; private set; }
        public IReadOnlyList<Student> Students => _students;

        public TebakArahGame(KebbiContext ctx) { _ctx = ctx; }

        public sealed class Student
        {
            public string Name;
            public float AngleDeg;
            public Dir Dir;
        }

        public sealed class RoundResult
        {
            public Dir? Claimed;     // 學生說出的方位詞
            public Dir Actual;       // DOA 真值換算的實際方位
            public Dir Asked;        // 本題問的方位
            public bool LanguageCorrect; // 方位詞是否正確描述真實位置（核心學習目標）
            public bool RightResponder;  // 回答者是否真的在被問的方位
            public bool Faced;       // 頭是否能完整轉向面對（false=被夾限，如正後方）
            public float FacedAngle;
        }

        // ── 校準：每位學生說 "Saya di sini!"，用 DOA 記錄其座位方位 ──
        // （Sim：呼叫前由腳本設定 body.CurrentDoa；Real：學生真的出聲、DOA 即時回傳）
        public async Task CalibrateOneAsync(string name)
        {
            await _ctx.Voice.SpeakAsync("Katakan: Saya di sini!", "id-ID");
            string heard = await _ctx.Voice.ListenAsync("id-ID");
            float doa = _ctx.Body.ReadDoaDegrees();
            var dir = Direction.FromAngle(doa);
            _students.Add(new Student { Name = name, AngleDeg = doa, Dir = dir });
            _ctx.Log($"   ✔ 校準 {name}：{doa:0.0}°（{Direction.ToZh(dir)} / {Direction.ToIndo(dir)}）heard=\"{heard}\"");
        }

        // ── 正向題：問「Siapa di sebelah {asked}?（誰在我的 X 邊?）」，學生回答 "Saya di X" ──
        public async Task<RoundResult> ForwardRoundAsync(Dir asked)
        {
            Rounds++;
            await _ctx.Voice.SpeakAsync($"Siapa di sebelah {Direction.ToIndo(asked)} saya?", "id-ID");

            string spoken = await _ctx.Voice.ListenAsync("id-ID");
            Dir? claimed = Direction.ParseIndo(spoken);
            float doa = _ctx.Body.ReadDoaDegrees();
            Dir actual = Direction.FromAngle(doa);

            // 面向說話者：FaceFully（輪式=底盤轉粗方向+頭補細→完整面向；H201桌上型=頭部部分面向）
            var ff = KebbiHead.FaceFully(_ctx.Body, doa);

            var r = new RoundResult
            {
                Claimed = claimed,
                Actual = actual,
                Asked = asked,
                LanguageCorrect = (claimed.HasValue && claimed.Value == actual),
                RightResponder = (actual == asked),
                Faced = ff.FullyFaced,
                FacedAngle = ff.FacedAngle
            };

            if (!ff.FullyFaced)
                _ctx.Log($"   ⚠ 說話者在 {doa:0.0}°（{Direction.ToZh(actual)}），{(_ctx.Body.CanMove ? "" : "無底盤、")}只能面向到 {ff.FacedAngle:0.0}°（部分面向）");

            if (r.LanguageCorrect && r.RightResponder)
            {
                Score++;
                await _ctx.Voice.SpeakAsync("Benar! Bagus sekali!", "id-ID");
            }
            else if (r.LanguageCorrect && !r.RightResponder)
            {
                await _ctx.Voice.SpeakAsync(
                    $"Arahnya betul, tapi saya tanya yang di {Direction.ToIndo(asked)}.", "id-ID");
            }
            else
            {
                await _ctx.Voice.SpeakAsync(
                    $"Salah. Kamu sebenarnya di {Direction.ToIndo(actual)}.", "id-ID");
                // 用 LLM 產生針對性糾錯提示（Sim：決定式；Real：真 LLM）
                string tip = await _ctx.Llm.AskAsync(
                    "Kamu guru bahasa Indonesia yang ramah untuk anak.",
                    $"Murid salah arah (salah): dia bilang '{(claimed.HasValue ? Direction.ToIndo(claimed.Value) : "?")}' " +
                    $"padahal sebenarnya di '{Direction.ToIndo(actual)}'. Beri satu tip singkat.");
                await _ctx.Voice.SpeakAsync(tip, "id-ID");
            }
            return r;
        }

        // ── 反向題：頭轉向某學生（實體刺激），該生需說出自己相對 Kebbi 的方位 ──
        public async Task<RoundResult> ReverseRoundAsync(Student target)
        {
            Rounds++;
            var ff = KebbiHead.FaceFully(_ctx.Body, target.AngleDeg);
            await _ctx.Voice.SpeakAsync("Kamu di mana? (Saya di ...)", "id-ID");

            string spoken = await _ctx.Voice.ListenAsync("id-ID");
            Dir? claimed = Direction.ParseIndo(spoken);
            var r = new RoundResult
            {
                Claimed = claimed,
                Actual = target.Dir,
                Asked = target.Dir,
                LanguageCorrect = (claimed.HasValue && claimed.Value == target.Dir),
                RightResponder = true,
                Faced = ff.FullyFaced
            };
            if (r.LanguageCorrect) { Score++; await _ctx.Voice.SpeakAsync("Benar!", "id-ID"); }
            else await _ctx.Voice.SpeakAsync($"Salah. Kamu di {Direction.ToIndo(target.Dir)}.", "id-ID");
            return r;
        }

        public Student FindByName(string name)
        {
            return _students.Find(s => s.Name == name);
        }

        public void PrintSummary()
        {
            _ctx.Log("");
            _ctx.Log($"=== 結算：答對 {Score} / {Rounds} 題 ===");
        }

        // ══════════════════════════════════════════════════════════════════════════════
        // G4-judge 擴充：裁判賽（JudgeMatch）+ 視角轉換（PerspectiveRound）+ 多輪排名（Tournament）
        // 全部純加法、向後相容：舊 Score/Rounds/ForwardRoundAsync/ReverseRoundAsync 一字不改；
        // 裁判賽分數只進獨立的 _matchScores 字典 + TournamentRounds，絕不污染舊 Score。
        // ══════════════════════════════════════════════════════════════════════════════

        // 累積排名用獨立欄位（與舊 Score 完全隔離）。
        private readonly Dictionary<string, int> _matchScores = new Dictionary<string, int>();
        public int TournamentRounds { get; private set; }
        public IReadOnlyList<RankEntry> Ranking { get; private set; } = new List<RankEntry>();

        // 排名項目（LangVersion 9.0：用 class 而非 record；公開簽章不用 tuple/target-typed new）。
        public sealed class RankEntry
        {
            public string Name;
            public int Points;
            public RankEntry(string name, int points) { Name = name; Points = points; }
        }

        // 一場裁判賽腳本：誰是提問者（描述者）、誰被描述。
        public sealed class MatchSpec
        {
            public string AskerName;
            public string TargetName;
            public MatchSpec(string asker, string target) { AskerName = asker; TargetName = target; }
        }

        // 裁判賽/視角題結果。
        public sealed class JudgeResult
        {
            public string Asker;
            public string Target;
            public Dir? Claimed;           // A 說 B 相對 Kebbi 在哪
            public Dir ActualSector;       // DOA 真值（B 相對 Kebbi 的扇區）
            public Dir? ClaimedFromAsker;  // 視角題：A 說 B「相對自己」在哪（裁判賽=null）
            public Dir PerspectiveSector;  // 視角題：B 相對 A 的真值扇區（裁判賽=ActualSector）
            public bool Correct;           // 是否得分（裁判賽=方位對；視角題=兩個方位都要對）
            public bool Faced;             // Kebbi 轉頭面向 B 是否可達（false=被夾限，如正後方）
            public float FacedAngle;
        }

        // ── 視角轉換純函式（無狀態、最易測）──
        // observerDeg=觀察者相對 Kebbi 的角度；targetDeg=被描述者相對 Kebbi 的角度。
        // 約定：觀察者「面向 Kebbi」（背對自己座位）→ 其前=朝 Kebbi、左右相對觀察者翻轉。
        // 故手冊核心句『我的右＝Kebbi 的左』：rel = -(target - observer) 正規化後再分扇區。
        public static Dir RelativeDir(float observerDeg, float targetDeg)
        {
            float rel = Direction.Normalize(-(targetDeg - observerDeg));
            return Direction.FromAngle(rel);
        }

        // 取某學生在 _matchScores 的累積分（LangVersion 9.0 安全：用 TryGetValue，Unity Mono 也可搬）。
        public int MatchScoreOf(string name)
        {
            return _matchScores.TryGetValue(name, out var v) ? v : 0;
        }

        private void AddMatchScore(string name)
        {
            _matchScores[name] = MatchScoreOf(name) + 1;
        }

        // ── 單場裁判賽：A 出聲描述 B 相對 Kebbi 的方位 → Kebbi 用 B 的 DOA 真值核對 → 轉頭面向 B ──
        // （Sim：呼叫前由腳本設定 body.CurrentDoa＝B 此刻方位、EnqueueHeard A 的答案；Real：B 真的出聲被 DOA 量到）
        public async Task<JudgeResult> JudgeRoundAsync(MatchSpec m)
        {
            TournamentRounds++;
            await _ctx.Voice.SpeakAsync($"{m.AskerName}, di mana {m.TargetName}?", "id-ID");
            string spoken = await _ctx.Voice.ListenAsync("id-ID");
            Dir? claimed = Direction.ParseIndo(spoken);

            float doa = _ctx.Body.ReadDoaDegrees();      // B 此刻出聲 → 真值
            Dir actual = Direction.FromAngle(doa);
            var ff = KebbiHead.FaceFully(_ctx.Body, doa);   // 面向 B(輪式底盤+頭、H201頭部部分)

            bool correct = claimed.HasValue && claimed.Value == actual;
            if (correct)
            {
                AddMatchScore(m.AskerName);
                await _ctx.Voice.SpeakAsync("Benar! Bagus!", "id-ID");
            }
            else
            {
                await _ctx.Voice.SpeakAsync(
                    $"Salah. {m.TargetName} sebenarnya di {Direction.ToIndo(actual)}.", "id-ID");
            }

            return new JudgeResult
            {
                Asker = m.AskerName,
                Target = m.TargetName,
                Claimed = claimed,
                ActualSector = actual,
                ClaimedFromAsker = null,
                PerspectiveSector = actual,
                Correct = correct,
                Faced = ff.FullyFaced,
                FacedAngle = ff.FacedAngle
            };
        }

        // ── 視角轉換題：要求 A 同時說出 B『相對 Kebbi』與『相對自己』的方位 ──
        // 教學重點：兩者常相反（手冊 `Kamu di kiri saya, tapi di kanan Kebbi`）。
        // 兩個方位詞都對才得分；任一錯（含把兩者講反）皆不得分。
        // 兩答案由腳本依序 EnqueueHeard：先『相對 Kebbi』、後『相對自己』。
        public async Task<JudgeResult> PerspectiveRoundAsync(string askerName, string targetName)
        {
            TournamentRounds++;
            var asker = FindByName(askerName);
            var target = FindByName(targetName);

            Dir fromKebbi = target != null ? target.Dir : Dir.Depan;     // B 相對 Kebbi 真值
            Dir fromAsker = (asker != null && target != null)
                ? RelativeDir(asker.AngleDeg, target.AngleDeg)           // B 相對 A 視角真值
                : Dir.Depan;

            // 出題：先問相對 Kebbi、再問相對自己。
            await _ctx.Voice.SpeakAsync(
                $"{askerName}, di mana {targetName} dari Kebbi, dan dari kamu?", "id-ID");
            string spokenKebbi = await _ctx.Voice.ListenAsync("id-ID");
            string spokenAsker = await _ctx.Voice.ListenAsync("id-ID");
            Dir? claimedKebbi = Direction.ParseIndo(spokenKebbi);
            Dir? claimedAsker = Direction.ParseIndo(spokenAsker);

            // 面向 B（具身回饋；真值用校準時的座位方位）。FaceFully:輪式底盤+頭、H201頭部部分。
            var ff = KebbiHead.FaceFully(_ctx.Body, target != null ? target.AngleDeg : 0f);

            bool kebbiOk = claimedKebbi.HasValue && claimedKebbi.Value == fromKebbi;
            bool askerOk = claimedAsker.HasValue && claimedAsker.Value == fromAsker;
            bool correct = kebbiOk && askerOk;
            if (correct)
            {
                AddMatchScore(askerName);
                await _ctx.Voice.SpeakAsync("Benar! Kamu mengerti sudut pandang!", "id-ID");
            }
            else
            {
                await _ctx.Voice.SpeakAsync(
                    $"Coba lagi. {targetName} di {Direction.ToIndo(fromKebbi)} dari Kebbi, " +
                    $"tapi di {Direction.ToIndo(fromAsker)} dari kamu.", "id-ID");
            }

            return new JudgeResult
            {
                Asker = askerName,
                Target = targetName,
                Claimed = claimedKebbi,
                ActualSector = fromKebbi,
                ClaimedFromAsker = claimedAsker,
                PerspectiveSector = fromAsker,
                Correct = correct,
                Faced = ff.FullyFaced,
                FacedAngle = ff.FacedAngle
            };
        }

        // ── 多輪賽事：逐場跑裁判賽、累加 per-match 分數，結束輸出降冪排名 ──
        // 可重入慣例：開頭重置該場累積分／輪數／名次（仿 RunDebateAsync）。
        public async Task RunTournamentAsync(List<MatchSpec> matches)
        {
            _matchScores.Clear();
            TournamentRounds = 0;
            Ranking = new List<RankEntry>();

            if (matches != null)
                foreach (var m in matches)
                    await JudgeRoundAsync(m);

            // 平手以校準先後 stable（LINQ OrderByDescending 為 stable）。
            Ranking = _students
                .Select(s => new RankEntry(s.Name, MatchScoreOf(s.Name)))
                .OrderByDescending(x => x.Points)
                .ToList();
        }

        // 印名次 1./2./3.；冠軍機器人舉手致意。
        public void PrintRanking()
        {
            _ctx.Log("");
            _ctx.Log($"=== 裁判賽排名（{TournamentRounds} 場）===");
            int rank = 1;
            foreach (var e in Ranking)
            {
                _ctx.Log($"   {rank}. {e.Name}：{e.Points} 分");
                rank++;
            }
            if (Ranking.Count > 0 && Ranking[0].Points > 0)
            {
                _ctx.Body.SetMotor(KebbiMotor.RShoulderY, 100f);
                _ctx.Log($"   🏆 冠軍 {Ranking[0].Name}，Kebbi 舉手致意！");
            }
        }

        // 便利：把一串學生名兩兩配對成 round-robin 裁判賽（每對只配一次，前者為提問者）。
        public static List<MatchSpec> MakeRoundRobin(IEnumerable<string> names)
        {
            var list = new List<string>(names ?? Array.Empty<string>());
            var matches = new List<MatchSpec>();
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                    matches.Add(new MatchSpec(list[i], list[j]));
            return matches;
        }
    }
}
