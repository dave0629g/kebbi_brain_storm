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

        public MirrorCoachGame(KebbiContext ctx, IPoseSensor pose) { _ctx = ctx; _pose = pose; }

        // 一個動作幀：標籤 + 一組(馬達, 角度)
        public sealed class JointFrame
        {
            public string Label;
            public List<KeyValuePair<KebbiMotor, float>> Targets = new List<KeyValuePair<KebbiMotor, float>>();
            public JointFrame(string label) { Label = label; }
            public JointFrame Set(KebbiMotor m, float deg) { Targets.Add(new KeyValuePair<KebbiMotor, float>(m, deg)); return this; }
        }

        public sealed class Move
        {
            public string Name;
            public List<JointFrame> Frames = new List<JointFrame>();
            public Move(string name) { Name = name; }
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

        // 逐幀示範（依 BPM 決定每拍停留；不真的 sleep，以保持自測快速）
        public async Task PlayMoveAsync(Move move)
        {
            int holdMs = (int)(60000.0 / Math.Max(1, Bpm));
            await _ctx.Voice.SpeakAsync("跟我做：" + move.Name + "（我的左手＝你的右手）", "zh-TW");
            foreach (var f in move.Frames)
            {
                foreach (var t in f.Targets) _ctx.Body.SetMotor(t.Key, t.Value);
                _ctx.Log("   🤸 [示範] " + f.Label + "（停留 " + holdMs + "ms @ " + Bpm + " BPM）");
            }
        }

        // 跑一拍：示範 → 攝影機檢查學生姿態 → 計分/回饋
        public async Task<bool> RunRepAsync(Move move)
        {
            Reps++;
            await PlayMoveAsync(move);
            bool ok = await _pose.CheckPoseAsync(move.Name);
            if (ok) { Score++; await _ctx.Voice.SpeakAsync("很好！動作很標準！", "zh-TW"); }
            else { await _ctx.Voice.SpeakAsync("再一次，慢慢來，跟上我的手。", "zh-TW"); }
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

        // 學生喊「太快了」：讀 DOA → 轉頭面向他 → 降 BPM。回傳是否能完整面向。
        public async Task<bool> HandleTooFastAsync(float doaDeg)
        {
            float faced = KebbiHead.TurnToward(_ctx.Body, doaDeg, out bool reachable);
            Bpm = Math.Max(30, Bpm - 15);
            if (!reachable)
                _ctx.Log("   ⚠ 發問者在 " + doaDeg.ToString("0.0") + "°，超出頭部可達，只能轉到 " + faced.ToString("0.0") + "°");
            await _ctx.Voice.SpeakAsync("好，我們放慢一點。", "zh-TW");
            _ctx.Log("   🐢 BPM 降到 " + Bpm);
            return reachable;
        }

        public void PrintSummary()
        {
            _ctx.Log("");
            _ctx.Log("=== 結算：標準 " + Score + " / " + Reps + " 拍，結束 BPM=" + Bpm + " ===");
        }
    }
}
