// 輔導室具身共情層(全平台共用,不依賴 UnityEngine/JSON)。把「安全燈號 + 說話時機」映成凱比的小幅身體動作:
// 點頭(NeckY)/前傾(NeckY)/柔和面向(讀DOA + FaceFully)。規格白紙黑字稱具身共情是「平板做不到的賣點」。
// 鐵律:純加分、不阻塞對話、絕不丟例外;body=null(Sim/主控台)時整層為 no-op → 既有安全流程與測試完全不受影響。
// 只用「免授權同步」能力(IKebbiBody.SetMotor / ReadDoaDegrees / KebbiHead.FaceFully),不碰被授權牆擋的 LED/原生臉。
// 角度刻意保守(|deg|≤25;NeckZ 實機僅 ±40),真機再對齊必測⑥微調幅度/時序。
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App.Counselor
{
    // 共情時機:由安全燈號 + 說話脈絡(登入/沉默拋題)決定。
    public enum EmpathyMoment
    {
        Login,         // 開場告知:溫暖、抬頭打開
        GreenChat,     // 🟢 開放陪聊:輕點頭表示「我在聽」
        Probe,         // 🟢 沉默拋題:歪頭邀請
        YellowHandoff, // 🟡 交接:沉穩前傾、放慢、不彈回
        RedHandoff,    // 🔴 呼叫真人:穩定前傾守住,低幅長停、最克制
        Rest           // 收尾回中
    }

    // 單一動作:一顆馬達轉到某角度,停留 HoldMs 毫秒。LangVersion 9.0:用 struct + 公開欄位。
    public struct EmpathyMove
    {
        public KebbiMotor Motor;
        public float Degrees;
        public int HoldMs;
        public EmpathyMove(KebbiMotor m, float deg, int holdMs) { Motor = m; Degrees = deg; HoldMs = holdMs; }
    }

    public interface IEmpathyBody
    {
        void Express(EmpathyMoment moment); // 播一段共情動作(fire-and-forget,不阻塞對話)
        void FaceSpeaker();                 // 讀 DOA → 柔和面向說話的學生(眼神接觸)
    }

    // 純函式:燈號/時機 → 動作序列。無副作用、可直接斷言(見 T_EmpathyBody)。
    public static class EmpathyGestures
    {
        public const float MaxAbsDeg = 25f;   // 共情動作幅度上限(保守,遠在 NeckZ ±40 內)

        // 安全燈號 + 是否沉默拋題 + 是否登入 → 共情時機。Speak 端據此挑動作。
        public static EmpathyMoment MomentFor(Layer layer, bool isProbe, bool isLogin)
        {
            if (isLogin) return EmpathyMoment.Login;
            if (layer == Layer.Red) return EmpathyMoment.RedHandoff;
            if (layer == Layer.Yellow) return EmpathyMoment.YellowHandoff;
            return isProbe ? EmpathyMoment.Probe : EmpathyMoment.GreenChat;
        }

        public static IReadOnlyList<EmpathyMove> GestureFor(EmpathyMoment moment)
        {
            switch (moment)
            {
                case EmpathyMoment.Login:        // 抬頭打開 → 回中:溫暖招呼
                    return new[] { new EmpathyMove(KebbiMotor.NeckY, -8f, 350), new EmpathyMove(KebbiMotor.NeckY, 0f, 250) };
                case EmpathyMoment.GreenChat:    // 輕點頭(低頭→回中):「嗯,我在聽」
                    return new[] { new EmpathyMove(KebbiMotor.NeckY, 12f, 280), new EmpathyMove(KebbiMotor.NeckY, 0f, 240) };
                case EmpathyMoment.Probe:        // 歪頭邀請(NeckZ 小幅)→ 回中
                    return new[] { new EmpathyMove(KebbiMotor.NeckZ, 10f, 320), new EmpathyMove(KebbiMotor.NeckZ, 0f, 260) };
                case EmpathyMoment.YellowHandoff:// 沉穩前傾、停住(不彈回):接住
                    return new[] { new EmpathyMove(KebbiMotor.NeckY, 14f, 700) };
                case EmpathyMoment.RedHandoff:   // 穩定前傾守住:最克制、低幅長停
                    return new[] { new EmpathyMove(KebbiMotor.NeckY, 10f, 1200) };
                case EmpathyMoment.Rest:
                default:                         // 回中(頭俯仰 + 偏擺歸零)
                    return new[] { new EmpathyMove(KebbiMotor.NeckY, 0f, 200), new EmpathyMove(KebbiMotor.NeckZ, 0f, 200) };
            }
        }
    }

    // 執行層:把動作序列丟給真身體(免授權 setMotor),逐幀以 Task.Delay 停留。
    // 全程 try/catch + fire-and-forget:任何馬達/授權問題都不可影響對話與安全流程。body=null → 全 no-op。
    public sealed class MotorEmpathyBody : IEmpathyBody
    {
        private readonly IKebbiBody _body;
        private readonly float _speed;
        public MotorEmpathyBody(IKebbiBody body, float speed = 35f) { _body = body; _speed = speed; }

        public void Express(EmpathyMoment moment)
        {
            if (_body == null) return;
            _ = PlayAsync(EmpathyGestures.GestureFor(moment));
        }

        private async Task PlayAsync(IReadOnlyList<EmpathyMove> moves)
        {
            try
            {
                foreach (var mv in moves)
                {
                    float deg = Clamp(mv.Degrees, -EmpathyGestures.MaxAbsDeg, EmpathyGestures.MaxAbsDeg);
                    _body.SetMotor(mv.Motor, deg, _speed);
                    if (mv.HoldMs > 0) await Task.Delay(mv.HoldMs);
                }
            }
            catch { /* 共情動作純加分,失敗忽略 */ }
        }

        public void FaceSpeaker()
        {
            if (_body == null) return;
            try { KebbiHead.FaceFully(_body, _body.ReadDoaDegrees()); } catch { }
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
