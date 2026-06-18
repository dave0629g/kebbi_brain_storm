using System;
using KebbiBrain.Hardware;
using KebbiBrain.Sim;

namespace KebbiBrain
{
    // 依 Target 建立硬體上下文。這就是「切參數即轉實機」的接點。
    public static class KebbiFactory
    {
        public static KebbiContext Create(RobotTarget target, Action<string> log)
        {
            if (target == RobotTarget.Sim)
            {
                return new KebbiContext(new SimKebbiBody(log), new SimVoice(log), new SimLlm(log), log);
            }

#if !UNITY
            // CloudSim：真 Azure 印尼語語音 + 真 Claude，但機器人仍是文字模擬器（免實機，主控台可跑）。
            if (target == RobotTarget.CloudSim)
            {
                if (string.IsNullOrEmpty(Config.SpeechKey) || string.IsNullOrEmpty(Config.SpeechRegion))
                    throw new InvalidOperationException("CloudSim 需要 KEBBI_SPEECH_KEY 與 KEBBI_SPEECH_REGION（見 金鑰申請步驟.md）。");
                var speech = new Cloud.AzureSpeech(Config.SpeechKey, Config.SpeechRegion);
                IVoice voice = new Cloud.AzureVoice(speech, Config.SpeechVoice, log);
                return new KebbiContext(new SimKebbiBody(log, canMove: false), voice, CreateLlm(log), log);
            }
#endif

#if UNITY
            // Unity build：語音/LLM 走雲端(UnityVoice/UnityLlm)、多機走 UnityRobotLink(由各遊戲自行建立)。
            // 機身二擇一(見 Config.UseRealRobotApi)：
            //   • 真 Kebbi → UnityKebbiBody(NuwaRobotAPI)
            //   • 一般 Android(模擬器/手機,非凱比) → SimKebbiBody(模擬馬達/DOA;DOA 可由螢幕/腳本設定)
            //     → 讓「Android 當模擬器、Unity 當 middleware」能測除馬達/DOA 外的所有功能、含多機 UDP 互連。
            IKebbiBody realBody = Config.UseRealRobotApi
                ? (IKebbiBody)new KebbiBrain.Real.UnityKebbiBody()
                : new SimKebbiBody(log, canMove: false);
            return new KebbiContext(
                realBody,
                new KebbiBrain.Real.UnityVoice(Config.SpeechProvider, Config.SpeechKey, Config.SpeechRegion, Config.SpeechVoice),
                new KebbiBrain.Real.UnityLlm(Config.LlmProvider, Config.LlmKey),
                log);
#else
            throw new NotSupportedException(
                "RobotTarget.Real 只能在 Unity 實機建置使用（需定義編譯符號 UNITY）。" +
                "主控台自測請用 RobotTarget.Sim。");
#endif
        }

#if !UNITY
        // 依 KEBBI_LLM_PROVIDER 或金鑰前綴自動選 LLM provider（sk-ant…→Anthropic，其餘→OpenAI）。
        public static ILlm CreateLlm(Action<string> log)
        {
            if (string.IsNullOrEmpty(Config.LlmKey)) { log("   ℹ 未設 KEBBI_LLM_KEY → 用內建 SimLlm 提示"); return new SimLlm(log); }
            string provider = Config.LlmProvider;
            if (string.IsNullOrEmpty(provider))
                provider = Config.LlmKey.StartsWith("sk-ant") ? "anthropic" : "openai";
            if (provider == "openai")
            {
                log("   ℹ LLM provider=openai model=" + Config.OpenAiModel);
                return new Cloud.OpenAiLlm(Config.LlmKey, Config.OpenAiModel, log);
            }
            log("   ℹ LLM provider=anthropic model=" + Config.LlmModel);
            return new Cloud.ClaudeLlm(Config.LlmKey, Config.LlmModel, log);
        }
#endif
    }
}
