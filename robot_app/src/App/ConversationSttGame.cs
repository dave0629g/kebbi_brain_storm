using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // 「Kebbi ↔ 真人」聽說對話(STT 版,語意端點偵測)。見 README「Kebbi↔真人對話系統」一節。
    //
    // 真正要驗的能力:Kebbi 用「耳朵+腦」自己判斷『對方(真人)講完了沒』,不靠對方送任何網路信號。
    //   - 內容走空氣:對方說話 → Kebbi 麥克風聽 + Azure 整段批次 STT 轉文字(IVoice.ListenAsync)。
    //   - 端點(換誰說)走語意+靜音:每聽一個錄音窗就把 STT 文字累積起來,遇到靜音窗就問 LLM
    //     「累積到現在這句語意上講完了沒」(COMPLETE/INCOMPLETE)。INCOMPLETE=只是講到一半停頓想詞
    //     → 繼續聽、不插話;COMPLETE+靜音確認 → 才換 Kebbi 回應。完全不靠網路 floor token。
    //
    // 為何把前一版的 floor token 整段移除(使用者校正):
    //   前一版用網路信號 `STTFLOOR|done` 交棒,只有「兩台都是我們的機器」才成立 = 作弊。
    //   真實情境是 Kebbi 對一個『真人』,真人不會送 token,Kebbi 必須自己用聲學/語意判斷對方停了沒。
    //   floor token 只保留給多機協作(G1/G2/G5 雙 Kebbi 接力,見 ConversationGame 文字版),不用於真人對話。
    //
    // 依據(見 README References 第二輪):
    //   Phoenix-VAD 2025(語意端點)、LiveKit Turn Detector(純文字 transcript 判端點,以「高 recall 下的假插話率」為主指標)、
    //   Speculative End-Turn 2025(分清『真講完』vs『停頓想字』、未完就延長錄音)、
    //   STEER(Apple 2023,COMPLETE 後留短 grace 等對方是否要補一句/改口)、
    //   Schlangen-Skantze IU 2009(把『他講完了』當暫定假設、對方續講就 revoke 把兩段接起來)、
    //   Addlesee & Papaioannou 2025(長停頓/斷續說話是端點誤判主因 → 用語意而非純靜音)。
    //
    // 第二支手機 = 忠實的「真人替身」(Persona.Human=true):有 goal/agenda、反 assistant、會遲疑、
    //   會把話講一半再接(用 PauseMark 標 → 此處分段 TTS、插真實停頓),用真實停頓逼 Kebbi 判端點。
    //
    // _link 可選:只用於「兩支手機開機對齊(都就緒再開始)」的測試起跑同步,turn-taking 一律不碰它。
    // 純 C#(IVoice/ILlm 抽象)→ 主控台可自測(見 Tests.T_ConversationStt 的 VirtualAir 兩機端到端)。
    public sealed class ConversationSttGame
    {
        private readonly IVoice _voice;
        private readonly ILlm _llm;
        private readonly ConversationGame.Persona _me;
        private readonly string _peerName;
        private readonly Action<string> _log;
        private readonly IRobotLink _link;   // 可為 null;僅開場起跑同步用,非 turn 信號
        private readonly string _peerId;
        private readonly List<string> _history = new List<string>();

        // ── 端點偵測參數(語意完整度 + 靜音雙路)──
        public int ConfirmSilences = 1;     // 判 COMPLETE 後需幾個靜音窗確認(STEER 短 grace,等對方是否要補一句)
        public int MaxSilenceHold = 3;      // 連續靜音但語意一直判 INCOMPLETE 時,最多撐幾窗就收(對方真的講一半就不講了)
        public int MaxListenWindows = 40;   // 整輪聽窗上限(對方一直沒聲音 → 自我修正、主動再開口)
        public int PeerChunkGapMs = 700;    // 扮真人時:停頓標記處插入的真實停頓(逼對方判端點)

        // ── 起跑同步(可選,僅對齊兩支手機開機,非 turn 信號)──
        public int HandshakeIntervalMs = 600;
        private volatile bool _peerReady;
        private const string Ready = "STTSYNC|rdy";

        // ── 指標(供評測:Takeover Rate / 假插話率 / 隔空收音脆弱度)──
        public int MyTurns { get; private set; }
        public int HeardTurns { get; private set; }     // 我判定「對方講完」並接話的次數
        public int PauseHolds { get; private set; }     // 對方「講到一半停頓」時我忍住沒插話的次數(=不誤插的成績)
        public int TrailOffs { get; private set; }      // 對方講一半就不講了、我撐到上限才接話的次數
        public int Revocations { get; private set; }    // IU:已偵測靜音後對方又續講、把暫定判斷撤回串接的次數
        public int MissedListens { get; private set; }  // STT 聽到空窗(衡量隔空收音脆弱度)
        public IReadOnlyList<string> History => _history;

        public ConversationSttGame(IVoice voice, ILlm llm, ConversationGame.Persona me,
                                   string peerName = null, Action<string> log = null,
                                   IRobotLink link = null, string peerId = null)
        {
            _voice = voice; _llm = llm; _me = me;
            _peerName = string.IsNullOrEmpty(peerName) ? "對方" : peerName;
            _log = log; _link = link; _peerId = peerId;
            if (_link != null)
                _link.OnMessage((from, t) => { if (t == Ready) _peerReady = true; });
        }

        public async Task RunAsync(bool starter, int maxTurns = 0)
        {
            _log?.Invoke($"💬(STT) {_me.Name}{(_me.Human ? "(扮真人)" : "")} ↔ {_peerName}:語意端點偵測(不靠網路 token)" +
                         (starter ? ",我先開口" : ",先聽對方"));
            if (_link != null) await ReadySyncAsync();

            if (starter) await SpeakTurnAsync(opening: true);

            while (maxTurns <= 0 || MyTurns < maxTurns)
            {
                string heard = await ListenForPeerTurnAsync();   // 我自己判斷對方何時講完(無 token)
                if (!string.IsNullOrWhiteSpace(heard))
                {
                    _history.Add(_peerName + ": " + heard);
                    HeardTurns++;
                    _log?.Invoke($"👂(STT) 判定 {_peerName} 講完: {heard}");
                }
                else _log?.Invoke("⌛(STT) 對方久無聲音 → 我主動再開口(自我修正)");
                if (maxTurns > 0 && MyTurns >= maxTurns) break;
                await SpeakTurnAsync(opening: false);
            }
            _log?.Invoke($"💬(STT) {_me.Name} 結束(我說 {MyTurns} 句、聽到對方 {HeardTurns} 輪、" +
                         $"忍住不插話 {PauseHolds} 次、串接 {Revocations} 次)");
        }

        // 聽對方說話,用「語意完整度 + 靜音」自己判斷對方何時講完(不靠對方送信號)。
        // INCOMPLETE 是「繼續聽、不要回」的明確狀態(投機式,不固定秒數就插話);
        // 對方在偵測到靜音後又續講 → revoke 暫定判斷、把兩段 transcript 串接再判(IU 可撤回假設)。
        private async Task<string> ListenForPeerTurnAsync()
        {
            string buf = "";
            int windows = 0, silenceRun = 0;
            bool sawSilence = false;       // 進過「靜音(暫定講完)」狀態嗎?(用來算 revoke)
            while (windows < MaxListenWindows)
            {
                string h;
                try { h = (await _voice.ListenAsync(_me.Lang) ?? "").Trim(); }
                catch (Exception e) { _log?.Invoke("⚠ STT 失敗: " + e.Message); h = ""; }
                windows++;

                if (h.Length > 0)
                {
                    if (sawSilence) { Revocations++; sawSilence = false; }  // 對方又開口 → 撤回「他講完了」的暫定判斷
                    buf = buf.Length == 0 ? h : buf + " " + h;             // IU:把切半的句子串接起來
                    silenceRun = 0;
                    continue;
                }

                // 靜音窗
                MissedListens++;
                if (buf.Length == 0) continue;     // 對方還沒開口,繼續等(別把開頭的沉默當講完)
                silenceRun++; sawSilence = true;
                bool complete = await JudgeCompleteAsync(buf);
                if (complete)
                {
                    if (silenceRun >= ConfirmSilences) return buf;  // 語意完整 + 靜音確認 → 換我說
                    continue;                                        // 再等一個靜音窗(STEER grace,讓對方有機會補一句)
                }
                // INCOMPLETE = 對方只是講到一半停頓想詞 → 忍住不插話、繼續聽
                if (silenceRun >= MaxSilenceHold) { TrailOffs++; return buf; }  // 但對方真的不講了 → 撐到上限才接
                PauseHolds++;
            }
            return buf; // 安全上限(避免對方掉線無限等)
        }

        // LLM 判端點:對方目前說出的內容,語意上講完了沒(回 COMPLETE/INCOMPLETE)。
        // 純文字 → 合我們無 partial 的批次 STT;判不出來時傾向「講完」(避免卡死,靜音確認那層仍把關)。
        private async Task<bool> JudgeCompleteAsync(string heardSoFar)
        {
            try
            {
                string ans = (await _llm.AskAsync(_me.EndpointJudgeSystem(), heardSoFar) ?? "").Trim().ToUpperInvariant();
                if (ans.Contains("INCOMPLETE")) return false;  // 先比 INCOMPLETE(它含子字串 COMPLETE)
                if (ans.Contains("COMPLETE")) return true;
                return true;
            }
            catch (Exception e) { _log?.Invoke("⚠ 端點判斷失敗,當作講完: " + e.Message); return true; }
        }

        // 換我說:LLM 生台詞 → TTS 說(等播畢)。扮真人時遇停頓標記分段說、中間插真實停頓(逼對方判端點)。
        private async Task SpeakTurnAsync(bool opening)
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
            await SpeakMaybeChunkedAsync(line);
        }

        // 扮真人:把停頓標記(…)切段、逐段 TTS、段間插真實停頓 → 對方 STT 會看到「半句 → 靜音 → 補完」,
        //   專逼固定秒數錄音最容易切錯的點。非真人(Kebbi):整句一次說完。
        private async Task SpeakMaybeChunkedAsync(string line)
        {
            string mark = ConversationGame.Persona.PauseMark;
            if (!_me.Human || line.IndexOf(mark, StringComparison.Ordinal) < 0)
            {
                await _voice.SpeakAsync(line, _me.Lang);
                return;
            }
            var parts = line.Split(new[] { mark }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                string seg = parts[i].Trim();
                if (seg.Length > 0) await _voice.SpeakAsync(seg, _me.Lang);
                if (i < parts.Length - 1) await Task.Delay(PeerChunkGapMs); // 真實的「想詞」停頓
            }
        }

        // 起跑同步(可選,僅對齊兩支手機開機;不是 turn 信號,turn-taking 一律不碰)。
        private async Task ReadySyncAsync()
        {
            for (int i = 0; i < 20 && !_peerReady; i++)
            {
                await SendReadyAsync();
                await Task.Delay(HandshakeIntervalMs);
            }
            await SendReadyAsync();
            _log?.Invoke(_peerReady ? "🤝(STT) 起跑對齊完成" : "⚠(STT) 起跑對齊逾時,仍開始(靠聲學自癒)");
        }

        private Task SendReadyAsync()
            => string.IsNullOrEmpty(_peerId) ? _link.BroadcastAsync(Ready) : _link.SendAsync(_peerId, Ready);

        private static string Clean(string s) => (s ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
    }
}
