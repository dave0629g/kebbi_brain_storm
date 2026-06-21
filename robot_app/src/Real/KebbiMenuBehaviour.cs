// 功能選單(整檔 #if UNITY):一個功能一個按鈕。點下去就跑該功能(生成 KebbiAppBehaviour 跑對應 Mode)。
// 取代「一個 APK 綁死一個 Mode」——裝一支 menu APK,在手機上點選要玩哪個。
#if UNITY
using UnityEngine;
using KebbiBrain;

namespace KebbiBrain.Real
{
    public sealed class KebbiMenuBehaviour : MonoBehaviour
    {
        public KebbiSecrets secrets;
        public string counselorRulesJson = "";   // KebbiBuild 讀檔注入(供選單啟動輔導室)
        public string counselorTopicsJson = "";
        private bool _launched;
        private string _note = "";

        private struct Item
        {
            public string Label; public KebbiAppBehaviour.Mode Mode;
            public string Lang, Name, Char; public bool FullScreenUi; public string Note;
        }
        private Item[] _items;

        private void Start()
        {
            Config.Target = RobotTarget.Real;
            Config.UseRealRobotApi = false;           // 一般 Android(非真凱比)當開發機
            if (secrets != null) secrets.ApplyToConfig();
            else Debug.LogWarning("[Menu] 未指定 secrets,雲端功能會缺金鑰。");

            // 金鑰盤點:把「沒設/還是範例值」一眼標出(不印任何金鑰值),非工程的人也能自助排除。
            int missingKeys = 0;
            foreach (var k in KebbiBrain.Hardware.SecretsCheck.ReportConfig())
            {
                Debug.Log("[Menu][金鑰] " + k.Name + ": " + (k.Ok ? "OK " : "缺 ") + k.Hint);
                if (!k.Ok) missingKeys++;
            }
            if (missingKeys > 0) _note = "注意:有 " + missingKeys + " 個金鑰未設定/疑似佔位,相關雲端功能會失敗(詳見 log)";

            _items = new[]
            {
                new Item{ Label="輔導室陪伴機器人", Mode=KebbiAppBehaviour.Mode.Counselor, Lang="zh-TW", Name="凱比", Char="溫暖、穩、接得住的陪伴者", FullScreenUi=true },
                new Item{ Label="即時語音對話(台灣中文)", Mode=KebbiAppBehaviour.Mode.LiveConversation, Lang="zh-TW", Name="凱比", Char="親切又有耐心的教育機器人", FullScreenUi=true },
                new Item{ Label="視覺認物(Robotics-ER)", Mode=KebbiAppBehaviour.Mode.RoboticsVision, FullScreenUi=true },
                new Item{ Label="看著物件對話(Live+視覺)", Mode=KebbiAppBehaviour.Mode.RoboVisionTalk, Lang="zh-TW", Name="凱比", Char="看著東西陪你聊的教育機器人", FullScreenUi=true },
                new Item{ Label="鏡像體操教練(G3)", Mode=KebbiAppBehaviour.Mode.G3_MirrorCoach },
                new Item{ Label="印尼語方位遊戲(G4)", Mode=KebbiAppBehaviour.Mode.G4_TebakArah },
                new Item{ Label="兩台對話 · STT 端點(需兩台)", Mode=KebbiAppBehaviour.Mode.ConverseStt, Lang="zh-TW", Name="凱比", Char="親切", Note="這個要兩支手機" },
                new Item{ Label="兩台對話 · 文字交棒(需兩台)", Mode=KebbiAppBehaviour.Mode.Converse, Lang="zh-TW", Name="凱比", Char="親切", Note="這個要兩支手機" },
            };
        }

