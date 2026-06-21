using System;
using KebbiBrain.Hardware;

namespace KebbiBrain.Sim
{
    // 文字型臉部表情模擬器:記錄目前表情與嘴型,供測試斷言。
    public sealed class SimFaceExpression : IFaceExpression
    {
        public FaceExpression Last { get; private set; } = FaceExpression.Neutral;
        public bool Talking { get; private set; }
        private readonly Action<string> _out;

        public SimFaceExpression(Action<string> output = null) { _out = output ?? (s => { }); }

        public void Show(FaceExpression expr) { Last = expr; _out("   🙂 [臉] " + expr); }
        public void Mouth(bool talking) { Talking = talking; }
    }
}
