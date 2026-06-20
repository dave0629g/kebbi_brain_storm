// 輔導室陪伴機器人 Unity 端(整檔 #if UNITY)。用 JsonUtility 解析烘進場景的設定檔 → 共用 SafetyGateCore/ExplorationCore;
// 跑 CounselorSession,有聲(UnityVoice STT/TTS)/無聲(螢幕打字),🟢 開放聊走真 LLM(UnityLlm)。安全閘為確定性硬規則。
#if UNITY
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;
using KebbiBrain.App.Counselor;
using KebbiBrain.Hardware;

namespace KebbiBrain.Real
{
    // JsonUtility 解析器(Unity-safe,不依賴 System.Text.Json)。
    public static class UnityCounselorLoader
    {
        [Serializable] private class RuleDto { public string category, label, layer; public string[] keywords, phrases, situations; }
        [Serializable] private class RulesFile { public RuleDto[] rules; }
        [Serializable] private class TopicDto { public string id, label; public int weight; public string[] openers; }
        [Serializable] private class TopicsFile { public string login_disclosure; public string[] soft_close; public TopicDto[] topics; }

        public static List<SafetyRule> ParseRules(string json)
        {
            var outp = new List<SafetyRule>();
            var f = JsonUtility.FromJson<RulesFile>(json);
            if (f == null || f.rules == null) return outp;
            foreach (var r in f.rules)
                outp.Add(new SafetyRule
                {
                    Id = r.category, Label = r.label,
                    Layer = r.layer == "red" ? Layer.Red : Layer.Yellow,
                    Keywords = r.keywords ?? new string[0], Phrases = r.phrases ?? new string[0], Situations = r.situations ?? new string[0],
                });
            return outp;
        }

        public static ExplorationCore ParseTopics(string json)
        {
            var f = JsonUtility.FromJson<TopicsFile>(json);
            var openers = new Dictionary<string, string[]>();
            var topics = new List<(int w, TopicProbe t)>();
            if (f != null && f.topics != null)
                foreach (var t in f.topics)
                {
                    openers[t.id] = t.openers ?? new string[0];
                    topics.Add((t.weight, new TopicProbe { Id = t.id, Label = t.label }));
                }
            topics.Sort((a, b) => b.w.CompareTo(a.w));
            var land = new List<TopicProbe>(); foreach (var (w, t) in topics) land.Add(t);
            return new ExplorationCore(land, openers, f?.soft_close ?? new string[0], f?.login_disclosure ?? "");
        }
    }

    public sealed class CounselorBehaviour : MonoBehaviour
    {
        public string rulesJson = "";
        public string topicsJson = "";
        public string personaName = "凱比";

        private CounselorSession _sess;
        private SimConversationLog _log;
        private SimNotifyHuman _notify;
        private ExplorationCore _planner;
        private IVoice _voice;
        private ConvMode _mode = ConvMode.Voice;
        private string _status = "啟動中…";
        private string _alert = "";
        private string _typed = "";
        private bool _busy, _voiceRunning, _ready;

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(rulesJson)) { _status = "⚠ 缺安全規則設定(build 未注入 JSON)"; yield break; }
            ISafetyGate gate;
            try
            {
                gate = new SafetyGateCore(UnityCounselorLoader.ParseRules(rulesJson));
                _planner = UnityCounselorLoader.ParseTopics(topicsJson);
            }
            catch (Exception e) { _status = "⚠ 設定檔解析失敗: " + e.Message; yield break; }

