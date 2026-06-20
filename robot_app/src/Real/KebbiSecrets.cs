// 金鑰注入用 ScriptableObject。整檔 #if UNITY。
// Unity 沒有環境變數 → 把雲端金鑰放在一個 .asset 設定檔,於開機 ApplyToConfig() 推入 Config。
//
// ⚠️ 安全:含金鑰的 .asset 會被打包進 APK。請務必:
//   1) 把該 .asset 路徑加入 .gitignore(勿提交版本庫);
//   2) 正式賽/公開場合改用更安全來源(開機讀本地檔、後端換臨時 token);
//   3) 用完即 rotate。
//
// 建立方式:Unity 選單 Assets → Create → Kebbi → Secrets,填好欄位,拖到場景的
//   KebbiAppBehaviour.secrets 欄位即可。
#if UNITY
using UnityEngine;

namespace KebbiBrain.Real
{
    [CreateAssetMenu(fileName = "KebbiSecrets", menuName = "Kebbi/Secrets")]
    public sealed class KebbiSecrets : ScriptableObject
    {
        [Header("Azure 語音(印尼語 TTS/STT)")]
        public string speechKey = "";
        public string speechRegion = "southeastasia";
        public string speechVoice = "id-ID-GadisNeural";

        [Header("LLM(OpenAI 或 Anthropic;provider 留空=依金鑰前綴自動選)")]
        public string llmKey = "";
        public string llmProvider = "";          // "openai" | "anthropic" | "" (auto)
        public string openAiModel = "gpt-4o-mini";
        public string anthropicModel = "claude-opus-4-8";

        [Header("Gemini(視覺 Robotics-ER;之後 Live API 也用同一把)")]
        public string geminiKey = "";
        public string geminiVisionModel = "gemini-robotics-er-1.6-preview";

        // 把設定推入靜態 Config(KebbiFactory 會讀 Config 建立 Real 後端)。
        public void ApplyToConfig()
        {
            if (!string.IsNullOrEmpty(speechKey)) Config.SpeechKey = speechKey;
            if (!string.IsNullOrEmpty(speechRegion)) Config.SpeechRegion = speechRegion;
            if (!string.IsNullOrEmpty(speechVoice)) Config.SpeechVoice = speechVoice;
            if (!string.IsNullOrEmpty(llmKey)) Config.LlmKey = llmKey;
            if (!string.IsNullOrEmpty(llmProvider)) Config.LlmProvider = llmProvider;
            if (!string.IsNullOrEmpty(openAiModel)) Config.OpenAiModel = openAiModel;
            if (!string.IsNullOrEmpty(anthropicModel)) Config.LlmModel = anthropicModel;
            if (!string.IsNullOrEmpty(geminiKey)) Config.GeminiKey = geminiKey;
            if (!string.IsNullOrEmpty(geminiVisionModel)) Config.GeminiVisionModel = geminiVisionModel;
        }
    }
}
#endif