        private void OnGUI()
        {
            int sw = Screen.width, sh = Screen.height;
            if (_launched)
            {
                // 功能執行中:畫「返回選單」鍵在最上層(GUI.depth 低=蓋在功能畫面上、優先收點擊)。
                // 點了重載場景 → 乾淨關掉目前功能的 WebSocket/麥克風/相機(各 behaviour 的 OnDestroy 收尾)→ 回到選單。
                GUI.depth = -1000;
                float w = Mathf.Clamp(sw * 0.34f, 190f, 440f), h = Mathf.Clamp(sh * 0.058f, 72f, 130f);
                var backStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.Clamp(sh / 46, 18, 34), fontStyle = FontStyle.Bold };
                var oldBg = GUI.backgroundColor; GUI.backgroundColor = new Color(0f, 0f, 0f, 0.7f);
                if (GUI.Button(new Rect(16f, 16f, w, h), "← 返回選單", backStyle))
                    UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                GUI.backgroundColor = oldBg;
                return;
            }
            var title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Clamp(sh / 26, 26, 60), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            GUI.Label(new Rect(0, sh * 0.035f, sw, sh * 0.08f), "Kebbi 功能選單", title);
            var sub = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Clamp(sh / 56, 14, 26), alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.8f, .8f, .8f) } };
            GUI.Label(new Rect(0, sh * 0.115f, sw, sh * 0.04f), "點一個功能開始", sub);

            float bw = sw * 0.88f, bh = Mathf.Clamp(sh * 0.105f, 86f, 190f), x = (sw - bw) / 2f, y = sh * 0.17f, gap = bh * 0.22f;
            var bs = new GUIStyle(GUI.skin.button) { fontSize = Mathf.Clamp(sh / 36, 20, 40), alignment = TextAnchor.MiddleLeft, padding = new RectOffset(28, 12, 0, 0) };
            for (int i = 0; i < _items.Length; i++)
                if (GUI.Button(new Rect(x, y + i * (bh + gap), bw, bh), _items[i].Label, bs)) Launch(_items[i]);

            if (!string.IsNullOrEmpty(_note))
            {
                var ns = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Clamp(sh / 50, 16, 28), alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, .85f, .4f) } };
                GUI.Label(new Rect(0, sh * 0.92f, sw, sh * 0.06f), _note, ns);
            }
        }

        private void Launch(Item it)
        {
            // 開機自檢:這個功能要的金鑰/權限/網路缺什麼,先 log 標出(非工程的人也能自助排除「點下去沒反應」)。
            // 非阻塞:仍照常啟動(部分功能可降級;真正缺項由功能自身 UI 再提示)。
            var pf = KebbiBrain.Hardware.PreflightCore.Evaluate(
                KebbiBrain.Hardware.PreflightCore.RequirementsFor(it.Mode.ToString()), PreflightOk);
            foreach (var r in pf) Debug.Log("[Menu][自檢] " + it.Label + " · " + r.Label + ": " + (r.Ok ? "OK" : "缺 — " + r.Hint));
            var fail = KebbiBrain.Hardware.PreflightCore.Failing(pf);
            if (fail.Count > 0) _note = it.Label + " 可能無法正常運作,缺:" + string.Join("、", fail.ToArray());

            _launched = true;
            var go = new GameObject("Kebbi");
            var kab = go.AddComponent<KebbiAppBehaviour>();
            kab.secrets = secrets;
            kab.useRealRobotApi = false;
            kab.mode = it.Mode;
            if (!string.IsNullOrEmpty(it.Lang)) kab.personaLang = it.Lang;
            if (!string.IsNullOrEmpty(it.Name)) kab.personaName = it.Name;
            if (!string.IsNullOrEmpty(it.Char)) kab.personaCharacter = it.Char;
            if (it.Mode == KebbiAppBehaviour.Mode.Counselor)   // 把烘進選單的輔導室設定檔傳給功能
            { kab.counselorRulesJson = counselorRulesJson; kab.counselorTopicsJson = counselorTopicsJson; }
            if (!it.FullScreenUi) go.AddComponent<ScreenLogHud>(); // 沒有自己滿版 UI 的功能 → 加文字 HUD 看輸出
            Debug.Log("[Menu] 啟動功能: " + it.Mode);
            // KebbiAppBehaviour.Start 會自動跑該 Mode。
        }

        // 開機自檢:各資源現況(金鑰走 SecretsCheck、權限走 Permission、網路走 internetReachability)。
        private bool PreflightOk(KebbiBrain.Hardware.PreflightItem item)
        {
            switch (item)
            {
                case KebbiBrain.Hardware.PreflightItem.SpeechKey: return KebbiBrain.Hardware.SecretsCheck.IsUsable(Config.SpeechKey);
                case KebbiBrain.Hardware.PreflightItem.LlmKey: return KebbiBrain.Hardware.SecretsCheck.IsUsable(Config.LlmKey);
                case KebbiBrain.Hardware.PreflightItem.GeminiKey: return KebbiBrain.Hardware.SecretsCheck.IsUsable(Config.GeminiKey);
                case KebbiBrain.Hardware.PreflightItem.Microphone: return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
                case KebbiBrain.Hardware.PreflightItem.Camera: return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
                case KebbiBrain.Hardware.PreflightItem.Network: return Application.internetReachability != NetworkReachability.NotReachable;
                default: return true;
            }
        }
    }
}
#endif
