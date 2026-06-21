// Gemini Robotics-ER 視覺探索(整檔 #if UNITY)。
// 開相機 → 每隔幾秒拍一張 → 送 Gemini Robotics-ER 看 → 在螢幕上框出/指出物體+繁中標籤。
// 讓你在 Android 上直接「看 Gemini 能認出什麼、在哪」。需要 Config.GeminiKey(KEBBI_GEMINI_KEY 注入)。
// RoboGuide:點畫面上的物體 → 凱比讀座標用 RoboGuideMath→FaceFully 轉頭指它 + TTS 念出名稱。
#if UNITY
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Networking;
using KebbiBrain;
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

        // RoboGuide:轉頭指認 + 念名稱。Body/Voice 由 KebbiFactory 取(非真凱比=SimKebbiBody no-op)。
        public float cameraFovDeg = RoboGuideMath.DefaultCameraFovDeg;  // 相機水平 FOV,真機現場校(必測③)
        private IKebbiBody _body;
        private IVoice _voice;
        private bool _frontCamera;
        private string _pointing = "";
        private float _pointShownUntil;
        public System.Action<List<string>> OnSceneLabels;  // 每次認物完回報標籤清單(看著物件對話的橋接用)

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

            // RoboGuide:取 Body(轉頭)+ Voice(念名稱)。非真凱比時 Body=SimKebbiBody(SetMotor 只 log,不會動)。
            try { var ctx = KebbiFactory.Create(RobotTarget.Real, Debug.Log); _body = ctx.Body; _voice = ctx.Voice; }
            catch (Exception e) { Debug.LogWarning("[RoboGuide] 取 Body/Voice 失敗(指認/念名稱停用): " + e.Message); }

            // 給裝置時間枚舉相機(剛授權時 devices 可能還是空的)。
            float wd = 0f;
            while (WebCamTexture.devices.Length == 0 && wd < 4f) { yield return new WaitForSeconds(0.3f); wd += 0.3f; }
            string dev = null;
            foreach (var d in WebCamTexture.devices) { if (!d.isFrontFacing) { dev = d.name; break; } }  // 後鏡頭優先
            if (dev == null && WebCamTexture.devices.Length > 0) dev = WebCamTexture.devices[0].name;
            foreach (var d in WebCamTexture.devices) if (d.name == dev) { _frontCamera = d.isFrontFacing; break; }  // 前鏡頭影像鏡像→指認時翻 x
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
                    if (OnSceneLabels != null)
                    {
                        var labels = new List<string>();
                        foreach (var d in _dets) if (!string.IsNullOrEmpty(d.Label)) labels.Add(d.Label);
                        OnSceneLabels(labels);
                    }
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
                Rect tap;
                if (d.HasBox)
                {
                    float x = d.Xmin / 1000f * sw, y = d.Ymin / 1000f * sh;
                    float w = (d.Xmax - d.Xmin) / 1000f * sw, h = (d.Ymax - d.Ymin) / 1000f * sh;
                    DrawRect(new Rect(x, y, w, h), Color.green, 3);
                    DrawTag(d.Label, x, y, fs);
                    tap = new Rect(x, y, w, h);
                }
                else if (d.HasPoint)
                {
                    float x = d.X / 1000f * sw, y = d.Y / 1000f * sh;
                    DrawRect(new Rect(x - 13, y - 13, 26, 26), Color.green, 3);
                    DrawTag(d.Label, x + 16, y - fs - 6, fs);
                    tap = new Rect(x - 44, y - 44, 88, 88);
                }
                else continue;
                if (GUI.Button(tap, GUIContent.none, GUIStyle.none)) PointAt(d);  // 透明熱區:點它→轉頭指認+念名稱
            }

            // 頂部提示:操作說明 / 最近指認的物體。
            int hs = Mathf.Clamp(sh / 50, 14, 26), hbar = hs + 14;
            Fill(new Rect(0, 0, sw, hbar), new Color(0, 0, 0, 0.45f));
            var hint = new GUIStyle(GUI.skin.label) { fontSize = hs, normal = { textColor = Color.white } };
            string htext = (!string.IsNullOrEmpty(_pointing) && Time.realtimeSinceStartup < _pointShownUntil)
                         ? "指著:" + _pointing : "點畫面上的物體 → 凱比轉頭指它並念出名稱";
            GUI.Label(new Rect(12, 6, sw - 24, hbar), htext, hint);

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

        // RoboGuide:點一個偵測到的物體 → 讀其座標算轉頭角 → FaceFully 轉頭朝它 + TTS 念名稱。
        private void PointAt(Detection d)
        {
            try
            {
                float ang = RoboGuideMath.AngleForDetection(d, cameraFovDeg, _frontCamera);
                if (_body != null) KebbiHead.FaceFully(_body, ang);                       // 輪式=底盤+頭;H201=頭部夾 ±40 部分面向
                if (_voice != null && !string.IsNullOrEmpty(d.Label)) { try { _ = _voice.SpeakAsync(d.Label, "zh-TW"); } catch { } }
                _pointing = d.Label; _pointShownUntil = Time.realtimeSinceStartup + 3f;
                Debug.Log("[RoboGuide] 指「" + d.Label + "」@ x=" + RoboGuideMath.CenterX(d).ToString("0")
                          + " → 轉頭 " + ang.ToString("0.0") + "°" + (_frontCamera ? " (前鏡頭鏡像)" : ""));
            }
            catch (Exception e) { Debug.LogWarning("[RoboGuide] 指認失敗: " + e.Message); }
        }

        private static string Trunc(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "…");

        private void OnDestroy()   // 返回選單(重載場景)時關相機,釋放硬體
        {
            try { if (_cam != null) { _cam.Stop(); _cam = null; } } catch { }
        }
    }
}
#endif
