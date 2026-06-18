using System;
using System.Threading.Tasks;
using KebbiBrain.App;
using KebbiBrain.Hardware;
using KebbiBrain.Sim;

namespace KebbiBrain
{
    // 極簡自測（不依賴測試框架）：跑模擬器、斷言行為，回傳 0=全過 / 1=有失敗。
    public static class Tests
    {
        private static int _pass, _fail;

        public static int RunAll()
        {
            Console.WriteLine("========== 自我測試 ==========");
            T_AngleToDir();
            T_ParseIndo();
            T_HeadClampBack();
            T_Forward_Correct();
            T_Forward_WrongWord();
            T_Forward_BackReachability();
            T_Reverse_Correct();
            T_G3_MirrorCoach();
            T_G3_Session();
            T_RobotLink();
            T_RobotLinkProtocol();
            T_RemoteBody();
            T_RemoteVoice();
            T_RemoteVoiceDone();
            T_LinkAwaiter();
            T_NuwaMotorIds();
            T_Direction_Edges();
            T_HeadClamp_Edges();
            T_BodyCommand_Edges();
            T_G2_GeometryRelay();
            T_G2_Degrade();
            T_G5_Debate();
            T_G5_Score();
            T_G1_RelayQuest();
            T_G1_Obstacle();
            T_G1_Score();
            T_Finale();

            Console.WriteLine($"\n結果：{_pass} 通過 / {_fail} 失敗");
            Console.WriteLine("==============================");
            return _fail == 0 ? 0 : 1;
        }

        private static void Check(string name, bool cond)
        {
            if (cond) { _pass++; Console.WriteLine($"  ✅ {name}"); }
            else { _fail++; Console.WriteLine($"  ❌ {name}"); }
        }

        private static KebbiContext SilentSim(out SimKebbiBody body, out SimVoice voice)
        {
            Action<string> noop = _ => { };
            body = new SimKebbiBody(noop, canMove: false);
            voice = new SimVoice(noop);
            return new KebbiContext(body, voice, new SimLlm(noop), noop);
        }

        private static void T_AngleToDir()
        {
            Check("0°→Depan", Direction.FromAngle(0) == Dir.Depan);
            Check("90°→Kanan", Direction.FromAngle(90) == Dir.Kanan);
            Check("-90°→Kiri", Direction.FromAngle(-90) == Dir.Kiri);
            Check("170°→Belakang", Direction.FromAngle(170) == Dir.Belakang);
            Check("45°→Depan(邊界)", Direction.FromAngle(45) == Dir.Depan);
            Check("46°→Kanan(邊界)", Direction.FromAngle(46) == Dir.Kanan);
        }

        private static void T_ParseIndo()
        {
            Check("解析 'Saya di kanan'→Kanan", Direction.ParseIndo("Saya di kanan") == Dir.Kanan);
            Check("解析 'saya di BELAKANG'→Belakang", Direction.ParseIndo("saya di BELAKANG") == Dir.Belakang);
            Check("解析空字串→null", Direction.ParseIndo("") == null);
        }

        private static void T_HeadClampBack()
        {
            var ctx = SilentSim(out var body, out _);
            float faced = KebbiHead.TurnToward(body, 170f, out bool reachable);
            Check("正後方頭被夾限到 90°", Math.Abs(faced - 90f) < 0.01f);
            Check("正後方 reachable=false", reachable == false);
            float f2 = KebbiHead.TurnToward(body, 30f, out bool r2);
            Check("正前方 30° 可達", r2 && Math.Abs(f2 - 30f) < 0.01f);
        }

        private static TebakArahGame.RoundResult RunForward(Dir asked, float angle, string spoken, out TebakArahGame game)
        {
            var ctx = SilentSim(out var body, out var voice);
            game = new TebakArahGame(ctx);
            body.CurrentDoa = angle; voice.EnqueueHeard(spoken);
            return game.ForwardRoundAsync(asked).GetAwaiter().GetResult();
        }

        private static void T_Forward_Correct()
        {
            var r = RunForward(Dir.Kanan, 90, "Saya di kanan!", out var g);
            Check("正向-正確: LanguageCorrect", r.LanguageCorrect);
            Check("正向-正確: RightResponder", r.RightResponder);
            Check("正向-正確: 得分+1", g.Score == 1);
        }

        private static void T_Forward_WrongWord()
        {
            var r = RunForward(Dir.Kiri, -90, "Saya di kanan!", out var g);
            Check("正向-用錯詞: 真值=Kiri", r.Actual == Dir.Kiri);
            Check("正向-用錯詞: LanguageCorrect=false", !r.LanguageCorrect);
            Check("正向-用錯詞: 不得分", g.Score == 0);
        }

        private static void T_Forward_BackReachability()
        {
            var r = RunForward(Dir.Belakang, 170, "Saya di belakang!", out var g);
            Check("正向-後方: 語言正確", r.LanguageCorrect);
            Check("正向-後方: 頭無法完整面對(Faced=false)", !r.Faced);
            Check("正向-後方: 仍得分(語言對)", g.Score == 1);
        }

        private static void T_Reverse_Correct()
        {
            var ctx = SilentSim(out var body, out var voice);
            var g = new TebakArahGame(ctx);
            body.CurrentDoa = 0; voice.EnqueueHeard("Saya di sini!");
            g.CalibrateOneAsync("Citra").GetAwaiter().GetResult(); // depan
            voice.EnqueueHeard("Saya di depan.");
            var r = g.ReverseRoundAsync(g.FindByName("Citra")).GetAwaiter().GetResult();
            Check("反向-正確: LanguageCorrect", r.LanguageCorrect);
            Check("反向-正確: 得分", g.Score == 1);
        }

        private static void T_G3_MirrorCoach()
        {
            Action<string> noop = _ => { };
            var ctx = SilentSim(out var body, out _);
            var pose = new SimPoseSensor(noop);
            var game = new App.MirrorCoachGame(ctx, pose);
            var move = App.MirrorCoachGame.MakeWarmup();

            // 跑一拍、姿態正確 → 得分、末幀手臂歸位(0°)
            pose.Enqueue(true);
            game.RunRepAsync(move).GetAwaiter().GetResult();
            Check("G3-示範末幀 RShoulderY 歸位 0°", Math.Abs(body.GetMotor(KebbiMotor.RShoulderY) - 0f) < 0.01f);
            Check("G3-姿態正確得分", game.Score == 1);

            // 平舉幀有實際設角(中間幀 80°)：再跑一拍但只驗 play 有送角→改驗 LShoulder 對稱
            Check("G3-末幀 LShoulderY 也歸位", Math.Abs(body.GetMotor(KebbiMotor.LShoulderY) - 0f) < 0.01f);

            // 喊「太快了」(來自右邊 90°) → BPM 降低 + 頭轉向 90°
            int before = game.Bpm;
            body.CurrentDoa = 90f;
            bool reachable = game.HandleTooFastAsync(body.ReadDoaDegrees()).GetAwaiter().GetResult();
            Check("G3-太快→BPM 降低", game.Bpm < before);
            Check("G3-太快→頭轉向發問者(NeckZ≈90)", Math.Abs(body.GetMotor(KebbiMotor.NeckZ) - 90f) < 0.01f);
            Check("G3-右側 90° 可完整面向", reachable);

            // 姿態錯誤 → 不加分
            int s = game.Score;
            pose.Enqueue(false);
            game.RunRepAsync(move).GetAwaiter().GetResult();
            Check("G3-姿態錯誤不加分", game.Score == s);
        }

