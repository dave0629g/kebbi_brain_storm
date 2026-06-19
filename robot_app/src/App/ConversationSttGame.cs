using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // 「真·兩機聽說對話」STT 版 — 改良版(套用 turn-taking 研究,見 README References)。
    //
    // 設計取捨(文獻公認最可靠解):**內容走空氣、發言權(floor)走網路 token**。
    //   - 內容:我說(TTS)→ 對方用麥克風聽 + Azure STT 轉文字(真的用耳朵聽,不走網路傳台詞)。
    //   - 發言權:用一個極小的網路信號 `STTFLOOR|done` 當 floor token——任一刻只有持 token 的能說,
    //     說完(TTS 播畢)才把 token 交給對方。對方「收到 token」才開口。
    // 為何這樣:純對稱兩端各跑自己的「聽 N 秒→說」時鐘 = 沒有可靠載波偵測的 CSMA,注定週期碰撞
    //   (同時說/同時聽/都沒聽到),見 Sacks/Schegloff/Jefferson 1974、Skantze 2021、Token-DCF、802.11 DCF。
    //   floor token = explicit floor release(Bohus-Horvitz)= token passing,延遲最低、確定性最高。
    //   且 token 在「播畢後」才送 → 同時解了「端點誤判插話」(對方明確說『我講完了』)。
    // 跟文字版的差別:文字版「內容也走網路」;此版「內容真的走空氣靠 STT 聽」,只有 floor 走網路。
    // 純 C#(IVoice/ILlm/IRobotLink 抽象)→ 主控台可自測。
    public sealed class ConversationSttGame
    {
        private readonly IVoice _voice;
        private readonly ILlm _llm;
        private readonly IRobotLink _link;
        private readonly ConversationGame.Persona _me;
        private readonly string _peerId, _peerName;
        private readonly Action<string> _log;
        private readonly List<string> _history = new List<string>();

        private volatile bool _gotFloor;   // 收到對方交來的 floor token
        private volatile bool _peerReady;   // 握手:對方已就緒
        private const string Floor = "STTFLOOR|done";
        private const string Ready = "STTFLOOR|rdy";

        public int MyTurns { get; private set; }
        public int MissedListens { get; private set; }      // 一輪內 STT 聽到空的次數(衡量隔空收音脆弱度)
        public int ListenWindowsPerTurn = 1;                  // 拿到 floor 前每輪最多聽幾窗(各 ~4s);超過視為對方掉線自我修正
        public int MaxListenWindowsWaitingFloor = 8;          // 等 floor 期間最多聽幾窗(防對方掉線無限等)
        public int HandshakeIntervalMs = 800;
        public IReadOnlyList<string> History => _history;

        public ConversationSttGame(IVoice voice, ILlm llm, IRobotLink link,
                                   ConversationGame.Persona me, string peerId, string peerName = null, Action<string> log = null)
        {
            _voice = voice; _llm = llm; _link = link; _me = me;
            _peerId = peerId; _peerName = string.IsNullOrEmpty(peerName) ? peerId : peerName; _log = log;
            _link.OnMessage((from, t) =>
            {
                if (from != _peerId) return;
                if (t == Floor) _gotFloor = true;
                else if (t == Ready) _peerReady = true;
            });
        }

        public async Task RunAsync(bool starter, int maxTurns = 0)
        {
            _log?.Invoke($"💬(STT) {_me.Name} 聽說對話(對方={_peerName},floor token 交棒;內容走空氣)" +
                         (starter ? ",我先持 floor" : ",先聽對方"));
            await HandshakeAsync();

            if (starter) { await SpeakAndPassFloorAsync(opening: true); }

            while (maxTurns <= 0 || MyTurns < maxTurns)
            {
                string heard = await ListenUntilFloorAsync();   // 持續聽對方(STT 累積)直到收到 floor token
                _gotFloor = false;                              // 消費 token,我取得發言權
                if (!string.IsNullOrWhiteSpace(heard))
                {
                    _history.Add(_peerName + ": " + heard);
                    _log?.Invoke($"👂(STT) 聽到 {_peerName}: {heard}");
                }
                else _log?.Invoke("⌛(STT) 拿到發言權但沒聽清對方(隔空收音失敗),我照樣接話");
                if (maxTurns > 0 && MyTurns >= maxTurns) break;
                await SpeakAndPassFloorAsync(opening: false);
            }
            _log?.Invoke($"💬(STT) {_me.Name} 結束(我講 {MyTurns} 句,STT 漏聽 {MissedListens} 窗)");
        }

        // 開場握手:重送 RDY 直到對方就緒(確保兩邊都在聽再開始)
        private async Task HandshakeAsync()
        {
            for (int i = 0; i < 30 && !_peerReady; i++)
            {
                await _link.SendAsync(_peerId, Ready);
                await Task.Delay(HandshakeIntervalMs);
            }
            await _link.SendAsync(_peerId, Ready); // 補一次讓對方也收到
            _log?.Invoke(_peerReady ? "🤝(STT) 握手完成" : "⚠(STT) 握手逾時,仍嘗試");
        }

        // 沒持 floor 時:持續用 STT 聽對方說話、累積文字,直到收到對方交來的 floor token(=對方說完了)。
        private async Task<string> ListenUntilFloorAsync()
        {
            string buf = "";
            int windows = 0;
            while (!_gotFloor && windows < MaxListenWindowsWaitingFloor)
            {
                string h;
                try { h = (await _voice.ListenAsync(_me.Lang) ?? "").Trim(); }
                catch (Exception e) { _log?.Invoke("⚠ STT 失敗: " + e.Message); h = ""; }
                windows++;
                if (h.Length > 0) buf = buf.Length == 0 ? h : buf + " " + h;
                else MissedListens++;
            }
            return buf;
        }

        // 持 floor 時:LLM 生台詞 → TTS 說(等播畢)→ 把 floor token 交給對方(對方據此開口)。
        private async Task SpeakAndPassFloorAsync(bool opening)
        {
            string user = _history.Count == 0 ? _me.OpeningUser()
                                              : string.Join("\n", _history) + $"\n{_me.Name}:";
            string line;
            try { line = Clean(await _llm.AskAsync(_me.SystemPrompt(), user)); }
            catch (Exception e) { _log?.Invoke("⚠ LLM 失敗: " + e.Message); line = _me.OpeningUser(); }
            if (line.Length == 0) line = "...";
            _history.Add(_me.Name + ": " + line);
            MyTurns++;
            _log?.Invoke($"🗣️(STT) {_me.Name}: {line}");
            await _voice.SpeakAsync(line, _me.Lang);     // 等播畢(UnityVoice 等 AudioSource 結束)
            await _link.SendAsync(_peerId, Floor);        // 播畢後才交 floor → 對方明確知道我講完了
        }

        private static string Clean(string s) => (s ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
    }
}
