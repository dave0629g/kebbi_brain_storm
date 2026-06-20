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
using UnityEngine.Networking;
using KebbiBrain.Hardware;

namespace KebbiBrain.Real
{
    public sealed class RoboticsVisionBehaviour : MonoBehaviour
    {
        public string apiKey = "";
        public string model = GeminiRoboticsProtocol.DefaultModel;
        public string prompt = GeminiRoboticsProtocol.DefaultPrompt;
        public float intervalSec = 4f;

        private WebCamTexture _cam;
        private Texture2D _frame;
        private List<Detection> _dets = new List<Detection>();
        private string _status = "啟動中…";
        private string _reasoning = "";
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
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            { _status = "⚠ 沒有相機權限(請允許 CAMERA)"; Debug.LogWarning("[RoboVision] " + _status); yield break; }

            string dev = null;
            foreach (var d in WebCamTexture.devices) { if (!d.isFrontFacing) { dev = d.name; break; } }  // 後鏡頭優先
            if (dev == null && WebCamTexture.devices.Length > 0) dev = WebCamTexture.devices[0].name;
            _cam = string.IsNullOrEmpty(dev) ? new WebCamTexture(1280, 720, 30) : new WebCamTexture(dev, 1280, 720, 30);
            _cam.Play();
            _status = "相機開啟,每 " + intervalSec + "s 問一次 Gemini…";
            Debug.Log("[RoboVision] " + _status + " (cam=" + dev + ")");
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

            using (var req = new UnityWebRequest(GeminiRoboticsProtocol.Endpoint(model, apiKey), "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
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
                    _reasoning = Trunc(GeminiRoboticsProtocol.ExtractModelText(resp), 180);
                    _shots++;
                    var sb = new StringBuilder();
                    foreach (var d in _dets) sb.Append(d.Label).Append(' ');
                    _status = "✓ 第" + _shots + "張 · 認出 " + _dets.Count + " 物 · " + _lastMs + "ms";
                    Debug.Log("[RoboVision] " + _status + " → " + sb);
                }
            }
            _busy = false;
        }

        private void OnGUI()
        {
            int sw = Screen.width, sh = Screen.height;
            if (_cam != null && _cam.width > 16)
                GUI.DrawTexture(new Rect(0, 0, sw, sh), _cam, ScaleMode.ScaleToFit, false);

            var lbl = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(18, sh / 38), fontStyle = FontStyle.Bold, normal = { textColor = Color.cyan } };
            foreach (var d in _dets)
            {
                if (d.HasBox)
                {
                    float x = d.Xmin / 1000f * sw, y = d.Ymin / 1000f * sh;
                    float w = (d.Xmax - d.Xmin) / 1000f * sw, h = (d.Ymax - d.Ymin) / 1000f * sh;
                    DrawRect(new Rect(x, y, w, h), Color.green, 3);
                    GUI.Label(new Rect(x + 3, y + 1, sw, 48), d.Label, lbl);
                }
                else if (d.HasPoint)
                {
                    float x = d.X / 1000f * sw, y = d.Y / 1000f * sh;
                    DrawRect(new Rect(x - 13, y - 13, 26, 26), Color.green, 3);
                    GUI.Label(new Rect(x + 16, y - 16, sw, 48), d.Label, lbl);
                }
            }

            var s2 = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(16, sh / 50), normal = { textColor = Color.yellow }, wordWrap = true };
            GUI.Label(new Rect(12, 12, sw - 24, 140), "[Robotics-ER 視覺] " + _status + (_errors > 0 ? "  (err " + _errors + ")" : "") + "\n" + _reasoning, s2);
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
    }
}
#endif
