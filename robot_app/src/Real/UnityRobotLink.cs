// 多機通訊的實機實作（UDP 廣播）。整檔 #if UNITY。
// ⚠️ 實測:UDP「送」已在 H201 驗證、「收」未驗;Kebbi↔Kebbi 從未雙機實跑
//    → 上線前務必先做「雙機收送實測」(KebbiAppBehaviour 的 Mode.LinkPingTest 即為此測;見 進度追蹤.md)。
//
// 設計:同網段所有機綁同一 UDP 埠,每封包帶 from/to/text。送一律走子網廣播;
//   收端過濾(to==自己 或 to=="*",且 from!=自己)。如此點對點與廣播共用一條管道,免 IP 探索。
//   網路執行緒收到封包後,用建構時擷取的主執行緒 SynchronizationContext 切回主緒再呼叫 handler
//   (Unity API 與遊戲邏輯多半只能在主緒跑)。
#if UNITY
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using KebbiBrain.Hardware;
using UnityEngine;

namespace KebbiBrain.Real
{
    public sealed class UnityRobotLink : IRobotLink, IDisposable
    {
        public const int DefaultPort = 50505;

        public string RobotId { get; }
        private readonly int _port;
        private readonly UdpClient _udp;
        private readonly IPEndPoint _broadcast;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly SynchronizationContext _main; // 主執行緒(建構時擷取)
        private Action<string, string> _handler;

        public UnityRobotLink(string robotId, int port = DefaultPort)
        {
            RobotId = robotId;
            _port = port;
            _main = SynchronizationContext.Current; // 須在 Unity 主緒 new 此物件
            _broadcast = new IPEndPoint(IPAddress.Broadcast, port);

            _udp = new UdpClient();
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            _udp.EnableBroadcast = true;

            _ = ReceiveLoopAsync();
        }

        public void OnMessage(Action<string, string> handler) => _handler = handler;

        public Task SendAsync(string toRobotId, string text) => SendFramedAsync(toRobotId, text);
        public Task BroadcastAsync(string text) => SendFramedAsync(RobotLinkProtocol.All, text);

        private async Task SendFramedAsync(string to, string text)
        {
            byte[] payload = RobotLinkProtocol.Frame(RobotId, to, text);
            try { await _udp.SendAsync(payload, payload.Length, _broadcast); }
            catch (Exception e) { Debug.LogWarning("[RobotLink] 送出失敗: " + e.Message); }
        }

        private async Task ReceiveLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                UdpReceiveResult res;
                try { res = await _udp.ReceiveAsync(); }
                catch (ObjectDisposedException) { break; }            // Dispose 收尾
                catch (Exception e) { Debug.LogWarning("[RobotLink] 接收例外: " + e.Message); break; }

                if (!RobotLinkProtocol.TryParse(res.Buffer, res.Buffer.Length, out var from, out var to, out var text))
                    continue;
                if (!RobotLinkProtocol.ShouldDeliver(from, to, RobotId)) continue; // 自己的/非給我的 → 丟棄

                if (_main != null) _main.Post(_ => _handler?.Invoke(from, text), null);
                else _handler?.Invoke(from, text);                    // 無主緒 context 時直接呼叫
            }
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            try { _udp.Close(); } catch { }
            _cts.Dispose();
        }
    }
}
#endif
