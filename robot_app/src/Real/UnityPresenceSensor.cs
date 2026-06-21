// Air S PIR 人體感測(整檔 #if UNITY):requestSensor(SENSOR_PIR) + registerRobotEventListener 的 onPIREvent → OnPresence。
// ⚠ RobotEventListener 介面全名 / onPIREvent 簽章 / 一次只能註冊一個 listener 等須上機確認(真機驗項);
//   AndroidJavaProxy.Invoke 只處理 onPIREvent、其餘回呼一律忽略(回 null)→ 不因未實作方法而崩潰;全程 try/catch 非致命。
#if UNITY
using System;
using UnityEngine;
using KebbiBrain.Hardware;

namespace KebbiBrain.Real
{
    public sealed class UnityPresenceSensor : IPresenceSensor
    {
        private const int SENSOR_PIR = 0x02;   // NuwaRobotAPI.SENSOR_PIR
        private Action<bool> _handler;
        private AndroidJavaObject _api;

        public UnityPresenceSensor()
        {
            try
            {
                using (var cls = new AndroidJavaClass("com.nuwarobotics.service.agent.NuwaRobotAPI"))
                    _api = cls.CallStatic<AndroidJavaObject>("getInst");
                _api.Call("requestSensor", SENSOR_PIR);
                _api.Call("registerRobotEventListener", new PirProxy(present => { if (_handler != null) _handler(present); }));
                Debug.Log("[PIR] requestSensor(SENSOR_PIR) + listener 已註冊(真機驗 onPIREvent)");
            }
            catch (Exception e) { Debug.LogWarning("[PIR] 註冊失敗(真機驗 requestSensor/RobotEventListener): " + e.Message); }
        }

        public void OnPresence(Action<bool> handler) => _handler = handler;

        // 只處理 onPIREvent;其他 RobotEventListener 回呼(onTouchEvent/onWakeup…)一律忽略 → 不崩潰。
        private sealed class PirProxy : AndroidJavaProxy
        {
            private readonly Action<bool> _onPresence;
            public PirProxy(Action<bool> onPresence) : base("com.nuwarobotics.service.agent.RobotEventListener") { _onPresence = onPresence; }

            public override AndroidJavaObject Invoke(string methodName, AndroidJavaObject[] args)
            {
                try
                {
                    if (methodName == "onPIREvent" && args != null && args.Length >= 1)
                        _onPresence(args[0].Call<int>("intValue") > 0);
                }
                catch { /* 介面/簽章待上機確認;失敗忽略 */ }
                return null;
            }
        }
    }
}
#endif
