// Unity 場景入口元件。整檔 #if UNITY。
// 掛在場景一個空 GameObject 上,於 Inspector 指定 Mode 與 secrets(KebbiSecrets .asset),
// 開機即依 Mode 跑對應流程:
//   • G4_TebakArah  — 印尼語方位遊戲(真語音 + DOA + 轉頭),單台 Kebbi 即可。
//   • LinkPingTest  — UDP 雙機收送實測(對齊 進度追蹤.md 必測④);兩台各掛此元件、設不同 RobotId,
//                     互看是否收到對方 ping。
//   • G1Director    — 多機:本機當中控,跑既有 G1 遊戲,把第二台機身換成 RemoteBodyProxy(經 UDP 驅動)。
//   • Controlled    — 多機:本機當被控,只掛 BodyCommandReceiver,收到機身命令(BC|…)就執行。
// 多機跑法:一台 G1Director + 一台 Controlled(robotId 不同、peerRobotId 互指)。先用 LinkPingTest 驗證連線再上。
#if UNITY
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using KebbiBrain;
using KebbiBrain.App;
using KebbiBrain.Hardware;
using KebbiBrain.Real;
using KebbiBrain.Sim;

public sealed class KebbiAppBehaviour : MonoBehaviour
{
    public enum Mode { G4_TebakArah, LinkPingTest, G1Director, Controlled, G5Director, G2Director, Converse }

    [Header("執行模式")]
    public Mode mode = Mode.G4_TebakArah;

    [Header("金鑰(拖入 KebbiSecrets .asset)")]
    public KebbiSecrets secrets;

    [Header("硬體")]
    public bool useRealRobotApi = true; // 真凱比=true;一般 Android(模擬器/手機,測 middleware)=false
    public bool isH201Desktop = true;   // 桌上型不會動 → CanMove=false

    [Header("多機")]
    public string robotId = "Kebbi-A";      // 每台設不同 ID
    public string peerRobotId = "Kebbi-B";  // G1Director 要驅動的被控機 ID
    public string peerIp = "";              // 對方 IP(逗號分隔多台)。WiFi AP 丟廣播時靠 unicast 直連;空=純廣播
    public float pingIntervalSec = 2f;       // LinkPingTest 用

    [Header("Converse 對話模式(兩台印尼語對話)")]
    public string personaName = "Andi";                 // 本機角色名
    public string personaCharacter = "periang dan ramah."; // 本機人格描述(餵 LLM)
    public string peerName = "";                        // 對方角色名(顯示用;空=用 peerRobotId)
    public bool converseStarter = false;                // true=本機先開口

    private readonly CancellationTokenSource _life = new CancellationTokenSource();
    private UnityRobotLink _link;

    async void Start()
    {
        Config.Target = RobotTarget.Real;
        Config.UseRealRobotApi = useRealRobotApi; // false=一般 Android 當模擬器測 middleware
        if (secrets != null) secrets.ApplyToConfig();
        else Debug.LogWarning("[Kebbi] 未指定 secrets,雲端語音/LLM 會缺金鑰。");
        Debug.Log("[Kebbi] Mode=" + mode + " UseRealRobotApi=" + useRealRobotApi);

        switch (mode)
        {
            case Mode.LinkPingTest: await RunLinkPingTestAsync(); break;
            case Mode.G1Director: await RunG1DirectorAsync(); break;
            case Mode.G5Director: await RunG5DirectorAsync(); break;
            case Mode.G2Director: await RunG2DirectorAsync(); break;
            case Mode.Controlled: RunControlled(); break;
            case Mode.Converse: await RunConverseAsync(); break;
            default: await RunTebakArahAsync(); break;
        }
    }

    // ── G4：印尼語方位遊戲(單機;真學生發聲、DOA 即時) ──
    private async Task RunTebakArahAsync()
    {
        var ctx = KebbiFactory.Create(RobotTarget.Real, Debug.Log);
        if (isH201Desktop && ctx.Body is UnityKebbiBody body) body.CanMove = false;

        var game = new TebakArahGame(ctx);
        Debug.Log("[G4] 校準一位學生 → 出一題方位");
        await game.CalibrateOneAsync("學生A");
        await game.ForwardRoundAsync(Dir.Kanan);
        game.PrintSummary();
    }

