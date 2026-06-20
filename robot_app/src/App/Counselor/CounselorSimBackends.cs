// 輔導室三個抽象埠的 Sim 實作(整檔 #if !UNITY:主控台 Sim 階段)。
#if !UNITY
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace KebbiBrain.App.Counselor
{
    // 逐句記錄:append-only、嚴格時間順序、不可竄改。Timestamp/TurnIndex 由內部產生。
    public sealed class SimConversationLog : IConversationLog
    {
        private readonly List<LogTurn> _turns = new List<LogTurn>();
        private int _next;
        private readonly Func<DateTime> _now;
        private readonly string _path;
        public string SessionId { get; }

        public SimConversationLog(string sessionId = null, Func<DateTime> now = null, string dir = null)
        {
            SessionId = string.IsNullOrEmpty(sessionId) ? "S" + DateTime.UtcNow.Ticks : sessionId;
            _now = now ?? (() => DateTime.Now);
            _path = string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "counselor_log_" + SessionId + ".jsonl");
        }

        public LogTurn Append(Speaker speaker, ConvMode mode, string text, Layer layer, LogEvent ev)
        {
            var t = new LogTurn { Timestamp = _now(), TurnIndex = _next++, Speaker = speaker, Mode = mode, Text = text, Layer = layer, Event = ev };
            _turns.Add(t);
            if (_path != null) { try { File.AppendAllText(_path, Line(t) + "\n"); } catch { } }
            return t;
        }

        public IReadOnlyList<LogTurn> GetTurns() => _turns.ToArray(); // 唯讀快照:改寫不影響內部
        public void Flush() { }
        public string LogLink => _path ?? ("mem://" + SessionId);

        private static string Line(LogTurn t)
        {
            var sb = new StringBuilder();
            sb.Append("{\"timestamp\":\"").Append(t.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")).Append("\",");
            sb.Append("\"turn_index\":").Append(t.TurnIndex).Append(',');
            sb.Append("\"speaker\":\"").Append(t.Speaker == Speaker.Student ? "student" : "kebbi").Append("\",");
            sb.Append("\"mode\":\"").Append(t.Mode == ConvMode.Voice ? "voice" : "silent").Append("\",");
            sb.Append("\"text\":\"").Append((t.Text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ")).Append("\",");
            sb.Append("\"layer\":\"").Append(t.Layer == Layer.Red ? "red" : t.Layer == Layer.Yellow ? "yellow" : "green").Append("\",");
            sb.Append("\"event\":\"").Append(t.Event.ToString().ToLowerInvariant()).Append("\"}");
            return sb.ToString();
        }
    }

    // 通知真人:Red→即時呼叫;Yellow→交接卡待辦;結束→摘要。收集起來供測試斷言。
    public sealed class SimNotifyHuman : INotifyHuman
    {
        private readonly Action<string> _out;
        public readonly List<HandoffCard> CalledHuman = new List<HandoffCard>();
        public readonly List<HandoffCard> YellowQueue = new List<HandoffCard>();
        public readonly List<HandoffCard> Summaries = new List<HandoffCard>();
        public event Action<HandoffCard> OnHumanCalled;

        public SimNotifyHuman(Action<string> output = null) { _out = output ?? Console.WriteLine; }

        public void CallHumanNow(HandoffCard card)
        {
            CalledHuman.Add(card);
            _out("   🔴 即時呼叫現場真人 @ " + card.Time.ToString("HH:mm:ss") + "(safety=high)→ " + card.ToJson());
            OnHumanCalled?.Invoke(card);
        }
        public void QueueYellowCard(HandoffCard card)
        {
            YellowQueue.Add(card);
            _out("   🟡 交接卡進輔導老師待辦 → " + card.ToJson());
        }
        public void SendSessionSummary(HandoffCard card)
        {
            Summaries.Add(card);
            _out("   📋 會談摘要給老師 → " + card.ToJson());
        }
    }

    // 探索:只在🟢用。從未試過話題依權重挑題、開場語輪替;全試完回 null(不重複循環)。
    public sealed class SimExplorationPlanner : IExplorationPlanner
    {
        private readonly List<TopicProbe> _landscape = new List<TopicProbe>();
        private readonly Dictionary<string, string[]> _openers = new Dictionary<string, string[]>();
        private readonly Dictionary<string, int> _openerIdx = new Dictionary<string, int>();
        private readonly HashSet<string> _tried = new HashSet<string>();
        public string[] SoftClose { get; private set; } = Array.Empty<string>();
        public string LoginDisclosure { get; private set; } = "";

        public SimExplorationPlanner(string topicsPath = null)
        {
            string path = string.IsNullOrEmpty(topicsPath) ? Path.Combine(AppContext.BaseDirectory, "counselor_topics.json") : topicsPath;
            LoadFromJson(File.ReadAllText(path));
        }
        private SimExplorationPlanner(bool _) { }
        public static SimExplorationPlanner FromJson(string json) { var p = new SimExplorationPlanner(true); p.LoadFromJson(json); return p; }

        private void LoadFromJson(string json)
        {
            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("login_disclosure", out var ld) && ld.ValueKind == JsonValueKind.String) LoginDisclosure = ld.GetString();
                if (root.TryGetProperty("soft_close", out var sc) && sc.ValueKind == JsonValueKind.Array)
                { var l = new List<string>(); foreach (var x in sc.EnumerateArray()) l.Add(x.GetString()); SoftClose = l.ToArray(); }
                var topics = new List<(int w, TopicProbe t, string[] op)>();
                foreach (var e in root.GetProperty("topics").EnumerateArray())
                {
                    var t = new TopicProbe { Id = e.GetProperty("id").GetString(), Label = e.GetProperty("label").GetString() };
                    int w = e.TryGetProperty("weight", out var we) ? we.GetInt32() : 1;
                    var ops = new List<string>();
                    foreach (var o in e.GetProperty("openers").EnumerateArray()) ops.Add(o.GetString());
                    topics.Add((w, t, ops.ToArray()));
                }
                topics.Sort((a, b) => b.w.CompareTo(a.w)); // 權重高→低(穩定:List.Sort 非穩定,但同權重順序不影響測試正確性)
                foreach (var (w, t, op) in topics) { _landscape.Add(t); _openers[t.Id] = op; }
            }
        }

        public IReadOnlyList<TopicProbe> Landscape => _landscape;

        // 挑一個「尚未試過」的話題(權重高的在前),自動標記為已試,開場語輪替。全試完→null。
        public TopicProbe NextTopic()
        {
            foreach (var t in _landscape)
            {
                if (_tried.Contains(t.Id)) continue;
                _tried.Add(t.Id); t.Tried = true;
                var ops = _openers[t.Id];
                int i = _openerIdx.TryGetValue(t.Id, out var x) ? x : 0;
                t.OpenerLine = ops[i % ops.Length];
                _openerIdx[t.Id] = i + 1;       // 下次同話題換下一句開場語(不換句重講)
                return t;
            }
            return null;
        }

        public void MarkTried(string topicId, bool gotResponse)
        {
            _tried.Add(topicId);
            foreach (var t in _landscape) if (t.Id == topicId) { t.Tried = true; t.GotResponse = gotResponse; }
        }

        public void Reset() { _tried.Clear(); } // 只清「試過」紀錄;_openerIdx 保留 → 重探同話題仍輪替到新開場語
    }
}
#endif
