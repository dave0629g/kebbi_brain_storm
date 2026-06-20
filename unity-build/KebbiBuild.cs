#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KebbiBrain.Real;

// 最小 Android build 入口:batchmode -executeMethod KebbiBuild.<方法>。
// 驗證 robot_app 整條 Unity build 鏈(IL2CPP AOT 編 src/Real + NuwaSDK aar 打包 + 出 arm64 APK)。不需實機。
// 場景掛 KebbiAppBehaviour → 把 src/Real 物件圖當 root,避免 managed stripping 刪掉 #if UNITY 程式。
//   • BuildApk            ── 實機版(useRealRobotApi=true):只在真 Kebbi 跑(會呼叫 NuwaRobotAPI)。
//   • BuildMiddlewareApk  ── 中介版(useRealRobotApi=false):body 用 SimKebbiBody → 可跑在一般 Android。
//
// 🔐 金鑰注入流程(env → KebbiSecrets.asset → APK,腳本全程看不到 key):
//   build 前 InjectSecretsFromEnv() 從環境變數寫進 Assets/KebbiSecrets.asset、指派給場景 KebbiAppBehaviour.secrets;
//   build 後(try/finally)ClearSecretsAsset() 把 asset 金鑰清空 → 磁碟上的 .asset 永遠不留 key。
//   key 只在 Unity process 記憶體內出現(GetEnvironmentVariable→欄位),Log 只印長度、不印值;
//   wrapper(build-apk.sh)只把 env 傳給 Unity、不 echo;at-rest 的 .asset 一律空。
public static class KebbiBuild
{
    private const string SecretsAssetPath = "Assets/KebbiSecrets.asset";

    public static void BuildApk()           => Build(useRealRobotApi: true,  apk: "Build/kebbi-arm64.apk");
    public static void BuildMiddlewareApk() => Build(useRealRobotApi: false, apk: "Build/kebbi-middleware-arm64.apk");
    public static void BuildLinkPingApk()   => Build(useRealRobotApi: false, apk: "Build/kebbi-linkping-arm64.apk", mode: KebbiAppBehaviour.Mode.LinkPingTest);
    public static void BuildG1DirectorApk() => Build(useRealRobotApi: false, apk: "Build/kebbi-g1director-arm64.apk", mode: KebbiAppBehaviour.Mode.G1Director);
    public static void BuildControlledApk() => Build(useRealRobotApi: false, apk: "Build/kebbi-controlled-arm64.apk", mode: KebbiAppBehaviour.Mode.Controlled);
    public static void BuildG5DirectorApk() => Build(useRealRobotApi: false, apk: "Build/kebbi-g5director-arm64.apk", mode: KebbiAppBehaviour.Mode.G5Director);
    public static void BuildG2DirectorApk() => Build(useRealRobotApi: false, apk: "Build/kebbi-g2director-arm64.apk", mode: KebbiAppBehaviour.Mode.G2Director);
    public static void BuildConverseApk()   => Build(useRealRobotApi: false, apk: "Build/kebbi-converse-arm64.apk", mode: KebbiAppBehaviour.Mode.Converse);
    public static void BuildConverseSttApk()=> Build(useRealRobotApi: false, apk: "Build/kebbi-conversestt-arm64.apk", mode: KebbiAppBehaviour.Mode.ConverseStt);
    public static void BuildRoboticsVisionApk()=> Build(useRealRobotApi: false, apk: "Build/kebbi-robovision-arm64.apk", mode: KebbiAppBehaviour.Mode.RoboticsVision);
    public static void BuildLiveApk()       => Build(useRealRobotApi: false, apk: "Build/kebbi-live-arm64.apk", mode: KebbiAppBehaviour.Mode.LiveConversation);
    public static void BuildMenuApk()       => Build(useRealRobotApi: false, apk: "Build/kebbi-menu-arm64.apk", menu: true); // 功能選單:一個功能一個按鈕
    public static void BuildCounselorApk()  => Build(useRealRobotApi: false, apk: "Build/kebbi-counselor-arm64.apk", mode: KebbiAppBehaviour.Mode.Counselor);

