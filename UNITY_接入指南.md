# Unity 實機接入指南（robot_app → Kebbi）

> 同一份遊戲邏輯（`src/Hardware`/`Sim`/`App`）已在主控台用 Sim 後端驗證（47/47）。本指南把它接到 **Unity + NuwaUnity SDK** 上實體 Kebbi。
> ⚠️ Unity 專屬程式（`src/Real/*`）只在 Unity 編譯（`#if UNITY`），這台主控台測不到 → **最終以 Unity 實機編譯/實機實測為準**。

## 0. 前置
- Unity **2022.3.62f3**（與 NuwaSDK 對齊）、Android Studio。
- NuwaUnity unitypackage **1.6.2** ＋ NuwaSDK aar **2.1.0.08**（下載樞紐 https://dss.nuwarobotics.com/gitbook/archive.html ；範例 GitHub `nuwarobotics/NuwaSDKExampleUnity`）。
- Build target：min API 22 / target 33 / **arm64-v8a / IL2CPP / GLES3**。

## 1. 把程式放進 Unity
1. 新建 Unity 2022.3.62f3 專案 → import NuwaUnity unitypackage，放入 NuwaSDK aar。
2. 複製 `robot_app/src/` 的 **Hardware / Sim / App / Real / Config.cs / KebbiFactory.cs** 到 `Assets/Scripts/`。
   - **不要**複製 `Program.cs` 與 `Tests.cs`（那是主控台專用）。
3. **Player Settings → Scripting Define Symbols 加入 `UNITY`**。
   - 這樣才會編譯 `src/Real/*`；同時 `src/Cloud/*`（`#if !UNITY`）會被排除（Unity 改用 UnityWebRequest）。

## 2. 設定與金鑰
- `Config.Target = RobotTarget.Real;`
- **H201（桌上型，不會動）**：`UnityKebbiBody.CanMove = false`。
- Unity 沒有環境變數 → 金鑰改從**安全來源**注入（ScriptableObject 設定檔、PlayerPrefs、或開機讀檔），**勿硬編進版本庫**：
  - `Config.SpeechKey` / `Config.SpeechRegion`（`southeastasia`）/ `Config.SpeechVoice`（`id-ID-GadisNeural`）
  - `Config.LlmKey`（provider 依金鑰前綴自動選；OpenAI 或 Anthropic）

## 3. 入口元件 + 金鑰（已內建實檔，不必自己寫）
程式碼裡已附兩個實檔（皆 `#if UNITY`）：
- **`src/Real/KebbiSecrets.cs`**（ScriptableObject）：Unity 選單 `Assets → Create → Kebbi → Secrets` 建一個 `KebbiSecrets.asset`，填入 `speechKey / speechRegion(southeastasia) / speechVoice(id-ID-GadisNeural) / llmKey`（provider 留空＝依金鑰前綴自動選）。
  ⚠️ 含金鑰的 `.asset` **會被打包進 APK** → 路徑加 `.gitignore`、用完 rotate。
- **`src/Real/KebbiAppBehaviour.cs`**（MonoBehaviour 入口）：掛在場景一個空 GameObject，Inspector 裡：
  - `Mode`：`G4_TebakArah`（單機印尼語方位遊戲）或 `LinkPingTest`（雙機收送實測，見下方第④必測）。
  - `secrets`：把上面的 `KebbiSecrets.asset` 拖進來。
  - `isH201Desktop`：桌上型勾選（自動 `CanMove=false`）。
  - `robotId`：多機時每台設不同（如 `Kebbi-A` / `Kebbi-B`）。
  開機 `Start()` 會自動 `secrets.ApplyToConfig()` → `Config.Target=Real` → 依 Mode 跑。

### 多機遊戲（G1/G2/G5）怎麼接
單台裝置只能演一個角色。每台 Kebbi 各跑自己的角色，用 `UnityRobotLink` 收送：
```csharp
var link = new KebbiBrain.Real.UnityRobotLink("Kebbi-A"); // 每台不同 ID
link.OnMessage((from, text) => { /* 收到對方訊息：交棒/cue → 推進自己的劇本 */ });
await link.SendAsync("Kebbi-B", "HANDOFF#1");  // 點對點
await link.BroadcastAsync("CUE: 走位");          // 廣播（合體彩蛋）
```
協定（框架/解析/收件判斷）已抽到 `src/Hardware/RobotLinkProtocol.cs` 並在主控台自測過；**上線前先用 `LinkPingTest` 驗證 UDP 真的收得到**（必測④）。

**更省事：中控驅動（沿用既有遊戲，程式不改）。** G1/G2/G5 的 Sim 遊戲本來就用一個行程同時驅動兩台機身；真機分散式時，讓**一台當中控(director)** 跑遊戲，另一台的機身換成 `RemoteBodyProxy`：
```csharp
// 中控機（跑遊戲）
var link = new KebbiBrain.Real.UnityRobotLink("中控-Kebbi");
var localBody  = KebbiFactory.Create(RobotTarget.Real, Debug.Log).Body; // 自己的機身
var remoteBody = new KebbiBrain.Hardware.RemoteBodyProxy(link, "被控-Kebbi"); // 另一台
var game = new KebbiBrain.App.GeometryRelayGame(localBody, link, /*…*/);     // bodyB 用 remoteBody
//          ↑ remoteBody 的 SetMotor/Move/Turn 會自動經 link 送到被控機執行

// 被控機（只執行命令）
var link = new KebbiBrain.Real.UnityRobotLink("被控-Kebbi");
var body = KebbiFactory.Create(RobotTarget.Real, Debug.Log).Body;
new KebbiBrain.Hardware.BodyCommandReceiver(link, body); // 收到 BC|… 命令就動;其餘訊息可轉交遊戲
```
`BodyCommand / RemoteBodyProxy / BodyCommandReceiver`（`src/Hardware/`，純 C#）已在主控台用 SimRobotBus 自測（8 項），真機只剩 UDP 傳輸待驗（必測④）。

