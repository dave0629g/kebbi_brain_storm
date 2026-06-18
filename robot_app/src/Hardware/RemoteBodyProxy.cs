using System.Collections.Generic;

namespace KebbiBrain.Hardware
{
    // 遠端機身代理:實作 IKebbiBody,但每個「寫」動作都透過 IRobotLink 送 BodyCommand 給「另一台」Kebbi 執行。
    // 中控(director)即可把既有遊戲(G1/G2/G5)的 bodyB 換成本類 → 真機多台分散式跑同一套劇本,程式邏輯不改。
    //   • 寫(SetMotor/Move/Turn/Stop) → 送命令(命令式,fire-and-forget)。
    //   • 讀 GetMotor → 回「最後下達值」(中控記帳用,與被控機一致)。
    //   • ReadDoaDegrees → 不支援遠端(DOA 每台各自本機讀;中控請讀自己那台)。
    // 純 C#:Sim(SimRobotBus)可自測、Unity(UnityRobotLink)實機共用。
    public sealed class RemoteBodyProxy : IKebbiBody
    {
        private readonly IRobotLink _link;
        private readonly string _targetId;
        private readonly Dictionary<KebbiMotor, float> _last = new Dictionary<KebbiMotor, float>();

        public RemoteBodyProxy(IRobotLink link, string targetId, bool canMove = true,
                               float neckZMinDeg = -90f, float neckZMaxDeg = 90f)
        {
            _link = link; _targetId = targetId;
            CanMove = canMove; NeckZMinDeg = neckZMinDeg; NeckZMaxDeg = neckZMaxDeg;
        }

        public void SetMotor(KebbiMotor m, float degrees, float speed = 50f)
        {
            _last[m] = degrees;
            Send(BodyCommand.SetMotor(m, degrees, speed));
        }
        public float GetMotor(KebbiMotor m) => _last.TryGetValue(m, out var v) ? v : 0f;

        public float ReadDoaDegrees() => 0f; // 遠端 DOA 不支援:在持有麥克風的那台本機讀

        public bool CanMove { get; }
        public void Move(float metersPerSec) => Send(BodyCommand.Move(metersPerSec));
        public void Turn(float degPerSec) => Send(BodyCommand.Turn(degPerSec));
        public void StopWheels() => Send(BodyCommand.Stop());

        public float NeckZMinDeg { get; }
        public float NeckZMaxDeg { get; }

        private void Send(string cmd) { _ = _link.SendAsync(_targetId, cmd); }
    }
}
