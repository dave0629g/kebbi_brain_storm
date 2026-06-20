// 確定性安全閘(Sim)。整檔 #if !UNITY:Sim 階段在主控台跑、用 System.Text.Json 載設定檔。
// 核心:純硬規則比對,絕不呼叫 ILlm(Propose-vs-Gate)。Red>Yellow>Green 取最高;寧可誤報不可漏報。
#if !UNITY
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace KebbiBrain.App.Counselor
{
    public sealed class SimSafetyGate : ISafetyGate
    {
        private readonly string _path;
        private List<SafetyRule> _rules = new List<SafetyRule>();
        private List<(SafetyRule rule, string[] normTerms)> _red = new List<(SafetyRule, string[])>();
        private List<(SafetyRule rule, string[] normTerms)> _yellow = new List<(SafetyRule, string[])>();

        // 模糊/低落/告別線索:未硬命中紅線、但帶這些訊號 → 往上靠到 Yellow(EscalatedByAmbiguity)。
        // 多數真正危險詞已在紅線清單(會先硬命中 Red);這裡只補「沒硬命中但不該放過」的邊界。
        private static readonly string[] AmbiguousSignals =
        {
            "想不開", "沒希望", "好想不見", "道別", "沒什麼意義", "沒有意義", "沒意義",
            "送給朋友", "都送人", "東西整理好", "不想面對", "好想逃", "撐得好累"
        };
        private static readonly string[] AliveNegContext = { "累", "沒", "煩", "痛", "撐", "意義", "幹嘛", "厭" };

        public SimSafetyGate(string rulesPath = null)
        {
            _path = string.IsNullOrEmpty(rulesPath)
                ? Path.Combine(AppContext.BaseDirectory, "counselor_safety_rules.json") : rulesPath;
            Reload();
        }

        // 測試用:直接給 JSON 內容(免檔案)。
        public static SimSafetyGate FromJson(string json)
        {
            var g = new SimSafetyGate("\0skip\0");
            g.LoadFromJson(json);
            return g;
        }

        public IReadOnlyList<SafetyRule> Rules => _rules;

        public void Reload()
        {
            if (_path == "\0skip\0") return;
            LoadFromJson(File.ReadAllText(_path));
        }

        private void LoadFromJson(string json)
        {
            _rules = new List<SafetyRule>();
            _red = new List<(SafetyRule, string[])>();
            _yellow = new List<(SafetyRule, string[])>();
            using (var doc = JsonDocument.Parse(json))
            {
                if (!doc.RootElement.TryGetProperty("rules", out var rulesEl)) return;
                foreach (var r in rulesEl.EnumerateArray())
                {
                    var rule = new SafetyRule
                    {
                        Id = Get(r, "category"),
                        Label = Get(r, "label"),
                        Layer = Get(r, "layer") == "red" ? Layer.Red : Layer.Yellow,
                        Keywords = Arr(r, "keywords"),
                        Phrases = Arr(r, "phrases"),
                        Situations = Arr(r, "situations"),
                    };
                    _rules.Add(rule);
                    var terms = new List<string>();
                    foreach (var k in rule.Keywords) { var n = Norm(k); if (n.Length > 0) terms.Add(n); }
                    foreach (var p in rule.Phrases) { var n = Norm(p); if (n.Length > 0) terms.Add(n); }
                    var entry = (rule, terms.ToArray());
                    if (rule.Layer == Layer.Red) _red.Add(entry); else _yellow.Add(entry);
                }
            }
        }

        public GateResult Evaluate(string studentText)
        {
            string norm = Norm(studentText);
            if (norm.Length == 0) return new GateResult { Layer = Layer.Green };

            // 🔴 先:任一紅線詞命中即 Red(不進 LLM)。
            foreach (var (rule, terms) in _red)
                foreach (var t in terms)
                    if (norm.Contains(t))
                        return new GateResult { Layer = Layer.Red, HitReason = t, MatchedRuleId = rule.Id };

            // 🟡 次:黃燈詞。
            foreach (var (rule, terms) in _yellow)
                foreach (var t in terms)
                    if (norm.Contains(t))
                        return new GateResult { Layer = Layer.Yellow, HitReason = t, MatchedRuleId = rule.Id };

            // 模糊往上靠:帶低落/告別線索 → Yellow(EscalatedByAmbiguity)。
            if (IsAmbiguous(norm))
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

        // 正規化:去空白/標點、全形→半形、小寫。防『想 死』『想~死』『想，死』規避。
        private static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
            {
                char c = ch;
                if (char.IsWhiteSpace(c)) continue;
                if (c >= '！' && c <= '～') c = (char)(c - 0xFEE0); // 全形 ASCII → 半形
                if (IsStripPunct(c)) continue;
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        // 要去掉的標點(全形+半形;字串內重複無妨,避免 switch 重複標籤)。
        private const string StripChars = "，。！？、～…‧·「」『』（）【】《》〈〉：；—－─“”‘’,.!?~()[]{}:;-_*/\"'";
        private static bool IsStripPunct(char c) => StripChars.IndexOf(c) >= 0;

        private static string Get(JsonElement e, string k)
            => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
        private static string[] Arr(JsonElement e, string k)
        {
            if (!e.TryGetProperty(k, out var v) || v.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
            var list = new List<string>();
            foreach (var x in v.EnumerateArray()) if (x.ValueKind == JsonValueKind.String) list.Add(x.GetString());
            return list.ToArray();
        }
    }
}
#endif
