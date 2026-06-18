using System;
using System.Threading.Tasks;
using KebbiBrain.App;
using KebbiBrain.Hardware;
using KebbiBrain.Sim;

namespace KebbiBrain
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            bool test = Array.IndexOf(args, "--test") >= 0;
            bool cloudTest = Array.IndexOf(args, "--cloud-test") >= 0;
            bool targetReal = Array.IndexOf(args, "--target") >= 0 && Array.IndexOf(args, "real") >= 0;
            bool targetCloud = Array.IndexOf(args, "--target") >= 0 && Array.IndexOf(args, "cloudsim") >= 0;

            bool g3 = Array.IndexOf(args, "--g3") >= 0;

            if (test) return Tests.RunAll();

            if (Array.IndexOf(args, "--menu") >= 0 || Array.IndexOf(args, "--help") >= 0) { PrintMenu(); return 0; }

            if (g3) { await PlayG3DemoAsync(); return 0; }

            if (Array.IndexOf(args, "--link") >= 0) { await PlayLinkDemoAsync(); return 0; }

            if (Array.IndexOf(args, "--g2") >= 0) { await PlayG2DemoAsync(); return 0; }

            if (Array.IndexOf(args, "--g5") >= 0) { await PlayG5DemoAsync(); return 0; }

            if (Array.IndexOf(args, "--g1") >= 0) { await PlayG1DemoAsync(); return 0; }

#if !UNITY
            if (cloudTest) return await Cloud.CloudCheck.RunAsync(Console.WriteLine);