        // G3 多動作 session:逐組示範+計分、組間語音切換下一組(暖身/太極/CPR);可重入。
        private static void T_G3_Session()
        {
            Action<string> noop = _ => { };
            var routine = App.MirrorCoachGame.MakeDefaultRoutine();
            Check("G3 session-內建課表 3 組(暖身/太極/CPR)",
                routine.Count == 3 && routine[1].Name == "太極上肢起式" && routine[2].Name == "CPR 肘直手臂");

            var pose = new SimPoseSensor(noop);
            var game = new App.MirrorCoachGame(SilentSim(out _, out _), pose);
            pose.Enqueue(true); pose.Enqueue(true); pose.Enqueue(true);
            game.RunSessionAsync(routine).GetAwaiter().GetResult();
            Check("G3 session-跑完 3 組(Reps=3)", game.Reps == 3);
            Check("G3 session-全標準得 3 分", game.Score == 3);

            // 一組姿態錯 → 3 拍得 2 分;同實例再跑一場驗可重入(計數歸零不累加)
            var pose2 = new SimPoseSensor(noop);
            var game2 = new App.MirrorCoachGame(SilentSim(out _, out _), pose2);
            pose2.Enqueue(true); pose2.Enqueue(false); pose2.Enqueue(true);
            game2.RunSessionAsync(App.MirrorCoachGame.MakeDefaultRoutine()).GetAwaiter().GetResult();
            Check("G3 session-一組錯 → 3 拍得 2 分", game2.Reps == 3 && game2.Score == 2);
            pose2.Enqueue(true); pose2.Enqueue(true); pose2.Enqueue(true);
            game2.RunSessionAsync(App.MirrorCoachGame.MakeDefaultRoutine()).GetAwaiter().GetResult();
            Check("G3 session-可重入:第二場計數歸零(Reps=3、Score=3 非累加)", game2.Reps == 3 && game2.Score == 3);
        }

        private static void T_RobotLink()
        {
            Action<string> noop = _ => { };
            var bus = new SimRobotBus(noop);
            var a = bus.CreateLink("A");
            var b = bus.CreateLink("B");
            var c = bus.CreateLink("C");

            // 點對點送達
            string bGot = null;
            b.OnMessage((f, t) => bGot = f + "|" + t);
            a.SendAsync("B", "hi").GetAwaiter().GetResult();
            Check("多機-點對點送達 B", bGot == "A|hi");

            // 廣播到 B、C，不回送自己 A
            var got = new System.Collections.Generic.List<string>();
            int aCount = 0;
            a.OnMessage((f, t) => aCount++);
            b.OnMessage((f, t) => got.Add("B"));
            c.OnMessage((f, t) => got.Add("C"));
            a.BroadcastAsync("yo").GetAwaiter().GetResult();
            Check("多機-廣播到 B 與 C", got.Contains("B") && got.Contains("C"));
            Check("多機-廣播不回送自己", aCount == 0);

            // 交棒握手：A 送 HANDOFF → B 自動回 ACK → A 收到
            bool acked = false;
            b.OnMessage((f, t) => { if (t.StartsWith("HANDOFF")) b.SendAsync(f, "ACK"); });
            a.OnMessage((f, t) => { if (t == "ACK") acked = true; });
            a.SendAsync("B", "HANDOFF#1").GetAwaiter().GetResult();
            Check("多機-交棒握手 A 收到 ACK", acked);
        }

        // 實機 UDP 收送的「邏輯部分」(框架/解析/收件判斷)。真正剩下未驗的只有 UDP 傳輸(必測④)。
        private static void T_RobotLinkProtocol()
        {
            // 往返:Frame → TryParse 還原 from/to/text(含中文與空白)
            byte[] p = RobotLinkProtocol.Frame("A機", "B機", "HANDOFF#1 關卡完成");
            bool ok = RobotLinkProtocol.TryParse(p, p.Length, out var f, out var t, out var x);
            Check("協定-往返解析成功", ok);
            Check("協定-from 還原", f == "A機");
            Check("協定-to 還原", t == "B機");
            Check("協定-text 還原(含空白/中文)", x == "HANDOFF#1 關卡完成");

            // 空 text 仍可解析(末欄位保留)
            byte[] pe = RobotLinkProtocol.Frame("A", "B", "");
            Check("協定-空 text 可解析", RobotLinkProtocol.TryParse(pe, pe.Length, out _, out _, out var xe) && xe == "");

            // 壞封包(無分隔)→ 解析失敗,不丟例外
            Check("協定-壞封包解析失敗", !RobotLinkProtocol.TryParse("garbage-no-sep", out _, out _, out _));

            // 收件判斷
            Check("協定-點對點給我→收", RobotLinkProtocol.ShouldDeliver("A", "B", "B"));
            Check("協定-廣播→收", RobotLinkProtocol.ShouldDeliver("A", RobotLinkProtocol.All, "B"));
            Check("協定-非給我→丟", !RobotLinkProtocol.ShouldDeliver("A", "C", "B"));
            Check("協定-自己廣播回來→丟", !RobotLinkProtocol.ShouldDeliver("B", RobotLinkProtocol.All, "B"));
        }

        // 記錄式機身:給遠端命令測試斷言(SimKebbiBody 的 Move/Turn 只 log,無狀態可驗)。
        private sealed class RecordingBody : IKebbiBody
        {
            public readonly System.Collections.Generic.Dictionary<KebbiMotor, float> Motors
                = new System.Collections.Generic.Dictionary<KebbiMotor, float>();
            public float LastMove = float.NaN, LastTurn = float.NaN, LastSpeed = float.NaN;
            public bool Stopped;
            public void SetMotor(KebbiMotor m, float d, float s = 50f) { Motors[m] = d; LastSpeed = s; }
            public float GetMotor(KebbiMotor m) => Motors.TryGetValue(m, out var v) ? v : 0f;
            public float ReadDoaDegrees() => 0f;
            public bool CanMove => true;
            public void Move(float mps) { LastMove = mps; }
            public void Turn(float dps) { LastTurn = dps; }
            public void StopWheels() { Stopped = true; }
            public float NeckZMinDeg => -90f;
            public float NeckZMaxDeg => 90f;
        }

        // 記錄式語音:給遠端語音命令測試斷言(SimVoice 的 Speak 只 log,無狀態可驗)。
        // 記成 List 以驗「順序」;另留 LastText/LastLang 便利欄位與可預排的 ListenAsync 佇列(供後續 G4 誤接情境)。
        private sealed class RecordingVoice : IVoice
        {
            public readonly System.Collections.Generic.List<(string text, string lang)> Spoken
                = new System.Collections.Generic.List<(string text, string lang)>();
            public string LastText => Spoken.Count > 0 ? Spoken[Spoken.Count - 1].text : null;
            public string LastLang => Spoken.Count > 0 ? Spoken[Spoken.Count - 1].lang : null;
            private readonly System.Collections.Generic.Queue<string> _heard = new System.Collections.Generic.Queue<string>();
            public void EnqueueHeard(string t) => _heard.Enqueue(t);
            public int SpeakDelayMs = 0; // >0 → 模擬真機 TTS 播放耗時:延遲後才記錄(代表「播畢」)
            public Task SpeakAsync(string text, string lang = "id-ID")
            {
                if (SpeakDelayMs <= 0) { Spoken.Add((text, lang)); return Task.CompletedTask; }
                return DelayedSpeakAsync(text, lang);
            }
            private async Task DelayedSpeakAsync(string text, string lang)
            { await Task.Delay(SpeakDelayMs); Spoken.Add((text, lang)); }
            public Task<string> ListenAsync(string lang = "id-ID")
            { return Task.FromResult(_heard.Count > 0 ? _heard.Dequeue() : ""); }
        }

