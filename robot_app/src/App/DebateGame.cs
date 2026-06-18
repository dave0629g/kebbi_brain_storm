using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // G5《歷史現場法庭辯論劇場》核心邏輯：兩台 Kebbi 分飾控方/辯方。
    // 用 IRobotLink 做「事件交棒」接力辯論；關鍵爭點兩機向中央移動逼近對峙；
    // 學生席發言時用 DOA + 自寫 NeckZ 轉向該生（不用被擋的 turnToDOA）；手臂做指控/攤手手勢。
    // 平板免疫核心：兩個實體在空間兩端接力、逼近、轉身點名（多機 + 移動 + 聲源轉向 + 關節）。
    public sealed class DebateGame
    {
        private readonly IKebbiBody _proBody, _defBody;
        private readonly IRobotLink _proLink, _defLink;
        private readonly IVoice _proVoice, _defVoice;
        private readonly Action<string> _log;
        private bool _defReady, _proReady;

        public int Exchanges { get; private set; }
        public int CenterApproaches { get; private set; }

        // 結辯投票計分:每回合學生/評審投給較有說服力的一方,累計後宣判。
        public int ProVotes { get; private set; }
        public int DefVotes { get; private set; }
        public bool Concluded { get; private set; }
        // 勝負(票多者勝;同票平手)。
        public string Verdict => ProVotes > DefVotes ? "控方勝" : DefVotes > ProVotes ? "辯方勝" : "平手";

        public sealed class Exchange
        {
            public string Pro; public string Def;
            public int ProVotes; public int DefVotes; // 該回合雙方各得幾票(可選,預設 0)
            public Exchange(string pro, string def, int proVotes = 0, int defVotes = 0)
            { Pro = pro; Def = def; ProVotes = proVotes; DefVotes = defVotes; }
        }

        public DebateGame(IKebbiBody proBody, IRobotLink proLink, IVoice proVoice,
                          IKebbiBody defBody, IRobotLink defLink, IVoice defVoice, Action<string> log)
        {
            _proBody = proBody; _proLink = proLink; _proVoice = proVoice;
            _defBody = defBody; _defLink = defLink; _defVoice = defVoice; _log = log ?? Console.WriteLine;
            _defLink.OnMessage((f, t) => { if (t == "YOUR_TURN") _defReady = true; });
            _proLink.OnMessage((f, t) => { if (t == "BACK") _proReady = true; });
        }

        // 一回合接力：控方陳述+指控 → 交棒 → 辯方反駁+攤手 → 交棒回控方。
        // proVotes/defVotes:該回合學生/評審投給雙方的票(可選,預設 0;成功完成交棒才計入)。
        public async Task RunExchangeAsync(string proLine, string defLine, int proVotes = 0, int defVotes = 0)
        {
            await _proVoice.SpeakAsync(proLine, "zh-TW");
            _proBody.SetMotor(KebbiMotor.RShoulderY, 70f); // 伸臂指控
            _log("   ⚖️ 控方伸臂指控");

            _defReady = false;
            await _proLink.SendAsync(_defLink.RobotId, "YOUR_TURN"); // 事件交棒
            if (!_defReady) return;

            await _defVoice.SpeakAsync(defLine, "zh-TW");
            _defBody.SetMotor(KebbiMotor.RShoulderY, 70f); // 攤手反駁
            _log("   ⚖️ 辯方攤手反駁");

            _proReady = false;
            await _defLink.SendAsync(_proLink.RobotId, "BACK");
            if (_proReady)
            {
                Exchanges++;
                ProVotes += proVotes; DefVotes += defVotes;
                if (proVotes != 0 || defVotes != 0)
                    _log("   🗳️ 本回合投票：控方 +" + proVotes + "、辯方 +" + defVotes + "（累計 控" + ProVotes + " : 辯" + DefVotes + "）");
            }
        }

        // 跑完整場辯論:逐回合接力+計票 → 結辯宣判。可重入(每場重置)。
        public async Task RunDebateAsync(List<Exchange> exchanges)
        {
            Exchanges = 0; ProVotes = 0; DefVotes = 0; Concluded = false;
            foreach (var ex in exchanges)
                await RunExchangeAsync(ex.Pro, ex.Def, ex.ProVotes, ex.DefVotes);
            await ConcludeAsync();
        }

        // 結辯:宣判勝負,勝方舉手致意(平手則兩方都不舉)。
        public async Task ConcludeAsync()
        {
            Concluded = true;
            _log("   ⚖️ 結辯宣判：控方 " + ProVotes + " 票 : 辯方 " + DefVotes + " 票 → " + Verdict);
            if (ProVotes > DefVotes) { await _proVoice.SpeakAsync("控方勝訴。", "zh-TW"); _proBody.SetMotor(KebbiMotor.RShoulderY, 100f); }
            else if (DefVotes > ProVotes) { await _defVoice.SpeakAsync("辯方勝訴。", "zh-TW"); _defBody.SetMotor(KebbiMotor.RShoulderY, 100f); }
            else _log("   🤝 平手,雙方握手致意");
        }

        // 結算報告。
        public void PrintSummary()
            => _log("=== G5 結算：" + Exchanges + " 回合辯論、" + CenterApproaches + " 次中央逼近、票數 控"
                    + ProVotes + ":辯" + DefVotes + " → " + Verdict + " ===");

        // 爭點升溫：兩機同時向中央移動逼近（H201 不動；輪式會走）
        public Task ApproachCenterAsync()
        {
            _log("   🔥 兩機同時向中央移動、正面對峙");
            _proBody.Move(0.1f); _defBody.Move(0.1f);
            _proBody.StopWheels(); _defBody.StopWheels();
            CenterApproaches++;
            return Task.CompletedTask;
        }

        // 學生席發言：近側機 DOA 轉頭面向該生並請出證。回傳是否能完整面向。
        public async Task<bool> TurnToStudentAsync(bool proSide, float doaDeg)
        {
            var body = proSide ? _proBody : _defBody;
            var voice = proSide ? _proVoice : _defVoice;
            float faced = KebbiHead.TurnToward(body, doaDeg, out bool reachable);
            if (!reachable) _log("   ⚠ 學生在 " + doaDeg.ToString("0.0") + "°，頭只能轉到 " + faced.ToString("0.0") + "°");
            await voice.SpeakAsync("請這位同學出示證據。", "zh-TW");
            return reachable;
        }

        // 範例：伽利略宗教審判（2 回合）
        public static List<Exchange> MakeGalileoDebate()
        {
            return new List<Exchange>
            {
                new Exchange("控方：地球靜止不動，這是聖經與亞里斯多德的教導。",
                             "辯方：但望遠鏡觀測到木星有四顆衛星繞行，顯示並非萬物皆繞地球。", proVotes: 1, defVotes: 2),
                new Exchange("控方：我們的感官明明感覺不到地球在運動。",
                             "辯方：慣性使我們感覺不到等速運動，這不構成地球靜止的反證。", proVotes: 1, defVotes: 3),
            };
        }
    }
}
