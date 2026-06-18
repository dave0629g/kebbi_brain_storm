using System;

namespace KebbiBrain.Hardware
{
    // 方位（以機器人為原點）。角度約定：0=正前(depan)、+90=右(kanan)、±180=正後(belakang)、-90=左(kiri)。
    public enum Dir { Depan, Kanan, Belakang, Kiri }

    public static class Direction
    {
        // 度數正規化到 [-180,180]
        public static float Normalize(float deg)
        {
            while (deg > 180f) deg -= 360f;
            while (deg < -180f) deg += 360f;
            return deg;
        }

        // 角度 → 方位扇區（每區 90°）
        public static Dir FromAngle(float deg)
        {
            float a = Normalize(deg);
            if (a > -45f && a <= 45f) return Dir.Depan;
            if (a > 45f && a <= 135f) return Dir.Kanan;
            if (a > -135f && a <= -45f) return Dir.Kiri;
            return Dir.Belakang; // |a| > 135
        }

        // 方位 → 印尼語
        public static string ToIndo(Dir d)
        {
            switch (d)
            {
                case Dir.Depan: return "depan";
                case Dir.Kanan: return "kanan";
                case Dir.Belakang: return "belakang";
                case Dir.Kiri: return "kiri";
                default: return "?";
            }
        }

        // 方位 → 中文
        public static string ToZh(Dir d)
        {
            switch (d)
            {
                case Dir.Depan: return "前面";
                case Dir.Kanan: return "右邊";
                case Dir.Belakang: return "後面";
                case Dir.Kiri: return "左邊";
                default: return "?";
            }
        }

        // 從學生說的句子裡解析方位詞（含 kiri/kanan/depan/belakang；找不到回傳 null）
        public static Dir? ParseIndo(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string t = text.ToLowerInvariant();
            if (t.Contains("belakang")) return Dir.Belakang;
            if (t.Contains("depan")) return Dir.Depan;
            if (t.Contains("kanan")) return Dir.Kanan;
            if (t.Contains("kiri")) return Dir.Kiri;
            return null;
        }
    }
}
