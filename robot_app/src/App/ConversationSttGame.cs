using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // 「真·兩機聽說對話」STT 版(對比 ConversationGame 的文字直送版)。純 C# 可測。
    //
    // 跟文字版的差別:對話內容「不走網路」,完全靠空氣傳聲——
    //   我說(TTS)→ 對方用「麥克風聽 + STT」把我的話轉成文字 → 餵自己的 LLM → 回應(TTS)。
    //   兩台必須擺很近(對方麥克風要收得到我的喇叭)。
    // 交棒:沒有網路信號,靠「聽到對方說話(STT 非空)」當『輪到我』;聽到空(沒人講/沒聽清)
    //   就『主動再說一句打破沉默』(自我修正,不卡死)。
    //
    // ⚠️ 先天脆弱(這就是對比要展示的):ListenAsync 是固定 4 秒窗,可能錄到對方還沒開口的空檔、
    //   或只錄到半句;STT 也會聽錯,錯誤會累積進 LLM 上下文。文字直送版沒有這些問題。
    public sealed class ConversationSttGame
    {
        private readonly IVoice _voice;
        private readonly ILlm _llm;
        private readonly ConversationGame.Persona _me;
        private readonly string _peerName;
        private readonly Action<string> _log;
        private readonly List<string> _history = new List<string>();

        public int MyTurns { get; private set; }
        public int MissedListens { get; private set; }   // 聽到空(沒收到/沒聽清)的次數 → 衡量脆弱度
        public int MinListenRetries = 2;                  // 一輪聽的窗數下限
        public int MaxListenRetries = 5;                  // 一輪聽的窗數上限(各 ~4 秒)
        public int StarterWarmupMs = 4000;                // starter 先等一下,讓對方先進入「聽」
        private readonly System.Random _rng = new System.Random();
        public IReadOnlyList<string> History => _history;

        public ConversationSttGame(IVoice voice, ILlm llm, ConversationGame.Persona me,
                                   string peerName, Action<string> log = null)
        {
            _voice = voice; _llm = llm; _me = me; _peerName = peerName; _log = log;
        }

        public async Task RunAsync(bool starter, int maxTurns = 0)
        {
            _log?.Invoke($"💬(STT) {_me.Name} 開始『聽說對話』(對方={_peerName}," +
                         (starter ? "我先說" : "先聽對方") + ");兩台請擺近、麥克風對喇叭。");
            if (starter)
            {
                if (StarterWarmupMs > 0) await Task.Delay(StarterWarmupMs); // 讓對方先開始聽
                await SpeakMyTurnAsync();
            }

            while (maxTurns <= 0 || MyTurns < maxTurns)
            {
                string heard = await ListenUntilHeardAsync();
                if (string.IsNullOrWhiteSpace(heard))
                {
                    _log?.Invoke("⌛(STT) 沒聽到對方 → 主動再說一句打破沉默(自我修正)");
                    await SpeakMyTurnAsync();
                    continue;
                }
                _history.Add(_peerName + ": " + heard);
                _log?.Invoke($"👂(STT) 聽到 {_peerName}: {heard}");
                if (maxTurns > 0 && MyTurns >= maxTurns) break;
                await SpeakMyTurnAsync();
            }
            _log?.Invoke($"💬(STT) {_me.Name} 結束(我講 {MyTurns} 句,沒聽到 {MissedListens} 次)");
        }

        // 重聽直到聽到非空,或放棄(回空 → 上層打破沉默)。
        // 每輪窗數隨機(MinListenRetries..MaxListenRetries):打破「兩台同步聽說」的相位鎖死。
        private async Task<string> ListenUntilHeardAsync()
        {
            int retries = _rng.Next(MinListenRetries, MaxListenRetries + 1);
            for (int i = 0; i < retries; i++)
            {
                string t;
                try { t = (await _voice.ListenAsync(_me.Lang) ?? "").Trim(); }
                catch (Exception e) { _log?.Invoke("⚠ STT 失敗: " + e.Message); t = ""; }
                if (t.Length > 0) return t;
                MissedListens++;
            }
            return "";
        }

        private async Task SpeakMyTurnAsync()
        {
            string user = _history.Count == 0
                ? _me.OpeningUser()
                : string.Join("\n", _history) + $"\n{_me.Name}:";
            string line;
            try { line = Clean(await _llm.AskAsync(_me.SystemPrompt(), user)); }
            catch (Exception e) { _log?.Invoke("⚠ LLM 失敗: " + e.Message); line = "Maaf, bisa ulangi?"; }
            if (line.Length == 0) line = "Hmm, menarik.";
            _history.Add(_me.Name + ": " + line);
            MyTurns++;
            _log?.Invoke($"🗣️(STT) {_me.Name}: {line}");
            await _voice.SpeakAsync(line, _me.Lang);
        }

        private static string Clean(string s) => (s ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
    }
}
