using System;
using System.Collections.Generic;

namespace KebbiBrain.Hardware
{
    // 開機自檢核心(純):每個功能需要哪些資源(金鑰/麥克風/相機/網路)+ 把需求對現況聚合成 go/no-go。
    // 動機:demo 最怕「點下去沒反應」卻不知是金鑰/權限/WiFi → 一進選單就把缺的標出來,非工程的人也能自助排除。
    // Unity 端只負責提供「每項現況 ok 嗎」(金鑰走 SecretsCheck、權限走 Permission、網路走 internetReachability)。
    public enum PreflightItem { SpeechKey, LlmKey, GeminiKey, Microphone, Camera, Network }

    public struct PreflightResult { public PreflightItem Item; public bool Ok; public string Label; public string Hint; }

    public static class PreflightCore
    {
        // 功能(KebbiAppBehaviour.Mode 名)→ 所需資源(純對映,可斷言)。
        public static IReadOnlyList<PreflightItem> RequirementsFor(string feature)
        {
            switch (feature)
            {
                case "Counselor": return new[] { PreflightItem.LlmKey, PreflightItem.SpeechKey, PreflightItem.Microphone };
                case "LiveConversation": return new[] { PreflightItem.GeminiKey, PreflightItem.Microphone };
                case "RoboticsVision": return new[] { PreflightItem.GeminiKey, PreflightItem.Camera };
                case "RoboVisionTalk": return new[] { PreflightItem.GeminiKey, PreflightItem.Camera, PreflightItem.Microphone };
                case "G4_TebakArah": return new[] { PreflightItem.SpeechKey, PreflightItem.Microphone };
                case "ConverseStt": return new[] { PreflightItem.SpeechKey, PreflightItem.LlmKey, PreflightItem.Microphone, PreflightItem.Network };
                case "Converse": return new[] { PreflightItem.SpeechKey, PreflightItem.LlmKey, PreflightItem.Network };
                case "G3_MirrorCoach": return new[] { PreflightItem.SpeechKey };   // 只 TTS,缺也能降級示範
                default: return new PreflightItem[0];
            }
        }

        // 需求 × 現況(isOk) → 檢查結果清單。hint 可給未過項的補充說明。
        public static List<PreflightResult> Evaluate(IReadOnlyList<PreflightItem> reqs, Func<PreflightItem, bool> isOk, Func<PreflightItem, string> hint = null)
        {
            var outp = new List<PreflightResult>();
            if (reqs == null || isOk == null) return outp;
            foreach (var it in reqs)
            {
                bool ok = isOk(it);
                outp.Add(new PreflightResult { Item = it, Ok = ok, Label = Label(it), Hint = ok ? "OK" : (hint != null ? hint(it) : DefaultHint(it)) });
            }
            return outp;
        }

        public static bool AllOk(List<PreflightResult> results)
        {
            if (results == null) return true;
            foreach (var r in results) if (!r.Ok) return false;
            return true;
        }

        // 未過項清單(給警示文字用)。
        public static List<string> Failing(List<PreflightResult> results)
        {
            var outp = new List<string>();
            if (results == null) return outp;
            foreach (var r in results) if (!r.Ok) outp.Add(r.Label);
            return outp;
        }

        public static string Label(PreflightItem it)
        {
            switch (it)
            {
                case PreflightItem.SpeechKey: return "語音金鑰";
                case PreflightItem.LlmKey: return "LLM 金鑰";
                case PreflightItem.GeminiKey: return "Gemini 金鑰";
                case PreflightItem.Microphone: return "麥克風權限";
                case PreflightItem.Camera: return "相機權限";
                case PreflightItem.Network: return "網路連線";
                default: return it.ToString();
            }
        }

        private static string DefaultHint(PreflightItem it)
        {
            switch (it)
            {
                case PreflightItem.Microphone:
                case PreflightItem.Camera: return "未授權";
                case PreflightItem.Network: return "未連線";
                default: return "未設定/疑似佔位";
            }
        }
    }
}
