// 看著物件對話(整檔 #if UNITY):同掛 Robotics-ER 認物(相機) + Gemini Live 語音對話(麥克風),
// 把認到的物體在「場景變了」時注入 Live 對話 → 凱比看著真實世界陪你聊(認物教學/看圖說故事/陪讀)。
// 兩條皆為已驗管線,本元件只做組合 + 橋接(RoboticsVision 認物 → VisionTalkContext 節流 → Live.InjectContext)。
// UI 天然分層:RoboticsVision 畫相機+綠框,Live 畫字幕。⚠ Robotics-ER 免費額度緊(intervalSec=12);真機驗資源協調與額度。
#if UNITY
using System.Collections.Generic;
using UnityEngine;
using KebbiBrain;
using KebbiBrain.App;

namespace KebbiBrain.Real
{
    public sealed class RoboVisionTalkBehaviour : MonoBehaviour
    {
        public string apiKey = "";
        public string personaLang = "zh-TW";

        private GeminiLiveConversationBehaviour _live;
        private RoboticsVisionBehaviour _vision;
        private List<string> _lastLabels = new List<string>();

        private void Start()
        {
            string key = !string.IsNullOrEmpty(apiKey) ? apiKey : Config.GeminiKey;

            // Live 語音對話(視覺感知人格):用括號告知它「相機看到什麼」,讓它自然融入對話。
            _live = gameObject.AddComponent<GeminiLiveConversationBehaviour>();
            _live.apiKey = key;
            _live.systemInstruction =
                "你是凱比,一個親切的教育機器人,正用相機看著小朋友面前的東西陪他聊天。" +
                "我會用括號告訴你『相機現在看到什麼』;請自然地把看到的東西融入對話(認物教學/看圖說故事)," +
                "只用繁體中文(台灣),每次一兩句、簡短、鼓勵小朋友。";

            // Robotics-ER 認物:較慢間隔避開免費額度 429;只回報標籤,不自己畫對話。
            _vision = gameObject.AddComponent<RoboticsVisionBehaviour>();
            _vision.apiKey = key;
            _vision.intervalSec = 12f;
            _vision.OnSceneLabels = OnSceneLabels;   // 在 vision.Start 前設好(AddComponent 不會立即呼叫 Start)

            Debug.Log("[RoboVisionTalk] 啟動:相機認物 + Live 語音(geminiKey len=" + (key ?? "").Length + ")");
        }

        // 認到新場景 → 只在「變了」時注入 Live(避免每 12s 同場景洗版/打斷語音)。
        private void OnSceneLabels(List<string> labels)
        {
            if (!VisionTalkContext.SceneChanged(labels, _lastLabels)) return;
            _lastLabels = new List<string>(labels);
            string line = VisionTalkContext.VisionLine(labels);
            Debug.Log("[RoboVisionTalk] 場景變化 → 注入 Live:" + line);
            if (_live != null) _live.InjectContext(line);
        }
    }
}
#endif
