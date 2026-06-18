using System;
using System.Threading.Tasks;

namespace KebbiBrain.Hardware
{
    // 多機通訊抽象（Kebbi↔Kebbi）。給 G1/G2 接力、G5/合體彩蛋的多機協作用。
    // real = 純 socket(UDP/TCP) 或非公開 ConnectionManager(WiFi WebSocket P2P)；⚠️實測「從未雙機實跑」，須先做雙機收送實測。
    // sim  = 行程內 loopback 匯流排，免網路免機器人即可把協定跑通與自測。
    public interface IRobotLink
    {
        string RobotId { get; }
        void OnMessage(Action<string, string> handler); // (fromRobotId, text)
        Task SendAsync(string toRobotId, string text);  // 點對點
        Task BroadcastAsync(string text);               // 廣播給其他所有機
    }
}