    // ── 雙機收送實測(必測④):廣播 ping、印出收到的對方訊息 ──
    private async Task RunLinkPingTestAsync()
    {
        _link = new UnityRobotLink(robotId, UnityRobotLink.DefaultPort, PeerIps());
        _link.OnMessage((from, text) => Debug.Log("[Link] ✅ 收到 " + from + ": " + text));
        Debug.Log("[Link] 啟動,RobotId=" + robotId + ",每 " + pingIntervalSec + "s 廣播一次 ping。對方收到即代表雙機收送 OK。");

        int seq = 0;
        int delayMs = Mathf.Max(200, (int)(pingIntervalSec * 1000));
        while (!_life.IsCancellationRequested)
        {
            await _link.BroadcastAsync("ping #" + (++seq) + " from " + robotId);
            Debug.Log("[Link] 📡 已廣播 ping #" + seq);
            try { await Task.Delay(delayMs, _life.Token); }
            catch (System.OperationCanceledException) { break; }
        }
    }

    // ── 多機・中控(Director):跑既有 G1 遊戲,本機驅動自己 + 遠端機(RemoteBodyProxy 經 UDP) ──
    // 遊戲內部 A↔B 的握手(GO)走「本機 loopback bus」(in-process);只有「機身命令」才出網路到被控機。
    // 被控機只需 Mode=Controlled。G2/G5 可比照(換成各自遊戲建構式)。
    private async Task RunG1DirectorAsync()
    {
        var realLink = new UnityRobotLink(robotId, UnityRobotLink.DefaultPort, PeerIps());
        _link = realLink;                       // 交給 OnDestroy 釋放
        var ctx = KebbiFactory.Create(RobotTarget.Real, Debug.Log);
        if (isH201Desktop && ctx.Body is UnityKebbiBody ub) ub.CanMove = false;

        var localBus = new SimRobotBus(Debug.Log);   // 遊戲內部握手(loopback,不出網路)
        var linkA = localBus.CreateLink("A");
        var linkB = localBus.CreateLink("B");
        var remoteBody = new RemoteBodyProxy(realLink, peerRobotId); // 第二台機身→經 UDP 驅動

        var game = new RelayQuestGame(ctx.Body, linkA, remoteBody, linkB, Debug.Log);
        Debug.Log("[G1Director] 中控=" + robotId + " 驅動本機 + 遠端(" + peerRobotId + ");被控機請設 Mode=Controlled");
        await game.RunProgramAsync(RelayQuestGame.MakeSampleProgram());
        Debug.Log("[G1Director] 完成:走 " + game.Steps + " 格、抵達終點=" + game.ReachedGoal);
    }

    // ── 多機・G5 中控(Director):跑既有 G5 法庭辯論,本機=控方,辯方的「機身+語音」換成遠端代理(經 UDP)。
    // 遊戲內部 控方↔辯方 的事件交棒(YOUR_TURN/BACK)走本機 loopback bus;只有辯方「機身命令(BC|)+語音命令(VC|)」出網路到被控機。
    // 被控機設 Mode=Controlled(BodyCommandReceiver 收 BC| 動作 + VC| 用自己喇叭說)。G2 比照(換 GeometryRelayGame 建構式)。
    private async Task RunG5DirectorAsync()
    {
        var realLink = new UnityRobotLink(robotId, UnityRobotLink.DefaultPort, PeerIps());
        _link = realLink;
        var ctx = KebbiFactory.Create(RobotTarget.Real, Debug.Log);
        if (isH201Desktop && ctx.Body is UnityKebbiBody ub) ub.CanMove = false;

        var localBus = new SimRobotBus(Debug.Log);            // 控方↔辯方事件交棒(loopback,不出網路)
        var proLink = localBus.CreateLink("控方");
        var defLink = localBus.CreateLink("辯方");
        var defBody = new RemoteBodyProxy(realLink, peerRobotId);   // 辯方機身→經 UDP
        var defVoice = new RemoteVoiceProxy(realLink, peerRobotId); // 辯方語音→經 UDP(被控機用自己喇叭說)

        var game = new DebateGame(ctx.Body, proLink, ctx.Voice, defBody, defLink, defVoice, Debug.Log);
        Debug.Log("[G5Director] 中控=控方,辯方=遠端(" + peerRobotId + ");被控機請設 Mode=Controlled");
        await game.RunDebateAsync(DebateGame.MakeGalileoDebate());
        Debug.Log("[G5Director] 完成:控 " + game.ProVotes + " : 辯 " + game.DefVotes + " → " + game.Verdict);
    }

