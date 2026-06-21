using System.Collections.Generic;

namespace KebbiBrain.App
{
    // 看著物件對話(RoboVisionTalk)的純核心:把 Robotics-ER 認到的物體標籤,變成注入 Gemini Live 對話的「視覺脈絡」。
    // 讓凱比「看著真實世界陪你聊」(認物教學/看圖說故事/陪讀)。
    // 鐵律:只在「場景變了」才注入(避免每 12s 重複同一場景洗版、打斷語音對話);Live 只能注入文字(不能塞圖)→ 視覺只是「摘要旁白」。
    public static class VisionTalkContext
    {
        // 偵測標籤 → 場景摘要(去重、保序、限數量)。
        public static string SceneSummary(IReadOnlyList<string> labels, int maxItems = 6)
        {
            if (labels == null) return "";
            var seen = new List<string>();
            foreach (var l in labels)
            {
                string t = (l ?? "").Trim();
                if (t.Length == 0 || seen.Contains(t)) continue;
                seen.Add(t);
                if (seen.Count >= maxItems) break;
            }
            return string.Join("、", seen);
        }

        // 場景變了嗎?(去重後的集合不同 = 變了;順序/重複不算變 → 不因偵測抖動洗版)
        public static bool SceneChanged(IReadOnlyList<string> now, IReadOnlyList<string> last)
        {
            var a = DedupSet(now);
            var b = DedupSet(last);
            if (a.Count != b.Count) return true;
            foreach (var x in a) if (!b.Contains(x)) return true;
            return false;
        }

        // 注入 Live 的視覺脈絡文字。空場景也給一句(讓凱比知道「現在看不到明顯東西」)。
        public static string VisionLine(IReadOnlyList<string> labels, int maxItems = 6)
        {
            string s = SceneSummary(labels, maxItems);
            return s.Length == 0
                ? "(我現在用相機看,但沒認出明顯的東西。)"
                : "(我現在用相機看到:" + s + "。看著這些東西跟我聊。)";
        }

        // 包成 Live clientContent 一輪文字輸入(讓凱比針對新看到的東西回應)。
        public static string BuildVisionTurnJson(IReadOnlyList<string> labels, int maxItems = 6)
            => GeminiLiveProtocol.BuildTextTurnJson(VisionLine(labels, maxItems));

        private static HashSet<string> DedupSet(IReadOnlyList<string> labels)
        {
            var set = new HashSet<string>();
            if (labels != null)
                foreach (var l in labels)
                {
                    string t = (l ?? "").Trim();
                    if (t.Length > 0) set.Add(t);
                }
            return set;
        }
    }
}
