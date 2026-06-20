// 安全閘載入器(主控台 Sim,整檔 #if !UNITY):用 System.Text.Json 解析設定檔 → SafetyGateCore。
// Unity 端用 JsonUtility 解析(見 UnityCounselor.cs),共用同一個 SafetyGateCore 比對核心。
#if !UNITY
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace KebbiBrain.App.Counselor
{
    public sealed class SimSafetyGate : ISafetyGate
    {
        private readonly string _path;
        private SafetyGateCore _core;

        public SimSafetyGate(string rulesPath = null)
        {
            _path = string.IsNullOrEmpty(rulesPath)
                ? Path.Combine(AppContext.BaseDirectory, "counselor_safety_rules.json") : rulesPath;
            Reload();
        }
        private SimSafetyGate(bool _) { }

        public static SimSafetyGate FromJson(string json) { var g = new SimSafetyGate(true); g._core = new SafetyGateCore(Parse(json)); return g; }

        public IReadOnlyList<SafetyRule> Rules => _core.Rules;
        public GateResult Evaluate(string studentText) => _core.Evaluate(studentText);
        public void Reload() { _core = new SafetyGateCore(Parse(File.ReadAllText(_path))); }

        // JSON → SafetyRule[]。共用給 console;Unity 端有對應的 JsonUtility 版。
        public static List<SafetyRule> Parse(string json)
        {
            var rules = new List<SafetyRule>();
            using (var doc = JsonDocument.Parse(json))
            {
                if (!doc.RootElement.TryGetProperty("rules", out var rulesEl)) return rules;
                foreach (var r in rulesEl.EnumerateArray())
                    rules.Add(new SafetyRule
                    {
                        Id = Get(r, "category"),
                        Label = Get(r, "label"),
                        Layer = Get(r, "layer") == "red" ? Layer.Red : Layer.Yellow,
                        Keywords = Arr(r, "keywords"),
                        Phrases = Arr(r, "phrases"),
                        Situations = Arr(r, "situations"),
                    });
            }
            return rules;
        }

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
