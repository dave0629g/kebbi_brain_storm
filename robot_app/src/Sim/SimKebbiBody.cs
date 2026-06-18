using System;
using System.Collections.Generic;
using KebbiBrain.Hardware;

namespace KebbiBrain.Sim
{
    // 文字型 Kebbi 身體模擬器：把每個硬體動作印在主控台，並讓測試腳本注入 DOA。
    public sealed class SimKebbiBody : IKebbiBody
    {
        private readonly Dictionary<KebbiMotor, float> _motors = new Dictionary<KebbiMotor, float>();
        private readonly Action<string> _out;

        // 測試腳本設定「目前聲源角度」（模擬說話者方位）
        public float CurrentDoa { get; set; } = 0f;

        public SimKebbiBody(Action<string> output, bool canMove = true)
        {
            _out = output ?? Console.WriteLine;
            CanMove = canMove;
            foreach (KebbiMotor m in Enum.GetValues(typeof(KebbiMotor))) _motors[m] = 0f;
        }

        public void SetMotor(KebbiMotor m, float degrees, float speed = 50f)
        {
            _motors[m] = degrees;
            _out($"   🦾 [關節] {m} → {degrees,6:0.0}°  (speed {speed:0})");
        }

        public float GetMotor(KebbiMotor m) => _motors.TryGetValue(m, out var v) ? v : 0f;

        public float ReadDoaDegrees()
        {
            _out($"   👂 [DOA] 讀到聲源方位 {CurrentDoa,6:0.0}° ({Direction.ToZh(Direction.FromAngle(CurrentDoa))})");
            return CurrentDoa;
        }

        public bool CanMove { get; }

        public void Move(float metersPerSec)
        {
            if (!CanMove) { _out($"   🛑 [底盤] move({metersPerSec}) 無效：此機型(H201)無底盤、不會移動"); return; }
            _out($"   🚗 [底盤] 前進 {metersPerSec:0.00} m/s（開迴路，需配合計時）");
        }

        public void Turn(float degPerSec)
        {
            if (!CanMove) { _out($"   🛑 [底盤] turn({degPerSec}) 無效：此機型無底盤"); return; }
            _out($"   🔄 [底盤] 旋轉 {degPerSec:0.0} deg/s（開迴路）");
        }

        public void StopWheels()
        {
            if (!CanMove) return;
            _out("   ⏹️  [底盤] 停");
        }

        // 頭部偏擺實測可達範圍（模擬值；real 後端以真機量測填入）。刻意 < 180 表示頭轉不到正後方。
        public float NeckZMinDeg => -90f;
        public float NeckZMaxDeg => 90f;
    }
}
