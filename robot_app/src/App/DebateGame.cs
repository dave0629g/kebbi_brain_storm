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
        // 可選相依（G5-branch 七步審判 + 學生插話分支）：
        //   _studentMic = 學生席麥克風（null=不啟用插話偵測，腳本仍可用 InterjectionText 直接模擬）
        //   _poseSensor = 可選舉手確認（null=略過舉手確認，插話一律走分支）
        private readonly IVoice _studentMic;
        private readonly IPoseSensor _poseSensor;
        private bool _defReady, _proReady;

        public int Exchanges { get; private set; }
        public int CenterApproaches { get; private set; }

        // 結辯投票計分:每回合學生/評審投給較有說服力的一方,累計後宣判。
        public int ProVotes { get; private set; }
        public int DefVotes { get; private set; }
        public bool Concluded { get; private set; }
        // 本場成功處理的學生插話數（七步審判用；可重入時於 RunTrialAsync 開頭重置）。
        public int Interjections { get; private set; }
        // 勝負(票多者勝;同票平手)。
        public string Verdict => ProVotes > DefVotes ? "控方勝" : DefVotes > ProVotes ? "辯方勝" : "平手";

        public sealed class Exchange
        {
            public string Pro; public string Def;
            public int ProVotes; public int DefVotes; // 該回合雙方各得幾票(可選,預設 0)
            public Exchange(string pro, string def, int proVotes = 0, int defVotes = 0)
            { Pro = pro; Def = def; ProVotes = proVotes; DefVotes = defVotes; }
        }

        // 學生席「舉手插話」腳本模型：哪一側機較近轉頭、學生席方位、要說的話、對該回合票數的加成。
        // 走語音/姿態通道（非 link 通道），以免覆寫建構式已註冊的 YOUR_TURN/BACK 單一 handler。
        public sealed class StudentInterjection
        {
            public bool ProSide;                 // true=控方側機較近、由它轉頭面向；false=辯方側
            public float DoaDeg;                 // 學生席方位(度)
            public string ExpectedHand = "舉手"; // 給 IPoseSensor 確認的動作名（可選）
            public string InterjectionText;      // 模擬學生會說的話（供 log/腳本；real 用 _studentMic.ListenAsync 取代）
            public int ProVoteDelta;             // 此插話對該回合控方票數的加成（預設 0=不影響）
            public int DefVoteDelta;             // 此插話對該回合辯方票數的加成（預設 0=不影響）
        }

        // 一整場 7 步審判腳本：開場白 → 逐回合（陳述/交棒/反駁，可掛插話）→ 逼近 → 轉向學生 → 結辯。
        public sealed class TrialScript
        {
            public string Opening = "本庭現在開庭，請雙方陳述。";
            public string Closing;                                   // null=用 ConcludeAsync 預設宣判詞
            public List<Exchange> Rounds;                            // 沿用既有 Exchange(pro/def/proVotes/defVotes)
            public Dictionary<int, StudentInterjection> Interjections; // key=回合索引(0 起)，可空
        }

        public DebateGame(IKebbiBody proBody, IRobotLink proLink, IVoice proVoice,
                          IKebbiBody defBody, IRobotLink defLink, IVoice defVoice, Action<string> log)
            : this(proBody, proLink, proVoice, defBody, defLink, defVoice, log,
                   studentMic: null, poseSensor: null)
        {
            // 舊 7 參數簽章原封保留 → 轉呼叫新建構式帶 null（既有測試/Program 不動）。
        }

        public DebateGame(IKebbiBody proBody, IRobotLink proLink, IVoice proVoice,
                          IKebbiBody defBody, IRobotLink defLink, IVoice defVoice, Action<string> log,
                          IVoice studentMic, IPoseSensor poseSensor)
        {
            _proBody = proBody; _proLink = proLink; _proVoice = proVoice;
            _defBody = defBody; _defLink = defLink; _defVoice = defVoice; _log = log ?? Console.WriteLine;
            _studentMic = studentMic; _poseSensor = poseSensor;
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
            Exchanges = 0; ProVotes = 0; DefVotes = 0; Interjections = 0; Concluded = false;
            foreach (var ex in exchanges)
                await RunExchangeAsync(ex.Pro, ex.Def, ex.ProVotes, ex.DefVotes);
            await ConcludeAsync();
        }

        // 七步審判驅動器：把既有原語（開場/陳述/交棒/反駁/逼近/轉向學生/結辯）串成固定 7 步流程，
        // 並在每回合交棒後檢查腳本是否掛了「學生舉手插話」分支。可重入（每場重置場次狀態）。
        // 退化保證：無插話且 studentMic/poseSensor 皆 null 時，票數/回合/逼近結果與 RunDebateAsync 路徑一致。
        public async Task RunTrialAsync(TrialScript s)
        {
            if (s == null) return;
            // 可重入：場次狀態重置（同 RunDebateAsync 慣例；累計類在此一次歸零，避免插話加成跨場疊加）。
            Exchanges = 0; ProVotes = 0; DefVotes = 0; Interjections = 0; Concluded = false;

            // 步驟 1：開庭——由控方機說開場白（不入票）。
            await _proVoice.SpeakAsync(s.Opening ?? "", "zh-TW");
            _log("   ⚖️ [1/7] 開庭");

            var rounds = s.Rounds ?? new List<Exchange>();
            StudentInterjection lastHandled = null; // 步驟 6 只面向「真正處理過」的最後一筆插話（沒舉手被擋者不算）
            for (int i = 0; i < rounds.Count; i++)
            {
                var ex = rounds[i];
                // 步驟 2-4：陳述 → 交棒 → 反駁（+本回合基礎票），直接複用既有 RunExchangeAsync（零改動）。
                _log("   ⚖️ [2-4/7] 第" + (i + 1) + "回合 陳述→交棒→反駁");
                await RunExchangeAsync(ex.Pro, ex.Def, ex.ProVotes, ex.DefVotes);

                // 學生插話分支（交棒回控方後檢查）；回傳是否真的走完分支（姿態 gate 沒舉手則回 false）。
                StudentInterjection inj;
                if (s.Interjections != null && s.Interjections.TryGetValue(i, out inj) && inj != null)
                {
                    bool handled = await HandleInterjectionAsync(inj);
                    if (handled) lastHandled = inj;
                }
            }

            // 步驟 5：爭點逼近（複用）。
            _log("   ⚖️ [5/7] 爭點逼近");
            await ApproachCenterAsync();

            // 步驟 6：轉向學生席收尾（只在有「真正處理過」的插話時面向最後一位；沒舉手被擋者不觸發 → 不轉頭）。
            if (lastHandled != null)
            {
                _log("   ⚖️ [6/7] 轉向學生席收尾");
                await TurnToStudentAsync(lastHandled.ProSide, lastHandled.DoaDeg);
            }

            // 步驟 7：結辯宣判（複用；Closing 可覆寫預設宣判詞，由控方機先說）。
            _log("   ⚖️ [7/7] 結辯宣判");
            if (s.Closing != null) await _proVoice.SpeakAsync(s.Closing, "zh-TW");
            await ConcludeAsync();
        }

        // 學生插話分支：可選舉手確認 → 近側機轉頭面向 → 收一句 → 該側機回應 → 調整本回合票數。
        // 回傳 true=真的走完分支（轉頭/收音/改票/計數）；false=姿態 gate 沒舉手被擋（不轉頭、不改票、不計數）。
        private async Task<bool> HandleInterjectionAsync(StudentInterjection inj)
        {
            if (inj == null) return false;
            if (_poseSensor != null)
            {
                bool raised = await _poseSensor.CheckPoseAsync(inj.ExpectedHand ?? "舉手");
                if (!raised) { _log("   🙅 未偵測到舉手，忽略插話"); return false; } // 未舉手 → 不轉頭、不改票、不計插話
            }

            bool faced = await TurnToStudentAsync(inj.ProSide, inj.DoaDeg); // 複用既有轉向（含 NeckZ 夾限）
            string heard = inj.InterjectionText;
            if (_studentMic != null) heard = await _studentMic.ListenAsync("zh-TW"); // real：真的聽一句（Sim 用 EnqueueHeard）
            _log("   🙋 學生插話(" + (faced ? "已面向" : "僅部分面向") + ")：「" + heard + "」");

            var voice = inj.ProSide ? _proVoice : _defVoice;
            await voice.SpeakAsync("謝謝這位同學的補充，我方回應如下。", "zh-TW");

            ProVotes += inj.ProVoteDelta; DefVotes += inj.DefVoteDelta; // 插話影響票數（預設 0=不影響）
            Interjections++;
            if (inj.ProVoteDelta != 0 || inj.DefVoteDelta != 0)
                _log("   🗳️ 插話加成：控+" + inj.ProVoteDelta + " 辯+" + inj.DefVoteDelta
                     + "（累計 控" + ProVotes + ":辯" + DefVotes + "）");
            return true;
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
            => _log("=== G5 結算：" + Exchanges + " 回合辯論、" + CenterApproaches + " 次中央逼近、"
                    + Interjections + " 次學生插話、票數 控"
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
            var r = KebbiHead.FaceFully(body, doaDeg);   // 輪式:底盤轉粗方向+頭補細→完整面向;H201桌上型:頭部部分面向
            if (r.FullyFaced && r.BaseTurnDeg != 0f)
                _log("   ↪️ 底盤轉 " + r.BaseTurnDeg.ToString("0") + "° + 頭 " + r.HeadDeg.ToString("0") + "° → 完整面向學生");
            else if (!r.FullyFaced)
                _log("   ⚠ 學生在 " + doaDeg.ToString("0.0") + "°，" + (body.CanMove ? "" : "無底盤、") + "頭只能轉到 " + r.HeadDeg.ToString("0.0") + "°（部分面向）");
            await voice.SpeakAsync("請這位同學出示證據。", "zh-TW");
            return r.FullyFaced;
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

        // 範例：把伽利略辯論包成一場含「學生舉手插話」的 7 步審判腳本。
        // 第 2 回合（索引 1）右後方學生舉手補充，使辯方再 +1 票（驗插話確實改票）。
        public static TrialScript MakeGalileoTrial()
        {
            var interjections = new Dictionary<int, StudentInterjection>();
            interjections[1] = new StudentInterjection
            {
                ProSide = false,
                DoaDeg = 120f,
                InterjectionText = "可是教會後來也承認了日心說。",
                DefVoteDelta = 1,
            };
            return new TrialScript
            {
                Opening = "本庭現在開庭，審理『地球是否靜止』一案，請雙方陳述。",
                Rounds = MakeGalileoDebate(),
                Interjections = interjections,
            };
        }
    }
}