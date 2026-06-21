using System;

namespace KebbiBrain.Hardware
{
    // 人體存在感測抽象 —— 對映 Kebbi Air S 的 **PIR 硬體感測器**(`requestSensor(SENSOR_PIR)` + `onPIREvent(val)`)。
    // 非視覺、零雲端、零延遲、零隱私風險 → 用來知道「學生來了/離開」,驅動迎接/降待命(取代相機版存在偵測)。
    // ⚠ Real(真凱比)接線:`requestSensor(NuwaRobotAPI.SENSOR_PIR)` 一次 + `registerRobotEventListener` 的
    //   `onPIREvent(int val)` → 轉成 OnPresence(val>0)。屬真機驗項(同觸控 onTouchEvent)。
    public interface IPresenceSensor
    {
        void OnPresence(Action<bool> handler);  // PIR 事件:true=偵測到人、false=沒人
    }
}
