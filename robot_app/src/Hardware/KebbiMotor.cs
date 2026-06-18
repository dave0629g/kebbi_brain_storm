namespace KebbiBrain.Hardware
{
    // 馬達 ID（10 顆：頭 2 + 右臂 4 + 左臂 4）。
    // 數值 == NuwaSDK 2.1.0.08 的 MOTOR_* 常數(MOTOR_NECK_Y=1…MOTOR_LEFT_ELBOW_Y=10)，已對真 aar 反查驗證一致(見 Tests T_NuwaMotorIds)。
    // real 後端直接拿 (int) 值呼叫 ctlMotor(id, 度, 速)/getMotorPresentPositionInDegree(id)。
    public enum KebbiMotor
    {
        NeckY = 1,        // 頭：俯仰
        NeckZ = 2,        // 頭：偏擺（左右轉頭）—— 轉向說話者主要用這顆
        RShoulderZ = 3,
        RShoulderY = 4,
        RShoulderX = 5,
        RElbowY = 6,
        LShoulderZ = 7,
        LShoulderY = 8,
        LShoulderX = 9,
        LElbowY = 10
    }
}