        // 遠端機身控制:中控用 RemoteBodyProxy 經 link 驅動被控機(BodyCommandReceiver 套用到本機 body)。
        // 這是「真機多台分散式跑同一套劇本」的關鍵層,純 C# 可在此驗證(實機只剩 UDP 傳輸,必測④)。
        private static void T_RemoteBody()
        {
            Action<string> noop = _ => { };
            var bus = new SimRobotBus(noop);
            var dir = bus.CreateLink("DIR");
            var dev = bus.CreateLink("DEV");
            var devBody = new RecordingBody();
            new BodyCommandReceiver(dev, devBody);
            var proxy = new RemoteBodyProxy(dir, "DEV", canMove: true);

            proxy.SetMotor(KebbiMotor.RShoulderY, 70f);
            Check("遠端-SetMotor 套到被控機", Math.Abs(devBody.GetMotor(KebbiMotor.RShoulderY) - 70f) < 0.01f);
            Check("遠端-中控可讀回下達值", Math.Abs(proxy.GetMotor(KebbiMotor.RShoulderY) - 70f) < 0.01f);

            proxy.Move(0.2f);
            Check("遠端-Move 套到被控機", Math.Abs(devBody.LastMove - 0.2f) < 0.01f);
            proxy.Turn(30f);
            Check("遠端-Turn 套到被控機", Math.Abs(devBody.LastTurn - 30f) < 0.01f);
            proxy.StopWheels();
            Check("遠端-Stop 套到被控機", devBody.Stopped);

            // 非機身訊息(HANDOFF)不被當命令 → 轉交 alsoHandle(被控機仍能參與交棒)
            bool forwarded = false;
            var dev2 = bus.CreateLink("DEV2");
            new BodyCommandReceiver(dev2, new RecordingBody(),
                (f, t) => { if (t.StartsWith("HANDOFF")) forwarded = true; });
            dir.SendAsync("DEV2", "HANDOFF#1").GetAwaiter().GetResult();
            Check("遠端-非命令訊息轉交 alsoHandle", forwarded);

            // 負角度/小數命令往返(InvariantCulture)
            var spy = new RecordingBody();
            Check("遠端-負角度小數命令套用",
                BodyCommand.TryApply(BodyCommand.SetMotor(KebbiMotor.NeckZ, -45.5f, 50f), spy)
                && Math.Abs(spy.GetMotor(KebbiMotor.NeckZ) + 45.5f) < 0.01f);
            Check("遠端-壞命令不套用(回 false)", !BodyCommand.TryApply("NOTBC|x", spy));
        }

        // 遠端語音控制:中控用 RemoteVoiceProxy 經 link 讓被控機「用自己的喇叭」說台詞(VC|SAY 命令)。
        // 這是 G5 辯方/G2 乙機「對方機自己開口」的關鍵層。涵蓋:序列化邊角(含 '|'/空台詞/lang=null/lang 含 '|')、
        // 壞命令不丟例外、兩層編碼往返、順序保證、BC/VC/一般訊息混流零污染、handler 覆寫整合。純 C# 可驗(實機 UDP 必測④)。
        private static void T_RemoteVoice()
        {
            Action<string> noop = _ => { };
            var bus = new SimRobotBus(noop);
            var dir = bus.CreateLink("DIR");
            var dev = bus.CreateLink("DEV");
            var devVoice = new RecordingVoice();
            new BodyCommandReceiver(dev, new RecordingBody(), null, devVoice);
            var proxy = new RemoteVoiceProxy(dir, "DEV", noop);

            // 基本:SAY 套到被控機 voice、lang 保留
            proxy.SpeakAsync("控方陳述", "zh-TW").GetAwaiter().GetResult();
            Check("遠端語音-SAY 套到被控機 voice", devVoice.LastText == "控方陳述");
            Check("遠端語音-lang 保留", devVoice.LastLang == "zh-TW");

            // text 含 '|' 不被截斷(限數量切割保最後一欄)
            proxy.SpeakAsync("a|b|c", "zh-TW").GetAwaiter().GetResult();
            Check("遠端語音-text 含 '|' 不截斷", devVoice.LastText == "a|b|c");

            // 空台詞 round-trip:TryApply 回 true、收到空字串(對齊 RobotLinkProtocol 空 text 可解析)
            Check("遠端語音-空台詞命令成立(回 true、收空字串)",
                VoiceCommand.TryApply(VoiceCommand.Speak("", "zh-TW"), devVoice) && devVoice.LastText == "");

            // lang=null 正規化:預設值不過 wire → 兩端回填 id-ID
            proxy.SpeakAsync("halo", null).GetAwaiter().GetResult();
            Check("遠端語音-lang=null 正規化為 id-ID", devVoice.LastLang == "id-ID");

            // 長中文台詞「兩層編碼」(VoiceCommand + RobotLinkProtocol)往返逐字相等
            string longLine = "辯方：望遠鏡觀測到木星有四顆衛星繞行，顯示並非萬物皆繞地球；慣性使我們感覺不到等速運動。";
            byte[] frame = RobotLinkProtocol.Frame("DIR", "DEV", VoiceCommand.Speak(longLine, "zh-TW"));
            RobotLinkProtocol.TryParse(frame, frame.Length, out _, out _, out var inner);
            Check("遠端語音-長中文台詞兩層編碼往返",
                VoiceCommand.TryApply(inner, devVoice) && devVoice.LastText == longLine);

            // 壞命令/邊界:缺欄位/未知子命令/空字串/voice==null/非 VC → 一律回 false 且不丟例外(守 IndexOutOfRange)
            Check("遠端語音-VC|SAY 缺欄位回 false", !VoiceCommand.TryApply("VC|SAY", devVoice));
            Check("遠端語音-VC|SAY|zh-TW 缺 text 回 false", !VoiceCommand.TryApply("VC|SAY|zh-TW", devVoice));
            Check("遠端語音-未知子命令 VC|FOO 回 false", !VoiceCommand.TryApply("VC|FOO|zh-TW|x", devVoice));
            Check("遠端語音-空字串回 false", !VoiceCommand.TryApply("", devVoice));
            Check("遠端語音-voice==null 回 false", !VoiceCommand.TryApply("VC|SAY|zh-TW|x", null));
            Check("遠端語音-非 VC(BC 機身命令)回 false", !VoiceCommand.TryApply(BodyCommand.Move(0.1f), devVoice));

            // lang 含分隔符防呆:Speak fail-fast 拋 ArgumentException
            bool threw = false;
            try { VoiceCommand.Speak("x", "zh|TW"); } catch (ArgumentException) { threw = true; }
            Check("遠端語音-lang 含 '|' 拋 ArgumentException", threw);

            // ListenAsync 不支援遠端聽 → 回空字串
            Check("遠端語音-ListenAsync 回空字串", proxy.ListenAsync().GetAwaiter().GetResult() == "");

            // 順序保證:Speak A、Speak B、SetMotor 三連發 → voice 依序記、body 也套到(Sim 同步遞送)
            var dev2Voice = new RecordingVoice();
            var dev2Body = new RecordingBody();
            var dev2 = bus.CreateLink("DEV2");
            new BodyCommandReceiver(dev2, dev2Body, null, dev2Voice);
            var pv2 = new RemoteVoiceProxy(dir, "DEV2", noop);
            var pb2 = new RemoteBodyProxy(dir, "DEV2", canMove: true);
            pv2.SpeakAsync("第一句", "zh-TW").GetAwaiter().GetResult();
            pv2.SpeakAsync("第二句", "zh-TW").GetAwaiter().GetResult();
            pb2.SetMotor(KebbiMotor.RShoulderY, 70f);
            Check("遠端語音-順序:第一句先到", dev2Voice.Spoken.Count == 2 && dev2Voice.Spoken[0].text == "第一句");
            Check("遠端語音-順序:第二句後到", dev2Voice.Spoken[1].text == "第二句");
            Check("遠端語音-順序:機身命令也套到", Math.Abs(dev2Body.GetMotor(KebbiMotor.RShoulderY) - 70f) < 0.01f);

            // 混流零污染:BC|SM + VC|SAY + HANDOFF 三種混送 → body/voice/alsoHandle 各收各的、零交叉污染
            var mBody = new RecordingBody();
            var mVoice = new RecordingVoice();
            string also = null;
            var mLink = bus.CreateLink("MIX");
            new BodyCommandReceiver(mLink, mBody, (f, t) => also = t, mVoice);
            dir.SendAsync("MIX", BodyCommand.SetMotor(KebbiMotor.NeckZ, 30f, 50f)).GetAwaiter().GetResult();
            dir.SendAsync("MIX", VoiceCommand.Speak("台詞", "zh-TW")).GetAwaiter().GetResult();
            dir.SendAsync("MIX", "HANDOFF#1").GetAwaiter().GetResult();
            Check("遠端語音-混流:機身命令只進 body", Math.Abs(mBody.GetMotor(KebbiMotor.NeckZ) - 30f) < 0.01f);
            Check("遠端語音-混流:語音命令只進 voice", mVoice.LastText == "台詞");
            Check("遠端語音-混流:一般訊息只進 alsoHandle", also == "HANDOFF#1");

            // handler 覆寫整合:被控 link 同時掛 BodyCommandReceiver(localVoice)+遊戲交棒(alsoHandle)→ 互不吞
            var iVoice = new RecordingVoice();
            bool yourTurn = false;
            var iLink = bus.CreateLink("INTG");
            new BodyCommandReceiver(iLink, new RecordingBody(),
                (f, t) => { if (t == "YOUR_TURN") yourTurn = true; }, iVoice);
            dir.SendAsync("INTG", VoiceCommand.Speak("辯方反駁", "zh-TW")).GetAwaiter().GetResult();
            dir.SendAsync("INTG", "YOUR_TURN").GetAwaiter().GetResult();
            Check("遠端語音-整合:台詞套到 voice", iVoice.LastText == "辯方反駁");
            Check("遠端語音-整合:交棒訊息仍進 alsoHandle(未被誤吞)", yourTurn);
        }

