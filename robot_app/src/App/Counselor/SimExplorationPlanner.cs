// 探索話題載入器(主控台 Sim,整檔 #if !UNITY):System.Text.Json 解析 counselor_topics.json → ExplorationCore。
// Unity 端用 JsonUtility 解析(見 UnityCounselor.cs),共用同一個 ExplorationCore。
#if !UNITY
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace KebbiBrain.App.Counselor
{
    public sealed class SimExplorationPlanner : IExplorationPlanner
    {
        private readonly ExplorationCore _core;
        public string[] SoftClose => _core.SoftClose;
        public string LoginDisclosure => _core.LoginDisclosure;

        public SimExplorationPlanner(string topicsPath = null)
        {
            string path = string.IsNullOrEmpty(topicsPath) ? Path.Combine(AppContext.BaseDirectory, "counselor_topics.json") : topicsPath;
            _core = Build(File.ReadAllText(path));
        }
        private SimExplorationPlanner(ExplorationCore core) { _core = core; }
        public static SimExplorationPlanner FromJson(string json) => new SimExplorationPlanner(Build(json));

        public IReadOnlyList<TopicProbe> Landscape => _core.Landscape;
        public TopicProbe NextTopic() => _core.NextTopic();
        public void MarkTried(string id, bool got) => _core.MarkTried(id, got);
        public void Reset() => _core.Reset();

        public static ExplorationCore Build(string json)
        {
            string login = ""; var soft = new List<string>();
            var topics = new List<(int w, TopicProbe t)>();
            var openers = new Dictionary<string, string[]>();
            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("login_disclosure", out var ld) && ld.ValueKind == JsonValueKind.String) login = ld.GetString();
                if (root.TryGetProperty("soft_close", out var sc) && sc.ValueKind == JsonValueKind.Array)
                    foreach (var x in sc.EnumerateArray()) soft.Add(x.GetString());
                foreach (var e in root.GetProperty("topics").EnumerateArray())
                {
                    var t = new TopicProbe { Id = e.GetProperty("id").GetString(), Label = e.GetProperty("label").GetString() };
                    int w = e.TryGetProperty("weight", out var we) ? we.GetInt32() : 1;
                    var ops = new List<string>();
                    foreach (var o in e.GetProperty("openers").EnumerateArray()) ops.Add(o.GetString());
                    openers[t.Id] = ops.ToArray();
                    topics.Add((w, t));
                }
            }
            topics.Sort((a, b) => b.w.CompareTo(a.w));   // 權重高→低
            var land = new List<TopicProbe>(); foreach (var (w, t) in topics) land.Add(t);
            return new ExplorationCore(land, openers, soft.ToArray(), login);
        }
    }
}
#endif
