using System.Threading.Tasks;

namespace KebbiBrain.Hardware
{
    // 姿態感測抽象（G3 鏡像教練用）。
    // real = Unity WebCamTexture 取影像 → 自己跑 MediaPipe 比對學生關節（相機權限未驗，見 BLOCKED）。
    // sim  = 腳本回傳對/錯，免相機免機器人即可自測。
    public interface IPoseSensor
    {
        // 學生是否做對指定動作
        Task<bool> CheckPoseAsync(string poseName);
    }
}