        // 遠端語音「說完才交棒」握手:被控機 ackVoiceDone 播畢回 VC|DONE,中控 RemoteVoiceProxy 等播畢模式 await 到才返回。
        // 用「延遲語音」模擬真機 TTS 播放耗時,證明 proxy 真的等到播畢(非 fire-and-forget),且逾時不卡死。純 C# 可驗(實機 UDP 必測④)。
        private static void T_RemoteVoiceDone()
        {
            Action<string> noop = _ => { };

            // VC|DONE 線格式往返
            Check("握手-Done()/IsDone() 往返", VoiceCommand.IsDone(VoiceCommand.Done()) && !VoiceCommand.IsDone("VC|SAY|zh-TW|x"));
            // TryParseSay 取出 lang/text(含 '|' 的 text 保留)
            Check("握手-TryParseSay 取出 lang/text(含 '|')",
                VoiceCommand.TryParseSay("VC|SAY|zh-TW|a|b", out var pl, out var px) && pl == "zh-TW" && px == "a|b");
            Check("握手-TryParseSay 非 SAY 回 false", !VoiceCommand.TryParseSay("VC|DONE", out _, out _));

            // (1) 等播畢:被控機「延遲 30ms 播放」後回 DONE → proxy await 到才返回 → 返回時 Spoken 已記錄(證明真的等了)。
            //     此案亦涵蓋「DONE 非同步遲到」:DONE 是在 Task.Delay(30) 的 thread-pool 續接上才送回,proxy 端 await 須正確處理
            //     非同步抵達(非 Sim 同步遞送);doneTimeoutMs:1000 ≫ 30ms,故是「等到 DONE」而非「靠逾時」。
            var bus = new SimRobotBus(noop);
            var dirLink = bus.CreateLink("中控");
            var devLink = bus.CreateLink("被控");
            var slowVoice = new RecordingVoice { SpeakDelayMs = 30 };
            new BodyCommandReceiver(devLink, new RecordingBody(), null, slowVoice, ackVoiceDone: true);
            var awaiter = new LinkAwaiter(dirLink);
            var proxy = new RemoteVoiceProxy(dirLink, "被控", noop, awaiter, doneTimeoutMs: 1000);
            proxy.SpeakAsync("辯方台詞", "zh-TW").GetAwaiter().GetResult();
            Check("握手-等播畢:proxy 返回時被控機已說完(等到 VC|DONE)", slowVoice.LastText == "辯方台詞");

            // (2) 對照 fire-and-forget(無 awaiter):送出即返回,被控機延遲播放尚未說完(LastText 還沒記錄)
            var bus2 = new SimRobotBus(noop);
            var dirLink2 = bus2.CreateLink("中控2");
            var devLink2 = bus2.CreateLink("被控2");
            var slow2 = new RecordingVoice { SpeakDelayMs = 50 };
            new BodyCommandReceiver(devLink2, new RecordingBody(), null, slow2); // ackVoiceDone 預設 false
            var ff = new RemoteVoiceProxy(dirLink2, "被控2", noop);              // 無 awaiter → fire-and-forget
            ff.SpeakAsync("x", "zh-TW").GetAwaiter().GetResult();
            Check("握手-對照:fire-and-forget 不等播畢(返回時尚未說完)", slow2.LastText == null);

            // (3) 逾時不卡死:等播畢模式但被控機不回 DONE(ackVoiceDone:false) → proxy 短逾時後仍返回(不 hang)
            var bus3 = new SimRobotBus(noop);
            var dirLink3 = bus3.CreateLink("中控3");
            var devLink3 = bus3.CreateLink("被控3");
            new BodyCommandReceiver(devLink3, new RecordingBody(), null, new RecordingVoice()); // 不 ack
            var awaiter3 = new LinkAwaiter(dirLink3);
            var proxy3 = new RemoteVoiceProxy(dirLink3, "被控3", noop, awaiter3, doneTimeoutMs: 30);
            bool returned = false;
            proxy3.SpeakAsync("無人回 DONE", "zh-TW").GetAwaiter().GetResult();
            returned = true;
            Check("握手-逾時不卡死:被控機不回 DONE 也能返回", returned);
        }

