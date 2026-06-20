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

            _items = new[]
            {
                new Item{ Label="🎙️  即時語音對話(台灣中文)", Mode=KebbiAppBehaviour.Mode.LiveConversation, Lang="zh-TW", Name="凱比", Char="親切又有耐心的教育機器人", FullScreenUi=true },
                new Item{ Label="👁️  視覺認物(Robotics-ER)", Mode=KebbiAppBehaviour.Mode.RoboticsVision, FullScreenUi=true },
                new Item{ Label="🧭  印尼語方位遊戲(G4)", Mode=KebbiAppBehaviour.Mode.G4_TebakArah },
                new Item{ Label="💬  兩台對話 · STT 端點(需兩台)", Mode=KebbiAppBehaviour.Mode.ConverseStt, Lang="zh-TW", Name="凱比", Char="親切", Note="這個要兩支手機" },
                new Item{ Label="🗣️  兩台對話 · 文字交棒(需兩台)", Mode=KebbiAppBehaviour.Mode.Converse, Lang="zh-TW", Name="凱比", Char="親切", Note="這個要兩支手機" },
            };
        }

        private void OnGUI()
        {
            if (_launched) return;
            int sw = Screen.width, sh = Screen.height;
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
            _launched = true;
            var go = new GameObject("Kebbi");
            var kab = go.AddComponent<KebbiAppBehaviour>();
            kab.secrets = secrets;
            kab.useRealRobotApi = false;
            kab.mode = it.Mode;
            if (!string.IsNullOrEmpty(it.Lang)) kab.personaLang = it.Lang;
            if (!string.IsNullOrEmpty(it.Name)) kab.personaName = it.Name;
            if (!string.IsNullOrEmpty(it.Char)) kab.personaCharacter = it.Char;
            if (!it.FullScreenUi) go.AddComponent<ScreenLogHud>(); // 沒有自己滿版 UI 的功能 → 加文字 HUD 看輸出
            Debug.Log("[Menu] 啟動功能: " + it.Mode);
            // KebbiAppBehaviour.Start 會自動跑該 Mode。
        }
    }
}
#endif