#endif

            if (targetReal)
            {
                try { KebbiFactory.Create(RobotTarget.Real, Console.WriteLine); }
                catch (Exception e) { Console.WriteLine("[target=real] " + e.Message); }
                return 0;
            }

            await PlayDemoAsync(cloud: targetCloud);
            return 0;
        }

        // 列出所有主控台命令（--menu / --help）。
        private static void PrintMenu()
        {
            Console.WriteLine("KebbiBrain 主控台命令：");
            Console.WriteLine("  (無參數)          G4《Tebak Arah》印尼語方位 文字模擬器 Demo");
            Console.WriteLine("  --g1              G1 雙機接力闖關");
            Console.WriteLine("  --g2              G2 幾何證明接力站");
            Console.WriteLine("  --g3              G3 鏡像體操教練");
            Console.WriteLine("  --g5              G5 法庭辯論劇場");
            Console.WriteLine("  --link            多機協作(雙機交棒 + 合體彩蛋廣播)");
            Console.WriteLine("  --test            跑自我測試(全綠)");
            Console.WriteLine("  --cloud-test      雲端自測(需 Azure/OpenAI 金鑰)");
            Console.WriteLine("  --target cloudsim 真雲端跑 G4 整場 Demo(需金鑰)");
            Console.WriteLine("  --target real     實機守門(僅 Unity 建置可用)");
            Console.WriteLine("  --menu / --help   顯示本選單");
        }

        // 跑一段完整劇本。cloud=false → 純文字模擬器(免金鑰)；cloud=true → 真 Azure 印尼語語音 + 真 Claude。
        private static async Task PlayDemoAsync(bool cloud)
        {
            Action<string> log = Console.WriteLine;
            var body = new SimKebbiBody(log, canMove: false); // G4 用 H201 即可（不需移動）

            IVoice voice;
            ILlm llm;
            Action<string> enqueueHeard;

#if !UNITY
            if (cloud)
            {
                if (string.IsNullOrEmpty(Config.SpeechKey) || string.IsNullOrEmpty(Config.SpeechRegion))
                {
                    log("[cloudsim] 缺少 KEBBI_SPEECH_KEY/KEBBI_SPEECH_REGION，改用純模擬器。見 金鑰申請步驟.md");
                    cloud = false;
                }
            }
            Cloud.AzureVoice azVoice = null;
            if (cloud)
            {
                var speech = new Cloud.AzureSpeech(Config.SpeechKey, Config.SpeechRegion);
                azVoice = new Cloud.AzureVoice(speech, Config.SpeechVoice, log);
                voice = azVoice;
                llm = KebbiFactory.CreateLlm(log);
                enqueueHeard = azVoice.EnqueueHeard;
                log("========== G4《Tebak Arah》真雲端 Demo（Azure " + Config.SpeechVoice + " + LLM 見下行） ==========");
            }
            else
#endif
            {
                var simVoice = new SimVoice(log);
                voice = simVoice;
                llm = new SimLlm(log);
                enqueueHeard = simVoice.EnqueueHeard;
                log("========== G4《Tebak Arah》文字模擬器 Demo ==========");
            }

            var ctx = new KebbiContext(body, voice, llm, log);
            var game = new TebakArahGame(ctx);

            log("（▶ 校準階段：四位學生分坐機器人四周）");
            await CalibrateAsync(game, body, enqueueHeard, "Andi", 90);   // 右
            await CalibrateAsync(game, body, enqueueHeard, "Budi", -90);  // 左
            await CalibrateAsync(game, body, enqueueHeard, "Citra", 0);   // 前
            await CalibrateAsync(game, body, enqueueHeard, "Dewi", 170);  // 後

            log("\n（▶ 正向題 1：問『誰在我右邊?』Andi(右)正確回答）");
            await ForwardAsync(game, body, enqueueHeard, Dir.Kanan, 90, "Saya di kanan!");

            log("\n（▶ 正向題 2：問『誰在我左邊?』學生用錯方位詞 → 觸發 LLM 糾錯）");
            await ForwardAsync(game, body, enqueueHeard, Dir.Kiri, -90, "Saya di kanan!");

            log("\n（▶ 正向題 3：問『誰在我後面?』Dewi(後)正確，但頭轉不到正後方 → 夾限警告）");
            await ForwardAsync(game, body, enqueueHeard, Dir.Belakang, 170, "Saya di belakang!");

            log("\n（▶ 反向題：頭轉向 Citra(前)，她說出自己的方位）");
            enqueueHeard("Saya di depan.");
            await game.ReverseRoundAsync(game.FindByName("Citra"));

            game.PrintSummary();
            log("====================================================");
        }

        // G1《雙機接力闖關》Demo（指令序列 → 雙機地板接力，純 Sim）
        private static async Task PlayG1DemoAsync()
        {
            Action<string> log = Console.WriteLine;
            var bodyA = new SimKebbiBody(log, canMove: true);
            var bodyB = new SimKebbiBody(log, canMove: true);
            var bus = new SimRobotBus(log);
            var game = new RelayQuestGame(bodyA, bus.CreateLink("Kebbi-A"), bodyB, bus.CreateLink("Kebbi-B"), log);

            log("========== G1《雙機接力闖關》Demo（指令序列 → 雙機地板接力） ==========");
            log("（程式：A 走 3 格＋轉彎 → 交棒 → B 走 2 格到終點）");
            await game.RunProgramAsync(RelayQuestGame.MakeSampleProgram());
            log("\n=== 走 " + game.Steps + " 格、抵達終點=" + game.ReachedGoal + " ===");
            log("====================================================");
        }

        // G5《法庭辯論劇場》Demo（雙機接力 + 中央逼近 + 轉向學生，純 Sim）
        private static async Task PlayG5DemoAsync()
        {
            Action<string> log = Console.WriteLine;
            var proBody = new SimKebbiBody(log, canMove: true);
            var defBody = new SimKebbiBody(log, canMove: true);
            var bus = new SimRobotBus(log);
            var game = new DebateGame(
                proBody, bus.CreateLink("控方-Kebbi"), new SimVoice(log),
                defBody, bus.CreateLink("辯方-Kebbi"), new SimVoice(log), log);

            log("========== G5《法庭辯論劇場》Demo（伽利略審判） ==========");
            int n = 0;
            foreach (var ex in DebateGame.MakeGalileoDebate())
            {
                log("\n（▶ 第 " + (++n) + " 回合接力辯論）");
                await game.RunExchangeAsync(ex.Pro, ex.Def);
            }
            log("\n（▶ 爭點白熱化：兩機向中央逼近對峙）");
            await game.ApproachCenterAsync();
            log("\n（▶ 學生席右後方有人舉手 → 控方機轉頭面向）");
            await game.TurnToStudentAsync(true, 120f);

            log("\n=== 完成 " + game.Exchanges + " 回合辯論、" + game.CenterApproaches + " 次中央逼近 ===");
            log("====================================================");
        }

        // G2《幾何證明接力站》Demo（雙機接力，純 Sim）
        private static async Task PlayG2DemoAsync()
        {
            Action<string> log = Console.WriteLine;
            var guideBody = new SimKebbiBody(log, canMove: true); // 甲機需走位(輪式)
            var bus = new SimRobotBus(log);
            var gLink = bus.CreateLink("甲機(指引)");
            var rLink = bus.CreateLink("乙機(推理)");
            var voice = new SimVoice(log);
            var game = new GeometryRelayGame(guideBody, gLink, rLink, voice, log);

            log("========== G2《幾何證明接力站》Demo（雙機說—走—指—接棒） ==========");
            log("（題目：證明等腰三角形兩底角相等；學生站進地面大圖，跟著兩機接力）");
            await game.RunProofAsync(GeometryRelayGame.MakeIsoscelesProof());
            log("");
            log("=== 完成 " + game.StepsDone + " 步證明接力 ===");
            log("====================================================");
        }

        // 多機協作 Demo（行程內 loopback，模擬 3 台 Kebbi）
        private static async Task PlayLinkDemoAsync()
        {
            Action<string> log = Console.WriteLine;
            var bus = new SimRobotBus(log);
            var a = bus.CreateLink("Kebbi-A");
            var b = bus.CreateLink("Kebbi-B");
            var c = bus.CreateLink("Kebbi-C"); // 中控/導演

            // 統一處理：HANDOFF→回 ACK；CUE→回 READY；中控收 READY/ACK 印出
            b.OnMessage((from, t) =>
            {
                if (t.StartsWith("HANDOFF")) { log("   ✋ B 收到交棒，啟動接力後半段"); b.SendAsync(from, "ACK " + t); }
                else if (t.StartsWith("CUE")) b.SendAsync(from, "READY Kebbi-B");
            });
            a.OnMessage((from, t) =>
            {
                if (t.StartsWith("ACK")) log("   ✔ A 收到 B 的 ACK → A 停、B 起步");
                else if (t.StartsWith("CUE")) a.SendAsync(from, "READY Kebbi-A");
            });
            c.OnMessage((from, t) => { if (t.StartsWith("READY")) log("   🎬 中控收到 " + t); });

            log("========== 多機協作 Demo（雙機交棒 + 合體彩蛋廣播） ==========");
            log("（▶ G1/G2 式雙機交棒：A 跑完關卡1 → 交棒給 B）");
            await a.SendAsync("Kebbi-B", "HANDOFF#1(關卡1完成)");

            log("\n（▶ 合體彩蛋：中控 C 廣播 cue，各機回報 READY）");
            await c.BroadcastAsync("CUE: 全體走位到舞台中央");

            log("\n（說明：實機把 SimRobotBus 換成 socket/ConnectionManager 即可；務必先做雙機收送實測）");
            log("====================================================");
        }

        // G3《鏡像體操教練》文字模擬器 Demo（純 Sim，免金鑰）
        private static async Task PlayG3DemoAsync()
        {
            Action<string> log = Console.WriteLine;
            var body = new SimKebbiBody(log, canMove: false);
            var pose = new SimPoseSensor(log);
            var ctx = new KebbiContext(body, new SimVoice(log), new SimLlm(log), log);
            var game = new MirrorCoachGame(ctx, pose);
            var move = MirrorCoachGame.MakeWarmup();

            log("========== G3《鏡像體操教練》文字模擬器 Demo ==========");
            log("（▶ 第 1 拍：學生跟對）");
            pose.Enqueue(true);
            await game.RunRepAsync(move);

            log("\n（▶ 左後方學生喊「太快了！」→ Kebbi 轉頭面向 + 放慢）");
            body.CurrentDoa = -120;
            await game.HandleTooFastAsync(body.ReadDoaDegrees());

            log("\n（▶ 第 2 拍：放慢後學生還沒跟上）");
            pose.Enqueue(false);
            await game.RunRepAsync(move);

            log("\n（▶ 第 3 拍：跟上了）");
            pose.Enqueue(true);
            await game.RunRepAsync(move);

            game.PrintSummary();
            log("====================================================");
        }

        private static async Task CalibrateAsync(TebakArahGame g, SimKebbiBody b, Action<string> enqueue, string name, float angle)
        {
            b.CurrentDoa = angle; enqueue("Saya di sini!");
            await g.CalibrateOneAsync(name);
        }

        private static async Task ForwardAsync(TebakArahGame g, SimKebbiBody b, Action<string> enqueue, Dir asked, float angle, string spoken)
        {
            b.CurrentDoa = angle; enqueue(spoken);
            await g.ForwardRoundAsync(asked);
        }
    }
}
