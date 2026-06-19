using System;

namespace KebbiBrain.Hardware
{
    // 方位（以機器人為原點）。角度約定：0=正前(depan)、+90=右(kanan)、±180=正後(belakang)、-90=左(kiri)。
    // 8 向細粒度(每區 45°)：在四正向之間補入斜向(serong=印尼語「斜」)：
    //   右前 SerongKanan(+45)、右後 BelakangKanan(+135)、左後 BelakangKiri(-135)、左前 SerongKiri(-45)。
    // 平板免疫核心放大：45° 細粒度下,斜向常落在 NeckZ 物理不可達區 → 用 NearestReachable/TurnToward 回報
    //   「實際轉到的扇區」而非裸夾限度數,讓『馬達可達性直接決定教學判決』。
    public enum Dir { Depan, SerongKanan, Kanan, BelakangKanan, Belakang, BelakangKiri, Kiri, SerongKiri }

    public static class Direction
    {
        // 度數正規化到 [-180,180]
        public static float Normalize(float deg)
        {
            while (deg > 180f) deg -= 360f;
            while (deg < -180f) deg += 360f;
            return deg;
        }

        // 角度 → 方位扇區（每區 45°，中心對齊 8 向；沿用「含上界」邊界慣例：(下界, 上界]）
        public static Dir FromAngle(float deg)
        {
            float a = Normalize(deg);
            if (a > -22.5f && a <= 22.5f) return Dir.Depan;
            if (a > 22.5f && a <= 67.5f) return Dir.SerongKanan;
            if (a > 67.5f && a <= 112.5f) return Dir.Kanan;
            if (a > 112.5f && a <= 157.5f) return Dir.BelakangKanan;
            if (a > -67.5f && a <= -22.5f) return Dir.SerongKiri;
            if (a > -112.5f && a <= -67.5f) return Dir.Kiri;
            if (a > -157.5f && a <= -112.5f) return Dir.BelakangKiri;
            return Dir.Belakang; // a > 157.5 或 a <= -157.5（含 ±180）
        }

        // 扇區中心角（供 NeckZ 夾限 / 最近可達扇區計算）。
        public static float SectorCenterDeg(Dir d)
        {
            switch (d)
            {
                case Dir.Depan: return 0f;
                case Dir.SerongKanan: return 45f;
                case Dir.Kanan: return 90f;
                case Dir.BelakangKanan: return 135f;
                case Dir.Belakang: return 180f;
                case Dir.BelakangKiri: return -135f;
                case Dir.Kiri: return -90f;
                case Dir.SerongKiri: return -45f;
                default: return 0f;
            }
        }

        // 「最近可達扇區」：把目標扇區中心夾限到頭部物理可達範圍 [minDeg,maxDeg]，回傳實體頭真正面向的扇區。
        // 例：±90° 可達下，正後 Belakang(180°)→夾到 90°→Kanan；右後 BelakangKanan(135°)→Kanan；右前 SerongKanan(45°)→不降級。
        public static Dir NearestReachable(Dir d, float minDeg, float maxDeg)
        {
            float c = SectorCenterDeg(d);
            float clamped = c < minDeg ? minDeg : (c > maxDeg ? maxDeg : c);
            return FromAngle(clamped);
        }

        // 方位 → 印尼語（serong=斜；複合詞 belakang/serong + kanan/kiri）
        public static string ToIndo(Dir d)
        {
            switch (d)
            {
                case Dir.Depan: return "depan";
                case Dir.SerongKanan: return "serong kanan";
                case Dir.Kanan: return "kanan";
                case Dir.BelakangKanan: return "belakang kanan";
                case Dir.Belakang: return "belakang";
                case Dir.BelakangKiri: return "belakang kiri";
                case Dir.Kiri: return "kiri";
                case Dir.SerongKiri: return "serong kiri";
                default: return "?";
            }
        }

        // 方位 → 中文
        public static string ToZh(Dir d)
        {
            switch (d)
            {
                case Dir.Depan: return "前面";
                case Dir.SerongKanan: return "右前方";
                case Dir.Kanan: return "右邊";
                case Dir.BelakangKanan: return "右後方";
                case Dir.Belakang: return "後面";
                case Dir.BelakangKiri: return "左後方";
                case Dir.Kiri: return "左邊";
                case Dir.SerongKiri: return "左前方";
                default: return "?";
            }
        }

        // 從學生說的句子裡解析方位詞（找不到回傳 null）。
        // 鐵則：先比對複合詞(serong/belakang + kanan/kiri)再比對單詞，否則 "serong kanan" 會被 "kanan" 先吃掉。
        public static Dir? ParseIndo(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string t = text.ToLowerInvariant();
            if (t.Contains("serong kanan")) return Dir.SerongKanan;
            if (t.Contains("serong kiri")) return Dir.SerongKiri;
            if (t.Contains("belakang kanan")) return Dir.BelakangKanan;
            if (t.Contains("belakang kiri")) return Dir.BelakangKiri;
            if (t.Contains("belakang")) return Dir.Belakang;
            if (t.Contains("depan")) return Dir.Depan;
            if (t.Contains("kanan")) return Dir.Kanan;
            if (t.Contains("kiri")) return Dir.Kiri;
            return null;
        }
    }
}
