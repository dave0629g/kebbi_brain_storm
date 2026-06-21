using System;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // 把 PIR 存在事件 → 迎接 / 降待命。Air S 上「學生靠近 → 打招呼、離開 → 降待命」的陪伴存在感
    // (取代相機版 SafetyEye;使用者已定「不做連續視覺」)。去抖沿用已測的 PresenceWatcher;
    // PIR 是專用硬體感測,預設 confirmFrames=1(即時);要更穩可調大。
    public sealed class PresenceCompanion
    {
        private readonly PresenceWatcher _watcher;
        public Action OnArrived;   // 學生靠近(可接:打招呼 + 面向前方)
        public Action OnLeft;      // 學生離開(可接:降待命語/降表情)
        public int Greetings { get; private set; }
        public int Standbys { get; private set; }
        public bool Present => _watcher.Present;

        public PresenceCompanion(IPresenceSensor sensor, int confirmFrames = 1)
        {
            _watcher = new PresenceWatcher(confirmFrames, 0f);
            if (sensor != null) sensor.OnPresence(OnPir);
        }

        private void OnPir(bool present)
        {
            var ev = _watcher.Observe(new PresenceState { PersonPresent = present, PositionX = -1f });
            if (ev == PresenceEvent.Arrived) { Greetings++; if (OnArrived != null) OnArrived(); }
            else if (ev == PresenceEvent.Left) { Standbys++; if (OnLeft != null) OnLeft(); }
        }
    }
}
