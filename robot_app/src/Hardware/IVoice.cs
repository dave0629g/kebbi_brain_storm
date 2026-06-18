using System.Threading.Tasks;

namespace KebbiBrain.Hardware
{
    // 語音 I/O 抽象（喇叭+TTS / 麥克風+ASR）。
    // 內建語音無印尼語，real 後端走外部雲端（Azure/Google id-ID）經 HTTP，再用喇叭播放。
    // ⚠️ real 端先決條件：能在系統持麥時搶到麥克風取 PCM（G4 第一必測）。
    public interface IVoice
    {
        // 說（lang 例："id-ID"、"zh-TW"）
        Task SpeakAsync(string text, string lang = "id-ID");

        // 聽一句，回傳辨識文字（lang 指定辨識語言）
        Task<string> ListenAsync(string lang = "id-ID");
    }
}