    private static void Build(bool useRealRobotApi, string apk, KebbiAppBehaviour.Mode mode = KebbiAppBehaviour.Mode.G4_TebakArah, bool menu = false)
    {
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.kebbibrain.app");
        PlayerSettings.productName = "KebbiBrain";
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; // Kebbi/Apple Silicon 模擬器皆 arm64 → IL2CPP
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Minimal);
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, "UNITY");

        const string sceneDir = "Assets/Scenes";
        const string scenePath = sceneDir + "/Main.unity";
        System.IO.Directory.CreateDirectory(sceneDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var go = new GameObject("Kebbi");
        if (menu)
        {
            var mb = go.AddComponent<KebbiBrain.Real.KebbiMenuBehaviour>();
            mb.secrets = InjectSecretsFromEnv();   // 🔐 注入金鑰;選單依使用者點選再跑各功能
            ReadCounselorJson(out var cr, out var ct);   // 輔導室設定檔烘進選單(供啟動)
            mb.counselorRulesJson = cr; mb.counselorTopicsJson = ct;
        }
        else { BuildModeScene(go, useRealRobotApi, mode); }
        EditorSceneManager.SaveScene(scene, scenePath);

        System.IO.Directory.CreateDirectory("Build");
        UnityEditor.Build.Reporting.BuildReport report = null;
        try
        {
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
        }
        finally
        {
            ClearSecretsAsset(); // 🔐 不論成敗都清空磁碟上的 asset 金鑰(key 不落地)
        }
        var s = report.summary;
        Debug.Log($"[KebbiBuild] RESULT={s.result} useRealRobotApi={useRealRobotApi} errors={s.totalErrors} warnings={s.totalWarnings} sizeBytes={s.totalSize} out={s.outputPath}");
        EditorApplication.Exit(s.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }

    // 單一 Mode 場景:掛 KebbiAppBehaviour、依 env 設機身/多機/persona、注金鑰、必要時加 HUD。
    private static void BuildModeScene(GameObject go, bool useRealRobotApi, KebbiAppBehaviour.Mode mode)
    {
        var kab = go.AddComponent<KebbiAppBehaviour>();
        kab.useRealRobotApi = useRealRobotApi; // false → 一般 Android 當模擬器測 middleware(body=SimKebbiBody)
        kab.mode = mode;                       // LinkPingTest=驗 UDP 廣播;預設 G4_TebakArah
        kab.peerIp = System.Environment.GetEnvironmentVariable("KEBBI_PEER_IP") ?? ""; // 對方 IP:WiFi 丟廣播時靠 unicast 直連(非機密,可空)
        var rid = System.Environment.GetEnvironmentVariable("KEBBI_ROBOT_ID");        // 每台設不同 ID(多機;同 ID 會把對方當自己回音丟棄),可空=預設 Kebbi-A
        if (!string.IsNullOrEmpty(rid)) kab.robotId = rid;
        var prid = System.Environment.GetEnvironmentVariable("KEBBI_PEER_ROBOT_ID");  // Director 要驅動的被控機 ID,可空=預設 Kebbi-B
        if (!string.IsNullOrEmpty(prid)) kab.peerRobotId = prid;
        var pn = System.Environment.GetEnvironmentVariable("KEBBI_PERSONA_NAME");
        if (!string.IsNullOrEmpty(pn)) kab.personaName = pn;
        var pc = System.Environment.GetEnvironmentVariable("KEBBI_PERSONA_CHAR");
        if (!string.IsNullOrEmpty(pc)) kab.personaCharacter = pc;
        var pl = System.Environment.GetEnvironmentVariable("KEBBI_PERSONA_LANG"); // id-ID / zh-TW
        if (!string.IsNullOrEmpty(pl)) kab.personaLang = pl;
        kab.peerName = System.Environment.GetEnvironmentVariable("KEBBI_PEER_NAME") ?? "";
        kab.converseStarter = (System.Environment.GetEnvironmentVariable("KEBBI_CONV_STARTER") ?? "") == "1";
        kab.converseHuman = (System.Environment.GetEnvironmentVariable("KEBBI_CONV_HUMAN") ?? "") == "1"; // 1=本機扮真人(測試替身)
        kab.converseGoal = System.Environment.GetEnvironmentVariable("KEBBI_CONV_GOAL") ?? "";            // 扮真人的目標(agenda 錨)
        kab.secrets = InjectSecretsFromEnv();  // 🔐 從 env 注入金鑰、指派給場景(build 時打包進 APK)
        if (mode == KebbiAppBehaviour.Mode.Counselor)        // 輔導室:把設定檔 JSON 烘進場景
        { ReadCounselorJson(out var cr, out var ct); kab.counselorRulesJson = cr; kab.counselorTopicsJson = ct; }
        if (mode != KebbiAppBehaviour.Mode.RoboticsVision && mode != KebbiAppBehaviour.Mode.LiveConversation && mode != KebbiAppBehaviour.Mode.Counselor) // 視覺/Live/輔導室:有自己的疊圖 → 不加會蓋畫面的 HUD
            go.AddComponent<KebbiBrain.Real.ScreenLogHud>(); // 螢幕文字 HUD:即時顯示狀態與收/送(鏡像 Debug.Log)
    }

    // 讀輔導室設定檔(symlink 到 robot_app/src/App/Counselor 的 JSON),build 時烘進場景。
    private static void ReadCounselorJson(out string rules, out string topics)
    {
        rules = ReadFileIf("Assets/Scripts/App/Counselor/counselor_safety_rules.json");
        topics = ReadFileIf("Assets/Scripts/App/Counselor/counselor_topics.json");
        Debug.Log($"[Counselor] 設定檔注入 rules len={rules.Length} topics len={topics.Length}");
    }
    private static string ReadFileIf(string p) => System.IO.File.Exists(p) ? System.IO.File.ReadAllText(p) : "";

    // 從環境變數注入金鑰到 KebbiSecrets.asset。回傳該 asset(指派給 KebbiAppBehaviour.secrets)。
    // ⚠️ 只讀 env、只印長度,絕不 Log 金鑰值。asset 不存在則建立。
    private static KebbiSecrets InjectSecretsFromEnv()
    {
        var s = AssetDatabase.LoadAssetAtPath<KebbiSecrets>(SecretsAssetPath);
        if (s == null)
        {
            s = ScriptableObject.CreateInstance<KebbiSecrets>();
            AssetDatabase.CreateAsset(s, SecretsAssetPath);
        }
        s.speechKey = Environment.GetEnvironmentVariable("KEBBI_SPEECH_KEY") ?? "";
        s.llmKey = Environment.GetEnvironmentVariable("KEBBI_LLM_KEY") ?? "";
        s.geminiKey = Environment.GetEnvironmentVariable("KEBBI_GEMINI_KEY") ?? "";   // Gemini 視覺/Live(同一把)
        var gvm = Environment.GetEnvironmentVariable("KEBBI_GEMINI_MODEL");
        if (!string.IsNullOrEmpty(gvm)) s.geminiVisionModel = gvm;
        var glm = Environment.GetEnvironmentVariable("KEBBI_GEMINI_LIVE_MODEL");
        if (!string.IsNullOrEmpty(glm)) s.geminiLiveModel = glm;
        var region = Environment.GetEnvironmentVariable("KEBBI_SPEECH_REGION");
        if (!string.IsNullOrEmpty(region)) s.speechRegion = region;
        var voice = Environment.GetEnvironmentVariable("KEBBI_SPEECH_VOICE");
        if (!string.IsNullOrEmpty(voice)) s.speechVoice = voice;
        var provider = Environment.GetEnvironmentVariable("KEBBI_LLM_PROVIDER");
        if (!string.IsNullOrEmpty(provider)) s.llmProvider = provider;
        EditorUtility.SetDirty(s);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Secrets] injected from env (speechKey len={s.speechKey.Length}, llmKey len={s.llmKey.Length}, geminiKey len={s.geminiKey.Length}, region={s.speechRegion}) — values NOT logged");
        if (s.speechKey.Length == 0 && s.llmKey.Length == 0)
            Debug.LogWarning("[Secrets] env 無金鑰 → APK 將無雲端語音/LLM(先 export KEBBI_SPEECH_KEY/KEBBI_LLM_KEY 再 build)");
        return s;
    }

    // 把 asset 金鑰清空(build 後呼叫;磁碟上的 .asset 永不留 key)。
    private static void ClearSecretsAsset()
    {
        var s = AssetDatabase.LoadAssetAtPath<KebbiSecrets>(SecretsAssetPath);
        if (s == null) return;
        s.speechKey = "";
        s.llmKey = "";
        s.llmProvider = "";
        s.geminiKey = "";
        EditorUtility.SetDirty(s);
        AssetDatabase.SaveAssets();
        Debug.Log("[Secrets] asset cleared (no key persisted on disk)");
    }

    // 驗證用(不 build):注入 → 記長度 → 清空 → 確認已清。供 batchmode 快速驗 env→asset→clear,不印金鑰值。
    public static void VerifySecretsRoundTrip()
    {
        var s = InjectSecretsFromEnv();
        int sk = s.speechKey.Length, lk = s.llmKey.Length;
        ClearSecretsAsset();
        var after = AssetDatabase.LoadAssetAtPath<KebbiSecrets>(SecretsAssetPath);
        bool cleared = after != null && after.speechKey.Length == 0 && after.llmKey.Length == 0;
        Debug.Log($"[Secrets][Verify] injected speechKey len={sk} llmKey len={lk}; afterClear cleared={cleared}");
        EditorApplication.Exit((sk > 0 && lk > 0 && cleared) ? 0 : 2);
    }
}
#endif
