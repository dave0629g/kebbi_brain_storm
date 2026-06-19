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
            T_G3_Mirror();
            T_RobotLink();
            T_RobotLinkProtocol();
            T_PeerRegistry();
            T_RemoteBody();
            T_RemoteVoice();
            T_RemoteVoiceDone();
            T_LinkAwaiter();
            T_NuwaMotorIds();
            T_Direction_Edges();
            T_HeadClamp_Edges();
            T_FaceFully();
            T_BodyCommand_Edges();
            T_G2_GeometryRelay();
            T_G2_Degrade();
            T_G2_Worksheet();
            T_G2_Library();
            T_G2_Session();
            T_G5_Debate();
            T_G5_Score();
            T_G1_RelayQuest();
            T_G1_Obstacle();
            T_G1_Score();
            T_G1_Handoff();
            T_G1_HandoffSync();
            T_G1_Level3();
            T_Finale();
            T_G5_Trial();
            T_G4_Judge();
            T_G4_EightWay();
            T_G3_Rewind();
            T_G3_Frame();
            T_G2_Validator();
            T_G2_TurnHead();

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
            Check("22.5°→Depan(邊界含上界)", Direction.FromAngle(22.5f) == Dir.Depan);
            Check("45°→SerongKanan(右前,45°中心 8向)", Direction.FromAngle(45) == Dir.SerongKanan);
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
            Check("正後方頭被夾限到 40°(NeckZ 實機 ±40)", Math.Abs(faced - 40f) < 0.01f);
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

            // 喊「太快了」(來自右邊 90°) → BPM 降低 + 頭朝該方向轉(但 NeckZ 實機 ±40 → 只能轉到 40°、部分面向)
            int before = game.Bpm;
            body.CurrentDoa = 90f;
            bool reachable = game.HandleTooFastAsync(body.ReadDoaDegrees()).GetAwaiter().GetResult();
            Check("G3-太快→BPM 降低", game.Bpm < before);
            Check("G3-太快→頭朝發問者轉到上限(NeckZ=40，±40 夾限)", Math.Abs(body.GetMotor(KebbiMotor.NeckZ) - 40f) < 0.01f);
            Check("G3-右側 90° 超出頭部可達→只能部分面向(reachable=false)", !reachable);

            // 姿態錯誤 → 不加分
            int s = game.Score;
            pose.Enqueue(false);
            game.RunRepAsync(move).GetAwaiter().GetResult();
            Check("G3-姿態錯誤不加分", game.Score == s);
        }

        // G3 多動作 session:逐組示範+計分、組間語音切換下一組(暖身/太極/CPR);可重入。
        // G3 左右鏡像對映:R↔L 對調、中軸不換、對合(兩次回原);非對稱動作鏡像後左右互換。
        private static void T_G3_Mirror()
        {
            Check("G3鏡像-R↔L 對調", App.MirrorCoachGame.MirrorMotor(KebbiMotor.RShoulderY) == KebbiMotor.LShoulderY
                && App.MirrorCoachGame.MirrorMotor(KebbiMotor.LElbowY) == KebbiMotor.RElbowY);
            Check("G3鏡像-中軸 NeckZ/NeckY 不換", App.MirrorCoachGame.MirrorMotor(KebbiMotor.NeckZ) == KebbiMotor.NeckZ
                && App.MirrorCoachGame.MirrorMotor(KebbiMotor.NeckY) == KebbiMotor.NeckY);
            Check("G3鏡像-對合(鏡像兩次回原)",
                App.MirrorCoachGame.MirrorMotor(App.MirrorCoachGame.MirrorMotor(KebbiMotor.RShoulderZ)) == KebbiMotor.RShoulderZ);

            // 非對稱動作:只舉右手 → 鏡像後只舉左手(同角度)
            var oneArm = new App.MirrorCoachGame.Move("舉右手");
            oneArm.Frames.Add(new App.MirrorCoachGame.JointFrame("舉右").Set(KebbiMotor.RShoulderY, 90f));
            var mir = App.MirrorCoachGame.MirrorMove(oneArm);
            Check("G3鏡像-非對稱:舉右手鏡像成舉左手(LShoulderY=90)",
                mir.Frames.Count == 1 && mir.Frames[0].Targets.Count == 1
                && mir.Frames[0].Targets[0].Key == KebbiMotor.LShoulderY
                && Math.Abs(mir.Frames[0].Targets[0].Value - 90f) < 0.01f);
            Check("G3鏡像-名稱標(鏡像)", mir.Name.Contains("鏡像"));
        }

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

        private static void T_PeerRegistry()
        {
            // 靜態設定 + 自動學習都進得去;Snapshot 反映內容
            var r = new PeerRegistry("192.168.1.108"); // 本機 .108
            Check("Peer-靜態新增", r.AddStatic("192.168.1.112"));
            Check("Peer-學習新 IP", r.Learn("192.168.1.120"));
            Check("Peer-Count=2", r.Count == 2);
            Check("Peer-Knows 已加入", r.Knows("192.168.1.112") && r.Knows("192.168.1.120"));

            // 去重:同 IP 再加回傳 false、不增量
            Check("Peer-重複不新增", !r.Learn("192.168.1.112"));
            Check("Peer-Count 仍=2", r.Count == 2);

            // 排除自己(避免對自己的廣播來源 unicast)
            Check("Peer-排除本機 IP", !r.Learn("192.168.1.108"));

            // 排除無效/萬用位址、空字串
            Check("Peer-排除 0.0.0.0", !r.AddStatic("0.0.0.0"));
            Check("Peer-排除 255 廣播", !r.AddStatic("255.255.255.255"));
            Check("Peer-排除空字串", !r.AddStatic(""));
            Check("Peer-排除 null", !r.AddStatic(null));
            Check("Peer-Count 不受無效影響=2", r.Count == 2);

            // 無 selfIp 時不排除任何真實 IP
            var r2 = new PeerRegistry();
            Check("Peer-無 selfIp 仍可加", r2.AddStatic("10.0.0.5") && r2.Count == 1);

            // 畸形 IP 格式(放行的話送出時每輪丟例外/log)→ 註冊就擋
            Check("Peer-排除畸形 IP(300.300.300.300)", !r2.AddStatic("300.300.300.300"));
            Check("Peer-排除非 IP 字串", !r2.AddStatic("not-an-ip"));
            Check("Peer-畸形 IP 不增量", r2.Count == 1);
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
            // 8 向 45° 扇區邊界(含上界慣例 (下界,上界])
            Check("135°→BelakangKanan(右後,8向)", Direction.FromAngle(135) == Dir.BelakangKanan);
            Check("157.5°→BelakangKanan(邊界含上界)", Direction.FromAngle(157.5f) == Dir.BelakangKanan);
            Check("158°→Belakang", Direction.FromAngle(158) == Dir.Belakang);
            Check("-135°→BelakangKiri(左後,8向)", Direction.FromAngle(-135) == Dir.BelakangKiri);
            Check("-158°→Belakang", Direction.FromAngle(-158) == Dir.Belakang);
            Check("180°→Belakang", Direction.FromAngle(180) == Dir.Belakang);
            Check("-180°→Belakang", Direction.FromAngle(-180) == Dir.Belakang);
            Check("-45°→SerongKiri(左前,8向)", Direction.FromAngle(-45) == Dir.SerongKiri);
            Check("-22.5°→SerongKiri(邊界含上界)", Direction.FromAngle(-22.5f) == Dir.SerongKiri);
            Check("-22.4°→Depan(過邊界回正前)", Direction.FromAngle(-22.4f) == Dir.Depan);
            Check("正規化 270°→Kiri", Direction.FromAngle(270) == Dir.Kiri);
            Check("正規化 -270°→Kanan", Direction.FromAngle(-270) == Dir.Kanan);
            Check("正規化 360°→Depan", Direction.FromAngle(360) == Dir.Depan);
            // Normalize 防無限迴圈(實機 DOA SDK 偵測失敗可能回 NaN/哨兵大數;舊 while 版會卡死)
            Check("Normalize NaN→0(守衛)", Direction.Normalize(float.NaN) == 0f);
            Check("Normalize +Infinity→0", Direction.Normalize(float.PositiveInfinity) == 0f);
            Check("Normalize -Infinity→0", Direction.Normalize(float.NegativeInfinity) == 0f);
            Check("Normalize 大數 1e30 不卡死且落 [-180,180]",
                Direction.Normalize(1e30f) >= -180f && Direction.Normalize(1e30f) <= 180f);
            Check("Normalize 270→-90(modulo 正確)", Math.Abs(Direction.Normalize(270f) - (-90f)) < 0.01f);
            Check("Normalize -270→90", Math.Abs(Direction.Normalize(-270f) - 90f) < 0.01f);
            Check("Normalize 540→180(邊界保留)", Math.Abs(Direction.Normalize(540f) - 180f) < 0.01f);
            // 8 向印尼語詞往返(複合詞 serong/belakang + kanan/kiri 不可被單詞吃掉)
            foreach (Dir d in new[] { Dir.Depan, Dir.SerongKanan, Dir.Kanan, Dir.BelakangKanan,
                                      Dir.Belakang, Dir.BelakangKiri, Dir.Kiri, Dir.SerongKiri })
                Check("印尼語詞往返還原 " + d, Direction.ParseIndo(Direction.ToIndo(d)) == d);
        }

        // 轉頭夾限邊界(正後/左後/恰好邊界/超界正規化)。
        private static void T_HeadClamp_Edges()
        {
            var ctx = SilentSim(out var body, out _);   // SimKebbiBody NeckZ 實機範圍 ±40
            float f1 = KebbiHead.TurnToward(body, -170f, out bool r1);
            Check("左後 -170°→夾到 -40(±40 上限)", Math.Abs(f1 + 40f) < 0.01f);
            Check("左後 -170° 不可達", !r1);
            float f2 = KebbiHead.TurnToward(body, 90f, out bool r2);
            Check("正右 90°→夾到 40、不可達(頭轉不到 90)", !r2 && Math.Abs(f2 - 40f) < 0.01f);
            float f3 = KebbiHead.TurnToward(body, -90f, out bool r3);
            Check("正左 -90°→夾到 -40、不可達", !r3 && Math.Abs(f3 + 40f) < 0.01f);
            float f4 = KebbiHead.TurnToward(body, 40f, out bool r4);
            Check("邊界 40° 恰可達", r4 && Math.Abs(f4 - 40f) < 0.01f);
            float f5 = KebbiHead.TurnToward(body, 41f, out bool r5);
            Check("41°→夾到 40、不可達", !r5 && Math.Abs(f5 - 40f) < 0.01f);
            float f6 = KebbiHead.TurnToward(body, 200f, out bool r6); // 正規化→-160→夾 -40
            Check("200°(正規化-160)→夾 -40、不可達", !r6 && Math.Abs(f6 + 40f) < 0.01f);
            Check("TurnToward 有寫入 NeckZ", Math.Abs(body.GetMotor(KebbiMotor.NeckZ) + 40f) < 0.01f);
        }

        // 複合面向 FaceFully：底盤 turn() 轉粗方向 + NeckZ(±40)補細。輪式可完整面向任意角(含正後);
        // H201 桌上型不能轉底盤 → >±40 只能部分面向(等同 TurnToward 降級)。純 Sim 可驗(底盤開迴路,只驗角度分配)。
        private static void T_FaceFully()
        {
            Action<string> noop = _ => { };
            var wheeled = new SimKebbiBody(noop, canMove: true);   // 輪式,NeckZ ±40
            var desktop = new SimKebbiBody(noop, canMove: false);  // H201 桌上型(無底盤)

            // 輪式:範圍內(30°)只動頭、不動底盤、完整面向
            var r1 = KebbiHead.FaceFully(wheeled, 30f);
            Check("FaceFully-輪式 30°(範圍內):不動底盤、頭=30、完整面向、有寫 NeckZ",
                r1.BaseTurnDeg == 0f && Math.Abs(r1.HeadDeg - 30f) < 0.01f && r1.FullyFaced
                && Math.Abs(wheeled.GetMotor(KebbiMotor.NeckZ) - 30f) < 0.01f);

            // 輪式:90°(超出±40)→底盤轉50+頭40=合成90、完整面向
            var r2 = KebbiHead.FaceFully(wheeled, 90f);
            Check("FaceFully-輪式 90°:底盤50+頭40=合成90、完整面向",
                Math.Abs(r2.BaseTurnDeg - 50f) < 0.01f && Math.Abs(r2.HeadDeg - 40f) < 0.01f
                && Math.Abs(r2.FacedAngle - 90f) < 0.01f && r2.FullyFaced);

            // 輪式:-90°→底盤-50+頭-40、完整面向
            var r3 = KebbiHead.FaceFully(wheeled, -90f);
            Check("FaceFully-輪式 -90°:底盤-50+頭-40、完整面向",
                Math.Abs(r3.BaseTurnDeg + 50f) < 0.01f && Math.Abs(r3.HeadDeg + 40f) < 0.01f && r3.FullyFaced);

            // 輪式:正後 180°→底盤140+頭40=180、完整面向(連正後也能)
            var r4 = KebbiHead.FaceFully(wheeled, 180f);
            Check("FaceFully-輪式 180°正後:底盤140+頭40=180、完整面向",
                Math.Abs(r4.BaseTurnDeg - 140f) < 0.01f && Math.Abs(r4.FacedAngle - 180f) < 0.01f && r4.FullyFaced);

            // 邊界:恰 40°→頭40、不動底盤、完整
            var r5 = KebbiHead.FaceFully(wheeled, 40f);
            Check("FaceFully-輪式 邊界40°:頭40、不動底盤、完整面向",
                r5.BaseTurnDeg == 0f && Math.Abs(r5.HeadDeg - 40f) < 0.01f && r5.FullyFaced);

            // H201 桌上型:90°→底盤不能轉→只頭40、部分面向(FullyFaced=false)
            var r6 = KebbiHead.FaceFully(desktop, 90f);
            Check("FaceFully-H201 90°:底盤不轉、頭40、只部分面向(FullyFaced=false)",
                r6.BaseTurnDeg == 0f && Math.Abs(r6.HeadDeg - 40f) < 0.01f && !r6.FullyFaced);

            // H201 範圍內 30°→頭即可、完整面向、有寫 NeckZ
            var r7 = KebbiHead.FaceFully(desktop, 30f);
            Check("FaceFully-H201 30°(範圍內):頭30、完整面向、有寫 NeckZ",
                r7.BaseTurnDeg == 0f && Math.Abs(r7.HeadDeg - 30f) < 0.01f && r7.FullyFaced
                && Math.Abs(desktop.GetMotor(KebbiMotor.NeckZ) - 30f) < 0.01f);
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
            // 畸形網路封包:一律拒絕(回 false)而非丟例外崩掉被控機接收迴圈
            Check("命令-SM 非數字馬達ID→false(不丟例外)", !BodyCommand.TryApply("BC|SM|abc|10|50", spy));
            Check("命令-SM 馬達ID 超出範圍→false", !BodyCommand.TryApply("BC|SM|999|10|50", spy));
            Check("命令-SM 非數字角度→false", !BodyCommand.TryApply("BC|SM|2|x|50", spy));
            Check("命令-SM 非數字速度→false", !BodyCommand.TryApply("BC|SM|2|10|y", spy));
            Check("命令-MV 非數字→false", !BodyCommand.TryApply("BC|MV|fast", spy));
            Check("命令-TN 非數字→false", !BodyCommand.TryApply("BC|TN|left", spy));
            // 畸形命令不應殘留副作用:再送一個正常命令仍正確
            Check("命令-畸形後正常命令仍可套用",
                BodyCommand.TryApply(BodyCommand.SetMotor(KebbiMotor.RShoulderY, 70f, 50f), spy)
                && Math.Abs(spy.Motors[KebbiMotor.RShoulderY] - 70f) < 0.01f);
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

        // G2 甲機轉頭望向發言者:Step.TurnHeadToward(NeckZ)有值才轉頭(可為負=望左),null=不轉頭(向後相容)。
        // 轉頭在手臂指認之前;與既有走位/指認/DONE/學習單互不干擾;可重入。
        private static void T_G2_TurnHead()
        {
            Action<string> noop = _ => { };
            App.GeometryRelayGame NewGame(out SimKebbiBody guide, out SimVoice voice)
            {
                SilentSim(out guide, out voice);
                var bus = new SimRobotBus(noop);
                return new App.GeometryRelayGame(guide, bus.CreateLink("甲機"), bus.CreateLink("乙機"), voice, noop);
            }
            System.Collections.Generic.List<App.GeometryRelayGame.Step> One(App.GeometryRelayGame.Step s)
                => new System.Collections.Generic.List<App.GeometryRelayGame.Step> { s };

            // (1) TurnHeadToward=45 → NeckZ 轉到 45、手臂仍指認(RShoulderY=60)、完成 1 步
            var g1 = NewGame(out var guide1, out _);
            g1.RunProofAsync(One(new App.GeometryRelayGame.Step("因為AB=AC", "AB,AC", 60f, null, 45f))).GetAwaiter().GetResult();
            Check("G2轉頭-TurnHead=45:NeckZ 轉到 45", Math.Abs(guide1.GetMotor(KebbiMotor.NeckZ) - 45f) < 0.01f);
            Check("G2轉頭-轉頭同時手臂仍指認(RShoulderY=60)", Math.Abs(guide1.GetMotor(KebbiMotor.RShoulderY) - 60f) < 0.01f);
            Check("G2轉頭-仍完成接力 1 步", g1.StepsDone == 1);

            // (2) 負角度有效(望左):-45 不被當成「不轉頭」哨兵 → NeckZ=-45
            var g2 = NewGame(out var guide2, out _);
            g2.RunProofAsync(One(new App.GeometryRelayGame.Step("望左", "AB", 60f, null, -45f))).GetAwaiter().GetResult();
            Check("G2轉頭-負角度有效:NeckZ=-45(望左,非哨兵)", Math.Abs(guide2.GetMotor(KebbiMotor.NeckZ) + 45f) < 0.01f);

            // (3) 0° 有值=望正前(HasValue true):先撥 99,跑後 NeckZ 歸 0(真的下了轉頭命令)
            var g3 = NewGame(out var guide3, out _);
            guide3.SetMotor(KebbiMotor.NeckZ, 99f);
            g3.RunProofAsync(One(new App.GeometryRelayGame.Step("望前", "AD", 30f, null, 0f))).GetAwaiter().GetResult();
            Check("G2轉頭-0°有值=望正前:NeckZ 設回 0", Math.Abs(guide3.GetMotor(KebbiMotor.NeckZ) - 0f) < 0.01f);

            // (4) null=不轉頭(向後相容):先撥 99,跑無轉頭步 → NeckZ 仍 99(handler 沒碰)、手臂仍指認
            var g4 = NewGame(out var guide4, out _);
            guide4.SetMotor(KebbiMotor.NeckZ, 99f);
            g4.RunProofAsync(One(new App.GeometryRelayGame.Step("不轉頭", "AD", 30f, null))).GetAwaiter().GetResult();
            Check("G2轉頭-null 不轉頭(NeckZ 維持 99 未被碰)", Math.Abs(guide4.GetMotor(KebbiMotor.NeckZ) - 99f) < 0.01f);
            Check("G2轉頭-不轉頭步仍指認手臂(RShoulderY=30)", Math.Abs(guide4.GetMotor(KebbiMotor.RShoulderY) - 30f) < 0.01f);

            // (5) 舊式 4 參數建構式(無 turnHeadToward)→ 預設 null → 不轉頭(完全向後相容)
            var oldStep = new App.GeometryRelayGame.Step("舊式", "AB,AC", 60f, "已知");
            Check("G2轉頭-舊式 4 參數 Step:TurnHeadToward 預設 null", oldStep.TurnHeadToward == null);

            // (6) 多步不同角度:-45/0/45 → 末步停在 45、共 3 步
            var g6 = NewGame(out var guide6, out _);
            g6.RunProofAsync(App.GeometryRelayGame.MakeIsoscelesProofTurnHead()).GetAwaiter().GetResult();
            Check("G2轉頭-多步:末步 NeckZ 停在 45、共 3 步",
                Math.Abs(guide6.GetMotor(KebbiMotor.NeckZ) - 45f) < 0.01f && g6.StepsDone == 3);

            // (7) 可重入:同實例第二次跑轉頭題 → StepsDone 重置 3(非累加 6)、末步角度仍 45
            g6.RunProofAsync(App.GeometryRelayGame.MakeIsoscelesProofTurnHead()).GetAwaiter().GetResult();
            Check("G2轉頭-可重入:第二次 StepsDone 重置 3、末步 NeckZ=45",
                g6.StepsDone == 3 && Math.Abs(guide6.GetMotor(KebbiMotor.NeckZ) - 45f) < 0.01f);
        }

        // G2 證明題庫:多題型(等腰底角/內角和/外角定理)可換題,每題皆學習單版;逐題驗跑完 3 步、甲機末步指向角。
        // G2 多回合場次:RunSessionAsync 跑題庫多題,累計步數/學習單得分 → 場次結算;可重入。
        private static void T_G2_Session()
        {
            Action<string> noop = _ => { };
            var bus = new SimRobotBus(noop);
            var voice = new SimVoice(noop);
            var game = new App.GeometryRelayGame(new SimKebbiBody(noop, true),
                bus.CreateLink("甲機"), bus.CreateLink("乙機"), voice, noop);
            var lib = App.GeometryRelayGame.MakeProofLibrary(); // 3 題,每題學習單版 3 步(已知/因為/所以)
            for (int p = 0; p < lib.Count; p++) { voice.EnqueueHeard("已知"); voice.EnqueueHeard("因為"); voice.EnqueueHeard("所以"); }
            game.RunSessionAsync(lib).GetAwaiter().GetResult();
            Check("G2場次-完成 3 題", game.ProofsDone == 3);
            Check("G2場次-共 9 步(3題×3步)", game.SessionSteps == 9);
            Check("G2場次-學習單總分 9(全對)", game.SessionScore == 9);

            // 可重入:再跑一場(部分答錯)→ 計數歸零、總分反映本場
            for (int p = 0; p < lib.Count; p++) { voice.EnqueueHeard("已知"); voice.EnqueueHeard("錯"); voice.EnqueueHeard("所以"); }
            game.RunSessionAsync(lib).GetAwaiter().GetResult();
            Check("G2場次-可重入:第二場每題錯1 → 總分 6(非累加)、題數 3", game.SessionScore == 6 && game.ProofsDone == 3);
        }

        private static void T_G2_Library()
        {
            Action<string> noop = _ => { };
            var lib = App.GeometryRelayGame.MakeProofLibrary();
            Check("G2題庫-3 題", lib.Count == 3);
            Check("G2題庫-題目有標題(等腰/內角和/外角)",
                lib[0].Title == "等腰三角形兩底角相等" && lib[1].Title == "三角形內角和 180°" && lib[2].Title == "三角形外角定理");

            foreach (var p in lib)
            {
                var bus = new SimRobotBus(noop);
                var guideBody = new SimKebbiBody(noop, true);
                var voice = new SimVoice(noop);
                voice.EnqueueHeard("已知"); voice.EnqueueHeard("因為"); voice.EnqueueHeard("所以"); // 學習單正解
                var game = new App.GeometryRelayGame(guideBody, bus.CreateLink("甲機"), bus.CreateLink("乙機"), voice, noop);
                game.RunProofAsync(p.Steps).GetAwaiter().GetResult();
                Check("G2題庫-「" + p.Title + "」跑完 3 步、學習單滿分、末步指向角 80",
                    game.StepsDone == 3 && game.Score == 3 && System.Math.Abs(guideBody.GetMotor(KebbiMotor.RShoulderY) - 80f) < 0.01f);
            }
        }

        // G2 學習單作答驗證:乙機問「這步是已知/因為/所以?」,學生答(注入)→ 答對計分、答錯念提示;
        // 作答不擋接力(StepsDone 照走);無 Layer 的舊題目不出題(向後相容)。
        private static void T_G2_Worksheet()
        {
            Action<string> noop = _ => { };
            var bus = new SimRobotBus(noop);
            var voice = new SimVoice(noop); // 乙機 voice:注入學生答案
            var game = new App.GeometryRelayGame(new SimKebbiBody(noop, true),
                bus.CreateLink("甲機"), bus.CreateLink("乙機"), voice, noop);

            // 第1步對(已知)、第2步錯、第3步對(所以)→ 答對 2 題
            voice.EnqueueHeard("已知"); voice.EnqueueHeard("我不知道"); voice.EnqueueHeard("所以");
            game.RunProofAsync(App.GeometryRelayGame.MakeIsoscelesProofWorksheet()).GetAwaiter().GetResult();
            Check("G2學習單-完成 3 步證明(作答不擋接力)", game.StepsDone == 3);
            Check("G2學習單-答對 2 題得 2 分", game.Score == 2);

            // 全對 → 3 分;可重入(分數重置非累加)
            voice.EnqueueHeard("已知"); voice.EnqueueHeard("因為"); voice.EnqueueHeard("所以");
            game.RunProofAsync(App.GeometryRelayGame.MakeIsoscelesProofWorksheet()).GetAwaiter().GetResult();
            Check("G2學習單-全對得 3 分、可重入(分數重置)", game.Score == 3 && game.StepsDone == 3);

            // 無 Layer 的舊題目 → 不出學習單題、Score=0(向後相容)
            game.RunProofAsync(App.GeometryRelayGame.MakeIsoscelesProof()).GetAwaiter().GetResult();
            Check("G2學習單-無 Layer 題目不出題(Score=0)", game.Score == 0 && game.StepsDone == 3);
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

            // 學生在控方右側 30° 發言(在 NeckZ ±40 內) → 控方頭轉向面對(NeckZ≈30、可完整面向)
            bool faced = game.TurnToStudentAsync(true, 30f).GetAwaiter().GetResult();
            Check("G5-轉向發言學生(NeckZ≈30)", Math.Abs(proBody.GetMotor(KebbiMotor.NeckZ) - 30f) < 0.01f);
            Check("G5-30°(在 ±40 內) 可完整面向", faced);
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

        // G1 交棒「換你」語音 + 舉手手勢(注入 voiceA/voiceB)。null=不啟用(向後相容)。
        private static void T_G1_Handoff()
        {
            Action<string> noop = _ => { };
            var bodyA = new SimKebbiBody(noop, true); var bodyB = new SimKebbiBody(noop, true);
            var bus = new SimRobotBus(noop);
            var vA = new RecordingVoice(); var vB = new RecordingVoice();
            var game = new App.RelayQuestGame(bodyA, bus.CreateLink("A機"), bodyB, bus.CreateLink("B機"), noop, null, vA, vB);
            game.RunProgramAsync(App.RelayQuestGame.MakeSampleProgram()).GetAwaiter().GetResult();
            Check("G1交棒手勢-仍正常交棒到 B、抵達", game.OnRobotB && game.ReachedGoal);
            Check("G1交棒手勢-A 說『換你』", vA.Spoken.Exists(x => x.text != null && x.text.Contains("換你")));
            Check("G1交棒手勢-B 說『收到』", vB.Spoken.Exists(x => x.text != null && x.text.Contains("收到")));
            Check("G1交棒手勢-終點雙機舉手(GOAL 後 B=100,放手不破壞勝利手勢)",
                Math.Abs(bodyB.GetMotor(KebbiMotor.RShoulderY) - 100f) < 0.01f);

            // 單邊注入容錯:只 voiceA → 不丟例外、仍交棒抵達
            var g2 = new App.RelayQuestGame(new SimKebbiBody(noop, true), bus.CreateLink("A2"),
                new SimKebbiBody(noop, true), bus.CreateLink("B2"), noop, null, new RecordingVoice(), null);
            g2.RunProgramAsync(App.RelayQuestGame.MakeSampleProgram()).GetAwaiter().GetResult();
            Check("G1交棒手勢-單邊注入 voiceA 仍交棒抵達", g2.OnRobotB && g2.ReachedGoal);
        }

        // G1 交接點同步條件(Level2 有 H):A 沒走到交接點就 HANDOFF=交棒失敗、B 不啟動。
        private static void T_G1_HandoffSync()
        {
            Action<string> noop = _ => { };
            var map = App.LevelMap.Level2();
            Check("G1交接點-Level2 有交接點 H(0,3)", map.HasHandoffPoint && map.IsHandoffPoint(0, 3));
            RelayQuestGame NewGame()
            {
                var bus = new SimRobotBus(noop);
                return new App.RelayQuestGame(new SimKebbiBody(noop, true), bus.CreateLink("A機"),
                    new SimKebbiBody(noop, true), bus.CreateLink("B機"), noop, map);
            }

            // 太早交棒(沒走到 H)→ 交棒失敗、B 不啟動、未抵達
            var early = NewGame();
            early.RunProgramAsync(App.LevelMap.Level2HandoffTooEarlyProgram()).GetAwaiter().GetResult();
            Check("G1交接點-太早交棒:HandoffFailed=true、B 沒啟動、未抵達",
                early.HandoffFailed && !early.OnRobotB && !early.ReachedGoal);

            // 站對交接點 H 才交棒 → 成功、抵達、沒交棒失敗
            var ok = NewGame();
            ok.RunProgramAsync(App.LevelMap.Level2DetourProgram()).GetAwaiter().GetResult();
            Check("G1交接點-站對 H 交棒:成功交棒、抵達、無 HandoffFailed",
                ok.OnRobotB && ok.ReachedGoal && !ok.HandoffFailed);

            // 可重入:同實例先太早(失敗)後正解(成功)→ 第二次 HandoffFailed 歸零、抵達
            var reuse = NewGame();
            reuse.RunProgramAsync(App.LevelMap.Level2HandoffTooEarlyProgram()).GetAwaiter().GetResult();
            reuse.RunProgramAsync(App.LevelMap.Level2DetourProgram()).GetAwaiter().GetResult();
            Check("G1交接點-可重入:第二次正解 HandoffFailed 歸零、抵達", !reuse.HandoffFailed && reuse.ReachedGoal);
        }

        // G1 多障礙避障關(Level3):S 形強制路徑 + 交接點 H。撞版沿上排撞 #(1,3)失敗;正解下繞→過 H→到 G。
        // 演手冊命脈「改指令→物理結果改變」放大到多障礙:錯一步就撞牆,唯一最短路才 3 星。純 Sim 可驗。
        private static void T_G1_Level3()
        {
            Action<string> noop = _ => { };
            var map = App.LevelMap.Level3();
            Check("G1-Level3 有交接點 H(2,3)", map.HasHandoffPoint && map.IsHandoffPoint(2, 3));
            Check("G1-Level3 最短路徑=7(BFS,S 形繞行)", map.ShortestSteps() == 7);
            RelayQuestGame NewGame()
            {
                var bus = new SimRobotBus(noop);
                return new App.RelayQuestGame(new SimKebbiBody(noop, true), bus.CreateLink("A機"),
                    new SimKebbiBody(noop, true), bus.CreateLink("B機"), noop, map);
            }

            // 撞牆版:沿上排走到底再右轉下行 → 撞 #(1,3) → Crashed、未抵達、撞前走 3 格
            var gCrash = NewGame();
            gCrash.RunProgramAsync(App.LevelMap.Level3CrashProgram()).GetAwaiter().GetResult();
            Check("G1-Level3 撞牆版:Crashed=true、未抵達、撞前走 3 格",
                gCrash.Crashed && !gCrash.ReachedGoal && gCrash.Steps == 3);

            // 正解版:下→橫越→站對 H 交棒→下到 G → 抵達、沒撞、無交棒失敗、走 7 格(==最短)、3 星
            var gOk = NewGame();
            gOk.RunProgramAsync(App.LevelMap.Level3DetourProgram()).GetAwaiter().GetResult();
            Check("G1-Level3 正解:抵達且沒撞且交棒成功",
                gOk.ReachedGoal && !gOk.Crashed && gOk.OnRobotB && !gOk.HandoffFailed);
            Check("G1-Level3 正解:走 7 格(==最短)、效率 3 星", gOk.Steps == 7 && gOk.Stars == 3);

            // 可重入:同實例先撞牆後正解 → 第二次抵達、Crashed 歸零、走 7 格
            var gReuse = NewGame();
            gReuse.RunProgramAsync(App.LevelMap.Level3CrashProgram()).GetAwaiter().GetResult();
            gReuse.RunProgramAsync(App.LevelMap.Level3DetourProgram()).GetAwaiter().GetResult();
            Check("G1-Level3 可重入:第二次正解抵達、Crashed 歸零、走 7 格",
                gReuse.ReachedGoal && !gReuse.Crashed && gReuse.Steps == 7);
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

        // G5-branch 七步審判驅動器 RunTrialAsync + 學生席舉手插話分支。
        // 驗:7 步流程跑滿、開場/結辯順序、插話改票、轉向學生 NeckZ 夾限、姿態 gate(舉手/沒舉手)、
        //     學生麥 listen 路徑、可重入、無插話=退化舊行為、舊 7 參數建構式仍可跑。
        private static void T_G5_Trial()
        {
            Action<string> noop = _ => { };

            // 用新 9 參數建構式建一場(可注入學生麥/姿態 gate);out 出兩機身與兩主辯語音供斷言。
            DebateGame NewTrial(out SimKebbiBody pro, out SimKebbiBody def,
                                out RecordingVoice proV, out RecordingVoice defV,
                                IVoice studentMic = null, IPoseSensor pose = null)
            {
                pro = new SimKebbiBody(noop, true); def = new SimKebbiBody(noop, true);
                proV = new RecordingVoice(); defV = new RecordingVoice();
                var bus = new SimRobotBus(noop);
                return new App.DebateGame(pro, bus.CreateLink("控方"), proV,
                    def, bus.CreateLink("辯方"), defV, noop, studentMic, pose);
            }

            // (1) 7 步流程跑滿:MakeGalileoTrial → 2 回合接力都成功、逼近 1 次、宣判完成、插話計 1。
            var g = NewTrial(out _, out var def1, out _, out _);
            g.RunTrialAsync(App.DebateGame.MakeGalileoTrial()).GetAwaiter().GetResult();
            Check("G5審判-7步:2 回合接力成功(Exchanges==2)", g.Exchanges == 2);
            Check("G5審判-7步:步驟5 逼近一次(CenterApproaches==1)", g.CenterApproaches == 1);
            Check("G5審判-7步:結辯完成(Concluded)", g.Concluded);

            // (2) 插話改票:伽利略基礎辯方 5 票,第 2 回合掛 DefVoteDelta=1 的插話 → 跑後辯方 6 票、插話計 1。
            Check("G5審判-插話改票:辯方票含插話加成(5+1=6)", g.DefVotes == 6);
            Check("G5審判-插話改票:成功處理 1 次插話(Interjections==1)", g.Interjections == 1);
            Check("G5審判-插話改票:仍宣判辯方勝(控2:辯6)", g.Verdict == "辯方勝");

            // (3) 轉向學生:插話 ProSide=false、DoaDeg=120 → 辯方機(輪式 canMove)用 FaceFully:
            //     底盤轉 80° + 頭 NeckZ 40°(±40 上限) → 完整面向;NeckZ 停在頭部分量 40°。
            var ff120 = KebbiHead.FaceFully(new SimKebbiBody(noop, true), 120f);
            Check("G5審判-轉向學生:輪式 FaceFully(120°) 底盤80+頭40、完整面向",
                ff120.FullyFaced && System.Math.Abs(ff120.BaseTurnDeg - 80f) < 0.01f && System.Math.Abs(ff120.HeadDeg - 40f) < 0.01f);
            Check("G5審判-轉向學生:辯方機 NeckZ 停在頭部分量(=40)",
                System.Math.Abs(def1.GetMotor(KebbiMotor.NeckZ) - ff120.HeadDeg) < 0.01f);

            // (4) 開場/結辯順序:自訂 Opening+Closing,用 RecordingVoice 驗開場白先說、結辯詞在宣判前說。
            var g2 = NewTrial(out _, out _, out var proV2, out _);
            var script = new App.DebateGame.TrialScript
            {
                Opening = "【自訂開場】開庭。",
                Closing = "【自訂結辯】控方總結。",
                Rounds = App.DebateGame.MakeGalileoDebate(),
            };
            g2.RunTrialAsync(script).GetAwaiter().GetResult();
            int idxOpen = proV2.Spoken.FindIndex(x => x.text == "【自訂開場】開庭。");
            int idxClose = proV2.Spoken.FindIndex(x => x.text == "【自訂結辯】控方總結。");
            int idxVerdict = proV2.Spoken.FindIndex(x => x.text == "控方勝訴。");
            Check("G5審判-順序:開場白由控方機最先說(index 0)", idxOpen == 0);
            Check("G5審判-順序:結辯詞在開場白之後說出", idxClose > idxOpen);
            // 此腳本無插話、控2:辯5 → 辯方勝,故控方不說「控方勝訴。」(idxVerdict==-1);結辯詞仍應在最後。
            Check("G5審判-順序:辯方勝故控方不宣勝、自訂結辯詞為控方最後一句",
                idxVerdict < 0 && idxClose == proV2.Spoken.Count - 1);

            // (5) 姿態 gate-有舉手:注入 SimPoseSensor.Enqueue(true) → 走分支、插話計 1、票含加成。
            var poseYes = new SimPoseSensor(noop);
            poseYes.Enqueue(true);
            var gy = NewTrial(out _, out _, out _, out _, pose: poseYes);
            gy.RunTrialAsync(App.DebateGame.MakeGalileoTrial()).GetAwaiter().GetResult();
            Check("G5審判-姿態gate舉手:走分支(Interjections==1)", gy.Interjections == 1);
            Check("G5審判-姿態gate舉手:票含插話加成(辯6)", gy.DefVotes == 6);

            // (6) 姿態 gate-沒舉手:Enqueue(false) → 略過分支、不轉頭不改票、插話計 0。
            var poseNo = new SimPoseSensor(noop);
            poseNo.Enqueue(false);
            var gn = NewTrial(out _, out var defN, out _, out _, pose: poseNo);
            gn.RunTrialAsync(App.DebateGame.MakeGalileoTrial()).GetAwaiter().GetResult();
            Check("G5審判-姿態gate沒舉手:略過分支(Interjections==0)", gn.Interjections == 0);
            Check("G5審判-姿態gate沒舉手:票不含插話加成(辯5)", gn.DefVotes == 5);
            // 沒舉手 → HandleInterjection 在 TurnTo 之前 return,步驟6也不觸發,辯方機 NeckZ 不應被設(維持 0)。
            Check("G5審判-姿態gate沒舉手:不轉頭(NeckZ 未被設,維持 0)",
                System.Math.Abs(defN.GetMotor(KebbiMotor.NeckZ) - 0f) < 0.01f);

            // (7) 學生麥 listen 路徑:注入 SimVoice 當學生麥並 EnqueueHeard → log 走 listen、不影響既有交棒。
            var mic = new SimVoice(noop);
            mic.EnqueueHeard("自訂插話內容");
            string captured = null;
            Action<string> capLog = s => { if (s.Contains("自訂插話內容")) captured = s; };
            var micPro = new SimKebbiBody(noop, true); var micDef = new SimKebbiBody(noop, true);
            var micBus = new SimRobotBus(noop);
            var gm = new App.DebateGame(micPro, micBus.CreateLink("控方"), new SimVoice(noop),
                micDef, micBus.CreateLink("辯方"), new SimVoice(noop), capLog, mic, null);
            gm.RunTrialAsync(App.DebateGame.MakeGalileoTrial()).GetAwaiter().GetResult();
            Check("G5審判-學生麥:listen 路徑取得麥克風內容(非腳本 InterjectionText)", captured != null);
            Check("G5審判-學生麥:不影響既有交棒(Exchanges==2)", gm.Exchanges == 2);

            // (8) 可重入:同實例連跑兩場 MakeGalileoTrial → 第二場辯6(非 12)、Exchanges 2、Interjections 1。
            var gr = NewTrial(out _, out _, out _, out _);
            gr.RunTrialAsync(App.DebateGame.MakeGalileoTrial()).GetAwaiter().GetResult();
            gr.RunTrialAsync(App.DebateGame.MakeGalileoTrial()).GetAwaiter().GetResult();
            Check("G5審判-可重入:第二場票數/插話歸零不累加(辯6、Ex2、Inj1)",
                gr.DefVotes == 6 && gr.Exchanges == 2 && gr.Interjections == 1);

            // (9) 無插話=退化舊行為:TrialScript 不掛插話且 studentMic/poseSensor 皆 null(走舊建構式)
            //     → 票數/回合與直接 RunDebateAsync(MakeGalileoDebate) 完全相同。
            var oldPro = new SimKebbiBody(noop, true); var oldDef = new SimKebbiBody(noop, true);
            var oldBus = new SimRobotBus(noop);
            var gOld = new App.DebateGame(oldPro, oldBus.CreateLink("控方"), new SimVoice(noop),
                oldDef, oldBus.CreateLink("辯方"), new SimVoice(noop), noop); // 舊 7 參數建構式
            gOld.RunTrialAsync(new App.DebateGame.TrialScript { Rounds = App.DebateGame.MakeGalileoDebate() })
                .GetAwaiter().GetResult();
            Check("G5審判-退化:無插話走舊建構式票數同 RunDebate(控2:辯5)",
                gOld.ProVotes == 2 && gOld.DefVotes == 5 && gOld.Exchanges == 2);
            Check("G5審判-退化:無插話則 Interjections==0、仍宣判完成", gOld.Interjections == 0 && gOld.Concluded);

            // (10) 舊建構式仍可呼叫 RunTrialAsync(證明新建構式重載未破壞舊 7 參數簽章)。
            Check("G5審判-舊建構式:仍能跑 RunTrialAsync 並宣判辯方勝", gOld.Verdict == "辯方勝");
        }

        // G4-judge：裁判賽（A 描述 B → Kebbi 用 DOA 真值核對 + 轉頭面向 B）、視角轉換（B 相對 Kebbi vs 相對 A）、
        // 多輪排名（round-robin 逐場累積分、降冪排名、平手以校準序 stable、可重入）。
        // 全程不污染舊 Score（裁判分只進獨立 _matchScores/TournamentRounds）。
        private static void T_G4_Judge()
        {
            // 一個「可逐場腳本化 DOA」的記錄式身體：ReadDoaDegrees() 依序回傳預排角度（給 tournament 多場各自真值）。
            // 同時保留 NeckZ 夾限語意（沿用 SimKebbiBody 的 ±90），故 Faced 在正後方會是 false。
            float[] DoaSeq(params float[] xs) => xs;

            // 建一個已校準 4 生的遊戲（Andi 右90 / Budi 左-90 / Citra 前0 / Dewi 後170）。
            TebakArahGame Calibrated(out SimKebbiBody body, out SimVoice voice)
            {
                var ctx = SilentSim(out var b, out var v);
                var g = new TebakArahGame(ctx);
                void Cal(string n, float a) { b.CurrentDoa = a; v.EnqueueHeard("Saya di sini!"); g.CalibrateOneAsync(n).GetAwaiter().GetResult(); }
                Cal("Andi", 90f); Cal("Budi", -90f); Cal("Citra", 0f); Cal("Dewi", 170f);
                body = b; voice = v;
                return g;
            }

            // ── 1) 裁判賽-正確：A 說 "di kanan"、B 真值 DOA=90(Kanan) → Correct、A 計分；
            //    但 B 在 90° 超出頭部 ±40 → 只能部分面向(Faced=false，這是實機 NeckZ 真實限制) ──
            {
                var g = Calibrated(out var body, out var voice);
                body.CurrentDoa = 90f; voice.EnqueueHeard("Budi di kanan");
                var r = g.JudgeRoundAsync(new TebakArahGame.MatchSpec("Andi", "Budi")).GetAwaiter().GetResult();
                Check("G4裁判-正確: Correct=true", r.Correct);
                Check("G4裁判-正確: 真值=Kanan", r.ActualSector == Dir.Kanan);
                Check("G4裁判-正確: A 計分(_matchScores[Andi]=1)", g.MatchScoreOf("Andi") == 1);
                Check("G4裁判-正確: B 在 90° 超出頭部 ±40 → 只能部分面向(Faced=false)", !r.Faced);
                Check("G4裁判-正確: 不污染舊 Score", g.Score == 0);
            }

            // ── 2) 裁判賽-錯誤：A 說 "di kiri" 但 B 真值 DOA=90(Kanan) → Correct=false、A 不得分 ──
            {
                var g = Calibrated(out var body, out var voice);
                body.CurrentDoa = 90f; voice.EnqueueHeard("Budi di kiri");
                var r = g.JudgeRoundAsync(new TebakArahGame.MatchSpec("Andi", "Budi")).GetAwaiter().GetResult();
                Check("G4裁判-錯誤: Correct=false", !r.Correct);
                Check("G4裁判-錯誤: A 不得分", g.MatchScoreOf("Andi") == 0);
            }

            // ── 3) 裁判賽-頭夾限：B 在正後方 170° → 語言看對錯、但 Faced=false（沿用 KebbiHead 夾限）──
            {
                var g = Calibrated(out var body, out var voice);
                body.CurrentDoa = 170f; voice.EnqueueHeard("Dewi di belakang");
                var r = g.JudgeRoundAsync(new TebakArahGame.MatchSpec("Andi", "Dewi")).GetAwaiter().GetResult();
                Check("G4裁判-夾限: 真值=Belakang", r.ActualSector == Dir.Belakang);
                Check("G4裁判-夾限: 語言對仍 Correct", r.Correct);
                Check("G4裁判-夾限: 頭轉不到正後方 Faced=false", !r.Faced);
            }

            // ── 4) 視角純函式-基本：RelativeDir(observer=0/前, target=90/右) → 觀察者面向 Kebbi 時 target 在其 Kiri ──
            Check("G4視角-Basic: RelativeDir(0,90)=Kiri(我的右=Kebbi的左)", TebakArahGame.RelativeDir(0f, 90f) == Dir.Kiri);
            // ── 5) 視角純函式-同位：RelativeDir(90,90) → Depan(重合扇區) ──
            Check("G4視角-SameSpot: RelativeDir(90,90)=Depan", TebakArahGame.RelativeDir(90f, 90f) == Dir.Depan);
            // ── 6) 視角純函式-釘住約定：observer=0 時 RelativeDir(0,X) 應等同 FromAngle(-X) ──
            {
                bool roundTrip = true;
                foreach (float x in DoaSeq(0f, 30f, 90f, -90f, 135f, 170f))
                    if (TebakArahGame.RelativeDir(0f, x) != Direction.FromAngle(-x)) roundTrip = false;
                Check("G4視角-RoundTripVsKebbi: observer=0 → RelativeDir(0,X)==FromAngle(-X)", roundTrip);
            }

            // ── 7) 視角題-兩者皆對：A(Citra 前0°) 描述 B(Andi 右90°)；B 相對 Kebbi=Kanan、相對 A=Kiri；兩詞皆對 → 得分 ──
            {
                var g = Calibrated(out _, out var voice);
                voice.EnqueueHeard("di kanan");  // 相對 Kebbi（先答）
                voice.EnqueueHeard("di kiri");   // 相對自己（後答）
                var r = g.PerspectiveRoundAsync("Citra", "Andi").GetAwaiter().GetResult();
                Check("G4視角題-兩對: 相對Kebbi真值=Kanan", r.ActualSector == Dir.Kanan);
                Check("G4視角題-兩對: 相對A真值=Kiri", r.PerspectiveSector == Dir.Kiri);
                Check("G4視角題-兩對: Correct=true 且 A 計分", r.Correct && g.MatchScoreOf("Citra") == 1);
            }

            // ── 8) 視角題-講反：A 把『相對自己』與『相對 Kebbi』講反 → 只對一半 → 不得分 ──
            {
                var g = Calibrated(out _, out var voice);
                voice.EnqueueHeard("di kiri");   // 相對 Kebbi 應為 Kanan → 錯
                voice.EnqueueHeard("di kanan");  // 相對自己 應為 Kiri → 錯
                var r = g.PerspectiveRoundAsync("Citra", "Andi").GetAwaiter().GetResult();
                Check("G4視角題-講反: Correct=false", !r.Correct);
                Check("G4視角題-講反: 不得分", g.MatchScoreOf("Citra") == 0);
            }

            // ── 9) 多輪排名：B 全對、A 半對、C 全錯 → Ranking[0]=B、降冪正確 ──
            // 用「可逐場腳本化」的身體與語音：每場各自的 DOA 真值與 A 的方位詞。
            {
                var g = Calibrated(out var body, out var voice);
                // 三人 round-robin（A,B,C）共 3 場：A→B、A→C、B→C（MakeRoundRobin 前者為提問者）。
                var matches = TebakArahGame.MakeRoundRobin(new[] { "A", "B", "C" });
                Check("G4排名-round-robin 3 人=3 場", matches.Count == 3);

                // 用獨立的記錄身體逐場餵 DOA；語音逐場餵方位詞，使 A 半對、B 全對、C 全錯。
                var seqBody = new SeqDoaBody();
                var seqVoice = new SimVoice(_ => { });
                var ctx2 = new KebbiContext(seqBody, seqVoice, new SimLlm(_ => { }), _ => { });
                var g2 = new TebakArahGame(ctx2);
                // 校準 A,B,C（座位隨意，排名只看 _matchScores）。
                void Cal2(string n, float a) { seqBody.Next = a; seqVoice.EnqueueHeard("Saya di sini!"); g2.CalibrateOneAsync(n).GetAwaiter().GetResult(); }
                Cal2("A", 90f); Cal2("B", -90f); Cal2("C", 0f);

                // 場1 A→B：DOA=90(Kanan)，A 答 "kanan" → 對（A 提問者得分）
                // 場2 A→C：DOA=0(Depan)，A 答 "kiri" → 錯（A 不得分）→ A 半對(1/2)
                // 場3 B→C：DOA=0(Depan)，B 答 "depan" → 對（B 得分）→ B 全對(1/1)
                // C 從未當提問者 → C 0 分（全錯/沒分）
                seqBody.Queue(90f); seqVoice.EnqueueHeard("kanan");
                seqBody.Queue(0f); seqVoice.EnqueueHeard("kiri");
                seqBody.Queue(0f); seqVoice.EnqueueHeard("depan");
                g2.RunTournamentAsync(matches).GetAwaiter().GetResult();
                Check("G4排名-跑了 3 場", g2.TournamentRounds == 3);
                Check("G4排名-A 半對得 1 分", g2.MatchScoreOf("A") == 1);
                Check("G4排名-B 全對得 1 分", g2.MatchScoreOf("B") == 1);
                Check("G4排名-C 全錯得 0 分", g2.MatchScoreOf("C") == 0);
                Check("G4排名-降冪: 末位是 C(0 分)", g2.Ranking[g2.Ranking.Count - 1].Name == "C" && g2.Ranking[g2.Ranking.Count - 1].Points == 0);
                Check("G4排名-降冪: 榜首分數最高", g2.Ranking[0].Points >= g2.Ranking[1].Points);
                Check("G4排名-不污染舊 Score", g2.Score == 0);
            }

            // ── 10) 平手 stable：兩人同分 → 名次以校準先後排序（先校準者在前）──
            {
                var seqBody = new SeqDoaBody();
                var seqVoice = new SimVoice(_ => { });
                var g = new TebakArahGame(new KebbiContext(seqBody, seqVoice, new SimLlm(_ => { }), _ => { }));
                void Cal(string n, float a) { seqBody.Next = a; seqVoice.EnqueueHeard("Saya di sini!"); g.CalibrateOneAsync(n).GetAwaiter().GetResult(); }
                Cal("First", 0f); Cal("Second", 0f);  // 校準順序：First 先、Second 後
                var matches = new System.Collections.Generic.List<TebakArahGame.MatchSpec> {
                    new TebakArahGame.MatchSpec("First", "Second"),
                    new TebakArahGame.MatchSpec("Second", "First"),
                };
                // 兩場都答對 → First、Second 各 1 分（平手）。
                seqBody.Queue(0f); seqVoice.EnqueueHeard("depan");
                seqBody.Queue(0f); seqVoice.EnqueueHeard("depan");
                g.RunTournamentAsync(matches).GetAwaiter().GetResult();
                Check("G4平手-兩人同分(各1分)", g.MatchScoreOf("First") == 1 && g.MatchScoreOf("Second") == 1);
                Check("G4平手-stable: 先校準者(First)排前", g.Ranking[0].Name == "First" && g.Ranking[1].Name == "Second");
            }

            // ── 11) 可重入：同實例連跑兩場 RunTournamentAsync → 第二場 _matchScores/TournamentRounds/Ranking 歸零不累加 ──
            {
                var seqBody = new SeqDoaBody();
                var seqVoice = new SimVoice(_ => { });
                var g = new TebakArahGame(new KebbiContext(seqBody, seqVoice, new SimLlm(_ => { }), _ => { }));
                void Cal(string n, float a) { seqBody.Next = a; seqVoice.EnqueueHeard("Saya di sini!"); g.CalibrateOneAsync(n).GetAwaiter().GetResult(); }
                Cal("X", 90f); Cal("Y", -90f);
                var matches = new System.Collections.Generic.List<TebakArahGame.MatchSpec> {
                    new TebakArahGame.MatchSpec("X", "Y"),
                };
                seqBody.Queue(-90f); seqVoice.EnqueueHeard("kiri"); // X→Y, Y 在左 → 對
                g.RunTournamentAsync(matches).GetAwaiter().GetResult();
                Check("G4可重入-第一場: X 得 1 分、1 輪", g.MatchScoreOf("X") == 1 && g.TournamentRounds == 1);
                seqBody.Queue(-90f); seqVoice.EnqueueHeard("kiri");
                g.RunTournamentAsync(matches).GetAwaiter().GetResult();
                Check("G4可重入-第二場歸零不累加(X 仍 1、TournamentRounds 仍 1)", g.MatchScoreOf("X") == 1 && g.TournamentRounds == 1);
            }

            // ── 12) 向後相容：跑完整場 Tournament 後，舊 Score 仍為 0（裁判分只進 _matchScores）──
            {
                var seqBody = new SeqDoaBody();
                var seqVoice = new SimVoice(_ => { });
                var g = new TebakArahGame(new KebbiContext(seqBody, seqVoice, new SimLlm(_ => { }), _ => { }));
                void Cal(string n, float a) { seqBody.Next = a; seqVoice.EnqueueHeard("Saya di sini!"); g.CalibrateOneAsync(n).GetAwaiter().GetResult(); }
                Cal("P", 90f); Cal("Q", -90f);
                seqBody.Queue(-90f); seqVoice.EnqueueHeard("kiri");
                g.RunTournamentAsync(new System.Collections.Generic.List<TebakArahGame.MatchSpec> {
                    new TebakArahGame.MatchSpec("P", "Q") }).GetAwaiter().GetResult();
                Check("G4向後相容-Tournament 後舊 Score 仍 0", g.Score == 0 && g.Rounds == 0);
            }
        }

        // G4 八向方位系統:FromAngle 8 扇區(含斜向 serong)、印尼語複合詞解析、最近可達扇區降級、
        // TurnToward 多載回報「夾限後實際面向扇區」、E2E 斜向題。核心:45° 細粒度下斜向常不可達 → 馬達可達性決定判決。
        private static void T_G4_EightWay()
        {
            Action<string> noop = _ => { };

            // (1) 斜向扇區判決(中心對齊 ±45/±135)
            Check("8向-45°→SerongKanan(右前)", Direction.FromAngle(45) == Dir.SerongKanan);
            Check("8向-135°→BelakangKanan(右後)", Direction.FromAngle(135) == Dir.BelakangKanan);
            Check("8向--135°→BelakangKiri(左後)", Direction.FromAngle(-135) == Dir.BelakangKiri);
            Check("8向--45°→SerongKiri(左前)", Direction.FromAngle(-45) == Dir.SerongKiri);

            // (2) 複合詞解析:不可被單詞 substring 吃掉;單詞仍可
            Check("8向-解析 'serong kanan'(不被 kanan 吃)", Direction.ParseIndo("Saya di serong kanan") == Dir.SerongKanan);
            Check("8向-解析 'belakang kiri'(不被 belakang/kiri 吃)", Direction.ParseIndo("di belakang kiri") == Dir.BelakangKiri);
            Check("8向-解析單詞 'kanan' 仍可", Direction.ParseIndo("di kanan") == Dir.Kanan);
            Check("8向-解析單詞 'belakang' 仍可", Direction.ParseIndo("di belakang") == Dir.Belakang);
            Check("8向-解析 'xyz'→null", Direction.ParseIndo("xyz") == null);

            // (3) 最近可達扇區降級(實機 NeckZ ±40):斜向 45° 夾到 40 仍屬 SerongKanan;Kanan/正後皆只能夾到 serong
            Check("8向-NearestReachable SerongKanan(45°)→夾40 仍 SerongKanan", Direction.NearestReachable(Dir.SerongKanan, -40f, 40f) == Dir.SerongKanan);
            Check("8向-NearestReachable Kanan(90°)→SerongKanan(頭轉不到90)", Direction.NearestReachable(Dir.Kanan, -40f, 40f) == Dir.SerongKanan);
            Check("8向-NearestReachable Belakang(180°)→SerongKanan(夾到+40)", Direction.NearestReachable(Dir.Belakang, -40f, 40f) == Dir.SerongKanan);
            Check("8向-NearestReachable BelakangKiri(-135°)→SerongKiri(夾到-40)", Direction.NearestReachable(Dir.BelakangKiri, -40f, 40f) == Dir.SerongKiri);

            // (4) TurnToward 多載回報「夾限後實際面向扇區」;舊 3 參數多載行為不變(回歸)。SimKebbiBody 實機 NeckZ ±40。
            var body = new SimKebbiBody(noop, canMove: false);
            float f1 = KebbiHead.TurnToward(body, 45f, out bool reach1, out Dir sec1);
            Check("8向-TurnToward(45°):夾到40、不可達、實際扇區=SerongKanan",
                !reach1 && sec1 == Dir.SerongKanan && Math.Abs(f1 - 40f) < 0.01f);
            float f2 = KebbiHead.TurnToward(body, 170f, out bool reach2, out Dir sec2);
            Check("8向-TurnToward(170°正後):不可達、夾到+40、實際扇區=SerongKanan",
                !reach2 && sec2 == Dir.SerongKanan && Math.Abs(f2 - 40f) < 0.01f);
            float f3 = KebbiHead.TurnToward(body, 170f, out bool reach3);
            Check("8向-舊 3 參數多載仍夾到+40、不可達(向後相容)", !reach3 && Math.Abs(f3 - 40f) < 0.01f);

            // (5) E2E 斜向題:校準 doa=45 的學生→Dir=SerongKanan;正向題用 'serong kanan' 答對得分
            var ctx = SilentSim(out var b, out var v);
            var game = new TebakArahGame(ctx);
            b.CurrentDoa = 45f; v.EnqueueHeard("Saya di sini!");
            game.CalibrateOneAsync("Sari").GetAwaiter().GetResult();
            Check("8向-E2E 校準 45°→SerongKanan", game.FindByName("Sari").Dir == Dir.SerongKanan);
            b.CurrentDoa = 45f; v.EnqueueHeard("saya di serong kanan");
            var rr = game.ForwardRoundAsync(Dir.SerongKanan).GetAwaiter().GetResult();
            Check("8向-E2E 正向斜向題:方位詞對+回答者對+得分", rr.LanguageCorrect && rr.RightResponder && game.Score == 1);
        }

        // 可逐場腳本化 DOA 的記錄式身體：給 G4 tournament 多場各自真值（ReadDoaDegrees 依序回傳預排角度）。
        // 保留 NeckZ ±90 夾限語意，與 SimKebbiBody 一致。
        private sealed class SeqDoaBody : IKebbiBody
        {
            private readonly System.Collections.Generic.Dictionary<KebbiMotor, float> _m
                = new System.Collections.Generic.Dictionary<KebbiMotor, float>();
            private readonly System.Collections.Generic.Queue<float> _doa = new System.Collections.Generic.Queue<float>();
            public float Next = 0f;                  // 校準階段用：下次 ReadDoaDegrees 回傳值（無佇列時）
            public void Queue(float deg) => _doa.Enqueue(deg);
            public void SetMotor(KebbiMotor m, float d, float s = 50f) { _m[m] = d; }
            public float GetMotor(KebbiMotor m) => _m.TryGetValue(m, out var v) ? v : 0f;
            public float ReadDoaDegrees() => _doa.Count > 0 ? _doa.Dequeue() : Next;
            public bool CanMove => false;
            public void Move(float mps) { }
            public void Turn(float dps) { }
            public void StopWheels() { }
            public float NeckZMinDeg => -90f;
            public float NeckZMaxDeg => 90f;
        }

        // G3-rewind:逐幀播放 + currentFrame 索引 + RewindOneFrame/HandleAgainAsync(喊「再一次」回退一幀重示範,手冊 step4)。
        // 驗:逐幀化未改對外行為(末幀歸位)、HandleAgainAsync 真的回退並重送前一幀角度、首幀夾住、未開始不丟例外、
        //     RewindOneFrame 純狀態夾限、可重入重置索引,以及 RunRepAsync 內「再一次」攔截(注入才觸發、空佇列向後相容)。
        private static void T_G3_Rewind()
        {
            Action<string> noop = _ => { };

            // (1) 逐幀化未改對外行為:PlayMoveAsync(暖身,3 幀)後 CurrentMove==move、CurrentFrame==2(停末幀)、雙肩歸位 0°
            var ctx1 = SilentSim(out var body1, out _);
            var pose1 = new SimPoseSensor(noop);
            var g1 = new App.MirrorCoachGame(ctx1, pose1);
            var move1 = App.MirrorCoachGame.MakeWarmup();
            g1.PlayMoveAsync(move1).GetAwaiter().GetResult();
            Check("G3rewind-逐幀:CurrentMove==move", ReferenceEquals(g1.CurrentMove, move1));
            Check("G3rewind-逐幀:停在末幀索引 2(共 3 幀)", g1.CurrentFrame == 2);
            Check("G3rewind-逐幀:末幀 RShoulderY 仍歸位 0°", Math.Abs(body1.GetMotor(KebbiMotor.RShoulderY) - 0f) < 0.01f);
            Check("G3rewind-逐幀:末幀 LShoulderY 仍歸位 0°", Math.Abs(body1.GetMotor(KebbiMotor.LShoulderY) - 0f) < 0.01f);

            // (2) HandleAgainAsync 在末幀(2)→ 回 true、CurrentFrame 退到 1、中間幀「雙臂平舉」80° 被重送
            bool r2 = g1.HandleAgainAsync().GetAwaiter().GetResult();
            Check("G3rewind-末幀 HandleAgain 回 true(有回退)", r2);
            Check("G3rewind-HandleAgain 後 CurrentFrame 退到 1", g1.CurrentFrame == 1);
            Check("G3rewind-中間幀角度被重送(RShoulderY≈80)", Math.Abs(body1.GetMotor(KebbiMotor.RShoulderY) - 80f) < 0.01f);

            // (3) 連續再呼叫兩次:1→0(true),已在首幀(false,夾住),且首幀角度 0° 被重送
            bool r3a = g1.HandleAgainAsync().GetAwaiter().GetResult();
            Check("G3rewind-再 HandleAgain:1→0 回 true", r3a && g1.CurrentFrame == 0);
            bool r3b = g1.HandleAgainAsync().GetAwaiter().GetResult();
            Check("G3rewind-首幀 HandleAgain 回 false(夾住停 0)", !r3b && g1.CurrentFrame == 0);
            Check("G3rewind-首幀角度 0° 被重送", Math.Abs(body1.GetMotor(KebbiMotor.RShoulderY) - 0f) < 0.01f);

            // (4) 未開始(剛 new、CurrentFrame==-1、CurrentMove==null)直接 HandleAgainAsync → 回 false、不丟例外、CurrentFrame 仍 -1
            var g4 = new App.MirrorCoachGame(SilentSim(out _, out _), new SimPoseSensor(noop));
            Check("G3rewind-未開始:CurrentFrame==-1、CurrentMove==null", g4.CurrentFrame == -1 && g4.CurrentMove == null);
            bool r4 = g4.HandleAgainAsync().GetAwaiter().GetResult();
            Check("G3rewind-未開始 HandleAgain 回 false、不丟例外、CurrentFrame 仍 -1", !r4 && g4.CurrentFrame == -1);

            // (5) RewindOneFrame 純狀態:PlayMoveAsync 到末幀(2),連呼三次 → true(→1)、true(→0)、false(停 0)
            var g5 = new App.MirrorCoachGame(SilentSim(out _, out _), new SimPoseSensor(noop));
            g5.PlayMoveAsync(App.MirrorCoachGame.MakeWarmup()).GetAwaiter().GetResult();
            Check("G3rewind-RewindOneFrame:2→1 回 true", g5.RewindOneFrame() && g5.CurrentFrame == 1);
            Check("G3rewind-RewindOneFrame:1→0 回 true", g5.RewindOneFrame() && g5.CurrentFrame == 0);
            Check("G3rewind-RewindOneFrame:首幀回 false(夾住停 0)", !g5.RewindOneFrame() && g5.CurrentFrame == 0);

            // (6) 可重入:同實例第二次 PlayMoveAsync → CurrentFrame 重置為 2(非沿用上次回退後的 0)
            g5.PlayMoveAsync(App.MirrorCoachGame.MakeWarmup()).GetAwaiter().GetResult();
            Check("G3rewind-可重入:第二次 PlayMoveAsync 後 CurrentFrame 重置為 2", g5.CurrentFrame == 2);

            // (7) RunRepAsync 走姿態錯誤分支 + 事先注入「再一次」→ 觸發 HandleAgainAsync 重示範(末幀 2 退到 1)
            var ctx7 = SilentSim(out _, out var voice7);
            var pose7 = new SimPoseSensor(noop);
            var g7 = new App.MirrorCoachGame(ctx7, pose7);
            pose7.Enqueue(false);                  // 姿態錯誤 → 進 else 分支
            voice7.EnqueueHeard("再一次");          // 注入學生喊「再一次」
            g7.RunRepAsync(App.MirrorCoachGame.MakeWarmup()).GetAwaiter().GetResult();
            Check("G3rewind-RunRep 攔截:注入『再一次』→ HandleAgain 重示範(CurrentFrame 退到 1)", g7.CurrentFrame == 1);

            // (8) RunRepAsync 姿態錯誤但『不』注入「再一次」(佇列空 ListenAsync 回空字串)→ 不觸發、CurrentFrame 仍停末幀 2
            var pose8 = new SimPoseSensor(noop);
            var g8 = new App.MirrorCoachGame(SilentSim(out _, out _), pose8);
            pose8.Enqueue(false);                  // 姿態錯誤,但不注入語音
            g8.RunRepAsync(App.MirrorCoachGame.MakeWarmup()).GetAwaiter().GetResult();
            Check("G3rewind-向後相容:沒注入『再一次』→ 不誤觸發、CurrentFrame 仍停末幀 2", g8.CurrentFrame == 2);
        }

        // G3 動作幀資料化:JointFrame.HoldMs 自訂單幀停留(覆寫 BPM)、Move.Loops 整組循環。
        // FrameHoldMs/FramesPlayed 可純驗(不真 sleep);Loops=1 且無 HoldMs = 完全等價舊版(向後相容)。
        private static void T_G3_Frame()
        {
            Action<string> noop = _ => { };
            var game = new App.MirrorCoachGame(SilentSim(out _, out _), new SimPoseSensor(noop));

            // (1) FrameHoldMs:無 HoldMs → 依 BPM(預設 60 → 每拍 1000ms)
            var plain = new App.MirrorCoachGame.JointFrame("一般").Set(KebbiMotor.RShoulderY, 0f);
            Check("G3幀-無 HoldMs 依 BPM(60→1000ms)", plain.HoldMs == null && game.FrameHoldMs(plain) == 1000);

            // (2) FrameHoldMs:有 HoldMs → 用自訂值(不受 BPM 影響)
            var held = new App.MirrorCoachGame.JointFrame("停久").Set(KebbiMotor.RShoulderY, 0f).Hold(2000);
            Check("G3幀-自訂 HoldMs=2000 覆寫 BPM", held.HoldMs == 2000 && game.FrameHoldMs(held) == 2000);

            // (3) 混用:同組內自訂與無自訂幀 → 各取各的時長
            Check("G3幀-混用:自訂幀=2000、一般幀=1000", game.FrameHoldMs(held) == 2000 && game.FrameHoldMs(plain) == 1000);

            // (4) 降速:HandleTooFastAsync → BPM 60→45;一般幀跟 BPM(→1333ms),自訂幀不受影響(仍 2000)
            game.HandleTooFastAsync(0f).GetAwaiter().GetResult();
            Check("G3幀-降速後一般幀依新 BPM(45→1333ms)", game.FrameHoldMs(plain) == (int)(60000.0 / 45));
            Check("G3幀-降速後自訂幀仍=2000(不受 BPM)", game.FrameHoldMs(held) == 2000);

            // (5) Move.Loops 預設 1:播一次 → FramesPlayed==幀數、停末幀(等價舊版)
            var g2 = new App.MirrorCoachGame(SilentSim(out _, out _), new SimPoseSensor(noop));
            var warm = App.MirrorCoachGame.MakeWarmup();   // 3 幀
            Check("G3幀-Move.Loops 預設=1", warm.Loops == 1);
            g2.PlayMoveAsync(warm).GetAwaiter().GetResult();
            Check("G3幀-Loops=1:FramesPlayed==3、停末幀 2(等價舊版)", g2.FramesPlayed == 3 && g2.CurrentFrame == 2);

            // (6) Move.Loops=2(Repeat):整組重播兩次 → FramesPlayed==6、末幀索引仍 2
            var g3 = new App.MirrorCoachGame(SilentSim(out _, out _), new SimPoseSensor(noop));
            var warm2 = App.MirrorCoachGame.MakeWarmup().Repeat(2);
            Check("G3幀-Repeat(2) 設 Loops=2", warm2.Loops == 2);
            g3.PlayMoveAsync(warm2).GetAwaiter().GetResult();
            Check("G3幀-Loops=2:FramesPlayed==6、CurrentFrame 末幀 2", g3.FramesPlayed == 6 && g3.CurrentFrame == 2);

            // (7) Repeat 夾住:Repeat(0)/負數 → Loops 至少 1(不會 0 次或負)
            Check("G3幀-Repeat(0) 夾為 1", App.MirrorCoachGame.MakeWarmup().Repeat(0).Loops == 1);

            // (8) RunSessionAsync 內單組 Loops=2 → 該組仍重播兩次(FramesPlayed==6),Reps 仍只 +1
            var g4 = new App.MirrorCoachGame(SilentSim(out _, out _), new SimPoseSensor(noop));
            var routine = new System.Collections.Generic.List<App.MirrorCoachGame.Move> { App.MirrorCoachGame.MakeWarmup().Repeat(2) };
            g4.RunSessionAsync(routine).GetAwaiter().GetResult();
            Check("G3幀-Session 內 Loops=2:FramesPlayed==6、Reps==1", g4.FramesPlayed == 6 && g4.Reps == 1);

            // (9) 可重入:同實例第二次 PlayMoveAsync(Loops=2) → FramesPlayed 歸零重算==6(不累加)
            g3.PlayMoveAsync(App.MirrorCoachGame.MakeWarmup().Repeat(2)).GetAwaiter().GetResult();
            Check("G3幀-可重入:第二次 Loops=2 FramesPlayed 重算==6(不累加)", g3.FramesPlayed == 6);
        }

        // G2 學生自編走位腳本「結構驗證器」:ValidateScript 對證明步驟序列把關
        // (缺步/多步/層次錯置/順序逆置/缺標層),通過才放行 RunProofIfValidAsync 接力。
        // 純加法、向後相容:既有 RunProofAsync 不經 validator 仍可直接跑。
        private static void T_G2_Validator()
        {
            Action<string> noop = _ => { };

            // 標準解(等腰底角學習單版):3 步,Layer 序列 = 已知/因為/所以。
            var expected = App.GeometryRelayGame.MakeIsoscelesProofWorksheet();

            // 以標準解為基底複製一份學生腳本(深拷貝 Step,改 Layer 時不污染標準解)。
            System.Func<App.GeometryRelayGame.Step, App.GeometryRelayGame.Step> copy =
                s => new App.GeometryRelayGame.Step(s.Reason, s.Edge, s.ArmAngle, s.Layer);
            System.Func<System.Collections.Generic.List<App.GeometryRelayGame.Step>> clone =
                () => { var l = new System.Collections.Generic.List<App.GeometryRelayGame.Step>();
                        foreach (var s in expected) l.Add(copy(s)); return l; };

            // 1) 標準解通過。
            var okChk = App.GeometryRelayGame.ValidateScript(App.GeometryRelayGame.MakeIsoscelesProofWorksheet(), expected);
            Check("G2腳本-標準解通過(Ok=true、Error=None)",
                okChk.Ok && okChk.Error == App.GeometryRelayGame.ScriptError.None);

            // 2) 缺步:只給前 2 步 → MissingStep、指第 3 步。
            var missing = clone(); missing.RemoveAt(2);
            var mChk = App.GeometryRelayGame.ValidateScript(missing, expected);
            Check("G2腳本-缺步:Error=MissingStep、Step=3",
                !mChk.Ok && mChk.Error == App.GeometryRelayGame.ScriptError.MissingStep && mChk.Step == 3);

            // 3) 多步:標準解 3 步再 append 一步 → ExtraStep、指第 4 步。
            var extra = clone(); extra.Add(copy(expected[2]));
            var eChk = App.GeometryRelayGame.ValidateScript(extra, expected);
            Check("G2腳本-多步:Error=ExtraStep、Step=4",
                !eChk.Ok && eChk.Error == App.GeometryRelayGame.ScriptError.ExtraStep && eChk.Step == 4);

            // 4) 層次錯置:第 2 步 Layer『因為』改成『所以』(不造成倒退,故走 WrongLayer)。
            var wrongLayer = clone(); wrongLayer[1].Layer = "所以";
            var wlChk = App.GeometryRelayGame.ValidateScript(wrongLayer, expected);
            Check("G2腳本-層次錯置:Error=WrongLayer、Step=2、訊息含『應為『因為』』",
                !wlChk.Ok && wlChk.Error == App.GeometryRelayGame.ScriptError.WrongLayer
                && wlChk.Step == 2 && wlChk.Message.Contains("應為『因為』"));

            // 5) 順序逆置:把第 2(因為)與第 3(所以)步整步對調 → 第 2 步變『所以』、第 3 步變『因為』,
            //    第 3 步層次從『所以』倒退回『因為』被攔(WrongOrder),但題庫單線性解下第 2 步已先被 WrongLayer 攔。
            //    依規格此案可接受 WrongOrder 或 WrongLayer,且都指第 2 步。
            var swapped = clone();
            var tmp = swapped[1]; swapped[1] = swapped[2]; swapped[2] = tmp;
            var sChk = App.GeometryRelayGame.ValidateScript(swapped, expected);
            Check("G2腳本-順序逆置:Error=WrongOrder 或 WrongLayer、Step=2",
                !sChk.Ok && sChk.Step == 2
                && (sChk.Error == App.GeometryRelayGame.ScriptError.WrongOrder
                    || sChk.Error == App.GeometryRelayGame.ScriptError.WrongLayer));

            // 5b) 純逆序攔截:用一份「同層可重複」的標準解(已知→因為→因為),學生在第 3 步把層次倒退回『已知』
            //     → 在比對 WrongLayer 之前先被 WrongOrder(層次單調遞增) 攔下、指第 3 步。釘住「層次不可倒退」語意。
            var monoExpected = new System.Collections.Generic.List<App.GeometryRelayGame.Step>
            {
                new App.GeometryRelayGame.Step("已知條件", "AB", 60f, "已知"),
                new App.GeometryRelayGame.Step("推論一", "AD", 30f, "因為"),
                new App.GeometryRelayGame.Step("推論二", "CD", 40f, "因為"),
            };
            var orderBad = new System.Collections.Generic.List<App.GeometryRelayGame.Step>
            {
                new App.GeometryRelayGame.Step("已知條件", "AB", 60f, "已知"),
                new App.GeometryRelayGame.Step("推論一", "AD", 30f, "因為"),
                new App.GeometryRelayGame.Step("倒退回已知", "CD", 40f, "已知"), // 第 3 步層次倒退
            };
            var obChk = App.GeometryRelayGame.ValidateScript(orderBad, monoExpected);
            Check("G2腳本-層次倒退攔截:Error=WrongOrder、Step=3",
                !obChk.Ok && obChk.Error == App.GeometryRelayGame.ScriptError.WrongOrder && obChk.Step == 3);

            // 6) 缺標層:某步 Layer 設為 null → MissingLayer、指該步號。
            var noLayer = clone(); noLayer[1].Layer = null;
            var nlChk = App.GeometryRelayGame.ValidateScript(noLayer, expected);
            Check("G2腳本-缺標層:Error=MissingLayer、Step=2",
                !nlChk.Ok && nlChk.Error == App.GeometryRelayGame.ScriptError.MissingLayer && nlChk.Step == 2);

            // 7) 通過才接力:RunProofIfValidAsync(正確腳本) → Ok=true 且 StepsDone==3。
            var bus = new SimRobotBus(noop);
            var voice = new SimVoice(noop);
            voice.EnqueueHeard("已知"); voice.EnqueueHeard("因為"); voice.EnqueueHeard("所以"); // 學習單作答
            var game = new App.GeometryRelayGame(new SimKebbiBody(noop, true),
                bus.CreateLink("甲機"), bus.CreateLink("乙機"), voice, noop);
            var runOk = game.RunProofIfValidAsync(App.GeometryRelayGame.MakeIsoscelesProofWorksheet(), expected)
                            .GetAwaiter().GetResult();
            Check("G2腳本-通過才接力:Ok=true、StepsDone=3", runOk.Ok && game.StepsDone == 3);

            // 8) 不過不接力:RunProofIfValidAsync(缺步腳本) → Ok=false 且 StepsDone==0(沒跑接力)。
            var bus2 = new SimRobotBus(noop);
            var game2 = new App.GeometryRelayGame(new SimKebbiBody(noop, true),
                bus2.CreateLink("甲機"), bus2.CreateLink("乙機"), new SimVoice(noop), noop);
            var badStudent = App.GeometryRelayGame.MakeIsoscelesProofWorksheet();
            badStudent.RemoveAt(2); // 只剩 2 步
            var runBad = game2.RunProofIfValidAsync(badStudent, expected).GetAwaiter().GetResult();
            Check("G2腳本-不過不接力:Ok=false、StepsDone=0",
                !runBad.Ok && game2.StepsDone == 0);

            // 9) 向後相容:既有 RunProofAsync 不經 validator 仍可直接跑、StepsDone==3。
            var bus3 = new SimRobotBus(noop);
            var game3 = new App.GeometryRelayGame(new SimKebbiBody(noop, true),
                bus3.CreateLink("甲機"), bus3.CreateLink("乙機"), new SimVoice(noop), noop);
            game3.RunProofAsync(App.GeometryRelayGame.MakeIsoscelesProof()).GetAwaiter().GetResult();
            Check("G2腳本-向後相容:RunProofAsync 直接跑 StepsDone=3", game3.StepsDone == 3);

            // 10) null 輸入不丟例外:ValidateScript(null, expected) → Ok=false、MissingStep。
            var nullChk = App.GeometryRelayGame.ValidateScript(null, expected);
            Check("G2腳本-null 輸入不丟例外:Ok=false、Error=MissingStep",
                !nullChk.Ok && nullChk.Error == App.GeometryRelayGame.ScriptError.MissingStep);
        }

    }
}
