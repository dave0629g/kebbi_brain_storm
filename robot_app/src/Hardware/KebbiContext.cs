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

        // 複合面向 FaceFully：底盤 turn() 轉粗方向 + NeckZ(±範圍)補細 → 連 90°/正後方的目標也能完整面向。
        // NeckZ 實機僅 ±40，頭單獨面向不了側邊/後方；輪式 Kebbi(CanMove) 用底盤分擔超出頭部的部分 → 完整面向。
        // H201 桌上型(!CanMove)：底盤不能轉 → 退回頭部單獨夾限，>±範圍只能「部分面向」(等同 TurnToward 的降級)。
        // 底盤為開迴路(turn 度/秒×時間)，實機 turn() 受授權牆(必測③)影響；被擋則行為自然退回頭部部分面向。
        public static FaceResult FaceFully(IKebbiBody body, float doaDeg)
        {
            float a = Direction.Normalize(doaDeg);
            float min = body.NeckZMinDeg, max = body.NeckZMaxDeg;
            float baseTurn = 0f;
            float head = a;
            if (a > max) { head = max; if (body.CanMove) baseTurn = a - max; }
            else if (a < min) { head = min; if (body.CanMove) baseTurn = a - min; }
            // else：a 在頭部可達範圍內 → head=a、baseTurn=0(不必動底盤)

            if (baseTurn != 0f && body.CanMove)
            {
                body.Turn(baseTurn >= 0f ? -30f : 30f); // 朝目標轉(右=DOA+；SDK turn 右=負速)；開迴路,需配時。實機 sign 待必測③核對
                body.StopWheels();
            }
            body.SetMotor(KebbiMotor.NeckZ, head);
            float faced = baseTurn + head;
            bool full = Math.Abs(Direction.Normalize(faced - a)) < 0.5f;
            return new FaceResult(baseTurn, head, faced, full);
        }
    }

    // FaceFully 的結果：底盤要轉的角度 + 頭部 NeckZ 角度 + 合成面向角 + 是否完整面向。
    // LangVersion 9.0：用 class（非 record）；公開欄位。
    public sealed class FaceResult
    {
        public float BaseTurnDeg;   // 底盤需轉的角度(DOA 框架,+=朝右/+DOA;0=不需轉底盤)
        public float HeadDeg;       // NeckZ 實際角度(夾在 ±範圍內)
        public float FacedAngle;    // 合成後實際面向角(BaseTurnDeg + HeadDeg)
        public bool FullyFaced;     // 是否完整面向目標(輪式可;H201 桌上型對 >±範圍只能部分)
        public FaceResult(float baseTurn, float head, float faced, bool full)
        { BaseTurnDeg = baseTurn; HeadDeg = head; FacedAngle = faced; FullyFaced = full; }
    }
}
