using System;

namespace KebbiBrain
{
    // Sim=純文字模擬器(免金鑰)；CloudSim=真雲端語音/LLM + 模擬機器人(免機器人,主控台可跑)；Real=Unity 實機。
    public enum RobotTarget { Sim, CloudSim, Real }

    // 部署設定。切 Target 即切換後端；雲端金鑰由環境變數注入，勿硬編進版本庫。
    public static class Config
    {
        public static RobotTarget Target = RobotTarget.Sim;

        // 「Android 中介測試」開關（僅 Real/Unity build 有意義）。
        //   true  = 真 Kebbi：機身走 NuwaRobotAPI（UnityKebbiBody）。
        //   false = 一般 Android(模擬器/手機,非凱比)：機身用模擬器(SimKebbiBody)，
        //           但語音/LLM/多機 UDP 仍是真的 → 可在 Android 上測「除了馬達/DOA 以外」的所有功能、含多機互連。
        public static bool UseRealRobotApi = true;

        // ↓↓ CloudSim 與 Real 後端需要（環境變數注入）↓↓
        public static string SpeechProvider = "azure";                  // azure | google
        public static string SpeechKey = Env("KEBBI_SPEECH_KEY");
        public static string SpeechRegion = Env("KEBBI_SPEECH_REGION"); // 例 southeastasia
        public static string SpeechVoice = "id-ID-GadisNeural";         // Azure 印尼語原生女聲(男聲 id-ID-ArdiNeural)
        public static string LlmProvider = Env("KEBBI_LLM_PROVIDER");   // anthropic | openai | (空=依金鑰前綴自動偵測)
        public static string LlmKey = Env("KEBBI_LLM_KEY");
        public static string LlmModel = "claude-opus-4-8";              // Anthropic 用(可改 claude-haiku-4-5)
        public static string OpenAiModel = "gpt-4o-mini";              // OpenAI 用(可改 gpt-4o)

        // Gemini(視覺 Robotics-ER;之後 Live API 也用同一把 key)。
        public static string GeminiKey = Env("KEBBI_GEMINI_KEY");
        public static string GeminiVisionModel = "gemini-robotics-er-1.6-preview"; // 視覺認物/指認/座標

        private static string Env(string k) => Environment.GetEnvironmentVariable(k) ?? "";

        // 依語言挑 Azure 語音。原本只有單一 SpeechVoice(印尼語),但 G1/G2/G3/G5 講中文(zh-TW)、只有 G4 講印尼語(id-ID)
        //  → 單一語音會讓中文台詞用印尼語聲線合成而被 Azure 拒(語音/語言不符)。改為依 lang 自動選聲線:
        //   configuredVoice 與該語言相符就用它;否則中文用 zh-TW 預設、印尼語用 id-ID 預設。
        public static string VoiceForLang(string lang, string configuredVoice = null)
        {
            string v = string.IsNullOrEmpty(configuredVoice) ? SpeechVoice : configuredVoice;
            if (!string.IsNullOrEmpty(v) && !string.IsNullOrEmpty(lang) && v.StartsWith(lang, StringComparison.OrdinalIgnoreCase))
                return v;                                              // 配置聲線就是這語言 → 用它
            if (!string.IsNullOrEmpty(lang) && lang.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return "zh-TW-HsiaoChenNeural";                        // 台灣中文女聲(男聲 zh-TW-YunJheNeural)
            if (!string.IsNullOrEmpty(lang) && lang.StartsWith("id", StringComparison.OrdinalIgnoreCase))
                return "id-ID-GadisNeural";                            // 印尼語女聲
            return string.IsNullOrEmpty(v) ? "id-ID-GadisNeural" : v;  // fallback
        }
    }
}
