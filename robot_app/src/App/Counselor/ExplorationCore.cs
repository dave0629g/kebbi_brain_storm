using System.Collections.Generic;

namespace KebbiBrain.App.Counselor
{
    // 探索「挑題核心」:給定已解析的話題地景做挑題/輪替。不依賴 JSON/UnityEngine → 全平台共用。
    public sealed class ExplorationCore : IExplorationPlanner
    {
        private readonly List<TopicProbe> _landscape;
        private readonly Dictionary<string, string[]> _openers;
        private readonly Dictionary<string, int> _openerIdx = new Dictionary<string, int>();
        private readonly HashSet<string> _tried = new HashSet<string>();
        public string[] SoftClose { get; }
        public string LoginDisclosure { get; }

        // landscape 須已依權重高→低排好(載入器負責排序)。
        public ExplorationCore(List<TopicProbe> landscapeSortedByWeight, Dictionary<string, string[]> openers,
                               string[] softClose = null, string loginDisclosure = "")
        {
            _landscape = landscapeSortedByWeight ?? new List<TopicProbe>();
            _openers = openers ?? new Dictionary<string, string[]>();
            SoftClose = softClose ?? new string[0];
            LoginDisclosure = loginDisclosure ?? "";
        }

        public IReadOnlyList<TopicProbe> Landscape => _landscape;

        // 挑一個「尚未試過」的話題(權重高的在前),自動標記為已試,開場語輪替。全試完→null。
        public TopicProbe NextTopic()
        {
            foreach (var t in _landscape)
            {
                if (_tried.Contains(t.Id)) continue;
                _tried.Add(t.Id); t.Tried = true;
                string[] ops = _openers.TryGetValue(t.Id, out var o) && o.Length > 0 ? o : new[] { "我們慢慢聊就好。" };
                int i = _openerIdx.TryGetValue(t.Id, out var x) ? x : 0;
                t.OpenerLine = ops[i % ops.Length];
                _openerIdx[t.Id] = i + 1;   // 下次同話題換下一句開場語(不換句重講)
                return t;
            }
            return null;
        }

        public void MarkTried(string topicId, bool gotResponse)
        {
            _tried.Add(topicId);
            foreach (var t in _landscape) if (t.Id == topicId) { t.Tried = true; t.GotResponse = gotResponse; }
        }

        public void Reset() { _tried.Clear(); } // 只清「試過」;_openerIdx 保留 → 重探同話題輪替到新開場語
    }
}
