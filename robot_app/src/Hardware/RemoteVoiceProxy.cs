using System;
using System.Threading.Tasks;

namespace KebbiBrain.Hardware
{
    // 遠端語音代理:實作 IVoice,但 SpeakAsync 透過 IRobotLink 送 VoiceCommand 給「另一台」Kebbi,
    // 讓被控機用「自己的喇叭」說台詞(而非中控的喇叭)。與 RemoteBodyProxy 搭配 → 多機 Director 模式
    // (G5 辯方、G2 乙機)被控機既動機身又自己開口,既有遊戲邏輯(把 defVoice 換成本類)不改。純 C#:Sim/Unity 共用。
    //   • SpeakAsync → 送 VC|SAY 命令(命令式,fire-and-forget)。
    //     ⚠ 合約降級:送出即返回,「不保證被控機已播畢」。真機 UnityVoice 在 src.Play() 後即返回、本來就不等播完,
    //       本 proxy 只是把這層放大成「連送達都不等」。若真機要嚴格「說完才交棒」(DebateGame 的 YOUR_TURN/BACK 語意),
    //       需另補「被控機播畢→回 ACK/DONE」握手(見 進度追蹤.md 待辦);本階段先讓多機語音跑通。
    //   • ListenAsync → 不支援遠端聽(中控請用「自己的」麥克風聽);回空字串並 log 警告,避免靜默判錯。
    //     ⚠ 故此 proxy 僅限「不需遠端聽」的場景(G5/G2 Director);勿用於 G4 等 Speak→Listen 流程(空字串會被解析層吞掉)。
    public sealed class RemoteVoiceProxy : IVoice
    {
        private readonly IRobotLink _link;
        private readonly string _targetId;
        private readonly Action<string> _log;

        public RemoteVoiceProxy(IRobotLink link, string targetId, Action<string> log = null)
        {
            _link = link; _targetId = targetId; _log = log ?? (_ => { });
        }

        public Task SpeakAsync(string text, string lang = "id-ID")
        {
            _ = _link.SendAsync(_targetId, VoiceCommand.Speak(text, lang)); // 送出即返回,不保證播畢(見類別註解)
            return Task.CompletedTask;
        }

        public Task<string> ListenAsync(string lang = "id-ID")
        {
            // 對稱 RemoteBodyProxy.ReadDoaDegrees()=>0f:不支援的遠端讀回中性值,但語音回空字串易被解析層吞 → 不可靜默,故 log。
            _log("[RemoteVoiceProxy] ⚠ ListenAsync 不支援遠端聽,回空字串;此 proxy 僅限 G5/G2 Director 等不需遠端聽的場景。");
            return Task.FromResult("");
        }
    }
}
