using System;
using System.Collections.Generic;
using System.Text;

namespace KebbiBrain.App.Counselor
{
    // 輔導室陪伴機器人 — 型別與抽象埠。見 輔導室陪伴機器人_提案與開發規格.md。
    // 核心原則:確定性安全閘先於 LLM(Propose-vs-Gate)、🔴>🟡>🟢 取最高層、寧可誤報不可漏報。
    // LangVersion 9:全用 sealed class,不用 record。

    // 排序刻意 Green<Yellow<Red:取最高層可直接用 >,「邊界往上靠」斷言用 >=Yellow。
    public enum Layer { Green = 0, Yellow = 1, Red = 2 }
    public enum Speaker { Student, Kebbi }
    public enum ConvMode { Voice, Silent }
    public enum LogEvent { None, Login, ModeSwitch, YellowCard, RedEscalation, NotifySent }
    public enum SafetyFlag { None, High }
    public enum CounselingDimension { Learning, Living, Career, Emotion, Interpersonal, Family, Safety }
    public enum SuggestedTier { Developmental, Intervention, Safety }

    // 一條安全規則(由 counselor_safety_rules.json 載入)。Keywords=子字串命中;Phrases=委婉/間接片語。
    public sealed class SafetyRule
    {
        public string Id;            // = category(self_harm_suicide / family …)
        public Layer Layer;          // Red / Yellow
        public string Label;
        public string[] Keywords;
        public string[] Phrases;
        public string[] Situations;  // 語意樣式:供 LLM 輔助標註,不作為硬升級唯一依據
    }

    // 安全閘判定結果。
    public sealed class GateResult
    {
        public Layer Layer;
        public string HitReason;            // 命中的詞/片語(或原因)
        public string MatchedRuleId;        // 命中的規則 Id(可空)
        public bool EscalatedByAmbiguity;   // 模糊未硬命中但帶低落線索 → 往上靠到 Yellow
    }

    // 逐句記錄一筆(append-only)。Timestamp/TurnIndex 由 log 內部產生,外部不可指定。
    public sealed class LogTurn
    {
        public DateTime Timestamp;
        public int TurnIndex;
        public Speaker Speaker;
        public ConvMode Mode;
        public string Text;
        public Layer Layer;
        public LogEvent Event;
    }

    // 一個探索話題(地景)。
    public sealed class TopicProbe
    {
        public string Id;
        public string Label;
        public string OpenerLine;     // 本次選用的開場語(輪替)
        public bool Tried;
        public bool GotResponse;
    }

    // 交接卡(結構化 JSON;欄位順序=重要性,safety_flag 第一)。
    public sealed class HandoffCard
    {
        public SafetyFlag SafetyFlag;
        public string Student;
        public DateTime Time;
        public ConvMode Mode;
        public string MainConcern;
        public CounselingDimension[] Dimensions;
        public SuggestedTier Tier;
        public string EmotionState;
        public string StudentWillingness;
        public string[] KeyPoints;
        public string LogLink;

        // 手寫 ToJson 固定欄位順序(safety_flag 必為第一欄);不依賴 System.Text.Json 以便日後搬 Unity。
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"safety_flag\":\"").Append(SafetyFlag == SafetyFlag.High ? "high" : "none").Append("\",");
            sb.Append("\"student\":").Append(Str(Student)).Append(',');
            sb.Append("\"time\":\"").Append(Time.ToString("yyyy-MM-dd HH:mm:ss")).Append("\",");
            sb.Append("\"mode\":\"").Append(Mode == ConvMode.Voice ? "voice" : "silent").Append("\",");
            sb.Append("\"main_concern\":").Append(Str(MainConcern)).Append(',');
            sb.Append("\"dimensions\":").Append(DimArr(Dimensions)).Append(',');
            sb.Append("\"suggested_tier\":\"").Append(TierStr(Tier)).Append("\",");
            sb.Append("\"emotion_state\":").Append(Str(EmotionState)).Append(',');
            sb.Append("\"student_willingness\":").Append(Str(StudentWillingness)).Append(',');
            sb.Append("\"key_points\":").Append(StrArr(KeyPoints)).Append(',');
            sb.Append("\"log_link\":").Append(Str(LogLink));
            sb.Append('}');
            return sb.ToString();
        }

        private static string TierStr(SuggestedTier t)
            => t == SuggestedTier.Developmental ? "developmental" : t == SuggestedTier.Intervention ? "intervention" : "safety";
        private static string DimArr(CounselingDimension[] ds)
        {
            var sb = new StringBuilder("[");
            if (ds != null)
                for (int i = 0; i < ds.Length; i++) { if (i > 0) sb.Append(','); sb.Append('"').Append(DimStr(ds[i])).Append('"'); }
            return sb.Append(']').ToString();
        }
        private static string DimStr(CounselingDimension d)
        {
            switch (d)
            {
                case CounselingDimension.Learning: return "learning";
                case CounselingDimension.Living: return "living";
                case CounselingDimension.Career: return "career";
                case CounselingDimension.Emotion: return "emotion";
                case CounselingDimension.Interpersonal: return "interpersonal";
                case CounselingDimension.Family: return "family";
                default: return "safety";
            }
        }
        private static string StrArr(string[] a)
        {
            var sb = new StringBuilder("[");
            if (a != null) for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(','); sb.Append(Str(a[i])); }
            return sb.Append(']').ToString();
        }
        private static string Str(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.Append('"').ToString();
        }
    }

    // ── 四個抽象埠(Sim/CloudSim/Real 各自實作)──

    // 確定性安全閘:純硬規則比對,絕不呼叫 ILlm。Red>Yellow>Green 取最高;模糊往上靠。
    public interface ISafetyGate
    {
        GateResult Evaluate(string studentText);
        IReadOnlyList<SafetyRule> Rules { get; }
        void Reload();
    }

    // 逐句記錄:append-only、嚴格時間順序、不可竄改。
    public interface IConversationLog
    {
        string SessionId { get; }
        LogTurn Append(Speaker speaker, ConvMode mode, string text, Layer layer, LogEvent ev);
        IReadOnlyList<LogTurn> GetTurns();   // 回唯讀快照
        void Flush();
    }

    // 通知真人:Red→即時呼叫真人(不只回文字);Yellow→交接卡進待辦;結束→摘要。
    public interface INotifyHuman
    {
        void CallHumanNow(HandoffCard card);       // 🔴 即時呼叫現場真人
        void QueueYellowCard(HandoffCard card);    // 🟡 進待辦清單
        void SendSessionSummary(HandoffCard card); // 會談結束摘要
        event Action<HandoffCard> OnHumanCalled;
    }

    // 探索:只在🟢被呼叫;從未試過話題挑題,全試完回 null(不重複循環)。
    public interface IExplorationPlanner
    {
        TopicProbe NextTopic();
        void MarkTried(string topicId, bool gotResponse);
        IReadOnlyList<TopicProbe> Landscape { get; }
        void Reset();
    }
}
