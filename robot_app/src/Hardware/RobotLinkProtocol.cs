using System;
using System.Text;

namespace KebbiBrain.Hardware
{
    // 多機 UDP 封包的線格式與收件判斷（純 C#，無 Unity 相依 → 主控台可自測）。
    // UnityRobotLink（實機 UDP）走這裡;Sim 端不需要(行程內直送),但測這裡等於測實機收送的「邏輯部分」,
    // 真正剩下未驗的只有 UDP 傳輸本身(必測④)。
    // 封包 = from <SOH> to <SOH> text;to=="*" 為廣播。收件規則:不是自己送的,且收件人是我或廣播。
    public static class RobotLinkProtocol
    {
        public const string All = "*";
        private static readonly char Sep = (char)1; // SOH 分隔字元,不會出現在一般文字

        public static byte[] Frame(string from, string to, string text)
            => Encoding.UTF8.GetBytes((from ?? "") + Sep + (to ?? All) + Sep + (text ?? ""));

        public static bool TryParse(byte[] data, int len, out string from, out string to, out string text)
        {
            from = to = text = null;
            if (data == null || len <= 0) return false;
            return TryParse(Encoding.UTF8.GetString(data, 0, len), out from, out to, out text);
        }

        public static bool TryParse(string raw, out string from, out string to, out string text)
        {
            from = to = text = null;
            if (raw == null) return false;
            var parts = raw.Split(new[] { Sep }, 3); // 保留空欄位(text 可為空)
            if (parts.Length < 3) return false;
            from = parts[0]; to = parts[1]; text = parts[2];
            return true;
        }

        // 收件判斷:不是自己廣播回來的,且收件人是我或廣播。
        public static bool ShouldDeliver(string from, string to, string myId)
            => from != myId && (to == myId || to == All);
    }
}
