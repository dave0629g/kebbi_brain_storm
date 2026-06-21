namespace KebbiBrain.Hardware
{
    // 臉部表情抽象 —— 對映 Kebbi Air S **內建會動的臉**(Real 接 `NuwaRobotAPI.playFaceAnimation(id/名稱)` + `mouthEmotionOn`/`mouthOn`)。
    // Air S 不能移動,臉螢幕是它最主要的表情通道 → 把輔導陪伴的情緒(燈號/傾聽)畫在臉上。
    // ⚠ Real 接線:FaceExpression → `playFaceAnimation(動畫名)`;Nuwa 內建動畫「名稱清單」須上機確認(真機驗項)。
    public enum FaceExpression { Neutral, Warm, Listening, Happy, Concerned, Calm }

    public interface IFaceExpression
    {
        void Show(FaceExpression expr);  // 切換臉部表情
        void Mouth(bool talking);        // 講話時動嘴(對映 mouthOn/mouthOff)
    }
}
