using System;
using System.Collections.Generic;
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

            // 轉頭面向說話者（workaround：讀 DOA → 自寫 NeckZ，非 turnToDOA）
            float faced = KebbiHead.TurnToward(_ctx.Body, doa, out bool reachable);

            var r = new RoundResult
            {
                Claimed = claimed,
                Actual = actual,
                Asked = asked,
                LanguageCorrect = (claimed.HasValue && claimed.Value == actual),
                RightResponder = (actual == asked),
                Faced = reachable,
                FacedAngle = faced
            };

            if (!reachable)
                _ctx.Log($"   ⚠ 說話者在 {doa:0.0}°（{Direction.ToZh(actual)}），超出頭部可達範圍，只能轉到 {faced:0.0}°（正後方頭轉不過去）");

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
            KebbiHead.TurnToward(_ctx.Body, target.AngleDeg, out bool reachable);
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
                Faced = reachable
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
    }
}
