# robot_app — Kebbi 開發骨架（第一個垂直切片：G4《Tebak Arah》）

用你說的架構開工:**硬體抽象 interface ＋ 文字型模擬器(免金鑰免實機可自測) ＋ `Target=Sim/Real` 開關**。
核心邏輯是純 C#(鎖 LangVersion 9.0，可直接搬進 Unity 2022.3)；Unity/SDK 專屬程式整段包在 `#if UNITY`，主控台不編譯。

## 三層後端（一份遊戲邏輯，三種跑法）
| Target | 機器人 | 語音/LLM | 需要 | 用途 |
|---|---|---|---|---|
| **Sim** | 文字模擬器 | 文字模擬 | 無 | 邏輯自測，免金鑰免機器人 |
| **CloudSim** | 文字模擬器 | **真 Azure 印尼語 + 真 Claude** | Azure 金鑰 | 聽真發音、測雲端管線，**免機器人** |
| **Real** | Unity + NuwaSDK | 真雲端 | Unity + 實機 | 上實體 Kebbi（`#if UNITY`） |

## 跑起來（已驗證可跑）
本機已裝 .NET SDK 8 於 `~/.dotnet`（未加進 PATH）。
```bash
export PATH="$HOME/.dotnet:$PATH"
cd robot_app
dotnet run -- --test            # 邏輯自測（目前 23/23 通過，免金鑰）
dotnet run                      # 文字模擬器 Demo（免金鑰）
dotnet run -- --cloud-test      # 真雲端自測：TTS 可聽檔 + TTS→STT 來回 + Claude 提示（需 Azure 金鑰）
dotnet run -- --target cloudsim # 用真 Azure 印尼語語音跑整場 Demo（需 Azure 金鑰；無則自動降級）
dotnet run -- --target real     # 示範切實機（主控台會被守門擋下，需 Unity）
```
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
- 轉向說話者：`KebbiHead.TurnToward` = 讀 `getDirectionOfDOA` → 自寫 `setMotorPositionInDegree(NeckZ)`（**不用**會被授權牆擋的 `turnToDOA`），並把目標角夾限到頭部可達範圍（**正後方頭轉不過去**，Demo 已演示）。
- 說話：走雲端 TTS ＋ AudioSource（**不用**內建 TTS；內建也無印尼語）。
- 動作：逐幀 `setMotor`（**不用** `motionPlay`）。

## 上實機前必做的真機實測（gating）
1. **搶麥**：系統持麥時 `stopListen` 後 AudioRecord 能否取 PCM（G4 命門）。
2. **DOA 解析度/正後方/非語音**：校準 `getDirectionOfDOA` 誤差與盲區。
3. **相機權限**（若加視覺）：Unity WebCamTexture 能否開 Kebbi 相機。
4. （多機/移動題才需）move 是否被授權牆擋、雙機 socket 收送。

## 下一個切片
同一套抽象可加：G3《鏡像體操教練》(逐幀 setMotor + DOA 轉頭 + 攝影機 MediaPipe)、G5/G2（加 `IRobotLink` 多機通訊抽象 + Sim 雙機模擬）。