            var ctx = KebbiFactory.Create(RobotTarget.Real, Debug.Log);  // UnityVoice(雲端STT/TTS)+ UnityLlm(真 LLM)
            _voice = ctx.Voice;
            _log = new SimConversationLog("U" + DateTime.UtcNow.Ticks);
            _notify = new SimNotifyHuman(s => { _alert = s; Debug.Log("[Counselor] " + s); });
            _sess = new CounselorSession(null, ctx.Voice, ctx.Llm, gate, _log, _notify, _planner, s => Debug.Log("[Counselor] " + s));

            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                _status = "請允許麥克風…"; Permission.RequestUserPermission(Permission.Microphone);
                float t = 0; while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && t < 12f) { yield return new WaitForSeconds(0.5f); t += 0.5f; }
            }

            string disclosure = string.IsNullOrEmpty(_planner.LoginDisclosure) ? null : _planner.LoginDisclosure;
            _sess.Start("輔導室訪客", _mode, disclosure);
            _ready = true;
            if (_mode == ConvMode.Voice) StartVoiceLoop();
            else _status = "無聲模式:在下方打字跟凱比說";
        }

        private async void StartVoiceLoop()
        {
            if (_voiceRunning) return;
            _voiceRunning = true;
            try
            {
                while (_ready && _mode == ConvMode.Voice && _sess != null && !_sess.Ended)
                {
                    _status = "🎙️ 請說…(凱比在聽)";
                    string heard = "";
                    try { heard = (await _voice.ListenAsync("zh-TW")) ?? ""; } catch (Exception e) { Debug.LogError("[Counselor] STT: " + e.Message); }
                    heard = heard.Trim();
                    if (!_ready || _mode != ConvMode.Voice) break;
                    _busy = true;
                    if (heard.Length == 0) { await _sess.ProbeAsync(); }
                    else { _status = "凱比思考中…"; await _sess.StepAsync(heard); }
                    _busy = false;
                }
            }
            finally { _voiceRunning = false; }
        }

        private async void SendTyped()
        {
            if (_busy || _sess == null || _sess.Ended) return;
            string text = (_typed ?? "").Trim();
            _typed = "";
            _busy = true;
            try { if (text.Length == 0) await _sess.ProbeAsync(); else await _sess.StepAsync(text); }
            catch (Exception e) { Debug.LogError("[Counselor] " + e.Message); }
            _busy = false;
        }

        private void ToggleMode()
        {
            _mode = _mode == ConvMode.Voice ? ConvMode.Silent : ConvMode.Voice;
            _sess?.SwitchMode(_mode);
            if (_mode == ConvMode.Voice) StartVoiceLoop();
            else _status = "無聲模式:在下方打字跟凱比說";
        }

        private void OnGUI()
        {
            int sw = Screen.width, sh = Screen.height;
            int fs = Mathf.Clamp(sh / 42, 18, 40);

            // 頂部:標題 + 安全狀態
            var head = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Clamp(sh / 34, 20, 44), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            GUI.Label(new Rect(0, sh * 0.015f, sw, sh * 0.06f), "輔導室陪伴 · 凱比", head);

            // 模式切換鍵(右上)
            float tw = sw * 0.30f, thh = Mathf.Clamp(sh * 0.05f, 60, 110);
            if (GUI.Button(new Rect(sw - tw - 16, sh * 0.085f, tw, thh), _mode == ConvMode.Voice ? "切換無聲(打字)" : "切換有聲(說話)")) ToggleMode();

            var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Clamp(sh / 52, 16, 30), normal = { textColor = new Color(1f, .9f, .5f) } };
            GUI.Label(new Rect(20, sh * 0.085f, sw * 0.6f, thh), _status + (_busy ? " ⏳" : ""), st);

            // 對話逐句(從 log 取最後幾筆,依層級上色)
            float y = sh * 0.18f;
            var turns = _log != null ? _log.GetTurns() : (IReadOnlyList<LogTurn>)new LogTurn[0];
            int from = turns.Count > 9 ? turns.Count - 9 : 0;
            for (int i = from; i < turns.Count; i++)
            {
                var t = turns[i];
                Color c = t.Layer == Layer.Red ? new Color(1f, .5f, .5f) : t.Layer == Layer.Yellow ? new Color(1f, .85f, .4f)
                        : t.Speaker == Speaker.Student ? new Color(.7f, .9f, 1f) : new Color(.7f, 1f, .8f);
                var ls = new GUIStyle(GUI.skin.label) { fontSize = fs, wordWrap = true, normal = { textColor = c } };
                string who = t.Speaker == Speaker.Student ? "你" : "凱比";
                float hh = sh * 0.085f;
                GUI.Label(new Rect(20, y, sw - 40, hh), who + ": " + t.Text, ls);
                y += hh;
            }

            // 紅線警示橫幅
            if (!string.IsNullOrEmpty(_alert))
            {
                var ab = new GUIStyle(GUI.skin.box) { fontSize = Mathf.Clamp(sh / 50, 16, 30), wordWrap = true, normal = { textColor = Color.white } };
                var old = GUI.backgroundColor; GUI.backgroundColor = new Color(.7f, 0f, 0f, .85f);
                GUI.Box(new Rect(16, sh * 0.86f, sw - 32, sh * 0.08f), "🔴 已通知現場輔導老師,請稍候老師過來", ab);
                GUI.backgroundColor = old;
            }
            else if (_mode == ConvMode.Silent)
            {
                // 無聲模式:打字輸入列
                var tf = new GUIStyle(GUI.skin.textField) { fontSize = fs };
                GUI.SetNextControlName("typed");
                _typed = GUI.TextField(new Rect(16, sh * 0.88f, sw * 0.72f, sh * 0.06f), _typed ?? "", tf);
                if (GUI.Button(new Rect(sw * 0.74f, sh * 0.88f, sw * 0.22f, sh * 0.06f), "送出")) SendTyped();
            }
        }

        private void OnDestroy()  // 返回選單(重載場景)時停止聆聽迴圈
        {
            _ready = false;
            try { if (Microphone.devices != null) foreach (var d in Microphone.devices) Microphone.End(d); } catch { }
        }
    }
}
#endif
