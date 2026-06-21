using System;

namespace KebbiBrain.Hardware
{
    // STT 動態錄音窗的語音端點偵測(VAD)。取代「寫死 4 秒盲錄窗」——邊錄邊看音量包絡:
    // 偵測到語音起點後,連續靜音超過門檻就提早收窗(講完即換手,不必硬等滿 4 秒);設硬上限保底。
    // 純狀態機、無 I/O → 可用合成 PCM 主控台單測。樣本為 float(-1..1),RMS 即正規化能量 0..1。
    public static class VadMath
    {
        // 一段樣本的均方根能量(RMS)。
        public static float Rms(float[] samples, int offset, int count)
        {
            if (samples == null || count <= 0) return 0f;
            double sum = 0; int n = 0;
            for (int i = offset; i < offset + count && i < samples.Length; i++) { sum += (double)samples[i] * samples[i]; n++; }
            return n == 0 ? 0f : (float)Math.Sqrt(sum / n);
        }
        public static float Rms(float[] samples) => Rms(samples, 0, samples == null ? 0 : samples.Length);
    }

    public sealed class VadEndpointer
    {
        public float SilenceRms;       // 低於此 RMS 視為靜音
        public int MinSpeechMs;        // 至少累積這麼多語音才信任「有人講」(濾掉短噪音/咳嗽)
        public int TrailingSilenceMs;  // 語音後連續靜音超過此值 → 判定講完、收窗
        public int MaxWindowMs;        // 硬上限(沒語音也最多錄這麼久,保底一定終止)
        public int MinWindowMs;        // 最短窗(避免一啟動就被誤收)

        private int _elapsedMs, _speechMs, _silenceRunMs;
        private bool _sawSpeech;

        public VadEndpointer(float silenceRms = 0.012f, int minSpeechMs = 300, int trailingSilenceMs = 700, int maxWindowMs = 12000, int minWindowMs = 600)
        {
            SilenceRms = silenceRms; MinSpeechMs = minSpeechMs; TrailingSilenceMs = trailingSilenceMs;
            MaxWindowMs = maxWindowMs; MinWindowMs = minWindowMs;
        }

        public bool SawSpeech => _sawSpeech;
        public int ElapsedMs => _elapsedMs;

        public void Reset() { _elapsedMs = _speechMs = _silenceRunMs = 0; _sawSpeech = false; }

        // 餵一個音框(該框 RMS + 毫秒長度)。回傳是否該結束錄音窗。
        public bool Feed(float rms, int frameMs)
        {
            _elapsedMs += frameMs;
            if (rms >= SilenceRms) { _sawSpeech = true; _speechMs += frameMs; _silenceRunMs = 0; }
            else _silenceRunMs += frameMs;

            if (_elapsedMs >= MaxWindowMs) return true;                         // 硬上限保底
            if (_elapsedMs < MinWindowMs) return false;                         // 最短窗前不收
            if (_sawSpeech && _speechMs >= MinSpeechMs && _silenceRunMs >= TrailingSilenceMs) return true;  // 講完(語音後靜音夠久)
            return false;
        }
    }
}
