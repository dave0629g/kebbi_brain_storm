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

        public sealed class Exchange
        {
            public string Pro; public string Def;
            public Exchange(string pro, string def) { Pro = pro; Def = def; }
        }

        public DebateGame(IKebbiBody proBody, IRobotLink proLink, IVoice proVoice,
                          IKebbiBody defBody, IRobotLink defLink, IVoice defVoice, Action<string> log)
        {
            _proBody = proBody; _proLink = proLink; _proVoice = proVoice;
            _defBody = defBody; _defLink = defLink; _defVoice = defVoice; _log = log ?? Console.WriteLine;
            _defLink.OnMessage((f, t) => { if (t == "YOUR_TURN") _defReady = true; });
            _proLink.OnMessage((f, t) => { if (t == "BACK") _proReady = true; });
        }

        // 一回合接力：控方陳述+指控 → 交棒 → 辯方反駁+攤手 → 交棒回控方
        public async Task RunExchangeAsync(string proLine, string defLine)
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
            if (_proReady) Exchanges++;
        }

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
                             "辯方：但望遠鏡觀測到木星有四顆衛星繞行，顯示並非萬物皆繞地球。"),
                new Exchange("控方：我們的感官明明感覺不到地球在運動。",
                             "辯方：慣性使我們感覺不到等速運動，這不構成地球靜止的反證。"),
            };
        }
    }
}
