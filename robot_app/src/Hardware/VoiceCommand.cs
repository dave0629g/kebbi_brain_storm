using System;

namespace KebbiBrain.Hardware
{
    // 把語音動作(SpeakAsync)序列化成文字命令,經 IRobotLink 送到「另一台」Kebbi 用它「自己的喇叭」說。
    // 用途:多機 Director 模式(G5 辯方、G2 乙機)被控機要「自己開口說台詞」,不只動機身 → 與 BodyCommand 並用。
    // 純 C#(無 Unity 相依)→ 主控台可自測;Sim 與 Unity 後端共用同一份線格式。
    // 線格式(以 '|' 分隔,前綴 VC 以區隔 BC 機身命令與 HANDOFF/CUE 等一般 link 訊息):
    //   VC|SAY|<lang>|<text>   中控→被控:說一句
    //   VC|DONE                被控→中控:我「播畢」了(供「說完才交棒」握手;搭配 RemoteVoiceProxy 等播畢 + BodyCommandReceiver ackVoiceDone)
    // 設計重點:
    //   • text 是自由文字(可能含 '|'、換行),故解析用「限數量切割(最多 4 欄)」把 text 整段留在最後一欄。
    //   • lang 是「中間欄位」,不受限數量切割保護 → Speak 對含 '|'/換行的 lang fail-fast 拋例外(契約)。
    //   • lang 預設值「不過 wire」(C# 預設值只在呼叫端省略時生效),故送出/解析兩端都對空 lang 回填預設。
    public static class VoiceCommand
    {
        public const string Prefix = "VC";
        public const string DefaultLang = "id-ID";
        // char[] 多載 new[]{D} 才能在 netstandard2.1 / IL2CPP(Unity 2022.3)編譯;
        // 勿改成單一 char 的 Split('|', n, ...) 多載(那是 .NET Core 2.1+ 才有 → Unity 端編不過、net8 卻過,兩端不一致)。
        private const char D = '|';

        // 序列化「說一句」。text 可含 '|'/換行(留最後一欄);lang 不可含分隔符(中間欄位);lang 空→回填預設。
        public static string Speak(string text, string lang = DefaultLang)
        {
            lang = Norm(lang);
            if (lang.IndexOf(D) >= 0 || lang.IndexOf('\n') >= 0)
                throw new ArgumentException("lang 不可含分隔符 '|' 或換行: " + lang, nameof(lang));
            return Prefix + D + "SAY" + D + lang + D + (text ?? "");
        }

        // 解析 VC|SAY|lang|text → 取出 lang/text。非 SAY 或欄位不足回 false(不丟例外)。供 TryApply 與被控端 ackVoiceDone 路徑共用。
        public static bool TryParseSay(string msg, out string lang, out string text)
        {
            lang = DefaultLang; text = null;
            if (string.IsNullOrEmpty(msg)) return false;
            var p = msg.Split(new[] { D }, 4, StringSplitOptions.None); // 限 4 欄:text(末欄)保留內含的 '|'
            if (p.Length < 2 || p[0] != Prefix) return false;
            // 含空 text 的 "VC|SAY|zh-TW|" 切出 len=4、p[3]="";缺欄位的 "VC|SAY"/"VC|SAY|zh-TW" 切出 len=2/3 → 落到 false。
            if (p[1] == "SAY" && p.Length >= 4) { lang = Norm(p[2]); text = p[3]; return true; }
            return false;
        }

        // 若 msg 是語音命令(VC|SAY|…) → 套到 voice(fire-and-forget)並回 true;否則回 false(讓呼叫端把它當一般訊息轉交)。
        public static bool TryApply(string msg, IVoice voice)
        {
            if (voice == null) return false;
            if (TryParseSay(msg, out var lang, out var text)) { _ = voice.SpeakAsync(text, lang); return true; }
            return false;
        }

        // 播畢回報(被控→中控)。RemoteVoiceProxy 等播畢時 await 這則。
        public static string Done() => Prefix + D + "DONE";
        public static bool IsDone(string msg) => msg == Prefix + D + "DONE";

        private static string Norm(string lang) => string.IsNullOrEmpty(lang) ? DefaultLang : lang;
    }
}
