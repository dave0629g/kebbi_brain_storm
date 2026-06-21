using System;
using KebbiBrain.Hardware;

namespace KebbiBrain.Sim
{
    // 文字型 PIR 模擬器:測試/腳本用 Emit 注入「有人/沒人」事件。
    public sealed class SimPresenceSensor : IPresenceSensor
    {
        private Action<bool> _handler;
        private readonly Action<string> _out;

        public SimPresenceSensor(Action<string> output = null) { _out = output ?? (s => { }); }

        public void OnPresence(Action<bool> handler) => _handler = handler;

        public void Emit(bool present)
        {
            _out("   👁 [PIR] " + (present ? "偵測到人" : "沒人"));
            if (_handler != null) _handler(present);
        }
    }
}
