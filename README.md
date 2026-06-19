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

需要 .NET SDK(本機只有 .NET 10;專案目標 net8.0,執行需 roll-forward):

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROLL_FORWARD=Major        # 本機無 net8 runtime,滾到 net10 執行
cd robot_app
dotnet run --project KebbiBrain.Sim.csproj -- --test     # 自我測試(目前 153/153 綠)
dotnet run --project KebbiBrain.Sim.csproj -- --menu     # 列出所有命令
```

### 🎮 示範小遊戲 — 每個命令示範「哪一個功能」

> 原則:每個小功能都配一個主控台示範。下表把「命令 → 示範的功能 → 一句重點」對齊清楚(皆純 Sim、免金鑰、免實機)。

| 命令 | 示範的功能 | 一句重點(只有平板做不到的點) |
|---|---|---|
| (無參數) | **G4《Tebak Arah》印尼語方位遊戲** | DOA 聲源定位 + 轉頭面向學生 + 雲端印尼語語音(校準四生→出方位題→糾錯) |
| `--g1` | **G1《雙機接力闖關》** | 指令序列直譯→雙機地板接力;交棒握手 + 「未交棒就到終點=闖關失敗」判定 |
| `--g2` | **G2《幾何證明接力站》** | 雙機「說—走—指—接棒」:乙機念理由→甲機走位+手臂指認該邊→回報接棒 |
| `--g3` | **G3《鏡像體操教練》** | 關節逐幀立體示範 + 聽到「太快了」用 DOA 轉頭面向+放慢 BPM |
| `--g5` | **G5《法庭辯論劇場》** | 雙機事件交棒接力辯論 + 向中央移動逼近 + DOA 轉向發言學生 + 手臂指控/攤手 |
| `--g5t` | **G5 七步審判驅動器 + 學生插話** | RunTrialAsync 固定 7 步流程;學生席舉手(姿態 gate)→轉頭→學生麥發言→改票 |
| `--g4t` | **G4 裁判賽多輪排名** | round-robin 多輪;甲描述乙方位→DOA 核對→轉頭;視角轉換(相對 Kebbi/相對自己)+ 排名冠軍舞 |
| `--g3r` | **G3 逐幀回退(再一次)** | 學生喊「再一次」→回退一幀重示範同一動作(手冊 step4);純狀態回退、可重入 |
| `--g3f` | **G3 動作幀資料化** | 單幀自訂停留 `HoldMs`(CPR 下壓停 2 秒)+ 整組循環 `Move.Loops`(暖身連做 3 組);降 BPM 只影響未自訂幀 |
| `--g2v` | **G2 學生自編腳本驗證** | 學生排證明步驟→Kebbi 驗結構(缺步/層次錯置/順序逆置)→指出第幾步哪種錯才放行接力 |
| `--g2h` | **G2 甲機轉頭望向發言者** | 每步先用 NeckZ 轉頭「視線跟隨」被討論圖素(-45/0/+45°)再手臂指認;眼神接觸是平板做不到的具身互動 |
| `--link` | **多機協作基礎(IRobotLink)** | 雙機交棒握手(HANDOFF/ACK) + 合體彩蛋廣播(CUE/READY) |
| `--rv` | **多機遠端語音(RemoteVoiceProxy)** | 把 G5 辯方換成遠端被控機→台詞由「被控機自己的喇叭」說出;含 `VC\|DONE` 播畢握手(說完才交棒) |
| `--finale` | **合體彩蛋(FinaleShowGame)** | 中控導演機編排多站接力 + **降級備案**(離線/慢站自動跳過,壓軸照跑) + 非同步慢站 await |

雲端(需金鑰,見下):`--cloud-test`(雲端自測:Azure 印尼語 + LLM)、`--target cloudsim`(真語音跑整場 G4 Demo)、`--target real`(實機守門,僅 Unity 建置)。

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
