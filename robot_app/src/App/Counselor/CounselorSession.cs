// 輔導室會談協調器(全平台共用:不依賴 JSON/檔案/UnityEngine)。串 ISafetyGate/IConversationLog/INotifyHuman/
// IExplorationPlanner + 沿用 IKebbiBody/IVoice/ILlm。鐵律:每輪先過確定性安全閘,只有🟢才呼叫 LLM(Propose-vs-Gate)。
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App.Counselor
{
    public sealed class CounselorSession
    {
        private readonly IKebbiBody _body;   // 可空:Sim 不需要,Real 用來做表情/動作共情
        private readonly IVoice _voice;
        private readonly ILlm _llm;
        private readonly ISafetyGate _gate;
        private readonly IConversationLog _log;
        private readonly INotifyHuman _notify;
        private readonly IExplorationPlanner _planner;
        private readonly Action<string> _out;
        private readonly IEmpathyBody _empathy;   // 具身共情:body 非空(Real)才動作;Sim/主控台=null → no-op
        private readonly IFaceExpression _face;    // Air S 內建臉(可空):迎接/離開時切表情
        private PresenceCompanion _presence;       // PIR 存在感(可空):學生來/離開

        private string _student;
        private ConvMode _mode;
        private Layer _lastLayer = Layer.Green;
        public bool Ended { get; private set; }
        public HandoffCard LastCard { get; private set; }

        public const string GreenSystem =
            "你是輔導室陪伴機器人凱比,溫暖、穩、接得住。只在開放話題陪聊:單句、簡短、反映對方的情緒、鼓勵他多說一點。" +
            "不分析、不深挖、不給建議,只是好好聽。只用繁體中文(台灣)。";
        public const string RedHandoffLine = "這件事很重要,我想讓最能幫你的人來陪你談,我去請老師過來陪你,好嗎?";
        public const string YellowHandoffLine = "謝謝你願意跟我說。這個我幫你記下來,約輔導老師跟你好好談,好不好?";

        public CounselorSession(IKebbiBody body, IVoice voice, ILlm llm, ISafetyGate gate,
                                IConversationLog log, INotifyHuman notify, IExplorationPlanner planner,
                                Action<string> output = null, IFaceExpression face = null)
        {
            _body = body; _voice = voice; _llm = llm; _gate = gate; _log = log; _notify = notify; _planner = planner;
            _out = output ?? Console.WriteLine;
            _face = face;
            // body 或 face 任一存在就建共情層(Air S 主要靠臉,可無 body 單獨表情)。
            _empathy = (body != null || face != null) ? new MotorEmpathyBody(body, face: face) : null;
        }

        public const string GreetLine = "嗨,你來啦,我看到你了。";

        // 接 PIR 存在感:學生靠近 → 迎接(暖臉 + 招呼)、離開 → 降待命(中性臉)。Air S 用 PIR(非視覺)。
        public void WatchPresence(IPresenceSensor sensor)
        {
            if (sensor == null) return;
            _presence = new PresenceCompanion(sensor);
            _presence.OnArrived = OnStudentArrived;
            _presence.OnLeft = OnStudentLeft;
        }

        private void OnStudentArrived()
        {
            if (Ended) return;
            _log.Append(Speaker.Kebbi, _mode, GreetLine, Layer.Green, LogEvent.None);
            Speak(GreetLine, EmpathyMoment.Login);   // 暖臉 + 開場手勢
        }

        private void OnStudentLeft()
        {
            try { _face?.Show(FaceExpression.Neutral); } catch { }
            _out("   🤖 凱比:(待命中,我在這等你)");
        }

        // 登入 + 明確告知「對話會記錄並提供輔導老師」(規格第五、十三節)。第一筆 log Event=Login。
        public void Start(string student, ConvMode mode, string disclosure = null)
        {
            _student = student; _mode = mode; Ended = false;
            string line = string.IsNullOrEmpty(disclosure)
                ? "嗨,我是凱比。先跟你說一聲,我們等一下聊的內容我會記錄下來、提供給輔導老師。我們慢慢聊就好,你想說什麼都可以喔。"
                : disclosure;
            _log.Append(Speaker.Kebbi, _mode, line, Layer.Green, LogEvent.Login);
            Speak(line, EmpathyMoment.Login);
        }

        public void SwitchMode(ConvMode mode)
        {
            _mode = mode;
            _log.Append(Speaker.Kebbi, _mode, "(切換為" + (mode == ConvMode.Voice ? "有聲" : "無聲") + "模式)", _lastLayer, LogEvent.ModeSwitch);
        }

        // 處理一輪學生輸入。空字串 = 沉默 → 觸發探索。回傳該輪分層。
        public async Task<Layer> StepAsync(string studentText)
        {
            if (Ended) return Layer.Red;
            if (string.IsNullOrWhiteSpace(studentText)) { await ProbeAsync(); return _lastLayer; }

            try { _empathy?.FaceSpeaker(); } catch { }   // 學生開口 → 柔和轉頭面向他(眼神接觸;Sim=null→no-op)

            // ① 確定性安全閘先判(完全不進 LLM)
            GateResult g = _gate.Evaluate(studentText);
            _lastLayer = g.Layer;
            _log.Append(Speaker.Student, _mode, studentText, g.Layer, LogEvent.None);

            if (g.Layer == Layer.Red)   // 🔴 立即中止 + 呼叫真人 + 停探索
            {
                Ended = true;
                var card = BuildCard(SafetyFlag.High, studentText, g, SuggestedTier.Safety);
                LastCard = card;
                _notify.CallHumanNow(card);                                  // 即時呼叫現場真人(不只回文字)
                _log.Append(Speaker.Kebbi, _mode, RedHandoffLine, Layer.Red, LogEvent.RedEscalation);
                _log.Append(Speaker.Kebbi, _mode, "(已通知現場輔導老師)", Layer.Red, LogEvent.NotifySent);
                Speak(RedHandoffLine, EmpathyMoment.RedHandoff);
                return Layer.Red;
            }

            if (g.Layer == Layer.Yellow) // 🟡 不深挖 + 交接卡 + 停探索
            {
                var card = BuildCard(SafetyFlag.None, studentText, g, SuggestedTier.Intervention);
                LastCard = card;
                _notify.QueueYellowCard(card);
                _log.Append(Speaker.Kebbi, _mode, YellowHandoffLine, Layer.Yellow, LogEvent.YellowCard);
                Speak(YellowHandoffLine, EmpathyMoment.YellowHandoff);
                return Layer.Yellow;
            }

            // 🟢 才交給 LLM 生成陪聊回應(Propose);帶近幾輪脈絡讓陪聊連貫
            string reply;
            try { reply = (await _llm.AskAsync(GreenSystem, GreenContext(studentText)) ?? "").Trim(); }
            catch { reply = ""; }
            if (reply.Length == 0) reply = "嗯,我在聽,你想多說一點嗎?";
            _log.Append(Speaker.Kebbi, _mode, reply, Layer.Green, LogEvent.None);
            Speak(reply, EmpathyMoment.GreenChat);
            return Layer.Green;
        }

        // 沉默/話題停住 → 從未試過的話題挑一個拋題(只在🟢)。全試完用收尾語。
        public async Task ProbeAsync()
        {
            await Task.Yield();
            if (Ended || _lastLayer != Layer.Green) return;
            var t = _planner.NextTopic();
            string line = t != null ? t.OpenerLine : "沒關係,不一定要現在說,我都在這邊陪你。";
            _log.Append(Speaker.Kebbi, _mode, line, Layer.Green, LogEvent.None);
            Speak(line, EmpathyMoment.Probe);
        }

        // 會談結束 → 交接卡摘要給老師(即使全程🟢也給一份)。
        public HandoffCard End()
        {
            var card = LastCard ?? BuildCard(SafetyFlag.None, MainConcernFromLog(), null, SuggestedTier.Developmental);
            _notify.SendSessionSummary(card);
            return card;
        }

        private HandoffCard BuildCard(SafetyFlag flag, string concern, GateResult g, SuggestedTier tier)
        {
            return new HandoffCard
            {
                SafetyFlag = flag,
                Student = _student,
                Time = _log.GetTurns().Count > 0 ? _log.GetTurns()[_log.GetTurns().Count - 1].Timestamp : DateTime.Now,
                Mode = _mode,
                MainConcern = string.IsNullOrEmpty(concern) ? "(等待期間陪伴,無特定主訴)" : concern,
                Dimensions = Dimensions(g, flag),
                Tier = tier,
                EmotionState = flag == SafetyFlag.High ? "高度不安/危機訊號(觀察自語氣與用詞)"
                              : g != null && g.Layer == Layer.Yellow ? "情緒偏低或有壓力(觀察自用詞)" : "平穩",
                StudentWillingness = "(待輔導老師當面確認)",
                KeyPoints = KeyPoints(),
                LogLink = (_log as SimConversationLog)?.LogLink ?? _log.SessionId,
            };
        }

        private CounselingDimension[] Dimensions(GateResult g, SafetyFlag flag)
        {
            var set = new List<CounselingDimension>();
            if (flag == SafetyFlag.High) set.Add(CounselingDimension.Safety);
            string id = g?.MatchedRuleId ?? "";
            if (id == "family") set.Add(CounselingDimension.Family);
            else if (id == "interpersonal_relationship") set.Add(CounselingDimension.Interpersonal);
            else if (id == "long_term_low_mood") set.Add(CounselingDimension.Emotion);
            else if (id == "major_academic_decision") { set.Add(CounselingDimension.Career); set.Add(CounselingDimension.Learning); }
            else if (g != null && g.EscalatedByAmbiguity) set.Add(CounselingDimension.Emotion);
            if (set.Count == 0) set.Add(CounselingDimension.Living);   // 至少一個向度
            return set.ToArray();
        }

        private string[] KeyPoints()
        {
            var pts = new List<string>();
            foreach (var t in _log.GetTurns())
                if (t.Speaker == Speaker.Student && !string.IsNullOrWhiteSpace(t.Text)) pts.Add(t.Text);
            if (pts.Count == 0) pts.Add("(等待期間陪伴,學生尚未開口)");
            return pts.ToArray();
        }

        private string MainConcernFromLog()
        {
            foreach (var t in _log.GetTurns()) if (t.Speaker == Speaker.Student) return t.Text;
            return "";
        }

        // 給🟢開放聊的 user 內容:近幾輪對話脈絡 + 這句,讓真 LLM 陪聊連貫。
        private string GreenContext(string latest)
        {
            var turns = _log.GetTurns();
            var sb = new System.Text.StringBuilder();
            int start = turns.Count > 8 ? turns.Count - 8 : 0;
            for (int i = start; i < turns.Count; i++)
                if (!string.IsNullOrWhiteSpace(turns[i].Text))
                    sb.Append(turns[i].Speaker == Speaker.Student ? "學生:" : "凱比:").Append(turns[i].Text).Append('\n');
            sb.Append("學生:").Append(latest).Append("\n凱比:");
            return sb.ToString();
        }

        private void Speak(string text, EmpathyMoment moment)
        {
            _out("   🤖 凱比" + (_mode == ConvMode.Silent ? "(螢幕)" : "") + ": " + text);
            if (_mode == ConvMode.Voice && _voice != null) { try { _voice.SpeakAsync(text, "zh-TW"); } catch { } }
            try { _empathy?.Express(moment); } catch { }   // Real:依燈號/時機做點頭/前傾(免授權 setMotor);Sim/主控台 _empathy=null → no-op
        }
    }
}
