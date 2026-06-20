using System.Collections.Generic;
using System.Text;

namespace KebbiBrain.App.Counselor
{
    // 安全閘「比對核心」:給定已解析的 SafetyRule 清單做確定性比對。不依賴 JSON/檔案/UnityEngine → 全平台共用。
    // 載入器(主控台 System.Text.Json / Unity JsonUtility)負責把 JSON 解析成 SafetyRule[] 再交給這裡。
    public sealed class SafetyGateCore : ISafetyGate
    {
        private readonly List<SafetyRule> _rules;
        private readonly List<KeyValuePair<SafetyRule, string[]>> _red = new List<KeyValuePair<SafetyRule, string[]>>();
        private readonly List<KeyValuePair<SafetyRule, string[]>> _yellow = new List<KeyValuePair<SafetyRule, string[]>>();

        private static readonly string[] AmbiguousSignals =
        {
            "想不開", "沒希望", "好想不見", "道別", "沒什麼意義", "沒有意義", "沒意義",
            "送給朋友", "都送人", "東西整理好", "不想面對", "好想逃", "撐得好累"
        };
        private static readonly string[] AliveNegContext = { "累", "沒", "煩", "痛", "撐", "意義", "幹嘛", "厭" };
        private const string StripChars = "，。！？、～…‧·「」『』（）【】《》〈〉：；—－─“”‘’,.!?~()[]{}:;-_*/\"'";

        public SafetyGateCore(IReadOnlyList<SafetyRule> rules)
        {
            _rules = new List<SafetyRule>(rules ?? new List<SafetyRule>());
            foreach (var rule in _rules)
            {
                var terms = new List<string>();
                if (rule.Keywords != null) foreach (var k in rule.Keywords) { var n = Norm(k); if (n.Length > 0) terms.Add(n); }
                if (rule.Phrases != null) foreach (var p in rule.Phrases) { var n = Norm(p); if (n.Length > 0) terms.Add(n); }
                var e = new KeyValuePair<SafetyRule, string[]>(rule, terms.ToArray());
                if (rule.Layer == Layer.Red) _red.Add(e); else _yellow.Add(e);
            }
        }

        public IReadOnlyList<SafetyRule> Rules => _rules;
        public void Reload() { }  // 核心不擁有來源;重載由載入器負責(重建一個新核心)

        public GateResult Evaluate(string studentText)
        {
            string norm = Norm(studentText);
            if (norm.Length == 0) return new GateResult { Layer = Layer.Green };

            foreach (var kv in _red)                       // 🔴 任一紅線詞命中即 Red(不進 LLM)
                foreach (var t in kv.Value)
                    if (norm.Contains(t)) return new GateResult { Layer = Layer.Red, HitReason = t, MatchedRuleId = kv.Key.Id };

            foreach (var kv in _yellow)                    // 🟡
                foreach (var t in kv.Value)
                    if (norm.Contains(t)) return new GateResult { Layer = Layer.Yellow, HitReason = t, MatchedRuleId = kv.Key.Id };

            if (IsAmbiguous(norm))                          // 模糊往上靠(寧可誤報)
                return new GateResult { Layer = Layer.Yellow, HitReason = "邊界/低落線索,寧可誤報往上靠", EscalatedByAmbiguity = true };

            return new GateResult { Layer = Layer.Green };
        }

        private static bool IsAmbiguous(string norm)
        {
            foreach (var s in AmbiguousSignals) if (norm.Contains(s)) return true;
            if (norm.Contains("活著") || norm.Contains("活下去"))
                foreach (var c in AliveNegContext) if (norm.Contains(c)) return true;
            return false;
        }

        // 正規化:去空白/標點、全形→半形、小寫。防『想 死』『想~死』『想,死』規避。
        public static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
            {
                char c = ch;
                if (char.IsWhiteSpace(c)) continue;
                if (c >= '！' && c <= '～') c = (char)(c - 0xFEE0); // 全形 ASCII → 半形
                if (StripChars.IndexOf(c) >= 0) continue;
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
