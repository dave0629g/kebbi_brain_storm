// 視覺安全之眼(整檔 #if UNITY):隱私優先——只判「有沒有人 + 大致位置」,看到人就打招呼/轉頭看你,人離開降為待命。
// 隱私紅線(對齊規格第十五節 + 使用者要求):
//   • 提示只問 presence/position,不辨識身分、不描述外觀。
//   • 相機幀只在「記憶體」編碼送出、用完即丟 —— 絕不落檔、絕不顯示畫面(螢幕只顯示存在狀態 + 告知)。
//   • 回應只取 person + x,其餘一律丟棄。
// 與 #1 EmpathyBody 的 DOA 眼神接觸互補:DOA 在學生「開口」時轉頭(免相機);本功能在「沉默」時也知道有沒有人。
#if UNITY
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Networking;
using KebbiBrain;
using KebbiBrain.App;
using KebbiBrain.Hardware;

namespace KebbiBrain.Real
{
    public sealed class PresenceVisionBehaviour : MonoBehaviour
    {
        public string apiKey = "";
        public float intervalSec = 12f;   // 避開 Robotics-ER 免費額度 429
        public float cameraFovDeg = RoboGuideMath.DefaultCameraFovDeg;

        private WebCamTexture _cam;
        private Texture2D _frame;
        private readonly PresenceWatcher _watcher = new PresenceWatcher(confirmFrames: 2, moveThreshold: 120f);
        private IKebbiBody _body;
        private IVoice _voice;
        private bool _busy, _greetedOnce;
        private string _status = "啟動中…";
        private const string Disclosure = "視覺只用來知道「有沒有人、大概在哪」,不認身分、不存影像、不錄影。";

        private IEnumerator Start()
        {
            string key = !string.IsNullOrEmpty(apiKey) ? apiKey : Config.GeminiKey;
            apiKey = key;
            if (string.IsNullOrEmpty(key)) { _status = "⚠ 沒有 Gemini 金鑰(KEBBI_GEMINI_KEY)"; yield break; }

            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                _status = "請允許相機權限(只判有沒有人,不存影像)…";
                Permission.RequestUserPermission(Permission.Camera);
                float t = 0f;
                while (!Permission.HasUserAuthorizedPermission(Permission.Camera) && t < 12f) { yield return new WaitForSeconds(0.5f); t += 0.5f; }
            }
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera)) { _status = "⚠ 沒有相機權限"; yield break; }

            try { var ctx = KebbiFactory.Create(RobotTarget.Real, Debug.Log); _body = ctx.Body; _voice = ctx.Voice; }
            catch (Exception e) { Debug.LogWarning("[SafetyEye] 取 Body/Voice 失敗: " + e.Message); }

            float wd = 0f;
            while (WebCamTexture.devices.Length == 0 && wd < 4f) { yield return new WaitForSeconds(0.3f); wd += 0.3f; }
            string dev = null;
            foreach (var d in WebCamTexture.devices) { if (d.isFrontFacing) { dev = d.name; break; } }  // 前鏡頭優先(面向學生)
            if (dev == null && WebCamTexture.devices.Length > 0) dev = WebCamTexture.devices[0].name;
            _cam = string.IsNullOrEmpty(dev) ? new WebCamTexture(640, 480, 15) : new WebCamTexture(dev, 640, 480, 15);
            _cam.Play();
            float wp = 0f;
            while (_cam.width < 16 && wp < 6f) { yield return new WaitForSeconds(0.2f); wp += 0.2f; }
            _status = "待命中(看看有沒有人)";
            StartCoroutine(LoopAsync());
        }

        private IEnumerator LoopAsync()
        {
            var wait = new WaitForSeconds(intervalSec);
            while (true)
            {
                yield return wait;
                if (_busy || _cam == null || _cam.width < 16) continue;
                yield return StartCoroutine(GlanceAsync());
            }
        }

        // 看一眼:拍幀(記憶體)→ 問 presence → 解析只取 person+x → 用完即丟。
        private IEnumerator GlanceAsync()
        {
            _busy = true;
            if (_frame == null || _frame.width != _cam.width || _frame.height != _cam.height)
                _frame = new Texture2D(_cam.width, _cam.height, TextureFormat.RGB24, false);
            _frame.SetPixels32(_cam.GetPixels32());
            _frame.Apply();
            byte[] jpg = _frame.EncodeToJPG(45);   // 只在記憶體;送出後即丟,不落檔
            string body = GeminiRoboticsProtocol.BuildRequestBody(Convert.ToBase64String(jpg), PresenceVision.PresencePrompt());
            jpg = null;

            using (var req = new UnityWebRequest(GeminiRoboticsProtocol.Endpoint(GeminiRoboticsProtocol.DefaultModel), "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader(GeminiRoboticsProtocol.ApiKeyHeader, apiKey);
                yield return req.SendWebRequest();

                PresenceState st;
                if (req.result != UnityWebRequest.Result.Success)
                { _status = "視覺暫時看不清(" + req.responseCode + "),維持待命"; st = new PresenceState { PersonPresent = false, PositionX = -1f }; }
                else
                    st = PresenceVision.FromResponse(req.downloadHandler.text);   // 只取 person+x,其餘丟棄

                HandleEvent(_watcher.Observe(st), st);
            }
            _busy = false;
        }

        private void HandleEvent(PresenceEvent ev, PresenceState st)
        {
            switch (ev)
            {
                case PresenceEvent.Arrived:
                    FaceUser(st.PositionX);
                    if (!_greetedOnce && _voice != null) { try { _voice.SpeakAsync("嗨,你來啦,我看到你了。", "zh-TW"); } catch { } }
                    _greetedOnce = true;
                    _status = "看到你了 🙂";
                    break;
                case PresenceEvent.Moved:
                    FaceUser(st.PositionX);
                    _status = "看著你";
                    break;
                case PresenceEvent.Left:
                    _greetedOnce = false;
                    _status = "待命中(人離開了,我在這等你)";
                    break;
                case PresenceEvent.StillPresent:
                    _status = "看著你";
                    break;
                default:
                    _status = "待命中(看看有沒有人)";
                    break;
            }
        }

        // 轉頭面向人(前鏡頭鏡像 → 翻 x)。免授權 setMotor / FaceFully。
        private void FaceUser(float x)
        {
            if (_body == null || x < 0) return;
            try { KebbiHead.FaceFully(_body, RoboGuideMath.AngleForX(RoboGuideMath.MirrorX(x), cameraFovDeg)); } catch { }
        }

        // 螢幕「不顯示相機畫面」——只顯示存在狀態 + 隱私告知(強化「我們只感測存在、不錄你」)。
        private void OnGUI()
        {
            int sw = Screen.width, sh = Screen.height;
            var bg = new Color(0.05f, 0.06f, 0.1f, 1f);
            var old = GUI.color; GUI.color = bg; GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture); GUI.color = old;

            var big = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Clamp(sh / 16, 28, 72), alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            GUI.Label(new Rect(0, sh * 0.34f, sw, sh * 0.18f), _status, big);

            var note = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Clamp(sh / 42, 16, 30), wordWrap = true, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.7f, .8f, .95f) } };
            GUI.Label(new Rect(sw * 0.08f, sh * 0.78f, sw * 0.84f, sh * 0.16f), "🔒 " + Disclosure, note);
        }

        private void OnDestroy()
        {
            try { if (_cam != null) { _cam.Stop(); _cam = null; } } catch { }
            _frame = null;   // 不保留任何影像
        }
    }
}
#endif
