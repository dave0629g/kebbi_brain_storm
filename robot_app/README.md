# robot_app — Kebbi 開發骨架（五隊 G1–G5 Sim 切片 + 多機協調層，自測 153/153）

用你說的架構開工:**硬體抽象 interface ＋ 文字型模擬器(免金鑰免實機可自測) ＋ `Target=Sim/Real` 開關**。
核心邏輯是純 C#(鎖 LangVersion 9.0，可直接搬進 Unity 2022.3)；Unity/SDK 專屬程式整段包在 `#if UNITY`，主控台不編譯。

## 三層後端（一份遊戲邏輯，三種跑法）
| Target | 機器人 | 語音/LLM | 需要 | 用途 |
|---|---|---|---|---|
| **Sim** | 文字模擬器 | 文字模擬 | 無 | 邏輯自測，免金鑰免機器人 |
| **CloudSim** | 文字模擬器 | **真 Azure 印尼語 + 真 Claude** | Azure 金鑰 | 聽真發音、測雲端管線，**免機器人** |
| **Real** | Unity + NuwaSDK | 真雲端 | Unity + 實機 | 上實體 Kebbi（`#if UNITY`） |

## 跑起來（已驗證可跑）
本機只有 .NET 10（專案目標 net8.0，執行需 roll-forward；見根 README）。
```bash
export PATH="$HOME/.dotnet:$PATH"; export DOTNET_ROLL_FORWARD=Major
cd robot_app
dotnet run -- --test            # 邏輯自測（目前 153/153 通過，免金鑰）
dotnet run -- --menu            # 列出所有命令
```

### 🎮 示範小遊戲 — 每個命令示範「哪一個功能」（皆純 Sim、免金鑰）
| 命令 | 示範的功能 |
|---|---|
| (無參數) | **G4《Tebak Arah》印尼語方位遊戲**（DOA + 轉頭 + 雲端印尼語語音） |
| `--g1` | **G1《雙機接力闖關》**（指令序列→雙機地板接力 + 交棒判定） |
| `--g2` | **G2《幾何證明接力站》**（雙機說—走—指—接棒） |
| `--g3` | **G3《鏡像體操教練》**（關節逐幀示範 + DOA 轉頭調節奏） |
| `--g5` | **G5《法庭辯論劇場》**（雙機交棒 + 中央逼近 + DOA 轉向學生 + 手臂手勢） |
| `--link` | **多機協作基礎 IRobotLink**（雙機交棒握手 + 合體彩蛋廣播） |
| `--rv` | **多機遠端語音 RemoteVoiceProxy**（被控機自己的喇叭說台詞 + `VC\|DONE` 播畢握手） |
| `--finale` | **合體彩蛋 FinaleShowGame**（中控編排多站接力 + 降級備案離線站自動跳過） |

雲端（需金鑰）：`--cloud-test`（TTS 檔 + TTS→STT 來回 + LLM 提示）、`--target cloudsim`（真 Azure 印尼語跑整場 G4）、`--target real`（實機守門，需 Unity）。
> CloudSim/雲端會把合成的印尼語存到 `robot_app/cloud_audio_out/*.wav`，macOS 會自動用 `afplay` 播放。

## 架構
```
robot_app/
  Program.cs                 入口：--test 自測 / 預設跑模擬器 Demo
  Tests.cs                   極簡自測（不依賴測試框架）
  src/Hardware/              ★硬體抽象（純 C#，Sim 與 Real 共用）
    IKebbiBody.cs            關節/DOA/移動（只暴露實測可用能力）
    IVoice.cs  ILlm.cs       語音、LLM 抽象
    Direction.cs             方位↔角度↔印尼語 換算（含扇區）
    KebbiContext.cs          注入容器 ＋ KebbiHead.TurnToward（轉頭 workaround）
    KebbiMotor.cs            10 顆馬達 id（對應 SDK）
  src/Sim/                   文字型模擬器後端（印出動作、腳本注入 DOA/聽到）
  src/App/TebakArahGame.cs   ★G4 遊戲邏輯（同一份給 Sim/Real 用）
  src/Real/RealBackends.cs   Unity 實機後端骨架（#if UNITY，內含 SDK 呼叫與 TODO）
  src/Config.cs  KebbiFactory.cs   Target 開關與工廠（切 Real 的接點）
```

