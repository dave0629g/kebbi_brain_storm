using System;

namespace KebbiBrain.Hardware
{
    // 把身體 + 語音 + LLM + 記錄器打包，注入給各隊 App。
    public sealed class KebbiContext
    {
        public IKebbiBody Body { get; }
        public IVoice Voice { get; }
        public ILlm Llm { get; }
        public Action<string> Log { get; }

        public KebbiContext(IKebbiBody body, IVoice voice, ILlm llm, Action<string> log)
        {
            Body = body;
            Voice = voice;
            Llm = llm;
            Log = log ?? (s => { });
        }
    }

    // 轉向 workaround：讀 DOA → 自己寫 NeckZ（取代被授權牆擋的 turnToDOA）。
    // 會把目標角夾限到 NeckZ 物理可達範圍（頭轉不到正後方）。
    public static class KebbiHead
    {
        // 回傳實際轉到的角度；outReachable 標示是否能完整面向（false=被夾限，例如聲源在正後方）。
        public static float TurnToward(IKebbiBody body, float doaDeg, out bool reachable)
        {
            float a = Direction.Normalize(doaDeg);
            float min = body.NeckZMinDeg, max = body.NeckZMaxDeg;
            float clamped = a;
            if (clamped < min) clamped = min;
            if (clamped > max) clamped = max;
            reachable = Math.Abs(clamped - a) < 0.5f;
            body.SetMotor(KebbiMotor.NeckZ, clamped);
            return clamped;
        }

        // 多載：除了回傳角度/可達旗標,再回報「夾限後實體頭真正面向的扇區」。
        // 8 向細粒度下,斜向/正後方常不可達 → reachedSector 是頭能轉到的最接近扇區(非原扇區),體現馬達可達性決定判決。
        // 向後相容:既有 3 參數多載不動,本多載內部呼叫它(同樣 SetMotor + 夾限)。
        public static float TurnToward(IKebbiBody body, float doaDeg, out bool reachable, out Dir reachedSector)
        {
            float clamped = TurnToward(body, doaDeg, out reachable);
            reachedSector = Direction.FromAngle(clamped);
            return clamped;
        }
    }
}
