using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.Sim
{
    // 文字型姿態感測模擬器：由腳本預先排入「學生這次有沒有做對」。
    public sealed class SimPoseSensor : IPoseSensor
    {
        private readonly Queue<bool> _results = new Queue<bool>();
        private readonly Action<string> _out;

        public SimPoseSensor(Action<string> output) { _out = output ?? Console.WriteLine; }

        public void Enqueue(bool correct) => _results.Enqueue(correct);

        public Task<bool> CheckPoseAsync(string poseName)
        {
            bool ok = _results.Count > 0 ? _results.Dequeue() : true;
            _out("   📷 [姿態檢查] 「" + poseName + "」→ " + (ok ? "正確 ✔" : "待修正 ✘"));
            return Task.FromResult(ok);
        }
    }
}
