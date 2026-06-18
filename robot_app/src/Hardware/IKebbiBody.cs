namespace KebbiBrain.Hardware
{
    // Kebbi 身體（硬體）抽象。只暴露「實測可用」的能力。
    // 注意：刻意「不」提供 turnToDOA / motionPlay / 內建TTS —— 那些是會被 active-code
    // 授權牆擋的 async 命令。轉頭請用「讀 DOA + SetMotor(NeckZ)」的 workaround（見 KebbiHead）。
    public interface IKebbiBody
    {
        // 關節：免授權同步，實測可用
        void SetMotor(KebbiMotor m, float degrees, float speed = 50f);
        float GetMotor(KebbiMotor m);

        // 聲源定位：免授權同步讀取，回傳角度（度）。本專案約定 [-180,180]，0=正前、+右、-左、±180=正後。
        // ⚠️ 解析度/正後方/非語音是否更新＝未驗（real 後端需先實測）。
        float ReadDoaDegrees();

        // 底盤移動：只有「速度」沒有「距離/座標/里程計」。開迴路（速度×時間）。
        // H201 桌上型 CanMove=false（不會動）；輪式 Kebbi CanMove=true。
        bool CanMove { get; }
        void Move(float metersPerSec);
        void Turn(float degPerSec);
        void StopWheels();

        // NeckZ 物理可達範圍（度），real 後端以實測值填入；用於把 DOA 目標角夾限（頭無法轉到正後方）。
        float NeckZMinDeg { get; }
        float NeckZMaxDeg { get; }
    }
}
