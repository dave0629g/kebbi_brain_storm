// 在 Unity 畫面上以「文字」即時呈現 sim Kebbi 的狀態與收/送(接收/回應)訊息。
// 做法:鏡像 Application.logMessageReceived(遊戲狀態、機身動作、多機 BC|/VC| 收送、學到對端…
//   全都已走 Debug.Log),用 OnGUI 直接畫在螢幕上 → 免 Canvas/prefab、batchmode 出的 APK 也能顯示。
// 用途:兩台真機分散式測試時,被控機螢幕直接看到「收到並執行的命令」,不必接 logcat。整檔 #if UNITY。
#if UNITY
using System.Collections.Generic;
using UnityEngine;

namespace KebbiBrain.Real
{
    public sealed class ScreenLogHud : MonoBehaviour
    {
        [Header("HUD")]
        public string title = "Kebbi Sim — 即時狀態 / 收送";
        public int maxLines = 24;

        private readonly Queue<string> _lines = new Queue<string>();
        private readonly object _lock = new object();
        private GUIStyle _body, _head;

        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }

        private void OnLog(string msg, string stack, LogType type)
        {
            if (string.IsNullOrEmpty(msg)) return;
            // 濾掉 Unity 引擎雜訊與 async 堆疊行,只留遊戲/多機可讀訊息
            if (msg.Contains("UnityEngine.") || msg.Contains("MoveNext()") ||
                msg.Contains("d__") || msg.Contains("AsyncTaskMethodBuilder") ||
                msg.Contains("AsyncVoidMethodBuilder")) return;
            string prefix = (type == LogType.Error || type == LogType.Exception) ? "[!] " : "";
            lock (_lock)
            {
                _lines.Enqueue(prefix + msg);
                while (_lines.Count > maxLines) _lines.Dequeue();
            }
        }

        void OnGUI()
        {
            int w = Screen.width, h = Screen.height;
            if (_body == null)
            {
                int fs = Mathf.Max(16, h / 52);
                _body = new GUIStyle { fontSize = fs, wordWrap = true, richText = false };
                _body.normal.textColor = new Color(0.75f, 1f, 0.75f);
                _head = new GUIStyle { fontSize = fs + 4, fontStyle = FontStyle.Bold };
                _head.normal.textColor = new Color(0.5f, 1f, 0.6f);
            }
            // 半透明黑底
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(14, 12, w - 28, 40), title, _head);
            string text;
            lock (_lock) { text = string.Join("\n", _lines.ToArray()); }
            GUI.Label(new Rect(14, 56, w - 28, h - 66), text, _body);
        }
    }
}
#endif