## 3.5 階2：用一般 Android 當模擬器測 middleware（不需真凱比）
策略（使用者定）：**Android 為模擬器、Unity 為 middleware**，先在一般 Android 手機/模擬器把「除馬達/DOA 外的所有功能（含多機 UDP 互連）」測完，再上真凱比。
- 在 KebbiAppBehaviour Inspector **取消勾選 `useRealRobotApi`**（或程式設 `Config.UseRealRobotApi=false`）。
  → KebbiFactory 會把機身換成 `SimKebbiBody`（模擬馬達/DOA，不呼叫 NuwaRobotAPI，故一般 Android 不會崩），但 **UnityVoice(Azure 印尼語) / UnityLlm / UnityRobotLink(UDP) 仍是真的**。
- **單機功能**（TTS 出聲、STT 收音、LLM 回覆、UI）：Android 模擬器或手機皆可。
- **多機互連**：用 **≥2 台真 Android 手機**接同一 WiFi，各設不同 `RobotId`、`Mode=LinkPingTest` → 互收 ping 即通；再換各隊多機遊戲。
  ⚠️ **AVD 模擬器彼此網路隔離(NAT)，UDP 廣播多半互通不到** → 多機請用真手機；模擬器只適合單機測。
- DOA 在階2 沒有真麥陣列 → 用螢幕按鈕/腳本設 `SimKebbiBody.CurrentDoa` 餵值（測試 UI 待補）。

## 4. Build & 部署
```bash
# Unity: File → Build Settings → Android → Build（出 arm64 APK）
adb install -r kebbi.apk
adb shell am start -n <package>/<activity>
adb logcat -d | grep -iE "auth|licence|kebbi|denied|TTS|STT|OpenAI|Anthropic"
# 截圖前先喚醒避免 Doze 黑屏：adb shell input keyevent KEYCODE_WAKEUP
```

## 5. ⚠️ 上線前必測（對齊 進度追蹤.md「需要的資訊」）
| 必測 | 怎麼測 | 失敗時 |
|---|---|---|
| ① 搶麥 | 系統 wakeup 持麥時，`UnityVoice.ListenAsync` 的 `Microphone.Start` 能否取到音訊（必要時先 `Nuwa.stopListen()`） | STT 走不通 → 改用平板收音或外接麥 |
| ② 相機權限 | 一般 app 能否開 `WebCamTexture`（G3/G4 自跑 CV 才需） | 不啟用視覺，走 DOA/人工確認 |
| ③ move 授權牆 | 輪式機下 `move(0.1)` 1 秒看是否真動（靜默=被擋） | 走定點版 |
| ④ 雙機收送 | 兩台同 WiFi，各掛 `KebbiAppBehaviour` 設 `Mode=LinkPingTest`、不同 `RobotId` → 看 logcat 是否互收到 `ping`（`UnityRobotLink` UDP 廣播） | 退單機；或檢查 AP 是否擋廣播/換 TCP 直連 |
| ⑤ DOA 解析度 | 各方位出聲比對 `getDirectionOfDOA()` 誤差、正後方/非語音是否更新 | 降扇區/排除正後方 |
| ⑥ NeckZ 角度範圍 | 往復 `setMotor`+`getMotor` 量上下限，填回 `UnityKebbiBody.NeckZMin/MaxDeg` | — |

## 6. 已內建（RealBackends.cs，照已驗證 REST 形狀）
- **UnityKebbiBody**：`setMotorPositionInDegree` / `getMotorPresentPossitionInDegree` / `getDirectionOfDOA` / `move` / `turn`。
- **UnityVoice**：Azure TTS（SSML→`riff-16khz-16bit-mono-pcm`→`WavUtil.ToAudioClip`→AudioSource）；STT（Microphone 錄 16k→`WavUtil.FromAudioClip`→Azure STT）。
- **UnityLlm**：依金鑰前綴打 OpenAI(`/v1/chat/completions`) 或 Anthropic(`/v1/messages`)；JSON 用 `JsonUtility`（免額外套件）。
- **UnityRobotLink**：真 **UDP 廣播**收送（同埠 50505 廣播 + 收端過濾；收到後切回主緒呼叫 handler）。框架/解析/收件判斷走 `RobotLinkProtocol`（已自測）；剩 UDP 傳輸本身待必測④。
- UnityWebRequest 以 `op.completed` 包成 Task（主執行緒回呼，免 UniTask）。

> 轉頭一律「讀 DOA + 自寫 NeckZ」(不用會被授權牆擋的 turnToDOA)；說話走雲端(不用內建 TTS，內建無印尼語)；動作逐幀 setMotor(不用 motionPlay)。
