using System;
using System.Threading.Tasks;

namespace KebbiBrain.Hardware
{
    // 遠端語音代理:實作 IVoice,但 SpeakAsync 透過 IRobotLink 送 VoiceCommand 給「另一台」Kebbi,
    // 讓被控機用「自己的喇叭」說台詞(而非中控的喇叭)。與 RemoteBodyProxy 搭配 → 多機 Director 模式
    // (G5 辯方、G2 乙機)被控機既動機身又自己開口,既有遊戲邏輯(把 defVoice 換成本類)不改。純 C#:Sim/Unity 共用。
    //   • SpeakAsync → 送 VC|SAY 命令。兩種模式:
    //       (a) 預設(awaiter==null):fire-and-forget,送出即返回、不保證播畢(多機語音先跑通用)。
    //       (b) 等播畢(傳入 LinkAwaiter):await 被控機回的 VC|DONE(帶逾時)才返回 → 真機「說完才交棒」
    //           (DebateGame 的 YOUR_TURN/BACK 語意)。被控端需設 BodyCommandReceiver(ackVoiceDone:true) 才會回 DONE。
    //     ⚠ 真機 UnityVoice 在 src.Play() 後即返回、本來就不等播完;要嚴格等播畢,被控端的 IVoice.SpeakAsync 也需真的等到播放結束才回。
    //   • ListenAsync → 不支援遠端聽(中控請用「自己的」麥克風聽);回空字串並 log 警告,避免靜默判錯。
    //     ⚠ 故此 proxy 僅限「不需遠端聽」的場景(G5/G2 Director);勿用於 G4 等 Speak→Listen 流程(空字串會被解析層吞掉)。
    public sealed class RemoteVoiceProxy : IVoice
    {
        private readonly IRobotLink _link;
        private readonly string _targetId;
        private readonly Action<string> _log;
        private readonly LinkAwaiter _awaiter;   // 非 null → 等被控機回 VC|DONE 才返回
        private readonly int _doneTimeoutMs;

        // awaiter:傳入則啟用「等播畢」模式(await VC|DONE)。注意 awaiter 須建在「本 proxy 送出所用的同一條 link」上,
        //         由它獨佔該 link 的 OnMessage handler 收 DONE(該 link 若還需其他分派,透過 LinkAwaiter 的 alsoHandle)。
        public RemoteVoiceProxy(IRobotLink link, string targetId, Action<string> log = null,
                                LinkAwaiter awaiter = null, int doneTimeoutMs = 4000)
        {
            _link = link; _targetId = targetId; _log = log ?? (_ => { });
            _awaiter = awaiter; _doneTimeoutMs = doneTimeoutMs;
        }

        public async Task SpeakAsync(string text, string lang = "id-ID")
        {
            if (_awaiter == null)
            {
                _ = _link.SendAsync(_targetId, VoiceCommand.Speak(text, lang)); // fire-and-forget,不保證播畢
                return;
            }
            // 等播畢模式:鐵則「先註冊 DONE 等待者,再送 SAY」(Sim 同步遞送下 DONE 可能在 SendAsync 當下就回)。
            // ⚠ 真機僅限 await 呼叫;勿在 Unity 主緒對本方法 blocking(.Result/.Wait()) —— DONE 經 UnityRobotLink 的
            //   _main.Post 排到主緒,若主緒正被阻塞 → 死鎖(各遊戲一律 await,故目前安全)。
            var doneTask = _awaiter.WaitForAsync((f, t) => f == _targetId && VoiceCommand.IsDone(t), _doneTimeoutMs);
            await _link.SendAsync(_targetId, VoiceCommand.Speak(text, lang));
            if (await doneTask == null)
                _log("[RemoteVoiceProxy] ⚠ 等被控機播畢(VC|DONE)逾時,視為已完成繼續(避免卡死交棒)。");
        }

        public Task<string> ListenAsync(string lang = "id-ID")
        {
            // 對稱 RemoteBodyProxy.ReadDoaDegrees()=>0f:不支援的遠端讀回中性值,但語音回空字串易被解析層吞 → 不可靜默,故 log。
            _log("[RemoteVoiceProxy] ⚠ ListenAsync 不支援遠端聽,回空字串;此 proxy 僅限 G5/G2 Director 等不需遠端聽的場景。");
            return Task.FromResult("");
        }
    }
}
