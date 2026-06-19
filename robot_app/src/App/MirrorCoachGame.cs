using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // G3《Kebbi 體育課：動作鏡像教練》核心邏輯。
    // 平板免疫核心：用真實「關節逐幀」做可繞看的立體示範（setMotor，免授權實測可用）；
    // 聽到「太快了」用 DOA 讀方位 + 自寫 NeckZ 轉頭面向發問者並降 BPM（不用被擋的 turnToDOA）。
    // 攝影機姿態檢查抽象成 IPoseSensor（real=MediaPipe，sim=腳本）。
    // 刻意只示範「上肢/頭部」動作——Kebbi 無下肢，落在硬體優勢內（對應實測誠實邊界）。
    public sealed class MirrorCoachGame
    {
        private readonly KebbiContext _ctx;
        private readonly IPoseSensor _pose;

        public int Bpm { get; private set; } = 60;
        public int Score { get; private set; }
        public int Reps { get; private set; }

        // G3-rewind:逐幀播放狀態。CurrentFrame=目前停留幀(-1=尚未開始);CurrentMove=目前示範的動作(null=未開始)。
        // 兩者皆「該場示範狀態」(非累計類),PlayMoveAsync 開頭重置 → 可重入;對外唯讀。
        public int CurrentFrame { get; private set; } = -1;
        public Move CurrentMove { get; private set; }

        // G3-frame：本場累計已播幀數(含循環、含重示範)。PlayMoveAsync 開頭歸零 → 可重入、可驗循環次數。
        public int FramesPlayed { get; private set; }

        // 該幀實際停留毫秒：有自訂 HoldMs 就用自訂,否則依目前 BPM 換算(每拍)。供 PlayFrame 與自測共用。
        public int FrameHoldMs(JointFrame f) => f.HoldMs ?? (int)(60000.0 / Math.Max(1, Bpm));

        // 觸發「再一次」的關鍵字(手冊 step4)。比對用 Contains,容錯口語。
        private const string AgainPhrase = "再一次";

        public MirrorCoachGame(KebbiContext ctx, IPoseSensor pose) { _ctx = ctx; _pose = pose; }

        // 一個動作幀：標籤 + 一組(馬達, 角度)。可選 HoldMs：本幀自訂停留毫秒(null=依 Move BPM 換算,向後相容)。
        // 用途：讓某些幀刻意停久(如 CPR 下壓停 2 秒強調、太極沉落慢拍),不必整組改 BPM。
        public sealed class JointFrame
        {
            public string Label;
            public List<KeyValuePair<KebbiMotor, float>> Targets = new List<KeyValuePair<KebbiMotor, float>>();
            public int? HoldMs;   // null=用 BPM 換算;非 null=本幀停留毫秒(覆寫)
            public JointFrame(string label) { Label = label; }
            public JointFrame Set(KebbiMotor m, float deg) { Targets.Add(new KeyValuePair<KebbiMotor, float>(m, deg)); return this; }
            public JointFrame Hold(int ms) { HoldMs = ms; return this; }   // 流暢設定本幀自訂停留
        }

        public sealed class Move
        {
            public string Name;
            public List<JointFrame> Frames = new List<JointFrame>();
            public int Loops = 1;   // 整組循環次數(預設 1=播一次,向後相容)。用於暖身重複 N 組等。
            public Move(string name) { Name = name; }
            public Move Repeat(int loops) { Loops = System.Math.Max(1, loops); return this; }   // 流暢設定循環次數
        }

        // 內建暖身動作：上肢開合（雙臂下垂 → 平舉 → 下垂），只用肩關節 Y 軸。
        public static Move MakeWarmup()
        {
            var m = new Move("上肢開合");
            m.Frames.Add(new JointFrame("預備：雙臂下垂").Set(KebbiMotor.RShoulderY, 0f).Set(KebbiMotor.LShoulderY, 0f));
            m.Frames.Add(new JointFrame("雙臂平舉").Set(KebbiMotor.RShoulderY, 80f).Set(KebbiMotor.LShoulderY, 80f));
            m.Frames.Add(new JointFrame("收回：雙臂下垂").Set(KebbiMotor.RShoulderY, 0f).Set(KebbiMotor.LShoulderY, 0f));
            return m;
        }

        // 太極上肢起式：緩起前舉 → 沉落（肩 Y + 肘 Y，慢節奏）。
        public static Move MakeTaichi()
        {
            var m = new Move("太極上肢起式");
            m.Frames.Add(new JointFrame("起式：垂臂沉肩").Set(KebbiMotor.RShoulderY, 0f).Set(KebbiMotor.LShoulderY, 0f).Set(KebbiMotor.RElbowY, 0f).Set(KebbiMotor.LElbowY, 0f));
            m.Frames.Add(new JointFrame("緩起：雙臂前舉至肩高").Set(KebbiMotor.RShoulderY, 90f).Set(KebbiMotor.LShoulderY, 90f).Set(KebbiMotor.RElbowY, 20f).Set(KebbiMotor.LElbowY, 20f));
            m.Frames.Add(new JointFrame("沉落：緩緩收回").Set(KebbiMotor.RShoulderY, 0f).Set(KebbiMotor.LShoulderY, 0f).Set(KebbiMotor.RElbowY, 0f).Set(KebbiMotor.LElbowY, 0f));
            return m;
        }

        // CPR 肘直手臂姿勢：雙臂前伸、手肘打直下壓（衛教情境）。
        public static Move MakeCpr()
        {
            var m = new Move("CPR 肘直手臂");
            m.Frames.Add(new JointFrame("預備：雙臂前伸").Set(KebbiMotor.RShoulderY, 70f).Set(KebbiMotor.LShoulderY, 70f).Set(KebbiMotor.RElbowY, 0f).Set(KebbiMotor.LElbowY, 0f));
            m.Frames.Add(new JointFrame("下壓：肘打直、肩前傾").Set(KebbiMotor.RShoulderY, 100f).Set(KebbiMotor.LShoulderY, 100f).Set(KebbiMotor.RElbowY, 0f).Set(KebbiMotor.LElbowY, 0f));
            m.Frames.Add(new JointFrame("回彈：回到預備").Set(KebbiMotor.RShoulderY, 70f).Set(KebbiMotor.LShoulderY, 70f));
            return m;
        }

        // 內建課表：暖身 → 太極 → CPR（手冊運作流程 step5：完成一組語音切換下一組）。
        public static List<Move> MakeDefaultRoutine() => new List<Move> { MakeWarmup(), MakeTaichi(), MakeCpr() };

        // 左右鏡像對映:把右側關節↔左側關節對調(鏡像教練面對學生時「我的左=你的右」)。
        // 頭部 NeckY/NeckZ 為中軸,不對調。對合(involution):MirrorMotor(MirrorMotor(m))==m。
        public static KebbiMotor MirrorMotor(KebbiMotor m)
        {
            switch (m)
            {
                case KebbiMotor.RShoulderY: return KebbiMotor.LShoulderY;
                case KebbiMotor.LShoulderY: return KebbiMotor.RShoulderY;
                case KebbiMotor.RShoulderZ: return KebbiMotor.LShoulderZ;
                case KebbiMotor.LShoulderZ: return KebbiMotor.RShoulderZ;
                case KebbiMotor.RShoulderX: return KebbiMotor.LShoulderX;
                case KebbiMotor.LShoulderX: return KebbiMotor.RShoulderX;
                case KebbiMotor.RElbowY: return KebbiMotor.LElbowY;
                case KebbiMotor.LElbowY: return KebbiMotor.RElbowY;
                default: return m; // NeckY/NeckZ 中軸不換
            }
        }

        // 產生一個動作的「左右鏡像版」(每幀每關節 R↔L 對調,角度不變)。
        public static Move MirrorMove(Move move)
        {
            var mm = new Move(move.Name + "(鏡像)");
            foreach (var f in move.Frames)
            {
                var nf = new JointFrame(f.Label);
                foreach (var t in f.Targets) nf.Set(MirrorMotor(t.Key), t.Value);
                mm.Frames.Add(nf);
            }
            return mm;
        }

        // 抽出單幀播放（PlayMoveAsync 與 HandleAgainAsync 共用）：真正把該幀的(馬達,角度)送出 + Log + 設 CurrentFrame。
        private void PlayFrame(Move move, int index)
        {
            var f = move.Frames[index];
            int holdMs = FrameHoldMs(f);                         // 自訂 HoldMs 優先,否則依 BPM
            foreach (var t in f.Targets) _ctx.Body.SetMotor(t.Key, t.Value);
            CurrentFrame = index;                                // ← 索引前進(或回退重示範)到此幀
            FramesPlayed++;                                      // 累計播放幀數(含循環/重示範)
            _ctx.Log("   🤸 [示範] (" + (index + 1) + "/" + move.Frames.Count + ") "
                     + f.Label + "（停留 " + holdMs + "ms" + (f.HoldMs.HasValue ? "·自訂" : " @ " + Bpm + " BPM") + "）");
        }

        // 逐幀示範（依 BPM 或自訂 HoldMs 決定每拍停留；不真的 sleep，以保持自測快速）。
        // G3-rewind/frame：重置場狀態 + 外層 Loops 迴圈包逐幀 PlayFrame。Loops=1 時對外行為完全等價舊版。可重入。
        public async Task PlayMoveAsync(Move move)
        {
            CurrentMove = move; CurrentFrame = -1; FramesPlayed = 0;   // 可重入：每組重置索引/幀計數
            await _ctx.Voice.SpeakAsync("跟我做：" + move.Name + "（我的左手＝你的右手）", "zh-TW");
            int loops = Math.Max(1, move.Loops);
            for (int loop = 0; loop < loops; loop++)
            {
                if (loops > 1) _ctx.Log("   🔁 第 " + (loop + 1) + "/" + loops + " 次循環");
                for (int i = 0; i < move.Frames.Count; i++) PlayFrame(move, i);
            }
        }

        // 純狀態回退一幀（無 I/O，可單測）。
        // 回傳 true=有實際往回退；false=已在第 0 幀(夾住、停在 0)或尚未開始示範。
        public bool RewindOneFrame()
        {
            if (CurrentMove == null || CurrentFrame < 0) return false;
            if (CurrentFrame == 0) return false;                 // 已最前，夾住不越界
            CurrentFrame--;
            return true;
        }

        // 手冊 step4：喊「再一次」→ 回退一幀重示範。
        // 回傳 true=有回退到前一幀；false=已在首幀(原地重做)或未開始。已開始時兩種情況都會重播當前幀(重送角度+安撫語音)。
        public async Task<bool> HandleAgainAsync()
        {
            if (CurrentMove == null) return false;               // 還沒開始示範，無幀可重
            bool rewound = RewindOneFrame();
            await _ctx.Voice.SpeakAsync("好，我們再做一次這個動作，看我。", "zh-TW");
            PlayFrame(CurrentMove, CurrentFrame);                // 重送該幀角度 + Log
            return rewound;
        }

        // 跑一拍：示範 → 攝影機檢查學生姿態 → 計分/回饋
        public async Task<bool> RunRepAsync(Move move)
        {
            Reps++;
            await PlayMoveAsync(move);
            bool ok = await _pose.CheckPoseAsync(move.Name);
            if (ok) { Score++; await _ctx.Voice.SpeakAsync("很好！動作很標準！", "zh-TW"); }
            else
            {
                await _ctx.Voice.SpeakAsync("再一次，慢慢來，跟上我的手。", "zh-TW");
                // 手冊 step4：免相機也能攔『再一次』——多看一次語音佇列。
                // 向後相容：沒注入時 SimVoice.ListenAsync 回空字串 → Contains 為 false → 不觸發 HandleAgainAsync。
                string heard = await _ctx.Voice.ListenAsync("zh-TW");
                if (heard != null && heard.Contains(AgainPhrase)) await HandleAgainAsync();
            }
            return ok;
        }

        // 跑整套課表：逐組示範+計分,組間語音切換下一組(手冊運作流程 step5)。可重入(整場重置)。
        public async Task RunSessionAsync(List<Move> moves)
        {
            Reps = 0; Score = 0;
            for (int i = 0; i < moves.Count; i++)
            {
                if (i > 0) await _ctx.Voice.SpeakAsync("這組完成！接下來換「" + moves[i].Name + "」，準備好跟我做。", "zh-TW");
                _ctx.Log("── 第 " + (i + 1) + " 組：" + moves[i].Name + " ──");
                await RunRepAsync(moves[i]);
            }
            await _ctx.Voice.SpeakAsync("今天的體育課到這裡，做得很好！", "zh-TW");
        }

        // 學生喊「太快了」：讀 DOA → 面向他(FaceFully:輪式底盤+頭、H201頭部部分) → 降 BPM。回傳是否完整面向。
        public async Task<bool> HandleTooFastAsync(float doaDeg)
        {
            var r = KebbiHead.FaceFully(_ctx.Body, doaDeg);
            Bpm = Math.Max(30, Bpm - 15);
            if (r.FullyFaced && r.BaseTurnDeg != 0f)
                _ctx.Log("   ↪️ 底盤轉 " + r.BaseTurnDeg.ToString("0") + "° + 頭 " + r.HeadDeg.ToString("0") + "° 面向發問者");
            else if (!r.FullyFaced)
                _ctx.Log("   ⚠ 發問者在 " + doaDeg.ToString("0.0") + "°，無底盤、頭只能轉到 " + r.HeadDeg.ToString("0.0") + "°（部分面向）");
            await _ctx.Voice.SpeakAsync("好，我們放慢一點。", "zh-TW");
            _ctx.Log("   🐢 BPM 降到 " + Bpm);
            return r.FullyFaced;
        }

        public void PrintSummary()
        {
            _ctx.Log("");
            _ctx.Log("=== 結算：標準 " + Score + " / " + Reps + " 拍，結束 BPM=" + Bpm + " ===");
        }
    }
}