        // LinkAwaiter:在 IRobotLink 上「送出→await 等符合條件的回覆、帶逾時」。多機編排(FinaleShowGame)的真機正確性基礎。
        private static void T_LinkAwaiter()
        {
            Action<string> noop = _ => { };
            var bus = new SimRobotBus(noop);
            var a = bus.CreateLink("A");
            var b = bus.CreateLink("B");
            var awaiter = new LinkAwaiter(a); // a 的訊息由 awaiter 處理

            // 同步命中:先註冊等待者,B 立即回 → await 取得回覆內容
            var t1 = awaiter.WaitForAsync((f, x) => f == "B" && x.StartsWith("PONG"), 1000);
            b.OnMessage((f, x) => { if (x == "PING") b.SendAsync(f, "PONG-1"); });
            a.SendAsync("B", "PING").GetAwaiter().GetResult();
            Check("LinkAwaiter-同步命中取得回覆", t1.GetAwaiter().GetResult() == "PONG-1");

            // 逾時:沒人回 → 短逾時後回 null
            var t2 = awaiter.WaitForAsync((f, x) => x == "NEVER", 30);
            Check("LinkAwaiter-逾時回 null", t2.GetAwaiter().GetResult() == null);

            // from 比對:只接受指定來源(C 回的不算 → 逾時 null)
            var c = bus.CreateLink("C");
            var t3 = awaiter.WaitForAsync((f, x) => f == "B" && x == "HIT", 30);
            c.SendAsync("A", "HIT").GetAwaiter().GetResult(); // 來自 C,from 不符
            Check("LinkAwaiter-from 不符不命中(逾時 null)", t3.GetAwaiter().GetResult() == null);

            // 多等待者並存:兩個不同 predicate 同時等,各自被對應訊息命中
            b.OnMessage((f, x) => { }); // 清掉 B 的 PING handler 避免干擾
            var tx = awaiter.WaitForAsync((f, x) => x == "X", 1000);
            var ty = awaiter.WaitForAsync((f, x) => x == "Y", 1000);
            b.SendAsync("A", "Y").GetAwaiter().GetResult();
            b.SendAsync("A", "X").GetAwaiter().GetResult();
            Check("LinkAwaiter-多等待者各自命中(Y)", ty.GetAwaiter().GetResult() == "Y");
            Check("LinkAwaiter-多等待者各自命中(X)", tx.GetAwaiter().GetResult() == "X");

            // 非同步回覆:延遲後才回 → await 仍正確等到(真機 UDP 非同步代理)
            var t4 = awaiter.WaitForAsync((f, x) => x == "LATE", 1000);
            _ = Task.Run(async () => { await Task.Delay(20); await b.SendAsync("A", "LATE"); });
            Check("LinkAwaiter-非同步延遲回覆 await 等到", t4.GetAwaiter().GetResult() == "LATE");
        }

        // 釘住:KebbiMotor enum 值 == 真 NuwaSDK 2.1.0.08 aar 的 MOTOR_* 常數(用參考專案 UnityKebbi 內附 aar 以 javap 反查,2026-06-18)。
        // real 後端 UnityKebbiBody 直接拿 (int)KebbiMotor 當馬達 ID 呼叫 ctlMotor/getMotorPresentPositionInDegree;
        // 若有人改了 enum 順序/值,實機馬達就會控錯顆 → 此純 C# 測試在主控台就攔下(實機只剩角度/速度語意待必測⑥)。
        private static void T_NuwaMotorIds()
        {
            Check("Nuwa馬達ID-NeckY=1(MOTOR_NECK_Y)", (int)KebbiMotor.NeckY == 1);
            Check("Nuwa馬達ID-NeckZ=2(MOTOR_NECK_Z)", (int)KebbiMotor.NeckZ == 2);
            Check("Nuwa馬達ID-RShoulderZ=3(MOTOR_RIGHT_SHOULDER_Z)", (int)KebbiMotor.RShoulderZ == 3);
            Check("Nuwa馬達ID-RShoulderY=4(MOTOR_RIGHT_SHOULDER_Y)", (int)KebbiMotor.RShoulderY == 4);
            Check("Nuwa馬達ID-RShoulderX=5(MOTOR_RIGHT_SHOULDER_X)", (int)KebbiMotor.RShoulderX == 5);
            Check("Nuwa馬達ID-RElbowY=6(MOTOR_RIGHT_ELBOW_Y)", (int)KebbiMotor.RElbowY == 6);
            Check("Nuwa馬達ID-LShoulderZ=7(MOTOR_LEFT_SHOULDER_Z)", (int)KebbiMotor.LShoulderZ == 7);
            Check("Nuwa馬達ID-LShoulderY=8(MOTOR_LEFT_SHOULDER_Y)", (int)KebbiMotor.LShoulderY == 8);
            Check("Nuwa馬達ID-LShoulderX=9(MOTOR_LEFT_SHOULDER_X)", (int)KebbiMotor.LShoulderX == 9);
            Check("Nuwa馬達ID-LElbowY=10(MOTOR_LEFT_ELBOW_Y)", (int)KebbiMotor.LElbowY == 10);
        }

        // 方位扇區邊界 + 角度正規化 + 印尼語詞往返(把 G4 的方位判定逼到邊角)。
        private static void T_Direction_Edges()
        {
            Check("135°→Kanan(邊界含)", Direction.FromAngle(135) == Dir.Kanan);
            Check("136°→Belakang", Direction.FromAngle(136) == Dir.Belakang);
            Check("-135°→Belakang(邊界)", Direction.FromAngle(-135) == Dir.Belakang);
            Check("-134°→Kiri", Direction.FromAngle(-134) == Dir.Kiri);
            Check("180°→Belakang", Direction.FromAngle(180) == Dir.Belakang);
            Check("-180°→Belakang", Direction.FromAngle(-180) == Dir.Belakang);
            Check("-45°→Kiri(邊界)", Direction.FromAngle(-45) == Dir.Kiri);
            Check("正規化 270°→Kiri", Direction.FromAngle(270) == Dir.Kiri);
            Check("正規化 -270°→Kanan", Direction.FromAngle(-270) == Dir.Kanan);
            Check("正規化 360°→Depan", Direction.FromAngle(360) == Dir.Depan);
            foreach (Dir d in new[] { Dir.Depan, Dir.Kanan, Dir.Belakang, Dir.Kiri })
                Check("印尼語詞往返還原 " + d, Direction.ParseIndo(Direction.ToIndo(d)) == d);
        }

        // 轉頭夾限邊界(正後/左後/恰好邊界/超界正規化)。
        private static void T_HeadClamp_Edges()
        {
            var ctx = SilentSim(out var body, out _);
            float f1 = KebbiHead.TurnToward(body, -170f, out bool r1);
            Check("左後 -170°→夾到 -90", Math.Abs(f1 + 90f) < 0.01f);
            Check("左後 -170° 不可達", !r1);
            float f2 = KebbiHead.TurnToward(body, 90f, out bool r2);
            Check("正右 90° 可達(邊界含)", r2 && Math.Abs(f2 - 90f) < 0.01f);
            float f3 = KebbiHead.TurnToward(body, -90f, out bool r3);
            Check("正左 -90° 可達(邊界含)", r3 && Math.Abs(f3 + 90f) < 0.01f);
            float f4 = KebbiHead.TurnToward(body, 91f, out bool r4);
            Check("91°→夾到 90、不可達", !r4 && Math.Abs(f4 - 90f) < 0.01f);
            float f5 = KebbiHead.TurnToward(body, 200f, out bool r5); // 正規化→-160→夾 -90
            Check("200°(正規化-160)→夾 -90、不可達", !r5 && Math.Abs(f5 + 90f) < 0.01f);
            Check("TurnToward 有寫入 NeckZ", Math.Abs(body.GetMotor(KebbiMotor.NeckZ) + 90f) < 0.01f);
        }

