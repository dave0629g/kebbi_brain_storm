using System;
using System.Collections.Generic;
using KebbiBrain.Hardware;

namespace KebbiBrain.Sim
{
    // 文字型觸控模擬器:測試/腳本注入觸控事件。有訂閱 OnTouch → 事件模式(立即回呼);否則 Emit 進佇列供 Poll。
    public sealed class SimTouchSensor : ITouchSensor
    {
        private readonly Queue<TouchZone> _q = new Queue<TouchZone>();
        private readonly Action<string> _out;
        private Action<TouchZone> _handler;

        public SimTouchSensor(Action<string> output = null) { _out = output ?? (s => { }); }

        public void OnTouch(Action<TouchZone> handler) => _handler = handler;

        public bool Poll(out TouchZone zone)
        {
            if (_q.Count > 0) { zone = _q.Dequeue(); return true; }
            zone = TouchZone.None; return false;
        }

        // 注入一個觸控:有訂閱者 → 立即回呼(事件模式);否則進佇列(輪詢模式)。
        public void Emit(TouchZone zone)
        {
            _out("   👆 [觸控] " + zone);
            if (_handler != null) _handler(zone); else _q.Enqueue(zone);
        }
    }
}
