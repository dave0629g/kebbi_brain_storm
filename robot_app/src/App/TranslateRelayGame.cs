using System;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // 雙機口譯橋:兩台 Kebbi 各站老師端/學生端,當「即時翻譯員」。純 C#(IVoice/ILlm/IRobotLink 抽象)→ 主控台可自測。
    // 解決真實課堂痛點:中文老師 ↔ 印尼學生聽不懂彼此。
    //   每台:聽本地一句(localLang)→ 用 LLM 翻成對方聽得懂的語言(peerLocalLang)→ 經 link 送給對方機 → 對方機用本地語音說出。
    //   反向同理。線格式:`TX|<譯文>`(送出的已是對方語言;對方收到直接用自己的 localLang 說出)。
    //   先用已驗 Azure STT/TTS + LLM 落地;Gemini Live Translate 就緒後可替換翻譯/語音層。
    public sealed class TranslateRelayGame
    {
        public const string Pfx = "TX";
        private readonly IVoice _voice;
        private readonly ILlm _llm;
        private readonly IRobotLink _link;
        private readonly string _localLang;       // 本地說話者語言:我聽、我說都用這個
        private readonly string _peerLocalLang;   // 對方端聽眾語言:我把聽到的翻成這個再送出
        private readonly string _peerId;
        private readonly Action<string> _log;

        public int Relayed { get; private set; }   // 我聽到並轉譯送出的句數
        public int Spoken { get; private set; }     // 我替對方說出的譯文句數

        public TranslateRelayGame(IVoice voice, ILlm llm, IRobotLink link,
                                  string localLang, string peerLocalLang, string peerId, Action<string> log = null)
        {
            _voice = voice; _llm = llm; _link = link;
            _localLang = localLang; _peerLocalLang = peerLocalLang; _peerId = peerId; _log = log;
        }

        // 即時口譯員 system prompt(純函式:可斷言含 from/to 語言)。
        public static string TranslateSystem(string fromLang, string toLang)
            => "你是即時口譯員。把使用者說的話從「" + LangName(fromLang) + "」翻成「" + LangName(toLang) + "」。" +
               "只輸出譯文本身,不要加任何解釋、引號或原文,保留語氣與稱謂。";

        public static string LangName(string code)
        {
            if (string.IsNullOrEmpty(code)) return code;
            if (code.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "繁體中文(台灣)";
            if (code.StartsWith("id", StringComparison.OrdinalIgnoreCase)) return "印尼語";
            if (code.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return "英語";
            return code;
        }

        public async Task<string> TranslateAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            try { return Clean(await _llm.AskAsync(TranslateSystem(_localLang, _peerLocalLang), text)); }
            catch (Exception e) { _log?.Invoke("⚠ 翻譯失敗: " + e.Message); return ""; }
        }

        // 聽本地一句 → 翻成對方語言 → 送對方機(對方說出)。回傳送出的譯文;無人說話/翻譯空 → null。
        public async Task<string> RelayOnceAsync()
        {
            string heard = await _voice.ListenAsync(_localLang);
            if (string.IsNullOrWhiteSpace(heard)) return null;
            string tx = await TranslateAsync(heard);
            if (tx.Length == 0) return null;
            await _link.SendAsync(_peerId, Pfx + "|" + tx);
            Relayed++;
            _log?.Invoke("🌐 " + LangName(_localLang) + "→" + LangName(_peerLocalLang) + ":「" + heard + "」⇒「" + tx + "」");
            return tx;
        }

        // 收到對方送來的譯文(已是我方語言)→ 用本地語音說出。
        public async Task SpeakIncomingAsync(string framed)
        {
            if (!IsRelay(framed)) return;
            string text = framed.Substring(Pfx.Length + 1);
            if (text.Length == 0) return;
            Spoken++;
            _log?.Invoke("🔊 說出譯文(" + LangName(_localLang) + "):" + text);
            await _voice.SpeakAsync(text, _localLang);
        }

        // 掛上 link:對方送來的譯文自動說出。
        public void AttachSpeaker()
            => _link.OnMessage((from, text) => { if (from == _peerId) _ = SpeakIncomingAsync(text); });

        // 持續口譯:聽→譯→送。maxTurns<=0 持續;maxEmpty 連續空窗(沒人說話)達標即停,避免空轉。
        public async Task RunAsync(int maxTurns = 0, int maxEmpty = 0)
        {
            AttachSpeaker();
            int empty = 0;
            while (maxTurns <= 0 || Relayed < maxTurns)
            {
                var tx = await RelayOnceAsync();
                if (tx == null) { empty++; if (maxEmpty > 0 && empty >= maxEmpty) break; }
                else empty = 0;
            }
        }

        public static bool IsRelay(string t) => !string.IsNullOrEmpty(t) && t.StartsWith(Pfx + "|", StringComparison.Ordinal);
        private static string Clean(string s) => (s ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
    }
}
