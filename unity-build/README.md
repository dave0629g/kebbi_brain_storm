# unity-build — 從 env 注入金鑰、build APK 的安全流程

> 目標：金鑰只走「環境變數 → Unity process 記憶體 → `KebbiSecrets.asset` → APK」，**任何腳本/檔案/log 都看不到 key 值**，at-rest 的 `.asset` 永遠空白。

## 檔案
| 檔 | 作用 |
|---|---|
| `build-apk.sh` | 進入點 wrapper。把 env(`KEBBI_*`)傳給 Unity batchmode，**不引用/不 echo 任何 key 值**。 |
| `KebbiBuild.cs` | Unity Editor build 腳本的**正本**（鏡像 `~/Projects/KebbiBrainUnity/Assets/Editor/KebbiBuild.cs`）。含金鑰注入/清除邏輯。 |
| `KebbiSecrets.asset.template` | 空金鑰的 ScriptableObject 範本（鏡像 `KebbiBrainUnity/Assets/KebbiSecrets.asset` 的 at-rest 狀態）。 |

> ⚠️ `KebbiBrainUnity` 不是 git repo，故此處放正本以利重建/審查。改 `KebbiBuild.cs` 時兩邊都要更新（或把 KebbiBrainUnity 那份改成指向本檔的 symlink）。

## 金鑰怎麼流動（為何腳本看不到 key）
1. 你在 shell `export KEBBI_SPEECH_KEY=… KEBBI_SPEECH_REGION=southeastasia KEBBI_LLM_KEY=…`（也可選 `KEBBI_SPEECH_VOICE`/`KEBBI_LLM_PROVIDER`）。
2. `build-apk.sh` 啟動 Unity，env 由 shell **繼承**給 Unity process；腳本本身從不寫出/印出 key。
3. build 前 `KebbiBuild.InjectSecretsFromEnv()` 用 `Environment.GetEnvironmentVariable` 讀 env → 寫進 `Assets/KebbiSecrets.asset`、指派給場景 `KebbiAppBehaviour.secrets`。**Log 只印長度（len=84…），不印值。**
4. `BuildPipeline.BuildPlayer` 把含金鑰的 asset 序列化進 APK。
5. build 後（`try/finally`）`ClearSecretsAsset()` 把 asset 金鑰清空 → **磁碟上的 `.asset` 不留 key**（即使 build 中斷，finally 也會清）。

→ key 只在 Unity 記憶體存在一瞬間；committed/at-rest 的檔案、所有 log、wrapper 腳本都沒有 key。

## 用法
```bash
export KEBBI_SPEECH_KEY=...  KEBBI_SPEECH_REGION=southeastasia  KEBBI_LLM_KEY=...
./unity-build/build-apk.sh verify       # 只驗 env→asset→clear 流程,不 build(~1-2 分)
./unity-build/build-apk.sh middleware    # 中介版 APK(一般 Android 測 TTS/STT/LLM/UI)
./unity-build/build-apk.sh real          # 真機版 APK(真凱比)
```
產出在 `~/Projects/KebbiBrainUnity/Build/`：`kebbi-middleware-arm64.apk` / `kebbi-arm64.apk`。

## 已驗證（2026-06-19）
`verify` 跑過：`injected speechKey len=84 llmKey len=164; afterClear cleared=True`（值未外洩）。

## 安全提醒
- `.asset` 範本（含 committed 版）一律空金鑰；**切勿提交填了金鑰的 asset**。
- 金鑰用完 rotate；正式賽改更安全來源（開機讀本地檔／後端換臨時 token）。
