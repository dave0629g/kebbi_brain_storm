using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // 合體彩蛋《凱比聯合學園祭・多機接力大舞台》——G5 中控導演機側的「編排」邏輯(見 合體彩蛋_G2G3G5_多機協作.md)。
    // 中控依序 cue 各關卡站(G2 幾何/G3 律動/G5 對峙) → 站台回 ACK(我開始了)/DONE(我做完了) → 中控收到 DONE 才往下一站;
    // 壓軸:廣播 FINALE → 全體走位中央 + 同步勝利動作。
    // ★降級備案(doc 標「最重要」):任一站離線(逾時無 ACK)或卡住(有 ACK 但逾時無 DONE)→ 中控自動跳過該站,其餘照常,壓軸照跑。
    //   → 合體當天某台翻車也不拖垮全場,呼應「各隊獨立、彩蛋只是疊上去一層」。
    // 平板免疫核心:多台實體在共享空間被中控指揮著接力走位/轉頭/合做動作 —— 連好幾台平板並排也做不到。
    // 純 C#:建在 IRobotLink 上,Sim(SimRobotBus 同步遞送)可全程自測;實機換 UnityRobotLink(UDP)同款。
    // 等待機制用 LinkAwaiter(await ACK/DONE 帶逾時)→ Sim 同步遞送與 real 非同步遞送皆正確(解決舊版「同步讀旗標」只在 Sim 成立的問題)。
    // ⚠ 本類透過 LinkAwaiter 獨佔 director link 的唯一 OnMessage handler。若中控還需分派非 ACK/DONE 流量(如真機 FinaleDirector
    //   同機也要收 BC| 機身命令),務必用建構式的 alsoHandle 參數透傳,勿對同一條 link 再呼叫 OnMessage(會靜默覆寫、整路訊息消失)。
    public sealed class FinaleShowGame
    {
        private readonly IRobotLink _link;       // 中控的 link
        private readonly IKebbiBody _hostBody;   // 中控自己的機身(收尾動作)
        private readonly Action<string> _log;
        private readonly LinkAwaiter _awaiter;
        private readonly int _ackTimeoutMs, _doneTimeoutMs;

        public int StationsRun { get; private set; }
        public int StationsSkipped { get; private set; }
        public bool FinaleReached { get; private set; }

        // 一個關卡站:角色名(顯示用) + 該站機器人 ID(cue 的目標)。
        public sealed class Station
        {
            public string Role; public string RobotId;
            public Station(string role, string robotId) { Role = role; RobotId = robotId; }
        }

        // ackTimeoutMs:等「我開始了」的上限(過了算離線);doneTimeoutMs:等「我做完了」的上限(過了算卡住)。
        // alsoHandle:非 ACK/DONE 的訊息(中控若同機還需收其他流量)由此分派,避免對 director link 重複 OnMessage 互相覆寫。
        public FinaleShowGame(IRobotLink directorLink, IKebbiBody hostBody, Action<string> log,
                              Action<string, string> alsoHandle = null, int ackTimeoutMs = 1000, int doneTimeoutMs = 2500)
        {
            _link = directorLink; _hostBody = hostBody; _log = log ?? Console.WriteLine;
            _awaiter = new LinkAwaiter(directorLink, alsoHandle);
            _ackTimeoutMs = ackTimeoutMs; _doneTimeoutMs = doneTimeoutMs;
        }

        // 依序 cue 每一站;站台離線/卡住 → 自動跳過(降級),其餘照跑,最後一定進壓軸。可重入(每場計數歸零)。
        public async Task RunShowAsync(List<Station> stations)
        {
            StationsRun = 0; StationsSkipped = 0; FinaleReached = false;
            _log("🎬 中控開場：《凱比聯合學園祭・多機接力大舞台》開始！");
            foreach (var st in stations)
            {
                _log("📣 中控 cue → " + st.Role + "(" + st.RobotId + ")");
                // 鐵則:先註冊 ACK/DONE 等待者,再送 CUE(Sim 同步遞送下回覆會在 SendAsync 當下就到,先送會漏接)。
                // 只接受「被 cue 的那台(from==RobotId)」回的對應訊息,避免同名 role/串站/遲到封包誤判。
                var ackTask = _awaiter.WaitForAsync((f, t) => f == st.RobotId && t == "ACK|" + st.Role, _ackTimeoutMs);
                var doneTask = _awaiter.WaitForAsync((f, t) => f == st.RobotId && t == "DONE|" + st.Role, _doneTimeoutMs);
                await _link.SendAsync(st.RobotId, "CUE|" + st.Role);

                if (await ackTask == null)
                {
                    // 逾時連 ACK 都沒有(離線/同步失敗)→ 降級跳過。doneTask 無人等,會自行逾時清掉。
                    StationsSkipped++;
                    _log("   ⚠ " + st.Role + " 無回應(離線/同步失敗)→ 中控自動跳過(降級備案)");
                }
                else if (await doneTask == null)
                {
                    // 開始了卻逾時沒回 DONE(中途卡住)→ 不卡死全場,跳過續行。
                    StationsSkipped++;
                    _log("   ⚠ " + st.Role + " 已開始但未回報完成 → 中控跳過，繼續");
                }
                else
                {
                    StationsRun++;
                    _log("   ✅ " + st.Role + " 完成演出");
                }
            }
            await FinaleAsync();
        }

        // 壓軸:廣播 FINALE → 在場各站走位中央 + 同步勝利動作;中控自己也舉手收尾。
        private async Task FinaleAsync()
        {
            _log("🌟 中控：全體走位到舞台中央，一起做勝利動作！");
            await _link.BroadcastAsync("FINALE");
            _hostBody.SetMotor(KebbiMotor.RShoulderY, 100f); // 中控舉手
            FinaleReached = true;
            _log("   🙌 壓軸完成：跑了 " + StationsRun + " 站、跳過 " + StationsSkipped + " 站");
        }

        // 內建範例陣容:G2 幾何站、G3 律動站、G5 對峙站(壓軸夥伴機)。
        public static List<Station> MakeDefaultLineup()
        {
            return new List<Station>
            {
                new Station("G2 具身幾何站", "G2-站機"),
                new Station("G3 體感律動站", "G3-站機"),
                new Station("G5 對峙演出站", "G5-夥伴機"),
            };
        }
    }
}
