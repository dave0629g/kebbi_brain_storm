using System;
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
            T_RobotLink();
            T_RobotLinkProtocol();
            T_RemoteBody();
            T_Direction_Edges();
            T_HeadClamp_Edges();
            T_BodyCommand_Edges();
            T_G2_GeometryRelay();
            T_G5_Debate();
            T_G1_RelayQuest();

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
    }
}
