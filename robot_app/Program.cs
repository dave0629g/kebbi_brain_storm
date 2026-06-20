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

            if (Array.IndexOf(args, "--rv") >= 0) { await PlayRemoteVoiceDemoAsync(); return 0; }

            if (Array.IndexOf(args, "--finale") >= 0) { await PlayFinaleDemoAsync(); return 0; }

            if (Array.IndexOf(args, "--g5t") >= 0) { await PlayG5TrialDemoAsync(); return 0; }

            if (Array.IndexOf(args, "--g4t") >= 0) { await PlayG4TournamentDemoAsync(); return 0; }

            if (Array.IndexOf(args, "--g4e") >= 0) { await PlayG4EightWayDemoAsync(); return 0; }

            if (Array.IndexOf(args, "--face") >= 0) { PlayFaceDemo(); return 0; }

            if (Array.IndexOf(args, "--g3r") >= 0) { await PlayG3RewindDemoAsync(); return 0; }

            if (Array.IndexOf(args, "--g3f") >= 0) { await PlayG3FrameDemoAsync(); return 0; }

            if (Array.IndexOf(args, "--g2v") >= 0) { await PlayG2ValidatorDemoAsync(); return 0; }

            if (Array.IndexOf(args, "--g2h") >= 0) { await PlayG2TurnHeadDemoAsync(); return 0; }

