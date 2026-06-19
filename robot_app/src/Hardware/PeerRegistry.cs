using System.Collections.Generic;

namespace KebbiBrain.Hardware
{
    // 多機 UDP 的「已知對端 IP」登記簿（純 C#、無 Unity 相依 → 主控台可自測）。
    //
    // 為何需要:實機驗證發現有些 WiFi AP 會「丟棄無線客戶端之間的廣播幀」
    //   (unicast 通、255.255.255.255 / 子網廣播不通)。此時單靠 BroadcastAsync 兩機互相收不到。
    //   解法:除了照舊廣播,另對「已知對端 IP」直接 unicast,任何 AP 都能通。
    //
    // 來源有二:
    //   1) 靜態設定(AddStatic):上線前把對方 IP 填進來,最可靠(廣播全失也能通)。
    //   2) 自動學習(Learn):收到任何封包就把來源 IP 記下,之後回送改走 unicast;
    //      只要單一方向能先通(或先用靜態設定起手),另一向就能自動補上。
    //
    // 執行緒安全:接收緒會 Learn、傳送緒會讀 Snapshot,故全用同一把鎖保護。
    public sealed class PeerRegistry
    {
        private readonly HashSet<string> _peers = new HashSet<string>();
        private readonly string _selfIp;

        // selfIp:本機 IP,用來避免把自己的廣播來源學進來(免得對自己 unicast)。可為 null。
        public PeerRegistry(string selfIp = null)
        {
            _selfIp = string.IsNullOrEmpty(selfIp) ? null : selfIp;
        }

        // 靜態設定一個對端 IP。回傳是否真的新增(已存在或無效則 false)。
        public bool AddStatic(string ip) => Add(ip);

        // 從收到的封包來源學習一個對端 IP。回傳是否為「新學到」的(可用來決定要不要 log)。
        public bool Learn(string ip) => Add(ip);

        private bool Add(string ip)
        {
            if (!IsUsable(ip)) return false;
            lock (_peers) { return _peers.Add(ip); }
        }

        // 目前已知的所有對端 IP(快照,傳送緒據此逐一 unicast)。
        public List<string> Snapshot()
        {
            lock (_peers) { return new List<string>(_peers); }
        }

        public int Count { get { lock (_peers) { return _peers.Count; } } }

        public bool Knows(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return false;
            lock (_peers) { return _peers.Contains(ip); }
        }

        // 可用判斷:非空、非自己、排除明顯無效/萬用位址、且須為合法 IP 格式
        // (畸形靜態 peer 若放行,送出時 IPAddress.Parse 會每輪丟例外/log warning → 註冊時就擋掉,fail-fast)。
        private bool IsUsable(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return false;
            if (ip == _selfIp) return false;
            if (ip == "0.0.0.0" || ip == "255.255.255.255") return false;
            if (!System.Net.IPAddress.TryParse(ip, out _)) return false; // 畸形 IP(如 300.300.300.300)→ 拒絕
            return true;
        }
    }
}
