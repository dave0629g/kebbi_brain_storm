# Kebbi 凱比機器人競賽 — Brainstorm 與 robot_app 開發骨架

「STEM 人文跨域與 AI 應用競賽 — Kebbi 凱比機器人創新教案與應用競賽」的選題發想與程式開發。
輔導 13 名學生分 5 組(G1 小五升六、G2 國二升國三、G3 國三升高一、G4 高一升高二・印尼語、G5 高三升大學),
每組各自獨立、並有「多機協作」彩蛋。核心設計原則:**每個專案都要「只有平板做不到」**——靠 Kebbi 的
移動、關節手勢、指向性麥克風(DOA 聲源定位)、多機協作與實體存在感來達成。

## 目錄結構

```
robot_app/                C# 程式(硬體抽象 + 三層後端 + 五隊遊戲邏輯)
  src/Hardware/           抽象介面(IKebbiBody/IVoice/ILlm/IRobotLink/IPoseSensor)+ 共用邏輯
  src/Sim/                文字模擬器後端(免金鑰、可自測)
  src/Cloud/              雲端後端(Azure 印尼語 TTS/STT、OpenAI/Anthropic LLM;#if !UNITY)
  src/Real/               Unity 實機後端(NuwaSDK、UDP 多機、入口元件;#if UNITY)
  src/App/                五隊遊戲:G1 接力闖關 / G2 幾何證明 / G3 鏡像教練 / G4 Tebak Arah / G5 法庭辯論
  Program.cs, Tests.cs    主控台入口與自我測試

隊伍_G1~G5_*.md           各隊獨立手冊(選題、教案、技術)
KEBBI_點子庫_廣度.md       點子庫
開發平台_可行性評估.md      CodeLab / RaaS / Unity 比較 + SDK 實測
技術實作路線_各隊.md        各隊技術路線
UNITY_接入指南.md          Unity 專案建立、SDK 匯入、build/部署、上線必測
進度追蹤.md                開發進度 backlog ＋ handoff(含三階測試策略)
```

## 跑起來(主控台)

需要 .NET SDK 8(本機裝在 `~/.dotnet`):

```bash
export PATH="$HOME/.dotnet:$PATH"
cd robot_app
dotnet run --project KebbiBrain.Sim.csproj -- --test     # 自我測試(目前 92/92 綠)
dotnet run --project KebbiBrain.Sim.csproj -- --menu     # 列出所有命令
dotnet run --project KebbiBrain.Sim.csproj               # G4《Tebak Arah》文字模擬器 Demo
dotnet run --project KebbiBrain.Sim.csproj -- --g1       # 其他:--g1 --g2 --g3 --g5 --link
```

雲端(需金鑰,見下):`--cloud-test`(雲端自測)、`--target cloudsim`(真語音跑整場 Demo)。

## 架構:三層後端,切參數即換

| 後端 | 機身(馬達/DOA) | 語音/LLM | 用途 |
|---|---|---|---|
| **Sim** | 文字模擬 | 文字模擬 | 邏輯規格、CI 自測(免金鑰) |
| **CloudSim** | 文字模擬 | 真 Azure 印尼語 + 真 LLM | 主控台驗證雲端管線(免實機) |
| **Real** | NuwaSDK(真凱比) | 真雲端 | Unity 實機 |

同一份遊戲邏輯(`src/App`)建立在抽象介面上,三層共用。透過 `Config.Target` 切換。

## 三階測試策略(以 Android 為模擬器、Unity 為 middleware)

1. **主控台 Sim** — 邏輯/協定/邊界自測(92/92 綠)。
2. **Android 中介** — 一般 Android 手機/模擬器 + Unity,設 `Config.UseRealRobotApi=false`:機身用模擬器、
   語音/LLM/多機 UDP 仍是真的 → 在 Android 上測「除馬達/DOA 外的所有功能,含多機互連」。
3. **真凱比** — 上實機驗馬達/DOA/搶麥/相機。

多機分散式:一台當中控用既有遊戲邏輯,把另一台的機身換成 `RemoteBodyProxy`(經 UDP 驅動),程式不改。
詳見 `UNITY_接入指南.md` 與 `進度追蹤.md`。

## Unity / 實機

- Unity **2022.3.62f3**(LTS,與 NuwaSDK 對齊)+ Android Studio。
- NuwaUnity unitypackage **1.6.2** + NuwaSDK aar **2.1.0.08**。
- Android build:min API 22 / target 33 / arm64-v8a / IL2CPP / GLES3。
- 程式鎖 `LangVersion 9.0` 以相容 Unity 2022.3。

## 金鑰

雲端後端需要金鑰,**一律走環境變數,勿提交版本庫**:

```bash
export KEBBI_SPEECH_KEY=...        # Azure 語音
export KEBBI_SPEECH_REGION=southeastasia
export KEBBI_LLM_KEY=...           # OpenAI 或 Anthropic(依金鑰前綴自動選 provider)
```

Unity 端沒有環境變數 → 用 `KebbiSecrets` ScriptableObject 注入(其 `.asset` 已在 `.gitignore`,不入庫)。
申請步驟見 `金鑰申請步驟.md`。
