using System;
using System.Collections.Generic;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // 視覺安全之眼(隱私優先):只判「有沒有人 + 大致水平位置」,不辨識身分、不描述外觀、不儲存影像。
    // 用途:把純語音陪伴升級成「看到人才打招呼/轉頭看你、人離開降為待命」的存在感。
    // 鐵律(隱私紅線):提示只問 presence/position;相機幀拍完即丟(不落檔);解析只取 person + x,其餘一律丟棄。
    public struct PresenceState
    {
        public bool PersonPresent;
        public float PositionX;   // 0–1000 水平位置(左0右1000);<0 = 未知/沒有人
    }

    public static class PresenceVision
    {
        // 隱私優先提示:只問有沒有人 + 大致位置,明確禁止辨識身分/描述。
        public static string PresencePrompt()
            => "只判斷畫面中是否有『人』,以及人大致的水平位置。只輸出 JSON:" +
               "{\"person\":true或false,\"x\":0到1000的整數(人的水平中心,左0右1000;沒有人時可省略)}。" +
               "不要辨識身分、不要描述外觀或穿著、不要列出其他物體、不要任何多餘文字。";

        private static readonly string[] PersonWords =
        { "person", "people", "human", "face", "人", "臉", "男", "女", "孩", "童", "學生", "老師", "人物" };

        // 解析回應 → 只取 presence + position(其餘全丟)。容錯兩種形狀:
        //   (a) 最小 JSON {"person":true,"x":620};(b) Robotics-ER 偵測陣列含 person/人 標籤。
        public static PresenceState FromResponse(string json)
        {
            var none = new PresenceState { PersonPresent = false, PositionX = -1f };
            if (string.IsNullOrEmpty(json)) return none;

            // (b) 先試偵測陣列:有 person/人 標籤 → 有人,取其水平中心
            var dets = GeminiRoboticsProtocol.ParseDetections(json);
            foreach (var d in dets)
                if (IsPersonLabel(d.Label))
                    return new PresenceState { PersonPresent = true, PositionX = RoboGuideMath.CenterX(d) };

            // (a) 再試最小 JSON
            string compact = json.Replace(" ", "").ToLowerInvariant();
            if (compact.Contains("\"person\":true") || compact.Contains("\"present\":true"))
                return new PresenceState { PersonPresent = true, PositionX = ExtractX(json) };

            return none;
        }

        public static bool IsPersonLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return false;
            string low = label.ToLowerInvariant();
            foreach (var w in PersonWords) if (low.Contains(w) || label.Contains(w)) return true;
            return false;
        }

        // 取 "x":<數字>(0–1000);找不到回 -1。
        public static float ExtractX(string json)
        {
            if (string.IsNullOrEmpty(json)) return -1f;
            int k = json.IndexOf("\"x\"", StringComparison.OrdinalIgnoreCase);
            if (k < 0) return -1f;
            int i = json.IndexOf(':', k);
            if (i < 0) return -1f;
            i++;
            while (i < json.Length && (json[i] == ' ' || json[i] == '"')) i++;
            int start = i;
            while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == '-')) i++;
            if (i == start) return -1f;
            return float.TryParse(json.Substring(start, i - start), out var v) ? v : -1f;
        }
    }

    public enum PresenceEvent { None, Arrived, Left, StillPresent, StillAbsent, Moved }

    // 去抖狀態機:需連續 ConfirmFrames 幀同狀態才確認「有人來了/離開了」(避免偵測抖動誤觸)。
    public sealed class PresenceWatcher
    {
        public bool Present { get; private set; }
        public float LastX { get; private set; } = -1f;
        public int ConfirmFrames;
        public float MoveThreshold;     // 水平移動超過此值(0–1000)才算「移動了」→ 重新面向

        private int _pendingCount;
        private bool _pendingPresent;

        public PresenceWatcher(int confirmFrames = 2, float moveThreshold = 120f)
        { ConfirmFrames = confirmFrames < 1 ? 1 : confirmFrames; MoveThreshold = moveThreshold; }

        public PresenceEvent Observe(PresenceState s)
        {
            bool obs = s.PersonPresent;
            if (obs == Present)
            {
                _pendingCount = 0;
                if (Present)
                {
                    bool moved = s.PositionX >= 0 && LastX >= 0 && Math.Abs(s.PositionX - LastX) >= MoveThreshold;
                    if (s.PositionX >= 0) LastX = s.PositionX;
                    return moved ? PresenceEvent.Moved : PresenceEvent.StillPresent;
                }
                return PresenceEvent.StillAbsent;
            }

            // 觀測與已確認不同 → 累積,夠了才翻轉
            if (_pendingPresent != obs) { _pendingPresent = obs; _pendingCount = 1; }
            else _pendingCount++;

            if (_pendingCount >= ConfirmFrames)
            {
                Present = obs; _pendingCount = 0;
                if (s.PositionX >= 0) LastX = s.PositionX;
                return obs ? PresenceEvent.Arrived : PresenceEvent.Left;
            }
            return Present ? PresenceEvent.StillPresent : PresenceEvent.StillAbsent;
        }
    }
}
