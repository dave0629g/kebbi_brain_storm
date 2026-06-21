// 輔導室陪伴機器人 Unity 端(整檔 #if UNITY)。用 JsonUtility 解析烘進場景的設定檔 → 共用 SafetyGateCore/ExplorationCore;
// 跑 CounselorSession,有聲(UnityVoice STT/TTS)/無聲(螢幕打字),🟢 開放聊走真 LLM(UnityLlm)。安全閘為確定性硬規則。
#if UNITY
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        private string _logPath = "", _cardPath = "";
        private ReloadableSafetyGate _gate;
        private string _rulesPath = "";

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(rulesJson)) { _status = "⚠ 缺安全規則設定(build 未注入 JSON)"; yield break; }
            ISafetyGate gate;
            try
            {
                // 安全規則:優先讀「老師可維護」的外部檔(persistentDataPath/counselor_rules.json),無檔則用 build 烘進的內建範例。
                // 可熱重載 → 老師更新關鍵字後按「重載安全規則」即生效;重載失敗保留舊規則(絕不無守門)。內容須輔導專業定稿。
                _rulesPath = Path.Combine(Application.persistentDataPath, "counselor_rules.json");
                string bakedRules = rulesJson;
                Func<IReadOnlyList<SafetyRule>> loadRules = () =>
                {
                    string json = bakedRules;
                    try { if (File.Exists(_rulesPath)) json = File.ReadAllText(_rulesPath); } catch { }
                    return UnityCounselorLoader.ParseRules(json);
                };
                _gate = new ReloadableSafetyGate(loadRules);
                gate = _gate;
                _planner = UnityCounselorLoader.ParseTopics(topicsJson);
            }
            catch (Exception e) { _status = "⚠ 設定檔解析失敗: " + e.Message; yield break; }

            var ctx = KebbiFactory.Create(RobotTarget.Real, Debug.Log);  // UnityVoice(雲端STT/TTS)+ UnityLlm(真 LLM)
            _voice = ctx.Voice;

            // 逐句記錄落檔 + 交接卡輸出。⚠ 隱私:落在 app 私有 persistentDataPath(僅本機 app 可讀、不外傳);
            // 「存哪/誰能讀/保存多久」最終須由校方+使用者依隱私規格(第十/十五節)定案,此處先提供機制。
            string sid = "U" + DateTime.UtcNow.Ticks;
            string dir = Path.Combine(Application.persistentDataPath, "counselor_logs");
            try { Directory.CreateDirectory(dir); } catch { }
            _logPath = Path.Combine(dir, sid + ".jsonl");          // 逐句 append-only(每行一筆 JSON)
            _cardPath = Path.Combine(dir, sid + "_handoff.txt");   // 交接卡:老師可讀文字 + 機器讀 JSON
            _log = new SimConversationLog(sid, onAppendLine: AppendLogLine, logLink: "file://" + _logPath);
            _notify = new SimNotifyHuman(s => { _alert = s; Debug.Log("[Counselor] " + s); });
            _notify.OnHumanCalled += c => WriteCard("🔴 即時呼叫現場真人", c);
            _notify.OnYellowQueued += c => WriteCard("🟡 待辦交接卡", c);
            _notify.OnSummary += c => WriteCard("📋 會談結束摘要", c);
            Debug.Log("[Counselor] 記錄落檔: " + _logPath);
            // Air S 內建臉表情(playFaceAnimation)接進共情:燈號/時機→臉(Login暖/綠Happy/傾聽/黃Concerned/紅Calm)。
            var face = new UnityFaceExpression();
            _sess = new CounselorSession(ctx.Body, ctx.Voice, ctx.Llm, gate, _log, _notify, _planner, s => Debug.Log("[Counselor] " + s), face);  // ctx.Body→具身共情(真凱比=馬達;一般 Android=SimKebbiBody no-op);face→Air S 內建臉
            // PIR 存在感(非視覺):學生靠近→迎接+暖臉、離開→降待命。真機驗 requestSensor(SENSOR_PIR)+onPIREvent。
            try { _sess.WatchPresence(new UnityPresenceSensor()); } catch (Exception e) { Debug.LogWarning("[Counselor] PIR 接入略過: " + e.Message); }

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

            // 老師端:更新外部 counselor_rules.json 後按此熱重載安全關鍵字(重載失敗保留舊規則)。
            if (_gate != null)
            {
                var rs = new GUIStyle(GUI.skin.button) { fontSize = Mathf.Clamp(sh / 60, 12, 24) };
                if (GUI.Button(new Rect(sw * 0.63f, sh * 0.085f, sw * 0.34f, sh * 0.05f), "重載安全規則", rs))
                { _gate.TryReload(out string rst); _status = rst; }
            }

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

        // 逐句記錄落檔(append-only,每行一筆 JSON)。失敗只記警告,絕不影響對話/安全流程。
        private void AppendLogLine(string line)
        {
            try { File.AppendAllText(_logPath, line + "\n"); }
            catch (Exception e) { Debug.LogWarning("[Counselor] 記錄寫檔失敗: " + e.Message); }
        }

        // 交接卡輸出:老師可讀文字摘要 + 機器讀 JSON,各一段、append。
        private void WriteCard(string kind, HandoffCard card)
        {
            try
            {
                string block = "==== " + kind + " @ " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ====\n"
                             + HandoffCardFormatter.ToTeacherText(card) + "\n--- JSON ---\n" + card.ToJson() + "\n\n";
                File.AppendAllText(_cardPath, block);
                Debug.Log("[Counselor] 交接卡已寫: " + _cardPath);
            }
            catch (Exception e) { Debug.LogWarning("[Counselor] 交接卡寫檔失敗: " + e.Message); }
        }

        private void OnDestroy()  // 返回選單(重載場景)時停止聆聽迴圈
        {
            _ready = false;
            try { if (Microphone.devices != null) foreach (var d in Microphone.devices) Microphone.End(d); } catch { }
        }
    }
}
#endif