        // 機身命令邊角:負值(反向)、速度參數保留、Stop、壞/不足命令不丟例外。
        private static void T_BodyCommand_Edges()
        {
            var spy = new RecordingBody();
            Check("命令-反向 Move(負值)往返",
                BodyCommand.TryApply(BodyCommand.Move(-0.15f), spy) && Math.Abs(spy.LastMove + 0.15f) < 0.01f);
            Check("命令-反向 Turn(負值)往返",
                BodyCommand.TryApply(BodyCommand.Turn(-30f), spy) && Math.Abs(spy.LastTurn + 30f) < 0.01f);
            Check("命令-SetMotor 速度參數保留",
                BodyCommand.TryApply(BodyCommand.SetMotor(KebbiMotor.NeckZ, 10f, 30f), spy) && Math.Abs(spy.LastSpeed - 30f) < 0.01f);
            Check("命令-Stop 套用", BodyCommand.TryApply(BodyCommand.Stop(), spy) && spy.Stopped);
            Check("命令-SM 欄位不足→false(不丟例外)", !BodyCommand.TryApply("BC|SM|2", spy));
            Check("命令-空字串→false", !BodyCommand.TryApply("", spy));
        }

        private static void T_G2_GeometryRelay()
        {
            Action<string> noop = _ => { };
            var ctx = SilentSim(out var guideBody, out var voice);
            var bus = new SimRobotBus(noop);
            var gLink = bus.CreateLink("甲機");
            var rLink = bus.CreateLink("乙機");
            var game = new App.GeometryRelayGame(guideBody, gLink, rLink, voice, noop);
            var proof = App.GeometryRelayGame.MakeIsoscelesProof();

            game.RunProofAsync(proof).GetAwaiter().GetResult();
            Check("G2-完成全部 3 步接力", game.StepsDone == 3);
            Check("G2-甲機手臂停在末步角度 80°", Math.Abs(guideBody.GetMotor(KebbiMotor.RShoulderY) - 80f) < 0.01f);
        }

        // G2 接 LinkAwaiter 後的「甲機逾時降級」與「可重入」(送 POINT→await DONE 帶逾時,真機 UDP 也正確)。
        private static void T_G2_Degrade()
        {
            Action<string> noop = _ => { };
            // 甲機離線:乙機在 bus1、甲機在「另一條 bus」→ POINT 送不到甲機 → 乙機逾時降級(短逾時)。
            var bus1 = new SimRobotBus(noop);
            var bus2 = new SimRobotBus(noop);
            var offline = new App.GeometryRelayGame(new SimKebbiBody(noop, true),
                bus2.CreateLink("甲機"), bus1.CreateLink("乙機"), new SimVoice(noop), noop, doneTimeoutMs: 40);
            offline.RunProofAsync(App.GeometryRelayGame.MakeIsoscelesProof()).GetAwaiter().GetResult();
            Check("G2降級-甲機離線:3 步全降級(StepsSkipped=3)", offline.StepsSkipped == 3);
            Check("G2降級-甲機離線:完成 0 步、不卡死", offline.StepsDone == 0);

            // 可重入:同實例(同 bus,甲機在線)連跑兩場 → 第二場仍 3 步、計數歸零不累加
            var bus = new SimRobotBus(noop);
            var g = new App.GeometryRelayGame(new SimKebbiBody(noop, true),
                bus.CreateLink("甲機"), bus.CreateLink("乙機"), new SimVoice(noop), noop, doneTimeoutMs: 40);
            g.RunProofAsync(App.GeometryRelayGame.MakeIsoscelesProof()).GetAwaiter().GetResult();
            g.RunProofAsync(App.GeometryRelayGame.MakeIsoscelesProof()).GetAwaiter().GetResult();
            Check("G2降級-可重入:第二場仍 3 步、計數歸零不累加", g.StepsDone == 3 && g.StepsSkipped == 0);
        }

        private static void T_G5_Debate()
        {
            Action<string> noop = _ => { };
            var proBody = new SimKebbiBody(noop, canMove: true);
            var defBody = new SimKebbiBody(noop, canMove: true);
            var bus = new SimRobotBus(noop);
            var game = new App.DebateGame(
                proBody, bus.CreateLink("控方"), new SimVoice(noop),
                defBody, bus.CreateLink("辯方"), new SimVoice(noop), noop);

            game.RunExchangeAsync("控方論點", "辯方反駁").GetAwaiter().GetResult();
            Check("G5-一回合交棒接力完成", game.Exchanges == 1);
            Check("G5-控方伸臂指控(RShoulderY=70)", Math.Abs(proBody.GetMotor(KebbiMotor.RShoulderY) - 70f) < 0.01f);
            Check("G5-辯方攤手反駁(RShoulderY=70)", Math.Abs(defBody.GetMotor(KebbiMotor.RShoulderY) - 70f) < 0.01f);

            game.ApproachCenterAsync().GetAwaiter().GetResult();
            Check("G5-中央逼近計數+1", game.CenterApproaches == 1);

            // 學生在控方右側 45° 發言 → 控方頭轉向(NeckZ≈45)
            bool faced = game.TurnToStudentAsync(true, 45f).GetAwaiter().GetResult();
            Check("G5-轉向發言學生(NeckZ≈45)", Math.Abs(proBody.GetMotor(KebbiMotor.NeckZ) - 45f) < 0.01f);
            Check("G5-45° 可完整面向", faced);
        }

        // G5 結辯投票計分:逐回合計票 → 宣判勝方、勝方結辯舉手;平手不舉;可重入。(G5 原本是全專案唯一沒有 Score 的遊戲)
        private static void T_G5_Score()
        {
            Action<string> noop = _ => { };
            DebateGame NewGame(out SimKebbiBody pro, out SimKebbiBody def)
            {
                pro = new SimKebbiBody(noop, true); def = new SimKebbiBody(noop, true);
                var bus = new SimRobotBus(noop);
                return new App.DebateGame(pro, bus.CreateLink("控方"), new SimVoice(noop),
                    def, bus.CreateLink("辯方"), new SimVoice(noop), noop);
            }

            // 整場伽利略辯論(辯方票多 控2:辯5)→ 辯方勝、辯方結辯舉手到 100
            var g = NewGame(out _, out var def);
            g.RunDebateAsync(App.DebateGame.MakeGalileoDebate()).GetAwaiter().GetResult();
            Check("G5計分-票數累計(控2:辯5)", g.ProVotes == 2 && g.DefVotes == 5);
            Check("G5計分-宣判辯方勝", g.Verdict == "辯方勝" && g.Concluded);
            Check("G5計分-辯方結辯舉手(RShoulderY=100)", System.Math.Abs(def.GetMotor(KebbiMotor.RShoulderY) - 100f) < 0.01f);

            // 平手:兩回合各打平 → 平手、雙方都不舉到 100
            var t = NewGame(out var pro2, out var def2);
            t.RunDebateAsync(new System.Collections.Generic.List<App.DebateGame.Exchange> {
                new App.DebateGame.Exchange("a", "b", 1, 1), new App.DebateGame.Exchange("c", "d", 2, 2),
            }).GetAwaiter().GetResult();
            Check("G5計分-平手(控3:辯3)", t.Verdict == "平手" && t.ProVotes == 3 && t.DefVotes == 3);
            Check("G5計分-平手雙方不舉到 100",
                System.Math.Abs(def2.GetMotor(KebbiMotor.RShoulderY) - 100f) > 0.01f &&
                System.Math.Abs(pro2.GetMotor(KebbiMotor.RShoulderY) - 100f) > 0.01f);

            // 控方勝:控方票多 → 控方勝、控方結辯舉手
            var p = NewGame(out var pro3, out _);
            p.RunDebateAsync(new System.Collections.Generic.List<App.DebateGame.Exchange> {
                new App.DebateGame.Exchange("a", "b", 3, 0),
            }).GetAwaiter().GetResult();
            Check("G5計分-控方勝且控方結辯舉手", p.Verdict == "控方勝" && System.Math.Abs(pro3.GetMotor(KebbiMotor.RShoulderY) - 100f) < 0.01f);

            // 可重入:同實例跑兩場伽利略 → 第二場計數歸零不累加
            var r = NewGame(out _, out _);
            r.RunDebateAsync(App.DebateGame.MakeGalileoDebate()).GetAwaiter().GetResult();
            r.RunDebateAsync(App.DebateGame.MakeGalileoDebate()).GetAwaiter().GetResult();
            Check("G5計分-可重入:第二場票數歸零不累加(辯5 非 10、2 回合)", r.DefVotes == 5 && r.Exchanges == 2);
        }

