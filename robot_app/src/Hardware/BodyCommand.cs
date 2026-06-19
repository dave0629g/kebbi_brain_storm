using System.Globalization;

namespace KebbiBrain.Hardware
{
    // 把機身動作(SetMotor/Move/Turn/Stop)序列化成文字,經 IRobotLink 送到「另一台」Kebbi 執行。
    // 用途:讓一台中控(director)用既有遊戲邏輯(G1/G2/G5)同時驅動本機 + 遠端機身 → 真機多台分散式跑同一套劇本。
    // 純 C#(無 Unity 相依)→ 主控台可自測;Sim 與 Unity 後端共用同一份線格式。
    // 線格式(以 '|' 分隔,前綴 BC 以區隔 HANDOFF/CUE 等一般 link 訊息):
    //   BC|SM|<motorId>|<deg>|<speed>   BC|MV|<mps>   BC|TN|<dps>   BC|ST
    public static class BodyCommand
    {
        public const string Prefix = "BC";
        private const char D = '|';
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string SetMotor(KebbiMotor m, float deg, float speed)
            => Prefix + D + "SM" + D + ((int)m) + D + F(deg) + D + F(speed);
        public static string Move(float mps) => Prefix + D + "MV" + D + F(mps);
        public static string Turn(float dps) => Prefix + D + "TN" + D + F(dps);
        public static string Stop() => Prefix + D + "ST";

        // 若 msg 是機身命令 → 套到 body 並回 true;否則回 false(讓呼叫端把它當一般訊息轉交)。
        // ⚠️ msg 來自網路(可能損壞):一律用 TryParse + 馬達 ID 邊界檢查,畸形封包「拒絕(回 false)」
        //    而非丟例外崩掉被控機的接收迴圈。
        public static bool TryApply(string msg, IKebbiBody body)
        {
            if (body == null || string.IsNullOrEmpty(msg)) return false;
            var p = msg.Split(D);
            if (p.Length < 2 || p[0] != Prefix) return false;
            string op = p[1];
            if (op == "SM" && p.Length >= 5)
            {
                if (!int.TryParse(p[2], NumberStyles.Integer, Inv, out int mid)) return false;
                if (!System.Enum.IsDefined(typeof(KebbiMotor), mid)) return false; // 未知馬達 ID → 拒絕
                if (!TryP(p[3], out float deg) || !TryP(p[4], out float speed)) return false;
                body.SetMotor((KebbiMotor)mid, deg, speed); return true;
            }
            if (op == "MV" && p.Length >= 3) { if (!TryP(p[2], out float v)) return false; body.Move(v); return true; }
            if (op == "TN" && p.Length >= 3) { if (!TryP(p[2], out float v)) return false; body.Turn(v); return true; }
            if (op == "ST") { body.StopWheels(); return true; }
            return false;
        }

        private static string F(float v) => v.ToString("0.###", Inv);
        private static bool TryP(string s, out float v) => float.TryParse(s, NumberStyles.Float, Inv, out v);
    }
}
