namespace KebbiBrain.Hardware
{
    // 馬達 ID 對應實測 SDK（10 顆：頭 2 + 右臂 4 + 左臂 4）。
    // 數值即 NuwaMotorType 的 id，real 後端直接拿來呼叫 setMotorPositionInDegree。
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