        private static void T_G1_RelayQuest()
        {
            Action<string> noop = _ => { };
            var bodyA = new SimKebbiBody(noop, canMove: true);
            var bodyB = new SimKebbiBody(noop, canMove: true);
            var bus = new SimRobotBus(noop);
            var game = new App.RelayQuestGame(bodyA, bus.CreateLink("A機"), bodyB, bus.CreateLink("B機"), noop);

            game.RunProgramAsync(App.RelayQuestGame.MakeSampleProgram()).GetAwaiter().GetResult();
            Check("G1-抵達終點", game.ReachedGoal);
            Check("G1-總共走 5 格", game.Steps == 5);
            Check("G1-已交棒到 B", game.OnRobotB);
            Check("G1-終點雙機舉手(B RShoulderY=100)", Math.Abs(bodyB.GetMotor(KebbiMotor.RShoulderY) - 100f) < 0.01f);

            // GOAL 出現在 HANDOFF 之前 → 闖關失敗（沒交棒就到終點）
            var bodyA2 = new SimKebbiBody(noop, canMove: true);
            var bodyB2 = new SimKebbiBody(noop, canMove: true);
            var bus2 = new SimRobotBus(noop);
            var g2 = new App.RelayQuestGame(bodyA2, bus2.CreateLink("A2"), bodyB2, bus2.CreateLink("B2"), noop);
            g2.RunProgramAsync(new System.Collections.Generic.List<string> { "FWD", "GOAL" }).GetAwaiter().GetResult();
            Check("G1-未交棒就 GOAL → 不算抵達", !g2.ReachedGoal);
        }

        // G1 障礙/避障關卡:有地圖時 FWD 依朝向位移、撞 # 或出界=Crashed 闖關失敗;IF_OBSTACLE/IF_CLEAR 條件積木;可重入。
        // 演手冊命脈「改指令→物理結果改變」:撞牆版失敗 vs 繞行版成功。純 Sim 可驗。
        private static void T_G1_Obstacle()
        {
            Action<string> noop = _ => { };
            RelayQuestGame NewGame()
            {
                var bus = new SimRobotBus(noop);
                return new App.RelayQuestGame(new SimKebbiBody(noop, true), bus.CreateLink("A機"),
                    new SimKebbiBody(noop, true), bus.CreateLink("B機"), noop, App.LevelMap.Level1());
            }

            // 撞牆版:直直走 → 第 2 格撞 # → Crashed、未抵達、撞牆前只走 1 格
            var gCrash = NewGame();
            gCrash.RunProgramAsync(App.LevelMap.CrashProgram()).GetAwaiter().GetResult();
            Check("G1障礙-撞牆版:Crashed=true", gCrash.Crashed);
            Check("G1障礙-撞牆版:未抵達終點", !gCrash.ReachedGoal);
            Check("G1障礙-撞牆版:撞牆前只走 1 格", gCrash.Steps == 1);

            // 繞行版(手動):右轉下繞→交棒→到 G → 抵達、沒撞、走 4 格
            var gDetour = NewGame();
            gDetour.RunProgramAsync(App.LevelMap.DetourProgram()).GetAwaiter().GetResult();
            Check("G1障礙-繞行版:抵達終點", gDetour.ReachedGoal);
            Check("G1障礙-繞行版:沒撞牆", !gDetour.Crashed);
            Check("G1障礙-繞行版:走 4 格", gDetour.Steps == 4);

            // 智慧版(條件積木 IF_OBSTACLE 自動偵測前方障礙就繞):抵達、沒撞
            var gSmart = NewGame();
            gSmart.RunProgramAsync(App.LevelMap.SmartProgram()).GetAwaiter().GetResult();
            Check("G1障礙-智慧版(IF_OBSTACLE 自動繞):抵達終點且沒撞", gSmart.ReachedGoal && !gSmart.Crashed);

            // 可重入:同一實例先撞牆版後繞行版 → 第二次抵達、Crashed 歸零、不跨場污染
            var gReuse = NewGame();
            gReuse.RunProgramAsync(App.LevelMap.CrashProgram()).GetAwaiter().GetResult();
            gReuse.RunProgramAsync(App.LevelMap.DetourProgram()).GetAwaiter().GetResult();
            Check("G1障礙-可重入:第二次繞行抵達、Crashed 歸零、走 4 格",
                gReuse.ReachedGoal && !gReuse.Crashed && gReuse.Steps == 4);

            // 條件積木 IF_CLEAR:正前方有障礙時應「跳過」區塊(對照 IF_OBSTACLE 會執行)
            // 程式:FWD 到 (0,1)(前方是 #),IF_CLEAR FWD ENDIF → 前方有障礙 → 區塊被跳過 → 不前進、不撞牆、停 1 格
            var gCond = NewGame();
            gCond.RunProgramAsync(new System.Collections.Generic.List<string> { "FWD", "IF_CLEAR", "FWD", "ENDIF" }).GetAwaiter().GetResult();
            Check("G1障礙-IF_CLEAR 前方有障礙時跳過區塊(不撞牆、停 1 格)", !gCond.Crashed && gCond.Steps == 1);
        }

        // G1 闖關計分/星等:Attempts 累計嘗試、TotalCrashes 累計撞牆、Stars 效率星等(走越接近最短路徑越多星)、PrintSummary。
        private static void T_G1_Score()
        {
            Action<string> noop = _ => { };
            var map = App.LevelMap.Level1();
            Check("G1計分-Level1 最短路徑=4(BFS)", map.ShortestSteps() == 4);

            var bus = new SimRobotBus(noop);
            var g = new App.RelayQuestGame(new SimKebbiBody(noop, true), bus.CreateLink("A機"),
                new SimKebbiBody(noop, true), bus.CreateLink("B機"), noop, map);
            g.RunProgramAsync(App.LevelMap.CrashProgram()).GetAwaiter().GetResult();   // 第1次:撞牆失敗
            g.RunProgramAsync(App.LevelMap.DetourProgram()).GetAwaiter().GetResult();  // 第2次:繞行成功
            Check("G1計分-累計嘗試 2 次", g.Attempts == 2);
            Check("G1計分-累計撞牆 1 次(只第1次撞、不重複計)", g.TotalCrashes == 1);
            Check("G1計分-第2次成功", g.ReachedGoal);
            Check("G1計分-效率 3 星(走 4 格==最短路徑)", g.Stars == 3);
        }

