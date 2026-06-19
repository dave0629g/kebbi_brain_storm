using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // 兩台 Kebbi「用印尼語互相對話」的 app(各有人格 persona)。純 C#(IVoice/ILlm/IRobotLink 抽象)→ 主控台可自測。
    //
    // 一來一往的時機怎麼抓(使用者要求「知道對方停下來才主動開口、自我修正時機」):
    //   靠「說完才交棒」——我把台詞用 LLM 生出來 → 用 TTS 說、且 await 到「播畢」才回 → 播畢後才送 `CV|台詞` 給對方。
    //   對方「收到 CV|」這個事件本身就代表「我已經說完停下來了」→ 對方據此開口。不靠猜時間,所以時機自然正確。
    //   自我修正:若輪到我等對方、但 waitMs 內等不到對方的 CV|(對方掉線/出錯),我就主動再開口一次(不會卡死)。
    //   不撞車:同一時間只有一方「持 token」(剛收到 CV| 的那方),另一方在等 → 不會兩邊同時講。
    //
    // 線格式:`CV|<台詞>`(前綴 CV 區隔 BC/VC/HANDOFF 等)。
    public sealed class ConversationGame
    {
        public sealed class Persona
        {
            public string Name;       // 角色名(會出現在對話與 TTS,如 "Andi")
            public string Character;   // 人格描述(印尼語或中文皆可,餵進 LLM system)
            public string Lang = "id-ID";
            // 給 LLM 的 system:鎖印尼語、單句、保持人格、別複讀對方。
            public string SystemPrompt()
                => $"Kamu adalah {Name}. {Character} " +
                   "Bicaralah HANYA dalam Bahasa Indonesia, satu kalimat singkat, natural, tetap dalam karakter. " +
                   "Tanggapi lawan bicara; jangan mengulang kata-katanya.";
        }

        public const string Pfx = "CV";
        private readonly IVoice _voice;
        private readonly ILlm _llm;
        private readonly IRobotLink _link;
        private readonly Persona _me;
        private readonly string _peerId;
        private readonly string _peerName;
        private readonly Action<string> _log;
        private readonly List<string> _history = new List<string>();

        public int MyTurns { get; private set; }
        public IReadOnlyList<string> History => _history;
        public int HandshakeIntervalMs = 800; // 握手重送間隔(測試可調小)
        private const string Ready = Pfx + "|__RDY__";

        public ConversationGame(IVoice voice, ILlm llm, IRobotLink link, Persona me,
                                string peerId, string peerName = null, Action<string> log = null)
        {
            _voice = voice; _llm = llm; _link = link; _me = me;
            _peerId = peerId; _peerName = string.IsNullOrEmpty(peerName) ? peerId : peerName;
            _log = log;
        }

        // starter=true 的那台先開口;maxTurns>0 為自己最多講幾句(測試/展演用),<=0 為持續對話。
        // waitMs:輪到我等對方回應的逾時(等不到就自我修正、主動再開口)。
        public async Task RunAsync(bool starter, int maxTurns = 0, int waitMs = 20000)
        {
            var awaiter = new LinkAwaiter(_link); // 等對方的 CV|/RDY
            _log?.Invoke($"💬 {_me.Name} 開始對話(對方={_peerName},{(starter ? "我先說" : "等對方先說")})");

            // 開場握手:重送 RDY 直到收到對方 RDY → 確保兩邊都在聽才開始
            // (否則 starter 先講、對方還沒開始聽 → 第一句漏掉)。
            await HandshakeAsync(awaiter);

            if (!starter)
            {
                var first = await awaiter.WaitForAsync(IsLine, waitMs);
                if (first == null) { _log?.Invoke("⌛ 等不到對方開場,改由我先說(自我修正)"); }
                else RecordPeer(first);
            }

            while (maxTurns <= 0 || MyTurns < maxTurns)
            {
                await SpeakMyTurnAsync();
                if (maxTurns > 0 && MyTurns >= maxTurns) break;

                var reply = await awaiter.WaitForAsync(IsLine, waitMs);
                if (reply == null)
                {
                    _log?.Invoke("⌛ 等不到對方回應 → 主動再開口(自我修正時機)");
                    continue; // 不卡死:自己再講一句帶動對話
                }
                RecordPeer(reply);
            }
            _log?.Invoke($"💬 {_me.Name} 對話結束(我講了 {MyTurns} 句)");
        }

        private async Task SpeakMyTurnAsync()
        {
            string user = _history.Count == 0
                ? "Mulai percakapan dengan sapaan ramah."
                : string.Join("\n", _history) + $"\n{_me.Name}:";
            string line;
            try { line = Clean(await _llm.AskAsync(_me.SystemPrompt(), user)); }
            catch (Exception e) { _log?.Invoke("⚠ LLM 失敗,用簡單回應: " + e.Message); line = "Maaf, bisa ulangi?"; }
            if (line.Length == 0) line = "Hmm, menarik.";

            _history.Add(_me.Name + ": " + line);
            MyTurns++;
            _log?.Invoke($"🗣️ {_me.Name}: {line}");
            await _voice.SpeakAsync(line, _me.Lang);          // 等播畢(real 端 UnityVoice 等 AudioSource 結束)
            await _link.SendAsync(_peerId, Pfx + "|" + line); // 播畢後才交棒 → 對方據此知道我停了、輪到他
        }

        private void RecordPeer(string framed)
        {
            string line = framed.Length > Pfx.Length + 1 ? framed.Substring(Pfx.Length + 1) : "";
            _history.Add(_peerName + ": " + line);
            _log?.Invoke($"👂 {_peerName}: {line}");
        }

        // 開場握手:每 HandshakeIntervalMs 送一次 RDY,直到收到對方 RDY(最多 ~24 秒)。
        // 收到對方 RDY 後再回送一次(對稱,確保對方也收到),雙方據此確認都在聽。
        private async Task HandshakeAsync(LinkAwaiter awaiter)
        {
            for (int i = 0; i < 30; i++)
            {
                await _link.SendAsync(_peerId, Ready);
                var got = await awaiter.WaitForAsync((f, t) => f == _peerId && t == Ready, HandshakeIntervalMs);
                if (got != null)
                {
                    await _link.SendAsync(_peerId, Ready); // 補一次,讓對方也收到
                    _log?.Invoke("🤝 握手完成,雙方就緒");
                    return;
                }
            }
            _log?.Invoke("⚠ 握手逾時(對方可能未上線),仍嘗試對話");
        }

        // 對話台詞(CV| 但非 RDY 握手訊息)
        private bool IsLine(string f, string t) => f == _peerId && IsCv(t) && t != Ready;
        private static bool IsCv(string t) => !string.IsNullOrEmpty(t) && t.StartsWith(Pfx + "|", StringComparison.Ordinal);
        private static string Clean(string s) => (s ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
    }
}