    // ── 多機・G2 中控(Director):跑既有 G2 幾何證明接力,本機=乙機(念理由),甲機的「機身」換成遠端代理(經 UDP)。
    // 遊戲內部 乙機↔甲機 的 POINT/DONE 走本機 loopback bus;只有甲機「機身命令(BC|:走位+手臂指認)」出網路到被控機。
    // 被控機 Mode=Controlled。比照 G1Director/G5Director(遊戲邏輯本機跑、只有遠端機身/語音出網路)。
    private async Task RunG2DirectorAsync()
    {
        var realLink = new UnityRobotLink(robotId, UnityRobotLink.DefaultPort, PeerIps());
        _link = realLink;
        var ctx = KebbiFactory.Create(RobotTarget.Real, Debug.Log);

        var localBus = new SimRobotBus(Debug.Log);            // 乙機↔甲機 POINT/DONE(loopback,不出網路)
        var guideLink = localBus.CreateLink("甲機");
        var reasonerLink = localBus.CreateLink("乙機");
        var guideBody = new RemoteBodyProxy(realLink, peerRobotId);  // 甲機機身(走位+手臂指認)→經 UDP

        var game = new GeometryRelayGame(guideBody, guideLink, reasonerLink, ctx.Voice, Debug.Log); // 乙機語音=本機
        Debug.Log("[G2Director] 中控=乙機,甲機=遠端(" + peerRobotId + ");被控機請設 Mode=Controlled");
        await game.RunProofAsync(GeometryRelayGame.MakeIsoscelesProof());
        Debug.Log("[G2Director] 完成:" + game.StepsDone + " 步接力");
    }

    // ── 多機・被控(Controlled):收到中控的機身命令(BC|…)就動、語音命令(VC|…)就用本機喇叭說;非命令訊息印出 ──
    private void RunControlled()
    {
        var realLink = new UnityRobotLink(robotId, UnityRobotLink.DefaultPort, PeerIps());
        _link = realLink;
        var ctx = KebbiFactory.Create(RobotTarget.Real, Debug.Log);
        // 第 4 引數 ctx.Voice:讓被控機收到 VC|SAY 時用「自己的」喇叭說(G5 辯方/G2 乙機要自己開口)。
        new BodyCommandReceiver(realLink, ctx.Body,
            (from, t) => Debug.Log("[Controlled] 非命令訊息(" + from + "): " + t),
            ctx.Voice);
        Debug.Log("[Controlled] " + robotId + " 待命:收到中控機身/語音命令即執行(保持 app 在前景)。");
    }

    // ── 兩台印尼語對話(各有人格;靠「說完才交棒」的完成信號一來一往)──
    // 兩台各跑此模式、robotId/peerRobotId 互指、其中一台 converseStarter=true 先開口。
    // 用真 LLM 生印尼語人格台詞、真 TTS 說(UnityVoice 已等播畢)、UnityRobotLink unicast 交棒。
    private async Task RunConverseAsync()
    {
        var realLink = new UnityRobotLink(robotId, UnityRobotLink.DefaultPort, PeerIps());
        _link = realLink;
        var ctx = KebbiFactory.Create(RobotTarget.Real, Debug.Log);
        var me = new ConversationGame.Persona { Name = personaName, Character = personaCharacter, Lang = "id-ID" };
        var game = new ConversationGame(ctx.Voice, ctx.Llm, realLink, me, peerRobotId,
                                        string.IsNullOrEmpty(peerName) ? peerRobotId : peerName, Debug.Log);
        Debug.Log("[Converse] " + personaName + "(" + robotId + ")↔" + peerRobotId +
                  "(" + me.Lang + ")," + (converseStarter ? "我先說" : "等對方先說"));
        await game.RunAsync(converseStarter, maxTurns: 0); // 0=持續對話直到 app 關
    }

    // peerIp 欄位("1.2.3.4" 或逗號分隔多台)→ 去空白/空項的陣列;空則回 null(純廣播)。
    private string[] PeerIps()
    {
        if (string.IsNullOrEmpty(peerIp)) return null;
        var list = new System.Collections.Generic.List<string>();
        foreach (var part in peerIp.Split(','))
        {
            var ip = part.Trim();
            if (ip.Length > 0) list.Add(ip);
        }
        return list.Count > 0 ? list.ToArray() : null;
    }

    void OnDestroy()
    {
        try { _life.Cancel(); } catch { }
        _link?.Dispose();
        _life.Dispose();
    }
}
#endif
