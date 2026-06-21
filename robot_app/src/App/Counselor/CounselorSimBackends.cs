// 逐句記錄 + 通知真人的實作。不依賴 System.Text.Json/UnityEngine → 全平台共用(Sim 與 Unity 都用)。
using System;
using System.Collections.Generic;
using System.Text;

namespace KebbiBrain.App.Counselor
{
    // 逐句記錄:append-only、嚴格時間順序、不可竄改。Timestamp/TurnIndex 由內部產生。
    // 持久化抽象成 onAppendLine 委派(console 傳寫檔的、Unity 傳寫 persistentDataPath 的或 null=純記憶體)。
    public sealed class SimConversationLog : IConversationLog
    {
        private readonly List<LogTurn> _turns = new List<LogTurn>();
        private int _next;
        private readonly Func<DateTime> _now;
        private readonly Action<string> _persist;
        public string SessionId { get; }
        public string LogLink { get; }

        public SimConversationLog(string sessionId = null, Func<DateTime> now = null, Action<string> onAppendLine = null, string logLink = null)
        {
            SessionId = string.IsNullOrEmpty(sessionId) ? "S" + DateTime.UtcNow.Ticks : sessionId;
            _now = now ?? (() => DateTime.Now);
            _persist = onAppendLine;
            LogLink = string.IsNullOrEmpty(logLink) ? "mem://" + SessionId : logLink;
        }

        public LogTurn Append(Speaker speaker, ConvMode mode, string text, Layer layer, LogEvent ev)
        {
            var t = new LogTurn { Timestamp = _now(), TurnIndex = _next++, Speaker = speaker, Mode = mode, Text = text, Layer = layer, Event = ev };
            _turns.Add(t);
            if (_persist != null) { try { _persist(Line(t)); } catch { } }
            return t;
        }

        public IReadOnlyList<LogTurn> GetTurns() => _turns.ToArray(); // 唯讀快照:改寫不影響內部
        public void Flush() { }

        public static string Line(LogTurn t)
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

    // 通知真人:Red→即時呼叫;Yellow→交接卡待辦;結束→摘要。收集起來供測試/UI。
    public sealed class SimNotifyHuman : INotifyHuman
    {
        private readonly Action<string> _out;
        public readonly List<HandoffCard> CalledHuman = new List<HandoffCard>();
        public readonly List<HandoffCard> YellowQueue = new List<HandoffCard>();
        public readonly List<HandoffCard> Summaries = new List<HandoffCard>();
        public event Action<HandoffCard> OnHumanCalled;   // 🔴 即時呼叫
        public event Action<HandoffCard> OnYellowQueued;  // 🟡 待辦交接卡
        public event Action<HandoffCard> OnSummary;       // 📋 結束摘要

        public SimNotifyHuman(Action<string> output = null) { _out = output ?? (s => { }); }

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
            OnYellowQueued?.Invoke(card);
        }
        public void SendSessionSummary(HandoffCard card)
        {
            Summaries.Add(card);
            _out("   📋 會談摘要給老師 → " + card.ToJson());
            OnSummary?.Invoke(card);
        }
    }
}
