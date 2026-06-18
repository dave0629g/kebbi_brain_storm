using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.Sim
{
    // 行程內 loopback 匯流排：把多個 SimRobotLink 串起來，模擬多台 Kebbi 在同網段互通。
    public sealed class SimRobotBus
    {
        private readonly Dictionary<string, SimRobotLink> _links = new Dictionary<string, SimRobotLink>();
        private readonly Action<string> _out;

        public SimRobotBus(Action<string> output) { _out = output ?? Console.WriteLine; }

        public SimRobotLink CreateLink(string robotId)
        {
            var link = new SimRobotLink(this, robotId, _out);
            _links[robotId] = link;
            return link;
        }

        internal Task SendAsync(string from, string to, string text)
        {
            _out("   📡 [" + from + " → " + to + "] " + text);
            if (_links.TryGetValue(to, out var link)) link.Deliver(from, text);
            else _out("   ⚠ 找不到機器人 " + to);
            return Task.CompletedTask;
        }

        internal Task BroadcastAsync(string from, string text)
        {
            _out("   📡 [" + from + " → 全體] " + text);
            foreach (var kv in _links)
                if (kv.Key != from) kv.Value.Deliver(from, text);
            return Task.CompletedTask;
        }
    }

    public sealed class SimRobotLink : IRobotLink
    {
        private readonly SimRobotBus _bus;
        private readonly Action<string> _out;
        private Action<string, string> _handler;

        public string RobotId { get; }

        internal SimRobotLink(SimRobotBus bus, string id, Action<string> output) { _bus = bus; RobotId = id; _out = output; }

        public void OnMessage(Action<string, string> handler) => _handler = handler;
        public Task SendAsync(string toRobotId, string text) => _bus.SendAsync(RobotId, toRobotId, text);
        public Task BroadcastAsync(string text) => _bus.BroadcastAsync(RobotId, text);

        internal void Deliver(string from, string text)
        {
            _out("   📨 " + RobotId + " 收到(" + from + ")：" + text);
            _handler?.Invoke(from, text);
        }
    }
}
