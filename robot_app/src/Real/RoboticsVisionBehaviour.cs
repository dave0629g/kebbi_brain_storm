// Gemini Robotics-ER 視覺探索(整檔 #if UNITY)。
// 開相機 → 每隔幾秒拍一張 → 送 Gemini Robotics-ER 看 → 在螢幕上框出/指出物體+繁中標籤。
// 讓你在 Android 上直接「看 Gemini 能認出什麼、在哪」。需要 Config.GeminiKey(KEBBI_GEMINI_KEY 注入)。
// 之後可把偵測到的物體座標接 NeckZ/FaceFully 讓 Kebbi 轉頭/指它,或用 TTS 講出來。
#if UNITY
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Networking;
using KebbiBrain.Hardware;

namespace KebbiBrain.Real
{
    public sealed class RoboticsVisionBehaviour : MonoBehaviour
    {
        public string apiKey = "";
        public string model = GeminiRoboticsProtocol.DefaultModel;
        public string prompt = GeminiRoboticsProtocol.DefaultPrompt;
        public float intervalSec = 12f; // 放慢以避開 robotics-er preview 的免費額度限制(429);太快會 quota exceeded

        private WebCamTexture _cam;
        private Texture2D _frame;
        private List<Detection> _dets = new List<Detection>();
        private string _status = "啟動中…";
        private int _shots, _errors, _lastMs;
        private bool _busy;
        private static Texture2D _px;

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                _status = "⚠ 沒有 Gemini 金鑰(KEBBI_GEMINI_KEY)→ 先 export 再 build。";
                Debug.LogWarning("[RoboVision] " + _status);
                yield break;
            }
            // Android 正規相機權限(MIUI 會跳框 → 由 adb 自動點允許或使用者按)。
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                _status = "請允許相機權限…";
                Permission.RequestUserPermission(Permission.Camera);
                float t = 0f;
                while (!Permission.HasUserAuthorizedPermission(Permission.Camera) && t < 12f)
                { yield return new WaitForSeconds(0.5f); t += 0.5f; }
            }
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            { _status = "⚠ 沒有相機權限(請到設定允許 CAMERA)"; Debug.LogWarning("[RoboVision] " + _status); yield break; }

            // 給裝置時間枚舉相機(剛授權時 devices 可能還是空的)。
            float wd = 0f;
            while (WebCamTexture.devices.Length == 0 && wd < 4f) { yield return new WaitForSeconds(0.3f); wd += 0.3f; }
            string dev = null;
            foreach (var d in WebCamTexture.devices) { if (!d.isFrontFacing) { dev = d.name; break; } }  // 後鏡頭優先
            if (dev == null && WebCamTexture.devices.Length > 0) dev = WebCamTexture.devices[0].name;
            _cam = string.IsNullOrEmpty(dev) ? new WebCamTexture(1280, 720, 30) : new WebCamTexture(dev, 1280, 720, 30);
            _cam.Play();
            // 等相機真的開始吐影格(width 變 >16 才能取像)。
            float wp = 0f;
            while (_cam.width < 16 && wp < 6f) { yield return new WaitForSeconds(0.2f); wp += 0.2f; }
            _status = "相機開啟(" + _cam.width + "x" + _cam.height + "),每 " + intervalSec + "s 問一次 Gemini…";
            Debug.Log("[RoboVision] " + _status + " (cam=" + dev + " devs=" + WebCamTexture.devices.Length + ")");
            StartCoroutine(LoopAsync());
        }

        private IEnumerator LoopAsync()
        {
            var wait = new WaitForSeconds(intervalSec);
            while (true)
            {
                yield return wait;
                if (_busy || _cam == null || _cam.width < 16) continue;
                yield return StartCoroutine(ShotAsync());
            }
        }

        private IEnumerator ShotAsync()
        {
            _busy = true;
            if (_frame == null || _frame.width != _cam.width || _frame.height != _cam.height)
                _frame = new Texture2D(_cam.width, _cam.height, TextureFormat.RGB24, false);
            _frame.SetPixels32(_cam.GetPixels32());
            _frame.Apply();
            byte[] jpg = _frame.EncodeToJPG(60);
            string body = GeminiRoboticsProtocol.BuildRequestBody(Convert.ToBase64String(jpg), prompt);

            using (var req = new UnityWebRequest(GeminiRoboticsProtocol.Endpoint(model), "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader(GeminiRoboticsProtocol.ApiKeyHeader, apiKey);  // 金鑰走 header,不進 URL
                float t0 = Time.realtimeSinceStartup;
                yield return req.SendWebRequest();
                _lastMs = (int)((Time.realtimeSinceStartup - t0) * 1000f);

                if (req.result != UnityWebRequest.Result.Success)
                {
                    _errors++;
                    _status = "✗ Gemini 失敗(" + req.responseCode + "): " + req.error;
                    Debug.LogError("[RoboVision] " + _status + " " + Trunc(req.downloadHandler.text, 300));
                }
                else
                {
                    string resp = req.downloadHandler.text;
                    _dets = GeminiRoboticsProtocol.ParseDetections(resp);
                    _shots++;
                    var sb = new StringBuilder();
                    foreach (var d in _dets) sb.Append(d.Label).Append(' ');
                    _status = "✓ 第" + _shots + "張 · 認出 " + _dets.Count + " 物 · " + _lastMs + "ms";
                    Debug.Log("[RoboVision] " + _status + " → " + sb);
                    if (_dets.Count == 0) Debug.Log("[RoboVision] (0物)原始回應: " + Trunc(resp, 240)); // 診斷:空場景 vs 解析漏
                }
            }
            _busy = false;
        }

        // 極簡疊圖:相機 + 綠框 + 物體名稱小標籤(深色底好讀)+ 底部一行小狀態。無任何 log/推理文字。
        private void OnGUI()
        {
            int sw = Screen.width, sh = Screen.height;
            if (_cam != null && _cam.width > 16)
                GUI.DrawTexture(new Rect(0, 0, sw, sh), _cam, ScaleMode.ScaleToFit, false);

            int fs = Mathf.Clamp(sh / 36, 20, 40);
            foreach (var d in _dets)
            {
                if (d.HasBox)
                {
                    float x = d.Xmin / 1000f * sw, y = d.Ymin / 1000f * sh;
                    float w = (d.Xmax - d.Xmin) / 1000f * sw, h = (d.Ymax - d.Ymin) / 1000f * sh;
                    DrawRect(new Rect(x, y, w, h), Color.green, 3);
                    DrawTag(d.Label, x, y, fs);
                }
                else if (d.HasPoint)
                {
                    float x = d.X / 1000f * sw, y = d.Y / 1000f * sh;
                    DrawRect(new Rect(x - 13, y - 13, 26, 26), Color.green, 3);
                    DrawTag(d.Label, x + 16, y - fs - 6, fs);
                }
            }

            // 底部一行小狀態(深色半透明底);出錯才顯紅。
            int ss = Mathf.Clamp(sh / 64, 14, 26);
            var st = new GUIStyle(GUI.skin.label) { fontSize = ss, normal = { textColor = _errors > 0 ? new Color(1f, .5f, .5f) : Color.white } };
            Fill(new Rect(0, sh - ss - 14, sw, ss + 14), new Color(0, 0, 0, 0.5f));
            GUI.Label(new Rect(12, sh - ss - 9, sw - 24, ss + 8), _status, st);
        }

        // 物體名稱小標籤:深色半透明底 + 白字,疊在框左上角,好讀又不佔畫面。
        private static void DrawTag(string text, float x, float y, int fs)
        {
            if (string.IsNullOrEmpty(text)) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = fs, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            Vector2 sz = style.CalcSize(new GUIContent(text));
            if (y < 0) y = 0; if (x < 0) x = 0;
            Fill(new Rect(x, y, sz.x + 12, sz.y + 4), new Color(0, 0, 0, 0.6f));
            GUI.Label(new Rect(x + 6, y + 2, sz.x + 12, sz.y + 4), text, style);
        }

        private static void Fill(Rect r, Color c)
        {
            if (_px == null) { _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply(); }
            var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, _px); GUI.color = old;
        }

        private static void DrawRect(Rect r, Color c, float th)
        {
            if (_px == null) { _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply(); }
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, th), _px);
            GUI.DrawTexture(new Rect(r.x, r.yMax - th, r.width, th), _px);
            GUI.DrawTexture(new Rect(r.x, r.y, th, r.height), _px);
            GUI.DrawTexture(new Rect(r.xMax - th, r.y, th, r.height), _px);
            GUI.color = old;
        }

        private static string Trunc(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "…");

        private void OnDestroy()   // 返回選單(重載場景)時關相機,釋放硬體
        {
            try { if (_cam != null) { _cam.Stop(); _cam = null; } } catch { }
        }
    }
}
#endif