#if !UNITY
            if (Array.IndexOf(args, "--counselor") >= 0) { await App.Counselor.CounselorDemo.RunAsync(); return 0; }
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
            Console.WriteLine("  --rv              示範:多機遠端語音(被控機用自己的喇叭說台詞 + 手勢)");
            Console.WriteLine("  --finale          示範:合體彩蛋多機接力大舞台(含離線站降級跳過)");
            Console.WriteLine("  --g5t             G5 七步審判驅動器 + 學生席舉手插話分支");
            Console.WriteLine("  --g4t             G4 裁判賽多輪排名（視角轉換 + 聲源核對）");
            Console.WriteLine("  --g4e             G4 八向方位（含斜向 serong）+ NeckZ 可達性決定實際面向扇區");
            Console.WriteLine("  --face            複合面向 FaceFully（底盤 turn 轉粗方向 + NeckZ±40 補細，連正後也能完整面向）");
            Console.WriteLine("  --g3r             G3 鏡像教練:逐幀回退(喊「再一次」回退一幀重示範,手冊 step4)");
            Console.WriteLine("  --g3f             G3 動作幀資料化:單幀自訂停留 HoldMs + 整組循環 Move.Loops");
            Console.WriteLine("  --g2v             G2 學生自編腳本驗證(結構把關:缺步/層次錯置→才放行接力)");
            Console.WriteLine("  --g2h             G2 甲機轉頭望向發言者(NeckZ 視線跟隨被討論圖素,再手臂指認)");
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

        // G1《雙機接力闖關》Demo（指令序列 → 雙機地板接力 + 避障，純 Sim）
        // 演手冊命脈:同一張關卡,改指令 → 撞牆失敗 vs 繞行成功,「程式修改 → 物理結果改變」立即可見。
        private static async Task PlayG1DemoAsync()
        {
            Action<string> log = Console.WriteLine;
            var map = LevelMap.Level1();
            RelayQuestGame NewGame()
            {
                var bus = new SimRobotBus(log);
                return new RelayQuestGame(new SimKebbiBody(log, true), bus.CreateLink("Kebbi-A"),
                    new SimKebbiBody(log, true), bus.CreateLink("Kebbi-B"), log, map,
                    new SimVoice(log), new SimVoice(log)); // 交棒「換你/收到」語音 + 舉手手勢
            }

            log("========== G1《雙機接力闖關》Demo（指令序列 → 雙機接力 + 避障） ==========");
            log("關卡地圖（A 從 S 出發、面向右，要繞過 # 到 G）：");
            log(map.Render(map.StartR, map.StartC, 'A'));

            // 同一台「學生的機器人」連續嘗試,累計嘗試次數/撞牆次數 → 結算給效率星等。
            var g = NewGame();
            log("\n【第一回 ▶ 錯誤版：直直往前 FWD FWD FWD…】");
            await g.RunProgramAsync(LevelMap.CrashProgram());
            log("→ 結果：撞牆=" + g.Crashed + "、抵達=" + g.ReachedGoal + "（回去改積木！）");

            log("\n【第二回 ▶ 修正版：右轉繞過障礙 + 交棒接力】");
            await g.RunProgramAsync(LevelMap.DetourProgram());
            log("→ 結果：走 " + g.Steps + " 格、抵達=" + g.ReachedGoal + " 🎉 修好了！沒撞牆、安全繞過、成功破關");
            g.PrintSummary();

            log("\n【第三回 ▶ 進階版：用條件積木 IF_OBSTACLE 讓程式自己偵測前方障礙就繞】");
            var g3 = NewGame();
            await g3.RunProgramAsync(LevelMap.SmartProgram());
            log("→ 結果：抵達=" + g3.ReachedGoal + "（程式自動判斷避障，不用人工算路線）");

            // 關卡2(轉彎走廊 + 交接點 H):示範「站對交接點才能交棒」的同步條件。
            var map2 = LevelMap.Level2();
            RelayQuestGame NewGame2()
            {
                var bus = new SimRobotBus(log);
                return new RelayQuestGame(new SimKebbiBody(log, true), bus.CreateLink("Kebbi-A"),
                    new SimKebbiBody(log, true), bus.CreateLink("Kebbi-B"), log, map2,
                    new SimVoice(log), new SimVoice(log));
            }
            log("\n【關卡2 ▶ 交接點同步條件(H=交接點，A 要走到 H 才能交棒)】");
            log(map2.Render(map2.StartR, map2.StartC, 'A'));
            log("— 反例：A 只走 1 格就交棒 —");
            var e = NewGame2();
            await e.RunProgramAsync(LevelMap.Level2HandoffTooEarlyProgram());
            log("→ 交棒失敗=" + e.HandoffFailed + "、抵達=" + e.ReachedGoal + "（沒站對交接點，B 不啟動）");
            log("— 正解：A 走到 H 才交棒（含『換你/收到』語音+舉手） —");
            var s = NewGame2();
            await s.RunProgramAsync(LevelMap.Level2DetourProgram());
            log("→ 交棒失敗=" + s.HandoffFailed + "、抵達=" + s.ReachedGoal + " 🎉");

            // 關卡3(多障礙 S 形強制路徑 + 交接點 H):錯一步就撞牆,唯一最短路才 3 星。
            var map3 = LevelMap.Level3();
            RelayQuestGame NewGame3()
            {
                var bus = new SimRobotBus(log);
                return new RelayQuestGame(new SimKebbiBody(log, true), bus.CreateLink("Kebbi-A"),
                    new SimKebbiBody(log, true), bus.CreateLink("Kebbi-B"), log, map3,
                    new SimVoice(log), new SimVoice(log));
            }
            log("\n【關卡3 ▶ 多障礙 S 形強制路徑(障礙更密，只有一條最短路)】");
            log(map3.Render(map3.StartR, map3.StartC, 'A'));
            log("— 反例：沿上排直直走到底再右轉下行 → 撞 # —");
            var c3 = NewGame3();
            await c3.RunProgramAsync(LevelMap.Level3CrashProgram());
            log("→ 撞牆=" + c3.Crashed + "、抵達=" + c3.ReachedGoal + "（上排是死路，要先往下繞）");
            log("— 正解：下繞 → 橫越中排到 H 交棒 → 下到 G(7 步即最短) —");
            var d3 = NewGame3();
            await d3.RunProgramAsync(LevelMap.Level3DetourProgram());
            log("→ 走 " + d3.Steps + " 格、抵達=" + d3.ReachedGoal + "、交棒成功=" + d3.OnRobotB + " 🎉");
            d3.PrintSummary();

            log("\n=== 重點：撞牆失敗vs繞行成功、效率星等結算、交接點同步(站對才交棒)、交棒換你語音+舉手手勢、多障礙 S 形關 ===");
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

            log("========== G5《法庭辯論劇場》Demo（伽利略審判 + 結辯投票計分） ==========");
            int n = 0;
            foreach (var ex in DebateGame.MakeGalileoDebate())
            {
                log("\n（▶ 第 " + (++n) + " 回合接力辯論 → 學生投票）");
                await game.RunExchangeAsync(ex.Pro, ex.Def, ex.ProVotes, ex.DefVotes);
            }
            log("\n（▶ 爭點白熱化：兩機向中央逼近對峙）");
            await game.ApproachCenterAsync();
            log("\n（▶ 學生席右後方有人舉手 → 控方機轉頭面向）");
            await game.TurnToStudentAsync(true, 120f);
            log("\n（▶ 結辯宣判：票多者勝、勝方舉手致意）");
            await game.ConcludeAsync();

            log("");
            game.PrintSummary();
            log("====================================================");
        }

        // 示範小遊戲:多機「遠端語音」(RemoteVoiceProxy)——把 G5 辯論的「辯方」換成遠端被控機。
        // 重點:DebateGame 程式『一字不改』,只把 defBody/defVoice 換成 Remote*Proxy → 辯方台詞改由「被控機自己的喇叭」說出。
        // 對應 進度追蹤.md TODO#1「對方機要自己說話」;實機把 SimRobotBus 換成 UnityRobotLink(UDP)即同款跑法。
        private static async Task PlayRemoteVoiceDemoAsync()
        {
            Action<string> log = Console.WriteLine;

            // ── 被控機(辯方):用「自己的」喇叭/機身,掛 BodyCommandReceiver 收中控的 VC|(語音)/BC|(機身)命令 ──
            Action<string> devLog = s => log("      [辯方機·被控] " + s);
            var realBus = new SimRobotBus(log);                 // 模擬中控↔被控的網路(實機=UDP 廣播)
            var devLink = realBus.CreateLink("辯方機-被控");
            var devVoice = new SimVoice(devLog);
            var devBody = new SimKebbiBody(devLog, canMove: true);
            new BodyCommandReceiver(devLink, devBody,
                (from, t) => log("      [辯方機·被控] 非命令訊息(" + from + "): " + t), devVoice);

            // ── 中控機(控方):本機自己的喇叭/機身;辯方則用 Remote*Proxy 經網路驅動被控機 ──
            Action<string> proLog = s => log("      [控方機·中控] " + s);
            var dirLink = realBus.CreateLink("控方機-中控");
            var proBody = new SimKebbiBody(proLog, canMove: true);
            var proVoice = new SimVoice(proLog);
            var remoteDefBody = new RemoteBodyProxy(dirLink, "辯方機-被控", canMove: true);
            var remoteDefVoice = new RemoteVoiceProxy(dirLink, "辯方機-被控", log);

            // 遊戲內部「控方↔辯方」事件交棒(YOUR_TURN/BACK)走本機 loopback(不出網路、靜音不洗版);只有機身/語音命令才出網路。
            var localBus = new SimRobotBus(_ => { });
            var game = new DebateGame(
                proBody, localBus.CreateLink("控方"), proVoice,
                remoteDefBody, localBus.CreateLink("辯方"), remoteDefVoice, log);

            log("========== 示範:多機「遠端語音」(RemoteVoiceProxy) ==========");
            log("設定:控方=中控本機;辯方=遠端被控機。DebateGame 程式『一字不改』,");
            log("      但辯方台詞與手勢會從『被控機』那台執行(下方有 [辯方機·被控] 標記者即是)。");
            log("      你會看到 VC|SAY 命令越過『網路』(📡/📨)抵達被控機,再由它自己的喇叭說出。\n");

            int n = 0;
            foreach (var ex in DebateGame.MakeGalileoDebate())
            {
                log("（▶ 第 " + (++n) + " 回合:控方陳述 → 交棒 → 辯方『自己開口』反駁）");
                await game.RunExchangeAsync(ex.Pro, ex.Def);
                log("");
            }
            log("（▶ 爭點白熱化:兩機向中央逼近——辯方機也經遠端機身命令移動）");
            await game.ApproachCenterAsync();

            // ── 後記:「說完才交棒」握手(VC|DONE)── 中控送台詞 → 等被控機回 VC|DONE(播畢)→ 才往下,真機不搶拍。
            log("\n【後記 ▶「說完才交棒」握手:中控等被控機回 VC|DONE(播畢)才繼續】");
            var hsBus = new SimRobotBus(log);
            Action<string> hsDevLog = s => log("      [辯方機·被控] " + s);
            var hsDevLink = hsBus.CreateLink("辯方機-被控2");
            new BodyCommandReceiver(hsDevLink, new SimKebbiBody(hsDevLog, canMove: true),
                null, new SimVoice(hsDevLog), ackVoiceDone: true); // 說完回 VC|DONE
            var hsDirLink = hsBus.CreateLink("控方機-中控2");
            var hsAwaiter = new LinkAwaiter(hsDirLink);
            var hsVoice = new RemoteVoiceProxy(hsDirLink, "辯方機-被控2", log, hsAwaiter, doneTimeoutMs: 1000);
            log("控方機:請辯方機說一句…(會『等』它回報播畢才往下)");
            await hsVoice.SpeakAsync("辯方：抗議，這違反程序正義！", "zh-TW");
            log("控方機:已收到辯方機的 VC|DONE → 確認播畢、安全交棒,不會搶拍。");

            log("\n=== 完成 " + game.Exchanges + " 回合;辯方台詞全由『被控機自己的喇叭』說出(非中控代說)、含 VC|DONE 播畢握手 ===");
            log("（實機:SimRobotBus→UnityRobotLink(UDP),被控機設 Mode=Controlled;先過必測④雙機收送）");
            log("====================================================");
        }

        // 示範小遊戲:合體彩蛋《凱比聯合學園祭・多機接力大舞台》——中控導演機指揮多站接力,含「降級備案」(離線站自動跳過)。
        // 對應 合體彩蛋_G2G3G5_多機協作.md;FinaleShowGame 編排邏輯純 C# 已自測,實機換 UnityRobotLink(UDP)同款。
        private static async Task PlayFinaleDemoAsync()
        {
            Action<string> log = Console.WriteLine;

            // 建一個「在場站台」:收 CUE→演出(印一行+舉手)→回 ACK/DONE;收 FINALE→走位中央+同步舉手。
            // delayMs>0 → 延遲「非同步」才回 ACK/DONE(模擬真機 UDP 回覆不在當下到),示範中控 await 等待。
            void Station(SimRobotBus bus, string id, string perform, int delayMs = 0)
            {
                Action<string> sLog = s => log("      [" + id + "] " + s);
                var body = new SimKebbiBody(sLog, canMove: true);
                var link = bus.CreateLink(id);
                link.OnMessage((from, t) =>
                {
                    if (t.StartsWith("CUE|"))
                    {
                        string role = t.Substring(4);
                        sLog("🎭 " + perform);
                        body.SetMotor(KebbiMotor.RShoulderY, 60f);
                        if (delayMs > 0)
                            _ = Task.Run(async () =>
                            {
                                await Task.Delay(delayMs);                  // 非同步:延遲後才回
                                await link.SendAsync(from, "ACK|" + role);
                                await link.SendAsync(from, "DONE|" + role);
                            });
                        else
                        {
                            link.SendAsync(from, "ACK|" + role);
                            link.SendAsync(from, "DONE|" + role);
                        }
                    }
                    else if (t == "FINALE")
                    {
                        sLog("→ 走位舞台中央 + 同步勝利動作");
                        body.Move(0.1f); body.StopWheels();
                        body.SetMotor(KebbiMotor.RShoulderY, 100f);
                    }
                });
            }

            log("========== 示範:合體彩蛋《凱比聯合學園祭・多機接力大舞台》 ==========");
            log("（中控導演機指揮 G2/G3/G5 三站接力,壓軸全體同步;任一站離線→自動跳過降級）\n");

            // ── 第一場:三站全到的完美接力 ──
            log("【第一場 ▶ 三站全到】");
            var busA = new SimRobotBus(log);
            Station(busA, "G2-站機", "觀眾上台解一步幾何,兩機說—走—指演示證明");
            Station(busA, "G3-站機", "帶全場做 30 秒體感律動");
            Station(busA, "G5-夥伴機", "與中控在中央做對峙接力,帶到高潮");
            var hostA = new SimKebbiBody(s => log("      [中控導演機] " + s), canMove: true);
            var showA = new FinaleShowGame(busA.CreateLink("中控導演機"), hostA, log);
            await showA.RunShowAsync(FinaleShowGame.MakeDefaultLineup());

            // ── 第二場:G5 夥伴機臨時離線 → 降級備案 ──
            log("\n【第二場 ▶ 降級備案:G5 夥伴機臨時離線】");
            var busB = new SimRobotBus(log);
            Station(busB, "G2-站機", "觀眾上台解一步幾何");
            Station(busB, "G3-站機", "帶全場做體感律動");
            // 故意不建 "G5-夥伴機" → 離線,示範中控自動跳過
            var hostB = new SimKebbiBody(s => log("      [中控導演機] " + s), canMove: true);
            var showB = new FinaleShowGame(busB.CreateLink("中控導演機"), hostB, log);
            await showB.RunShowAsync(FinaleShowGame.MakeDefaultLineup());

            // ── 第三場:G2 站「非同步」延遲回應 → 中控 await 等到才續(舊版同步檢查會誤判離線) ──
            log("\n【第三場 ▶ 非同步:G2 站回應較慢(延遲 120ms),中控 await 等到才算它完成】");
            var busC = new SimRobotBus(log);
            Station(busC, "G2-站機", "(推理中…)幾何步驟驗證完成", delayMs: 120); // 非同步延遲回 ACK/DONE
            Station(busC, "G3-站機", "帶全場做體感律動");
            var hostC = new SimKebbiBody(s => log("      [中控導演機] " + s), canMove: true);
            var showC = new FinaleShowGame(busC.CreateLink("中控導演機"), hostC, log);
            await showC.RunShowAsync(new System.Collections.Generic.List<FinaleShowGame.Station>
            {
                new FinaleShowGame.Station("G2 具身幾何站", "G2-站機"),
                new FinaleShowGame.Station("G3 體感律動站", "G3-站機"),
            });
            log("（G2 站延遲 120ms 才回 ACK/DONE,中控『await 等到』才算完成 → 跑 " + showC.StationsRun + " 站；舊版同步檢查會把它誤判離線）");

            log("\n=== 重點:離線站自動跳過(降級不崩)、慢站 await 等到(真機非同步也正確) —— 合體翻車不拖垮全場 ===");
            log("（實機:SimRobotBus→UnityRobotLink(UDP),靠 LinkAwaiter 的 await+逾時 → Sim 與真機 UDP 皆正確）");
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
            log("【正常 ▶ 乙機送 POINT → await 甲機回 DONE 才念下一步（真機 UDP 也正確）】");
            await game.RunProofAsync(GeometryRelayGame.MakeIsoscelesProof());
            log("→ 完成 " + game.StepsDone + " 步證明接力");

            // 降級備案:甲機離線(放到另一條 bus,POINT 送不到)→ 乙機逾時降級口述、不卡死。
            log("\n【降級 ▶ 甲機臨時離線：乙機 await DONE 逾時 → 降級口述、不卡死】");
            var busR = new SimRobotBus(log);
            var busG = new SimRobotBus(_ => { }); // 甲機在另一條 bus = 離線
            var degrade = new GeometryRelayGame(new SimKebbiBody(_ => { }, true),
                busG.CreateLink("甲機"), busR.CreateLink("乙機"), new SimVoice(log), log, doneTimeoutMs: 300);
            await degrade.RunProofAsync(GeometryRelayGame.MakeIsoscelesProof());
            log("→ 完成 " + degrade.StepsDone + " 步、降級 " + degrade.StepsSkipped + " 步（甲機離線也不卡死，乙機照念完）");

            // 學習單作答:乙機問每步邏輯層次,學生答 → 答對計分、答錯念提示(把證明結構外化成可驗證作答)。
            log("\n【學習單 ▶ 乙機問每步是「已知/因為/所以」，學生作答計分】");
            var wbBus = new SimRobotBus(log);
            var wbVoice = new SimVoice(log);
            wbVoice.EnqueueHeard("已知"); wbVoice.EnqueueHeard("我不確定"); wbVoice.EnqueueHeard("所以"); // 第2步故意答錯
            var wb = new GeometryRelayGame(new SimKebbiBody(log, true), wbBus.CreateLink("甲機"), wbBus.CreateLink("乙機"), wbVoice, log);
            await wb.RunProofAsync(GeometryRelayGame.MakeIsoscelesProofWorksheet());
            log("→ 學習單得分：" + wb.Score + " / 3（答錯那步乙機已念提示）");

            // 多回合場次:跑整個證明題庫(等腰/內角和/外角定理)+ 場次結算。
            log("\n【多回合場次 ▶ 跑完整題庫(等腰底角→內角和→外角定理)+ 結算】");
            var libBus = new SimRobotBus(log);
            var libVoice = new SimVoice(log);
            var lib = GeometryRelayGame.MakeProofLibrary();
            for (int p = 0; p < lib.Count; p++) { libVoice.EnqueueHeard("已知"); libVoice.EnqueueHeard("因為"); libVoice.EnqueueHeard("所以"); }
            var libGame = new GeometryRelayGame(new SimKebbiBody(log, true), libBus.CreateLink("甲機"), libBus.CreateLink("乙機"), libVoice, log);
            await libGame.RunSessionAsync(lib);

            log("\n=== 重點：LinkAwaiter 真機正確、離線降級、學習單可驗證作答、題庫換題+多回合場次結算 ===");
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
            log("（▶ 第 1 拍：暖身，學生跟對）");
            pose.Enqueue(true);
            await game.RunRepAsync(move);

            log("\n（▶ 左後方學生喊「太快了！」→ Kebbi 轉頭面向 + 放慢）");
            body.CurrentDoa = -120;
            await game.HandleTooFastAsync(body.ReadDoaDegrees());

            log("\n（▶ 完整課表：暖身 → 太極 → CPR，逐組帶完語音切換下一組）");
            pose.Enqueue(true); pose.Enqueue(true); pose.Enqueue(true);
            await game.RunSessionAsync(MirrorCoachGame.MakeDefaultRoutine());

            log("\n（▶ 左右鏡像：示範「只舉右手」的鏡像版＝舉左手，面對學生時「我的左＝你的右」）");
            var oneArm = new MirrorCoachGame.Move("舉右手");
            oneArm.Frames.Add(new MirrorCoachGame.JointFrame("舉右手").Set(KebbiMotor.RShoulderY, 90f));
            pose.Enqueue(true);
            await game.RunRepAsync(MirrorCoachGame.MirrorMove(oneArm)); // 播鏡像版 → 實際動的是左肩

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

        // G5《法庭辯論劇場》七步審判驅動器 Demo（RunTrialAsync + 學生席舉手插話分支，純 Sim）
        // 演手冊:整場 7 步固定流程(開庭→陳述/交棒/反駁→逼近→轉向學生→結辯),
        //         並在某回合掛「學生舉手插話」——先確認真的舉手(姿態 gate)→ 近側機轉頭面向 → 用學生麥聽一句 → 回應 → 改票。
        private static async Task PlayG5TrialDemoAsync()
        {
            Action<string> log = Console.WriteLine;
            var proBody = new SimKebbiBody(log, canMove: true);
            var defBody = new SimKebbiBody(log, canMove: true);
            var bus = new SimRobotBus(log);

            // 學生席麥克風(SimVoice 當麥,EnqueueHeard 預排學生會說的話)+ 姿態 gate(Enqueue(true) 表真的舉手)。
            var studentMic = new SimVoice(log);
            studentMic.EnqueueHeard("可是教會後來也承認了日心說，這算不算反證？");
            var pose = new SimPoseSensor(log);
            pose.Enqueue(true); // 第一位插話學生:真的舉手 → 走分支

            var game = new DebateGame(
                proBody, bus.CreateLink("控方-Kebbi"), new SimVoice(log),
                defBody, bus.CreateLink("辯方-Kebbi"), new SimVoice(log), log,
                studentMic, pose); // 新 9 參數建構式:掛學生麥 + 姿態 gate

            log("========== G5《法庭辯論劇場》七步審判 Demo（RunTrialAsync + 學生席舉手插話） ==========");
            log("（流程:[1/7]開庭 → [2-4/7]逐回合 陳述/交棒/反駁 → 某回合『🙋 學生舉手插話』→ [5/7]逼近 → [6/7]轉向學生席 → [7/7]結辯宣判）");
            log("（▶ 第 2 回合右後方(120°)學生舉手 → 姿態 gate 確認舉手 → 辯方機轉頭面向 → 學生麥聽一句 → 辯方回應 → 辯方 +1 票）\n");

            await game.RunTrialAsync(DebateGame.MakeGalileoTrial());

            log("");
            game.PrintSummary();

            // 對照組:同一場但學生『沒舉手』(姿態 gate 回 false)→ 插話被忽略、不轉頭、不改票,凸顯 gate 分支。
            log("\n【對照 ▶ 同一回合學生『沒真的舉手』→ 姿態 gate 擋下、插話被忽略、票數不變】");
            var pro2 = new SimKebbiBody(log, canMove: true);
            var def2 = new SimKebbiBody(log, canMove: true);
            var bus2 = new SimRobotBus(log);
            var pose2 = new SimPoseSensor(log);
            pose2.Enqueue(false); // 沒舉手 → 略過分支
            var game2 = new DebateGame(
                pro2, bus2.CreateLink("控方-Kebbi"), new SimVoice(log),
                def2, bus2.CreateLink("辯方-Kebbi"), new SimVoice(log), log,
                new SimVoice(log), pose2);
            await game2.RunTrialAsync(DebateGame.MakeGalileoTrial());
            log("→ 沒舉手:插話數=" + game2.Interjections + "、辯方票=" + game2.DefVotes
                + "（對照有舉手:插話數=" + game.Interjections + "、辯方票=" + game.DefVotes + "）");

            log("\n=== 重點:7 步固定流程一鍵跑完;學生舉手插話走『姿態 gate → 轉頭 → 學生麥 → 回應 → 改票』分支,沒舉手則自動跳過 ===");
            log("====================================================");
        }

        // G4《Tebak Arah》裁判賽多輪排名 Demo（裁判賽 + 視角轉換 + round-robin 排名，純 Sim）
        // 演手冊步驟 4（A 描述 B、Kebbi 用聲源 DOA 核對 + 轉頭面向 B）與步驟 5（視角轉換 + 累積排名）。
        private static async Task PlayG4TournamentDemoAsync()
        {
            Action<string> log = Console.WriteLine;
            var body = new SimKebbiBody(log, canMove: false);
            var voice = new SimVoice(log);
            var ctx = new KebbiContext(body, voice, new SimLlm(log), log);
            var game = new TebakArahGame(ctx);

            log("========== G4《Tebak Arah》裁判賽多輪排名 Demo ==========");
            log("（▶ 校準階段：四位學生分坐機器人四周）");
            await CalibrateAsync(game, body, voice.EnqueueHeard, "Andi", 90);   // 右
            await CalibrateAsync(game, body, voice.EnqueueHeard, "Budi", -90);  // 左
            await CalibrateAsync(game, body, voice.EnqueueHeard, "Citra", 0);   // 前
            await CalibrateAsync(game, body, voice.EnqueueHeard, "Dewi", 170);  // 後

            log("\n（▶ 裁判賽示範：Andi 描述 Budi 的方位 → Kebbi 用 Budi 的 DOA 真值核對 + 轉頭面向 Budi）");
            body.CurrentDoa = -90; voice.EnqueueHeard("Budi di kiri"); // Budi 在左 → 答對
            var jr = await game.JudgeRoundAsync(new TebakArahGame.MatchSpec("Andi", "Budi"));
            log("→ 裁判結果：Andi " + (jr.Correct ? "答對 ✔（+1）" : "答錯 ✘") + "、轉頭" + (jr.Faced ? "完整面向" : "被夾限") + " Budi");

            log("\n（▶ 視角轉換高光：Citra(前) 同時說 Andi(右) 相對 Kebbi 與相對自己的方位）");
            log("   手冊核心：『Kamu di kiri saya, tapi di kanan Kebbi』（我的左、卻是 Kebbi 的右）");
            voice.EnqueueHeard("di kanan"); // 相對 Kebbi：Andi 在右 → Kanan
            voice.EnqueueHeard("di kiri");  // 相對 Citra(面向 Kebbi)：左右翻轉 → Kiri
            var pr = await game.PerspectiveRoundAsync("Citra", "Andi");
            log("→ 視角題：相對 Kebbi=" + Direction.ToIndo(pr.ActualSector)
                + "、相對 Citra=" + Direction.ToIndo(pr.PerspectiveSector)
                + " → " + (pr.Correct ? "兩者皆對 ✔（+1）" : "未全對 ✘"));

            log("\n（▶ 多輪賽事：四人 round-robin 逐場裁判賽 → 累積積分排名）");
            var matches = TebakArahGame.MakeRoundRobin(new[] { "Andi", "Budi", "Citra", "Dewi" });
            // 為每一場預排「被描述者此刻的 DOA 真值」與「提問者的方位詞答案」。
            // 讓 Budi 全對、Andi 部分對、其餘較弱，凸顯排名差異。
            var seats = new System.Collections.Generic.Dictionary<string, float> {
                { "Andi", 90f }, { "Budi", -90f }, { "Citra", 0f }, { "Dewi", 170f } };
            foreach (var m in matches)
            {
                float doa = seats[m.TargetName];
                // 簡化腳本：Budi 當提問者一律答對；其餘提問者一律答錯（示範排名落差）。
                string word = m.AskerName == "Budi" ? Direction.ToIndo(Direction.FromAngle(doa)) : "di depan";
                body.CurrentDoa = doa; voice.EnqueueHeard(word);
            }
            // 注意：RunTournamentAsync 逐場讀 body.CurrentDoa；此 Demo 各場真值相近僅作示範，
            // 真正的逐場真值核對在 --test 用 SeqDoaBody 精確驗證。
            await game.RunTournamentAsync(matches);
            game.PrintRanking();

            log("\n=== 重點：聲源 DOA 當真值核對方位詞、轉頭具身回饋、視角轉換糾錯、多輪累積排名（平手以校準序 stable）===");
            log("====================================================");
        }

        // G4《Tebak Arah》八向方位 Demo（純 Sim）——示範:① 4 向擴 8 向(含斜向 serong=印尼語「斜」)、
        // ② 複合詞解析(belakang kiri 不被單詞吃掉)、③ NeckZ 物理可達性決定判決:正後方頭轉不過去 → 回報「最近可達扇區」。
        private static async Task PlayG4EightWayDemoAsync()
        {
            Action<string> log = Console.WriteLine;
            var body = new SimKebbiBody(log, canMove: false);
            var voice = new SimVoice(log);
            var ctx = new KebbiContext(body, voice, new SimLlm(log), log);
            var game = new TebakArahGame(ctx);

            log("========== G4《Tebak Arah》八向方位(含斜向 serong)Demo ==========");
            log("（▶ 校準階段：六位學生分坐 8 向中的 6 個方位）");
            await CalibrateAsync(game, body, voice.EnqueueHeard, "Andi", 0);     // depan 前
            await CalibrateAsync(game, body, voice.EnqueueHeard, "Budi", 45);    // serong kanan 右前
            await CalibrateAsync(game, body, voice.EnqueueHeard, "Citra", 90);   // kanan 右
            await CalibrateAsync(game, body, voice.EnqueueHeard, "Dewi", 135);   // belakang kanan 右後
            await CalibrateAsync(game, body, voice.EnqueueHeard, "Eka", -90);    // kiri 左
            await CalibrateAsync(game, body, voice.EnqueueHeard, "Fitri", -135); // belakang kiri 左後

            log("\n（▶ 斜向方位題：問『誰在我的右前(serong kanan)?』→ Budi 答 'saya di serong kanan'）");
            body.CurrentDoa = 45; voice.EnqueueHeard("saya di serong kanan");
            var r1 = await game.ForwardRoundAsync(Dir.SerongKanan);
            log("→ " + (r1.LanguageCorrect && r1.RightResponder ? "答對 ✔（斜向方位詞正確）" : "未全對 ✘"));

            log("\n（▶ 複合詞辨識：左後 belakang kiri 不會被 belakang 或 kiri 單詞吃掉）");
            body.CurrentDoa = -135; voice.EnqueueHeard("saya di belakang kiri");
            var r2 = await game.ForwardRoundAsync(Dir.BelakangKiri);
            log("→ " + (r2.LanguageCorrect ? "解析正確 ✔ = belakang kiri（左後）" : "解析錯 ✘"));

            log("\n（▶ 馬達可達性決定判決：問正後方(180°)，頭 NeckZ 實機只能 ±40 → 回報最近可達扇區）");
            float faced = KebbiHead.TurnToward(body, 180f, out bool reachable, out Dir reached);
            log("   🤖 聲源在正後方 180°，頭轉到 " + faced.ToString("0") + "°（"
                + (reachable ? "可達" : "不可達") + "）→ 實際面向最近可達扇區：" + Direction.ToZh(reached) + " / " + Direction.ToIndo(reached));

            log("\n=== 重點：方位從 4 向細到 8 向(serong 斜向)、印尼語複合詞解析、NeckZ 物理可達性(實機 ±40)決定『實際面向扇區』(正後方只能降級到右前 serong) ===");
            log("====================================================");
        }

        // 複合面向 FaceFully Demo（純 Sim）——NeckZ 實機僅 ±40，頭單獨轉不到側邊/正後；
        // 輪式 Kebbi 用底盤 turn() 補粗方向 + NeckZ 補細 → 連正後方學生也能完整面向（多致動器協調）。
        // 對照 H201 桌上型（無底盤）只能頭部部分面向。
        private static void PlayFaceDemo()
        {
            Action<string> log = Console.WriteLine;
            log("========== 複合面向 FaceFully Demo（底盤轉向 + 頭部微調）==========");
            log("（NeckZ 實機只有 ±40 → 頭單獨轉不到側邊/正後；輪式 Kebbi 用底盤 turn() 補粗方向 → 完整面向）\n");

            var wheeled = new SimKebbiBody(log, canMove: true);
            foreach (var doa in new[] { 30f, 90f, 180f, -120f })
            {
                log("【輪式 Kebbi 面向 " + doa.ToString("0") + "° 的學生】");
                var r = KebbiHead.FaceFully(wheeled, doa);
                log("→ 底盤轉 " + r.BaseTurnDeg.ToString("0") + "° + 頭 NeckZ " + r.HeadDeg.ToString("0")
                    + "° = 面向 " + r.FacedAngle.ToString("0") + "°（" + (r.FullyFaced ? "完整面向 ✔" : "部分面向") + "）\n");
            }

            log("【對照：H201 桌上型（無底盤）面向 90° 的學生】");
            var desktop = new SimKebbiBody(log, canMove: false);
            var rd = KebbiHead.FaceFully(desktop, 90f);
            log("→ 底盤不能轉、頭只能到 " + rd.HeadDeg.ToString("0") + "° → "
                + (rd.FullyFaced ? "完整面向" : "只能部分面向（頭轉不到 90°）") + "\n");

            log("=== 重點：NeckZ ±40 不夠面向側邊時，輪式 Kebbi 用底盤 turn() 補粗方向、頭補細 → 連正後方學生也能完整面向（多致動器協調）；桌上型則誠實部分面向 ===");
            log("（實機：底盤 turn() 受授權牆/必測③ 影響；被擋則退回頭部部分面向）");
            log("====================================================");
        }

        // G3《鏡像教練·逐幀回退》Demo（純 Sim，免金鑰）——手冊 step4：學生喊「再一次」→ Kebbi 回退一幀重新示範同一動作。
        private static async Task PlayG3RewindDemoAsync()
        {
            Action<string> log = Console.WriteLine;
            var body = new SimKebbiBody(log, canMove: false);
            var pose = new SimPoseSensor(log);
            var ctx = new KebbiContext(body, new SimVoice(log), new SimLlm(log), log);
            var game = new MirrorCoachGame(ctx, pose);
            var move = MirrorCoachGame.MakeWarmup();

            log("========== G3《鏡像教練·逐幀回退》文字模擬器 Demo ==========");
            log("（▶ 學生喊『再一次』→ Kebbi 回退一幀、重新示範同一動作）");
            // 模擬學生跟不上(姿態錯)並預排語音「再一次」,讓 RunRepAsync 內建攔截觸發 HandleAgainAsync。
            pose.Enqueue(false);
            ((SimVoice)ctx.Voice).EnqueueHeard("再一次");
            await game.RunRepAsync(move);
            log("→ 目前停在第 " + (game.CurrentFrame + 1) + " 幀（共 " + move.Frames.Count + " 幀），重新示範中：索引已由末幀回退一幀。");

            game.PrintSummary();
            log("====================================================");
        }

        // G3《鏡像教練·動作幀資料化》Demo（純 Sim，免金鑰）——示範:① 單幀自訂停留 HoldMs(CPR 下壓刻意停 2 秒)、
        // ② 整組循環 Move.Loops(暖身連做 3 組)、③ 降 BPM 後一般幀變慢、自訂 HoldMs 幀不受影響。
        private static async Task PlayG3FrameDemoAsync()
        {
            Action<string> log = Console.WriteLine;
            var body = new SimKebbiBody(log, canMove: false);
            var pose = new SimPoseSensor(log);
            var ctx = new KebbiContext(body, new SimVoice(log), new SimLlm(log), log);
            var game = new MirrorCoachGame(ctx, pose);

            log("========== G3《鏡像教練·動作幀資料化》文字模擬器 Demo ==========");

            // ① 自訂單幀停留:CPR 下壓那幀刻意停 2000ms(衛教強調),其餘幀依 BPM(60→1000ms)。
            log("\n【① 單幀自訂停留 HoldMs ▶ CPR 下壓刻意停 2 秒，其餘依 BPM】");
            var cpr = new MirrorCoachGame.Move("CPR 按壓");
            cpr.Frames.Add(new MirrorCoachGame.JointFrame("預備:雙臂前伸").Set(KebbiMotor.RShoulderY, 70f).Set(KebbiMotor.LShoulderY, 70f));
            cpr.Frames.Add(new MirrorCoachGame.JointFrame("下壓:肘打直下壓(停 2 秒強調)").Set(KebbiMotor.RShoulderY, 100f).Set(KebbiMotor.LShoulderY, 100f).Hold(2000));
            cpr.Frames.Add(new MirrorCoachGame.JointFrame("回彈:回到預備").Set(KebbiMotor.RShoulderY, 70f).Set(KebbiMotor.LShoulderY, 70f));
            await game.PlayMoveAsync(cpr);
            log("→ 下壓幀停留 " + game.FrameHoldMs(cpr.Frames[1]) + "ms(自訂)、預備幀停留 " + game.FrameHoldMs(cpr.Frames[0]) + "ms(依 BPM)");

            // ② 整組循環:暖身連做 3 組(Move.Loops=3),FramesPlayed 累計 = 幀數×3。
            log("\n【② 整組循環 Move.Loops ▶ 暖身連做 3 組】");
            var warm = MirrorCoachGame.MakeWarmup().Repeat(3);   // 3 幀 × 3 組
            await game.PlayMoveAsync(warm);
            log("→ 共播放 " + game.FramesPlayed + " 幀(= " + warm.Frames.Count + " 幀 × " + warm.Loops + " 組)");

            // ③ 學生喊「太快了」→ DOA 轉頭 + 降 BPM;之後一般幀變慢,自訂 HoldMs 幀不受影響。
            log("\n【③ 降 BPM 後 ▶ 一般幀跟著變慢、自訂 HoldMs 幀不受影響】");
            await game.HandleTooFastAsync(30f);   // BPM 60→45
            log("→ 降速後:一般幀 " + game.FrameHoldMs(cpr.Frames[0]) + "ms(隨 BPM 變慢)、CPR 下壓幀仍 " + game.FrameHoldMs(cpr.Frames[1]) + "ms(自訂不變)");

            log("\n=== 重點:每幀可獨立設停留時間(衛教強調幀停久)、整組可循環(暖身連做)、降速只影響未自訂的幀 ===");
            log("====================================================");
        }

        // G2《幾何證明接力站》學生自編腳本「結構驗證器」Demo（純 Sim）
        // 手冊命脈:學生先把證明步驟序列排出來,Kebbi 對「結構」把關(缺步/層次錯置)→ 指出第幾步哪種錯,
        // 改正後才放行接力(RunProofIfValidAsync)——呼應「腳本改正 → 才能往下」的即時因果回饋。
        private static async Task PlayG2ValidatorDemoAsync()
        {
            Action<string> log = Console.WriteLine;

            // 標準解(等腰底角學習單版):Layer 序列 = 已知/因為/所以。validator 以此為結構基準。
            var expected = GeometryRelayGame.MakeIsoscelesProofWorksheet();

            // 深拷貝標準解以產生「學生腳本」(改 Layer 時不污染標準解)。
            GeometryRelayGame.Step Copy(GeometryRelayGame.Step s)
                => new GeometryRelayGame.Step(s.Reason, s.Edge, s.ArmAngle, s.Layer);
            System.Collections.Generic.List<GeometryRelayGame.Step> Clone()
            {
                var l = new System.Collections.Generic.List<GeometryRelayGame.Step>();
                foreach (var s in expected) l.Add(Copy(s));
                return l;
            }

            GeometryRelayGame NewGame(out SimVoice voice)
            {
                var bus = new SimRobotBus(log);
                voice = new SimVoice(log);
                return new GeometryRelayGame(new SimKebbiBody(log, true),
                    bus.CreateLink("甲機(指引)"), bus.CreateLink("乙機(推理)"), voice, log);
            }

            log("========== G2《幾何證明接力站》學生自編腳本驗證 Demo ==========");
            log("（題目：證明等腰三角形兩底角相等；請學生先把證明步驟排出來，Kebbi 先驗結構再放行接力）");

            // ── 腳本一:正確腳本 → 驗證通過 → 跑完 3 步接力 ──
            log("\n【腳本一 ▶ 正確腳本：已知 → 因為 → 所以】");
            var g1 = NewGame(out var v1);
            v1.EnqueueHeard("已知"); v1.EnqueueHeard("因為"); v1.EnqueueHeard("所以"); // 學習單作答
            var ok = await g1.RunProofIfValidAsync(Clone(), expected);
            log("→ 驗證：" + (ok.Ok ? "通過 ✅ " : "未過 ⛔ ") + ok.Message + "；完成 " + g1.StepsDone + " 步接力");

            // ── 腳本二:缺步(只排 2 步,少了結論)→ validator 指『缺第 3 步』、不接力 ──
            log("\n【腳本二 ▶ 缺步：只排 2 步，少了『所以』那一步結論】");
            var g2 = NewGame(out _);
            var missing = Clone(); missing.RemoveAt(2);
            var chk2 = await g2.RunProofIfValidAsync(missing, expected);
            log("→ 驗證：未過 ⛔ 第 " + chk2.Step + " 步問題 — " + chk2.Message);
            log("   （Kebbi：少了結論那一步，請補上『所以』再來接力；StepsDone=" + g2.StepsDone + " 沒有接力）");

            // ── 腳本三:層次錯置(把第 2 步『因為』標成『所以』)→ validator 指『第 2 步邏輯層次錯置』、不接力 ──
            log("\n【腳本三 ▶ 邏輯層次錯置：第 2 步『因為』被標成『所以』】");
            var g3 = NewGame(out _);
            var wrong = Clone(); wrong[1].Layer = "所以";
            var chk3 = await g3.RunProofIfValidAsync(wrong, expected);
            log("→ 驗證：未過 ⛔ 第 " + chk3.Step + " 步問題 — " + chk3.Message);
            log("   （Kebbi：第 " + chk3.Step + " 步層次標錯了，改好再接力；StepsDone=" + g3.StepsDone + " 沒有接力）");

            log("\n=== 重點：腳本『結構』把關(缺步/層次錯置)→ 指出第幾步哪種錯 → 改正才放行接力 ===");
            log("====================================================");
        }

        // G2《幾何證明接力站·甲機轉頭望向發言者》Demo（純 Sim）——示範:每步乙機說完,甲機先轉頭(NeckZ)望向
        // 被討論的圖素/發言者,再手臂指認該邊。NeckZ 轉頭做「視線跟隨」是平板做不到的具身互動;TurnHeadToward 可為負(望左)。
        private static async Task PlayG2TurnHeadDemoAsync()
        {
            Action<string> log = Console.WriteLine;
            var guide = new SimKebbiBody(log, true);
            var bus = new SimRobotBus(log);
            var voice = new SimVoice(log);
            var game = new GeometryRelayGame(guide, bus.CreateLink("甲機(指引)"), bus.CreateLink("乙機(推理)"), voice, log);

            log("========== G2《幾何證明接力站·甲機轉頭望向發言者》Demo ==========");
            log("（▶ 每步:乙機念理由 → 甲機先『轉頭望向』該圖素(NeckZ) → 再手臂指認該邊 → 回報接棒）");
            log("（第 1/2/3 步分別轉頭 -45°(望左)/0°(望前)/+45°(望右)，示範視線跟隨被討論的幾何圖素）\n");

            // 預排學生對學習單的正解(已知/因為/所以),讓示範聚焦在轉頭+指認,不被答錯提示打斷。
            voice.EnqueueHeard("已知"); voice.EnqueueHeard("因為"); voice.EnqueueHeard("所以");
            await game.RunProofAsync(GeometryRelayGame.MakeIsoscelesProofTurnHead());

            log("\n→ 完成 " + game.StepsDone + " 步;甲機頭部最後停在 "
                + guide.GetMotor(KebbiMotor.NeckZ).ToString("0") + "°(望右兩底角)");
            log("=== 重點:甲機用 NeckZ 轉頭做『視線跟隨』(看向被討論的邊/角)再手臂指認——眼神接觸是平板做不到的具身互動 ===");
            log("====================================================");
        }

    }
}
