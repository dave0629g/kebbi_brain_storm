# Kebbi 凱比機器人競賽 — Brainstorm 與 robot_app 開發骨架

[![tests](https://github.com/dave0629g/kebbi_brain_storm/actions/workflows/test.yml/badge.svg)](https://github.com/dave0629g/kebbi_brain_storm/actions/workflows/test.yml)

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
| `--g4e` | **G4 八向方位(含斜向 serong)** | 方位 4 向細到 8 向(serong kanan 右前…);印尼語複合詞解析;NeckZ 物理可達性(實機 ±40°)決定『實際面向扇區』(正後方頭轉不過去→只能降級到右前 serong) |
| `--face` | **複合面向 FaceFully** | NeckZ 實機僅 ±40 → 頭轉不到側邊/正後;輪式 Kebbi 用底盤 `turn()` 補粗方向 + 頭補細 → 連正後方學生也能完整面向(多致動器協調);H201 桌上型誠實部分面向 |
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


---

## 相關研究 (References)

本專案的聽說對話分兩種情境,設計與依據不同——**先講最重要的校正**:

| 情境 | 換誰說(turn-taking)怎麼決定 | 用在 |
|---|---|---|
| **Kebbi ↔ 真人(主線)** | Kebbi **自己用聲音/語意判斷對方講完沒**(語意完整度 COMPLETE/INCOMPLETE + 靜音),**不靠對方送任何信號** | 真實落地:Kebbi 跟學生/使用者對話。測試時第二支手機「扮真人」 |
| **Kebbi ↔ Kebbi(多機協作)** | 網路 **floor token** 交棒(`CV|`/done 信號) | G1/G2/G5 雙機接力、合體彩蛋——兩台都是我們的機器,可走網路 |

> **⚠️ 校正(本輪重點):** 前一版的 STT 對話用網路 floor token 交棒——但**真人不會送網路信號給 Kebbi**,這在「真人↔Kebbi」情境屬**作弊**(繞過了「Kebbi 到底能不能自己聽出對方講完沒」這個真正要驗的能力)。本輪把 `ConversationSttGame` 改成**純靠聲學/語意自行判斷端點**,floor token 只留給多機。下面先講主線(真人版),再附多機版。

兩者都參考了對話 turn-taking、端點偵測(endpointing/VAD)、user simulation,以及分散式系統 floor-control 的研究。共同設計取捨:**內容走空氣(TTS→對方麥克風+Azure STT)**。

## 〔主線〕Kebbi↔真人對話系統 — 語意端點偵測(不靠網路 token)

> 真實用途:Kebbi 跟一個**真人**講話 —— 真人開口、Kebbi 麥克風聽+Azure STT、**自己判斷真人講完沒**、再 LLM+TTS 回應。
> 測試時沒有真人在場,改用**第二支 Android 手機「扮演真人」**(對空氣 TTS 發聲讓 Kebbi 收;`KEBBI_CONV_HUMAN=1`)。

### 技術限制(正交於串流方案)
Unity 固定秒數錄音(非串流)、Azure STT 整段批次(無 partial)、隔空收音有噪音、有 LLM 可用、cascaded ASR→LLM→TTS。因此**不做真 full-duplex**,只借串流論文的「語意完整度概念」與「假插話率評估法」,不照搬串流實作。

### 系統架構(對應 `IVoice` / `ILlm` / `ConversationGame`)
`IVoice.ListenAsync`(麥克風+整段 STT)→ **端點決策(把「何時回應」獨立出來)** → `ILlm.AskAsync`(語意理解+生回應)→ `IVoice.SpeakAsync`(TTS)。
- cascaded 路線有權威背書:ESPnet-SDS(NAACL'25)實測 cascaded 在音質/回應多樣性**仍勝** end-to-end;FurChat(SIGDIAL'23)證明「LLM 當腦+機器人當身體」在真實 HRI 已可用。
- 把「何時回應」從「回應什麼」拆開(Kennington/Lison/Schlangen 2025)。
- 非語言行為(轉向學生 DOA/FaceFully、點頭 backchannel)須與回話時序同步(Blossom-SAR 2025、Building for speech 2025)。

### Kebbi 端 endpoint 怎麼判(`ConversationSttGame` 實作)
| 我們的做法 | 解決的問題 | 來源論文 |
|---|---|---|
| **LLM 語意完整度 COMPLETE/INCOMPLETE**(累積 transcript 餵 `ILlm`,INCOMPLETE=「繼續聽、不要回」的明確狀態) | 端點誤判(把對方一句切斷就插話) | Phoenix-VAD (Wu et al. 2025);Speculative End-Turn (Ok et al. 2025);LiveKit Turn Detector 2024-25(純文字 transcript 路徑) |
| **靜音雙路 + 投機式判斷**(未完就延長錄音再收一段,不固定 4 秒就插話) | 把停頓誤判成講完 | Addlesee & Papaioannou 2025(長停頓/斷續是端點誤判主因) |
| **IU 可撤回假設**(每窗 STT 當「暫定講完」;對方續講就 revoke、把兩段 transcript 串接再判) | 批次 STT 把一句切兩半 | Schlangen & Skantze 2009(Incremental Unit);Kennington et al. 2025 |
| **COMPLETE 後留短 grace window** 等對方 steer/補一句 | 把「講完還想加一句/改口」切斷 | STEER (Zhang et al. 2023, Apple) |
| **短 backchannel(嗯/Iya)當「我在聽」**+ 臉部/姿態顯式聆聽訊號 + 依複雜度延遲掩蔽 | 真人因等太久而 barge-in | ERICA (Kawahara 2021);Building for speech 2025;SID-bench 2026(backchannel 不計搶話) |

### 第二支手機怎麼忠實「扮演真人」(不發網路信號)
- **persona+goal 硬綁的 agenda 骨架**(Schatzmann 2007):給明確 goal + agenda 決定每輪講什麼,讓測試可重現、可寫回歸 case。
- **刻意「反 assistant 化」**(Naous 2025 UserLM-8b):別再跑一個 Kebbi 同款助理 LLM 當人(太配合會低估 Kebbi 問題),給「會停頓想字、講不完整、會追問」的真人風格(Wang 2025 Implicit Profiles)。
- **goal 不可漂移**(Sekulić 2024 DAUS、Davidson 2023):checker 驗有沒有偏題,否則分不清是 Kebbi 爛還是模擬人亂跑。
- **只走空氣不走網路**:手機只 TTS 對空氣發聲讓 Kebbi 收,**不送任何 floor token、不告訴 Kebbi「我講完了」**。
- **植入難樣本**壓測端點:中途停頓再補一句(用 `…` 標,實作會在此插真實停頓)、改口、含糊/語速快、突然沉默(ChatChecker 非合作型)。
- 上場前先過「像不像真人」驗收:MirrorBench(詞彙多樣性)+ Eval4Sim(adherence/consistency/naturalness),防中途崩人設變回 AI 助理腔。

### 評估計畫(兩層 + 自動指標)
- **兩層**(Pietquin & Hastie 2013):直接層=手機扮得像不像真人;間接層=用它測 Kebbi 的結論能否外推真實學生。
- **核心指標**:Takeover Rate(該停不停=搶話率,Full-Duplex-Bench)、接話延遲、False Interruption Rate(固定高 TPR 下,LiveKit)、非語意斷點插話(SID-bench)、GSR(校準逼近真人,Davidson)、整場順暢度 LLM 評分。
- **最小可行實驗**:手機在預設位置插 1.5–3s 思考停頓 → 量 Kebbi 的 Takeover Rate / 假插話率,客觀證明「Kebbi 自己判端點、不作弊、不搶話」。
- **人評格式**(Skantze & Irfan HRI'25):within-subject 比「新版語意 endpoint」vs「固定 4 秒窗 baseline」,量偏好/延遲/插嘴;目標 <1.5s、插嘴<7%。

> **自測對應**:`Tests.T_ConversationStt` 用 `VirtualAir` 兩機端到端(無任何網路 token)驗證:Kebbi 在對方「…」停頓時忍住不插話(`PauseHolds≥1`)、IU 串接被切半的句子、判 COMPLETE 才接話、久無聲音自我修正主動開口。

---

## 〔多機版,僅 G1/G2/G5〕兩台 Kebbi 接力 — floor token 交棒

> 以下是**兩台都是我們機器人**時的設計(可走網路)。真人對話**不用**這套(見上面主線)。

### 我們用到的技術 ↔ 對應文獻

| 我們的做法 | 解決的問題 | 來源論文 |
|---|---|---|
| **Floor token 交棒**(沿用文字版 `CV|`/done 信號) | 相位鎖死(同時說/同時聽) | Token-DCF (Hosseinabadi & Vaidya 2012);802.11 DCF / Bianchi 2000;Sacks/Schegloff/Jefferson 1974(current-speaker-selects-next);Raux & Eskenazi 2009(FSTTM Release);Bohus & Horvitz 2009 |
| **LLM 語意端點判斷**(COMPLETE / CUTOFF 取代固定 4 秒窗) | 端點誤判(把對方一句切斷就插話) | TurnGPT (Ekstedt & Skantze 2020);Pinto-Bernardo & Belpaeme 2024;Thai Semantic EOT (Popit et al. 2025) |
| **靜音(RMS)+ 語意雙路融合投票** | 端點誤判 + 隔空收音抗噪 | Noise-Robust Turn-Taking (Inoue et al. 2025);Ferrer/Shriberg/Stolcke 2002 |
| **Gap vs Pause 二態 + 成本權衡** | 端點誤判 | SpeculativeETD (Ok et al. 2025);FSTTM (Raux & Eskenazi 2009) |
| **Response-conditioned**(用已知的「我下一句」錨定判斷) | 相位鎖死 + 端點 | Jiang, Ekstedt & Skantze 2023 |
| **多類別 turn 狀態 + 短 backchannel**(繼續聽/換我說/Iya·Hmm) | 逼近 full-duplex 體感 | Easy Turn 2025;Semantic VAD (Zhang et al. 2025);Duplex Conversation (Lin et al. 2022) |
| **語意層回音過濾**(把自己正播的 TTS 文字餵 LLM 比對,擋自我回音) | 邊說邊聽的 echo(繞過 Unity 拿不到 AEC 參考訊號) | Duplex Conversation (Lin et al. 2022) |

### 為何「不」走純聲學/真 full-duplex
- **聲學 turn 模型不跨語言通用**:Multilingual VAP (Inoue et al. 2024) 證明 turn 聲學模型換語言會失效,印尼語無訓練資料 → 我們改用 LLM 語意理解。
- **真 full-duplex 套不上我們的管線**:Moshi (Défossez et al. 2024)、VAP (Ekstedt & Skantze 2022)、訊號級/Android 原生 AEC 都需要「串流音訊 + 自己播放訊號當參考 + 對方乾淨分軌」,與「Unity 固定秒數錄音 + Azure 整段 WAV 批次 STT」正交。因此我們**不做真 full-duplex**,改用「更快的半雙工交棒 + 短 backchannel + 語意層回音過濾」。

### 一句話結論(多機版)
> **兩台都是我們機器人時**,done 信號就是分散式/對話系統文獻公認的 floor-control 最可靠解(token passing / explicit floor release):對稱兩端各跑自己的時鐘 = 沒有可靠載波偵測的 CSMA,注定週期碰撞;有網路通道就用 token。
> **但真人↔Kebbi 沒有這個網路通道**——真人不送 token,所以主線改成 Kebbi 自己用語意+靜音判端點(見上面主線),token 只留多機。

### 主要論文連結(第一輪:turn-taking 基本款)
- Sacks, Schegloff & Jefferson (1974), *A Simplest Systematics...* — https://www.jstor.org/stable/412243
- Skantze (2021), *Turn-taking ... A Review*, CS&L 67:101178 — https://www.sciencedirect.com/science/article/pii/S088523082030111X
- Ekstedt & Skantze (2020), *TurnGPT* — https://aclanthology.org/2020.findings-emnlp.268/
- Pinto-Bernardo & Belpaeme (2024), *Predictive Turn-Taking* — https://ieeexplore.ieee.org/document/10731379/
- Jiang, Ekstedt & Skantze (2023), *Response-conditioned Turn-taking* — https://arxiv.org/abs/2305.02036
- Inoue et al. (2025), *A Noise-Robust Turn-Taking System* — https://arxiv.org/abs/2503.06241
- Popit et al. (2025), *Thai Semantic End-of-Turn Detection* — https://arxiv.org/abs/2510.04016
- Ok, Yoo & Lee (2025), *SpeculativeETD* — https://arxiv.org/abs/2503.23439
- Raux & Eskenazi (2009), *FSTTM* — https://aclanthology.org/N09-1071/
- Lin et al. (2022), *Duplex Conversation*, KDD'22 — https://arxiv.org/abs/2205.15060
- Hosseinabadi & Vaidya (2012), *Token-DCF* — https://arxiv.org/abs/1202.0582
- Bianchi (2000), *802.11 DCF performance* — https://ieeexplore.ieee.org/document/840210
- Défossez et al. (2024), *Moshi* — https://arxiv.org/abs/2410.00037
- Inoue et al. (2024), *Multilingual VAP*, LREC-COLING — https://arxiv.org/abs/2403.06487

### 主要論文連結(第二輪:user-simulation / 人面向 endpoint / 評估)
新增於「Kebbi↔真人」校正:讓第二支手機忠實扮真人、讓 Kebbi 自己判端點、怎麼評估。
- Schatzmann et al. (2007), *Agenda-Based User Simulation* — https://aclanthology.org/N07-2038/
- Naous et al. (2025), *UserLM-8b (Flipping the Dialogue)* — https://arxiv.org/abs/2510.06552
- Sekulić et al. (2024), *DAUS: Reliable LLM-based User Simulator* — https://arxiv.org/abs/2402.13374
- Davidson et al. (2023), *User Simulation with LLMs for Eval* (AWS) — https://arxiv.org/abs/2309.13233
- Wang et al. (2025), *Know You First (Implicit Profiles)* — https://arxiv.org/abs/2502.18968
- Mayr et al. (2025), *ChatChecker* (Cambridge) — https://arxiv.org/abs/2507.16792
- Ai & Weng (2008), *User Simulation as Testing* — https://aclanthology.org/W08-0126/
- Ok, Yoo & Lee (2025), *Speculative End-Turn Detector* — https://arxiv.org/abs/2503.23439
- Wu et al. (2025), *Phoenix-VAD: Streaming Semantic Endpoint Detection* — https://arxiv.org/abs/2509.20410
- Zhang et al. (2023), *STEER* (Apple, EMNLP Industry) — https://aclanthology.org/2023.emnlp-industry.61/
- Addlesee & Papaioannou (2025), *Building for speech* — https://www.frontiersin.org/articles/10.3389/frobt.2024.1356477/full
- Skantze & Irfan (2025), *General Turn-taking Models to HRI* (HRI'25) — https://arxiv.org/abs/2501.08946
- Arora et al. (2025), *ESPnet-SDS* (NAACL Demo) — https://arxiv.org/abs/2503.08533
- Cherakara et al. (2023), *FurChat* (SIGDIAL) — https://arxiv.org/abs/2308.15214
- Kawahara et al. (2021), *ERICA Attentive Listening* — https://arxiv.org/abs/2105.00403
- Schlangen & Skantze (2009), *Incremental Unit framework* — https://aclanthology.org/E09-1081/
- Kennington, Lison & Schlangen (2025), *Incremental Dialogue Management survey* — https://arxiv.org/abs/2501.00953
- Lin et al. (2025), *Full-Duplex-Bench* — https://arxiv.org/abs/2503.04721 ;v2 — https://arxiv.org/abs/2510.07838
- LiveKit Turn Detector (2024-25) — https://livekit.com/blog/improved-end-of-turn-model-cuts-voice-ai-interruptions-39
- Zheng et al. (2023), *LLM-as-a-Judge (MT-Bench)* — https://arxiv.org/abs/2306.05685
- Deriu et al. (2021), *Survey on Evaluation Methods for Dialogue Systems* — https://arxiv.org/abs/1905.04071
- Pietquin & Hastie (2013), *Survey on metrics for user simulation* — https://homepages.inf.ed.ac.uk/hhastie2/pubs/pietquin_hastie_surveyusersimKER.pdf
- MirrorBench (Hathidara et al. 2026)、Eval4Sim (Bao et al. 2026)、SID-bench (Xia et al. 2026) 為 2026 預印,引用前再人工核對正式出處。