        // 合體彩蛋編排:中控依序 cue 各站,站離線/卡住→降級跳過,壓軸全體同步。
        // 純 C# 可驗(實機 UDP 非同步,跳過判定需改 await 逾時 → 見 進度追蹤 BLOCKED)。
        private static void T_Finale()
        {
            Action<string> noop = _ => { };

            // 建一個「在場站台」節點:收 CUE→回 ACK(+可選 DONE);收 FINALE→走位+舉手。回傳該站機身供斷言。
            RecordingBody MakeStation(SimRobotBus bus, string id, bool ack, bool done)
            {
                var body = new RecordingBody();
                var link = bus.CreateLink(id);
                link.OnMessage((from, t) =>
                {
                    if (t.StartsWith("CUE|"))
                    {
                        string role = t.Substring(4);
                        if (ack) link.SendAsync(from, "ACK|" + role);
                        if (done) link.SendAsync(from, "DONE|" + role);
                    }
                    else if (t == "FINALE")
                    {
                        body.Move(0.1f); body.StopWheels();
                        body.SetMotor(KebbiMotor.RShoulderY, 100f);
                    }
                });
                return body;
            }

            // 情境 1:三站全到 → 跑 3、跳 0、壓軸,各站與中控都舉手到 100、在場站走位中央
            var bus = new SimRobotBus(noop);
            var hostBody = new RecordingBody();
            var g2 = MakeStation(bus, "G2-站機", ack: true, done: true);
            var g3 = MakeStation(bus, "G3-站機", ack: true, done: true);
            MakeStation(bus, "G5-夥伴機", ack: true, done: true);
            var game = new App.FinaleShowGame(bus.CreateLink("中控導演機"), hostBody, noop, ackTimeoutMs: 40, doneTimeoutMs: 40);
            game.RunShowAsync(App.FinaleShowGame.MakeDefaultLineup()).GetAwaiter().GetResult();
            Check("彩蛋-三站全到:跑了 3 站", game.StationsRun == 3);
            Check("彩蛋-三站全到:跳過 0 站", game.StationsSkipped == 0);
            Check("彩蛋-壓軸達成", game.FinaleReached);
            Check("彩蛋-FINALE 讓在場站台舉手(G2=100)", Math.Abs(g2.GetMotor(KebbiMotor.RShoulderY) - 100f) < 0.01f);
            Check("彩蛋-在場站台走位中央(G3 有 Move)", Math.Abs(g3.LastMove - 0.1f) < 0.01f);
            Check("彩蛋-中控自己收尾舉手(host=100)", Math.Abs(hostBody.GetMotor(KebbiMotor.RShoulderY) - 100f) < 0.01f);

            // 情境 2:G3 站離線(不建該節點)→ 跳過 G3,其餘照跑,壓軸照常
            var bus2 = new SimRobotBus(noop);
            var hostBody2 = new RecordingBody();
            MakeStation(bus2, "G2-站機", true, true);
            MakeStation(bus2, "G5-夥伴機", true, true);   // 故意不建 "G3-站機" → 離線
            var game2 = new App.FinaleShowGame(bus2.CreateLink("中控導演機"), hostBody2, noop, ackTimeoutMs: 40, doneTimeoutMs: 40);
            game2.RunShowAsync(App.FinaleShowGame.MakeDefaultLineup()).GetAwaiter().GetResult();
            Check("彩蛋-一站離線:跑了 2 站", game2.StationsRun == 2);
            Check("彩蛋-一站離線:跳過 1 站(降級)", game2.StationsSkipped == 1);
            Check("彩蛋-一站離線:壓軸仍達成", game2.FinaleReached);

            // 情境 3:G2 站卡住(回 ACK 但不回 DONE)→ 也跳過(不卡死全場),壓軸照常
            var bus3 = new SimRobotBus(noop);
            var hostBody3 = new RecordingBody();
            MakeStation(bus3, "G2-站機", ack: true, done: false); // 卡住:開始了沒回完成
            MakeStation(bus3, "G3-站機", true, true);
            MakeStation(bus3, "G5-夥伴機", true, true);
            var game3 = new App.FinaleShowGame(bus3.CreateLink("中控導演機"), hostBody3, noop, ackTimeoutMs: 40, doneTimeoutMs: 40);
            game3.RunShowAsync(App.FinaleShowGame.MakeDefaultLineup()).GetAwaiter().GetResult();
            Check("彩蛋-一站卡住(ACK 無 DONE):跑了 2 站", game3.StationsRun == 2);
            Check("彩蛋-一站卡住:跳過 1 站", game3.StationsSkipped == 1);
            Check("彩蛋-一站卡住:壓軸仍達成", game3.FinaleReached);

            // 情境 4:全站離線 → 跑 0、跳 3,壓軸仍達成(中控獨撐收尾,降級不崩)
            var bus4 = new SimRobotBus(noop);
            var hostBody4 = new RecordingBody();
            var game4 = new App.FinaleShowGame(bus4.CreateLink("中控導演機"), hostBody4, noop, ackTimeoutMs: 40, doneTimeoutMs: 40);
            game4.RunShowAsync(App.FinaleShowGame.MakeDefaultLineup()).GetAwaiter().GetResult();
            Check("彩蛋-全站離線:跑了 0 站", game4.StationsRun == 0);
            Check("彩蛋-全站離線:跳過 3 站", game4.StationsSkipped == 3);
            Check("彩蛋-全站離線:壓軸仍達成(中控獨撐收尾)",
                game4.FinaleReached && Math.Abs(hostBody4.GetMotor(KebbiMotor.RShoulderY) - 100f) < 0.01f);

            // 情境 5(可重入):同一實例連跑兩場 → 計數不跨場累加(每場歸零),仍為該場的 3 跑/0 跳
            var bus5 = new SimRobotBus(noop);
            var hostBody5 = new RecordingBody();
            MakeStation(bus5, "G2-站機", true, true);
            MakeStation(bus5, "G3-站機", true, true);
            MakeStation(bus5, "G5-夥伴機", true, true);
            var game5 = new App.FinaleShowGame(bus5.CreateLink("中控導演機"), hostBody5, noop, ackTimeoutMs: 40, doneTimeoutMs: 40);
            game5.RunShowAsync(App.FinaleShowGame.MakeDefaultLineup()).GetAwaiter().GetResult();
            game5.RunShowAsync(App.FinaleShowGame.MakeDefaultLineup()).GetAwaiter().GetResult();
            Check("彩蛋-可重入:第二場計數歸零不累加(跑 3 非 6)", game5.StationsRun == 3 && game5.StationsSkipped == 0);

            // 情境 6(非同步路徑):站台「延遲後」才回 ACK/DONE(不在 SendAsync 當下) → 證明中控真的 await 等到,
            // 非舊版「同步讀旗標」(那會在這裡讀到 null 而誤判離線)。這是真機 UDP 非同步遞送的代理測試。
            var busAsync = new SimRobotBus(noop);
            var hostAsync = new RecordingBody();
            var aLink = busAsync.CreateLink("G2-站機");
            aLink.OnMessage((from, t) =>
            {
                if (t.StartsWith("CUE|"))
                {
                    string role = t.Substring(4);
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(20);                       // 延遲後才回(非同步)
                        await aLink.SendAsync(from, "ACK|" + role);
                        await aLink.SendAsync(from, "DONE|" + role);
                    });
                }
            });
            var gameAsync = new App.FinaleShowGame(busAsync.CreateLink("中控導演機"), hostAsync, noop); // 用預設較長逾時(>20ms)
            gameAsync.RunShowAsync(new System.Collections.Generic.List<App.FinaleShowGame.Station>
                { new App.FinaleShowGame.Station("G2 具身幾何站", "G2-站機") }).GetAwaiter().GetResult();
            Check("彩蛋-非同步延遲回應:中控 await 等到 → 跑 1 站(證明非同步路徑正確)",
                gameAsync.StationsRun == 1 && gameAsync.StationsSkipped == 0);
        }
    }
}
