using System;

namespace KebbiBrain.Hardware
{
    // RoboGuide:把 Robotics-ER 的視覺偵測座標 → 轉頭角度,讓凱比「轉頭指它」。
    // Robotics-ER 偵測座標是影像正規化 0–1000(x:左 0 → 右 1000;y:上 0 → 下 1000)。
    // 水平置中(500)= 正前(0°);左緣 → -FOV/2;右緣 → +FOV/2。輸出採本專案 DOA 慣例(0=前、+右、-左),
    // 可直接餵 KebbiHead.FaceFully(輪式=底盤+頭補細;H201=頭部夾在 NeckZ ±40 部分面向)。
    // ⚠ 後鏡頭:影像右=真實右(角度 +);前鏡頭影像為鏡像 → 指認前先翻 x(MirrorX)。
    // ⚠ 相機水平 FOV 與光軸對 NeckZ 零點需現場校(必測③);此處只做線性映射,FOV 可注入。
    public static class RoboGuideMath
    {
        public const float DefaultCameraFovDeg = 62f;  // 一般手機後鏡頭水平 FOV 約 60–65°(粗估,真機再校)

        // 偵測中心的水平正規化座標(0–1000):box 取左右中點,純 point 取 X,皆無 → 畫面中央 500。
        public static float CenterX(Detection d)
        {
            if (d.HasBox) return (d.Xmin + d.Xmax) * 0.5f;
            if (d.HasPoint) return d.X;
            return 500f;
        }

        // 正規化 x(0–1000)→ 水平角(度,+右/-左)。x 先夾在 [0,1000];fov<=0 視為預設值。
        public static float AngleForX(float x0_1000, float fovDeg = DefaultCameraFovDeg)
        {
            if (fovDeg <= 0f) fovDeg = DefaultCameraFovDeg;
            float x = x0_1000 < 0f ? 0f : (x0_1000 > 1000f ? 1000f : x0_1000);
            return (x / 1000f - 0.5f) * fovDeg;   // 500→0、0→-fov/2、1000→+fov/2
        }

        // 前鏡頭影像為鏡像 → 指認前把 x 翻面(0↔1000)。後鏡頭不需要。
        public static float MirrorX(float x0_1000) => 1000f - x0_1000;

        // 便捷:由偵測直接算轉頭角。frontCamera=true 先翻鏡像。
        public static float AngleForDetection(Detection d, float fovDeg = DefaultCameraFovDeg, bool frontCamera = false)
        {
            float x = CenterX(d);
            if (frontCamera) x = MirrorX(x);
            return AngleForX(x, fovDeg);
        }
    }
}
