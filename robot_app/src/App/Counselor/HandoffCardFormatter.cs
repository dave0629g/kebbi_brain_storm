// 把 HandoffCard 排成「輔導老師一頁就懂」的純文字摘要(危急度第一眼可見)。純函式、不依賴 UnityEngine/JSON → 可斷言。
// 規格:老師真正要的是「談了什麼、要不要跟進、危險等級」。ToJson()(機器讀)已有,本檔補人讀版。
using System;
using System.Text;

namespace KebbiBrain.App.Counselor
{
    public static class HandoffCardFormatter
    {
        public static string ToTeacherText(HandoffCard c)
        {
            if (c == null) return "(無交接卡)";
            var sb = new StringBuilder();
            sb.Append("【輔導陪伴交接卡】危急度:").Append(FlagZh(c.SafetyFlag)).Append('\n');
            sb.Append("學生:").Append(NA(c.Student)).Append('\n');
            sb.Append("時間:").Append(c.Time.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
            sb.Append("模式:").Append(c.Mode == ConvMode.Voice ? "有聲(語音)" : "無聲(打字)").Append('\n');
            sb.Append("主要關注:").Append(NA(c.MainConcern)).Append('\n');
            sb.Append("輔導向度:").Append(DimsZh(c.Dimensions)).Append('\n');
            sb.Append("建議層級:").Append(TierZh(c.Tier)).Append('\n');
            sb.Append("情緒狀態:").Append(NA(c.EmotionState)).Append('\n');
            sb.Append("學生意願:").Append(NA(c.StudentWillingness)).Append('\n');
            sb.Append("重點摘要:");
            if (c.KeyPoints == null || c.KeyPoints.Length == 0) sb.Append("(無)");
            else { sb.Append('\n'); foreach (var k in c.KeyPoints) sb.Append("  • ").Append(k).Append('\n'); }
            sb.Append("記錄連結:").Append(NA(c.LogLink));
            return sb.ToString();
        }

        public static string FlagZh(SafetyFlag f) => f == SafetyFlag.High ? "高(需即時關注)" : "一般";

        public static string TierZh(SuggestedTier t)
            => t == SuggestedTier.Safety ? "安全層級(危機處理)"
             : t == SuggestedTier.Intervention ? "介入層級(個別關懷)"
             : "發展層級(一般陪伴)";

        public static string DimZh(CounselingDimension d)
        {
            switch (d)
            {
                case CounselingDimension.Learning: return "學習";
                case CounselingDimension.Living: return "生活";
                case CounselingDimension.Career: return "生涯";
                case CounselingDimension.Emotion: return "情緒";
                case CounselingDimension.Interpersonal: return "人際";
                case CounselingDimension.Family: return "家庭";
                case CounselingDimension.Safety: return "安全";
                default: return d.ToString();
            }
        }

        private static string DimsZh(CounselingDimension[] ds)
        {
            if (ds == null || ds.Length == 0) return "(未分類)";
            var sb = new StringBuilder();
            for (int i = 0; i < ds.Length; i++) { if (i > 0) sb.Append('、'); sb.Append(DimZh(ds[i])); }
            return sb.ToString();
        }

        private static string NA(string s) => string.IsNullOrEmpty(s) ? "(無)" : s;
    }
}
