// Air S 內建會動的臉(整檔 #if UNITY):FaceExpression → NuwaRobotAPI.playFaceAnimation(動畫名) + mouthOn/Off。
// ⚠ Nuwa 內建動畫「名稱」須上機對照其動畫清單確認(真機驗項);名稱錯只是 no-op(全程 try/catch)。
#if UNITY
using System;
using UnityEngine;
using KebbiBrain.Hardware;

namespace KebbiBrain.Real
{
    public sealed class UnityFaceExpression : IFaceExpression
    {
        private readonly AndroidJavaObject _api;

        public UnityFaceExpression()
        {
            try { using (var cls = new AndroidJavaClass("com.nuwarobotics.service.agent.NuwaRobotAPI")) _api = cls.CallStatic<AndroidJavaObject>("getInst"); }
            catch (Exception e) { Debug.LogWarning("[Face] 取 NuwaRobotAPI 失敗: " + e.Message); }
        }

        public void Show(FaceExpression expr)
        {
            if (_api == null) return;
            string name = AnimName(expr);
            try { _api.Call("playFaceAnimation", name); }
            catch (Exception e) { Debug.LogWarning("[Face] playFaceAnimation('" + name + "') 失敗(動畫名待上機確認): " + e.Message); }
        }

        public void Mouth(bool talking)
        {
            if (_api == null) return;
            try { if (talking) _api.Call("mouthOn", 1L); else _api.Call("mouthOff"); } catch { }
        }

        // TODO(真機):對照 Nuwa 內建動畫清單換成正確名稱(目前為合理推測)。
        private static string AnimName(FaceExpression e)
        {
            switch (e)
            {
                case FaceExpression.Warm: return "happy";
                case FaceExpression.Happy: return "happy";
                case FaceExpression.Listening: return "normal";
                case FaceExpression.Concerned: return "sad";
                case FaceExpression.Calm: return "normal";
                default: return "normal";
            }
        }
    }
}
#endif