## 從模擬器「直接轉換」到實機（Unity）
1. 把 `src/` 與 `Program 之外的核心` 放進 Unity 專案。
2. Unity → Player Settings → Scripting Define Symbols 加入 **`UNITY`**（這樣才會編譯 `src/Real/`）。
3. `Config.Target = RobotTarget.Real;`（H201 另把 `UnityKebbiBody.CanMove=false`）。
4. 填 `src/Real/RealBackends.cs` 的 TODO（多數 SDK 呼叫已寫好，雲端語音/LLM 待接）。
5. 注入金鑰（見下）。同一份 `TebakArahGame` 不用改。

## 需要你申請的金鑰（只有 CloudSim/實機才需要；Sim 自測完全不用）
申請步驟見 `../金鑰申請步驟.md`。用環境變數注入，**不要寫進程式碼**：
```bash
export KEBBI_SPEECH_KEY="<Azure Speech KEY 1>"
export KEBBI_SPEECH_REGION="southeastasia"     # 你的 Azure 資源區域
export KEBBI_LLM_KEY="<Anthropic 或 OpenAI 金鑰>"   # 可選；沒設則用內建提示
```
| 變數 | 用途 | 預設 |
|---|---|---|
| `KEBBI_SPEECH_KEY` ＋ `KEBBI_SPEECH_REGION` | 印尼語 STT/TTS（**Azure AI Speech**） | — |
| `KEBBI_LLM_KEY` | LLM 出題/糾錯（Anthropic Claude，預設模型 `claude-opus-4-8`，可在 Config 改 `claude-haiku-4-5` 省成本） | 未設則 SimLlm |
| 語音 | `Config.SpeechVoice`：`id-ID-GadisNeural`(女)/`id-ID-ArdiNeural`(男) | Gadis |

## 對齊實測 SDK（已內建的 workaround）
- 轉向說話者：`KebbiHead.TurnToward` = 讀 `getDirectionOfDOA` → 自寫 `ctlMotor(NeckZ, 角度, 速)`（**不用**會被授權牆擋的 `turnToDOA`），並把目標角夾限到頭部可達範圍（**正後方頭轉不過去**，Demo 已演示）。
- 說話：走雲端 TTS ＋ AudioSource（**不用**內建 TTS；內建也無印尼語）。
- 動作：逐幀 `ctlMotor`（**不用** `motionPlay`）。
- ⚠️ 馬達 API 已對真 NuwaSDK 2.1.0.08 aar 反查校正（2026-06-18）：早先寫的 `setMotorPositionInDegree` **不存在**→`ctlMotor`；`getMotorPresentPossitionInDegree` 拼字錯→`getMotorPresentPositionInDegree`。詳見 `../UNITY_接入指南.md` §6。

## 上實機前必做的真機實測（gating）
1. **搶麥**：系統持麥時 `stopListen` 後 AudioRecord 能否取 PCM（G4 命門）。
2. **DOA 解析度/正後方/非語音**：校準 `getDirectionOfDOA` 誤差與盲區。
3. **相機權限**（若加視覺）：Unity WebCamTexture 能否開 Kebbi 相機。
4. （多機/移動題才需）move 是否被授權牆擋、雙機 socket 收送。

## 現況
五隊 G1–G5 Sim 切片全到齊（同一套抽象），並加了多機協調層：`IRobotLink` 雙機模擬、`RemoteBodyProxy`/`RemoteVoiceProxy`（遠端機身/語音）、`FinaleShowGame` 合體彩蛋編排（降級備案）、`LinkAwaiter`（await+逾時，真機 UDP 也正確）。示範見上方「示範小遊戲」表。純 Sim 仍有遊戲深度 backlog（見 `../進度追蹤.md`）。
