using System.Collections.Generic;

namespace KebbiBrain.Hardware
{
    // 金鑰佔位/空值偵測(純函式,可斷言)。啟動時把「沒設金鑰 / 還是範例值」一眼標出,
    // 避免拿佔位金鑰去打明文雲端 API(白白 401 + 不知為何)。⚠ 只判「是否疑似佔位」,不回傳/不印任何金鑰值。
    public static class SecretsCheck
    {
        // 明顯的佔位/範例字樣(小寫子字串比對)。刻意挑「真金鑰幾乎不可能含」的字串以免誤判。
        private static readonly string[] PlaceholderMarks =
        {
            "your", "changeme", "change-me", "placeholder", "example", "todo",
            "putyourkey", "keyhere", "<", ">", "你的", "範例", "請填", "尚未設定", "金鑰放"
        };

        // 真金鑰都很長(Azure 32、OpenAI ~51、Gemini ~39);低於此一律當佔位。
        public const int MinKeyLen = 12;

        // 是否為「空或佔位」金鑰 → 不可用於真正呼叫。
        public static bool IsPlaceholder(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            string v = value.Trim();
            if (v.Length < MinKeyLen) return true;
            string low = v.ToLowerInvariant();
            if (low == "null" || low == "none" || low == "test" || v == "...") return true;
            if (IsAllSameChar(v)) return true;                       // "aaaaaaaaaaaa" / "............"
            foreach (var mark in PlaceholderMarks) if (low.Contains(mark)) return true;
            return false;
        }

        public static bool IsUsable(string value) => !IsPlaceholder(value);

        public struct KeyStatus { public string Name; public bool Ok; public string Hint; }

        public static KeyStatus Check(string name, string value)
        {
            bool ok = IsUsable(value);
            string hint = ok ? "已設定" : (string.IsNullOrWhiteSpace(value) ? "未設定(空)" : "疑似佔位/範例值");
            return new KeyStatus { Name = name, Ok = ok, Hint = hint };
        }

        // 對目前 Config 的金鑰盤點(供啟動/preflight 顯示)。不含任何金鑰值。
        public static List<KeyStatus> ReportConfig()
        {
            return new List<KeyStatus>
            {
                Check("語音 SpeechKey", Config.SpeechKey),
                Check("LLM Key", Config.LlmKey),
                Check("Gemini Key", Config.GeminiKey),
            };
        }

        private static bool IsAllSameChar(string s)
        {
            for (int i = 1; i < s.Length; i++) if (s[i] != s[0]) return false;
            return s.Length > 0;
        }
    }
}
