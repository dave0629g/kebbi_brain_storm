# Kebbi × Gemini 整合 — 測試與發現總整理

> 本檔彙整本輪在 Kebbi(Unity/C# Android app)上**實際驗證過**的 Gemini API、模型、線上格式、實測結果、計費與雷點,供在 claude.ai 上討論後續設計。
> 最後更新:2026-06-20。所有「✅ 實測」都是真的對 Google 伺服器或真機跑過、有結果。
> 測試金鑰:Google **AI Studio** Gemini API key,以環境變數 `KEBBI_GEMINI_KEY` 注入,不落地、不進 log/URL。

---

## 0. 一頁總結

| 能力 | 模型 | 接法 | 計費模式 | Kebbi 用途 | 狀態 |
|---|---|---|---|---|---|
| **即時語音對話** | `gemini-3.1-flash-live-preview` | Live API(WebSocket 串流) | 按時間(音訊 token) | 跟真人自然語音對話、原生 turn-taking | ✅ 真機跑通(台灣國語) |
| **視覺認物** | `gemini-robotics-er-1.6-preview` | REST `generateContent` | 按次查詢(便宜) | 相機看懂場景、認物/指認/座標→轉頭 | ✅ 真機跑通(免費額度緊) |
| **即時翻譯** | `gemini-3.5-live-translate-preview` | Live API(WebSocket 串流) | 按時間 | 老師↔學生即時口譯(中⇄印尼) | 📋 已研究協定,未落地 |

**三個共用同一把 AI Studio key。** 語音/翻譯走同一套 WebSocket 串流工程(只差設定);視覺走一般 REST,最好接。

---

## 1. 即時語音對話 — `gemini-3.1-flash-live-preview`(✅ 真機跑通)

### 線上協定(已對真伺服器逐字驗證,Unity 端送的就是這個格式)
- **WebSocket**:`wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key=KEY`
- **第一則 setup**(連上立刻送):
  ```json
  {"setup":{
    "model":"models/gemini-3.1-flash-live-preview",
    "generationConfig":{"responseModalities":["AUDIO"]},
    "systemInstruction":{"parts":[{"text":"<persona>"}]},
    "inputAudioTranscription":{},
    "outputAudioTranscription":{}
  }}
  ```
  - ⚠️ 此模型**只支援 AUDIO 輸出**,送 `["TEXT"]` 會被拒(1007 invalid frame)。
  - `input/outputAudioTranscription:{}` = 開「原文/譯文逐字稿」(做字幕用)。
- **上行音訊**:`{"realtimeInput":{"audio":{"data":"<base64>","mimeType":"audio/pcm;rate=16000"}}}`
  - 規格:**16-bit PCM、16kHz、mono、little-endian**,每 ~100ms 一塊(=3200 byte)。
  - (`mediaChunks` 已 deprecated,用 `realtimeInput.audio`。)
- **下行**:`serverContent.modelTurn.parts[].inlineData.data` = base64 的 **24kHz 16-bit PCM** 語音;
  逐字稿在 `serverContent.inputTranscription.text`(使用者)與 `outputTranscription.text`(Kebbi);
  控制旗標:`setupComplete`、`turnComplete`、`interrupted`(被打斷=barge-in)、`goAway`(連線將斷)。
- **turn-taking / VAD / barge-in 由模型原生處理**(automaticActivityDetection 預設開)——不需自己算端點。

### ✅ 實測結果
1. **SDK / 原生 websockets 對真伺服器**:送一句「哈囉,你叫什麼名字?」→ 收到 **~387 KB 24kHz 語音** + 逐字稿:
   > 「哈囉!我叫凱比,是你的好朋友喔!很高興認識你!有什麼我可以幫忙的嗎?」
2. **真機(Black Shark, Android 11)整條跑通**:握手完成→開始對話。用 Mac 喇叭念台灣國語當「真人」測:
   - 問:「凱比你好,可以教我一句印尼語嗎?」
   - Kebbi(台灣國語語音 + 字幕)答:
     > 「…當然可以呀! 我們來學一句最實用的『謝謝』好了, 印尼語說 "Terima kasih"。你試著唸唸看? …你很棒喔!」
   - **= 麥克風聽到台灣國語 → 模型 → Kebbi 台灣國語語音回 + 字幕**,且真的聽懂、會教印尼語。persona/語言由 systemInstruction 控制(zh-TW 或 id-ID 可切)。

### 計費(AI Studio,2026-06)
- Live native audio:輸入 **$3** / 輸出 **$12** 每百萬 token;音訊 **~25 token/秒**。
- 估算:聽 ~$0.27/hr、講 ~$1/hr → 一小時真人對話 **約 $1 美元級**。**有免費額度(限速)**可開發測試。

### 雷點 / 待解
- **無 AEC → 回授**:邊播邊收會聽到自己。目前用「Kebbi 講話時不送麥克風」半雙工規避;要真全雙工 barge-in 需戴耳機或硬體 AEC。
- **音訊播放「忽快忽慢」**(已修):模型常**比即時更快**整段塞音訊,播放緩衝太小(2s)會丟樣本→跳著走。已改 **20 秒緩衝 + 串流式重採樣**(相位跨塊連續)→ 順了。
- **session 時長有上限**:別 hardcode,靠 `goAway.timeLeft` + `sessionResumption.handle` 重連。
- 安全:client 直連明文 `?key=` 有外洩風險,正式場合建議改 ephemeral token(`v1alpha` `?access_token=`)。

---

## 2. 視覺認物 — `gemini-robotics-er-1.6-preview`(✅ 真機跑通)

### 線上協定(一般 REST,最好接,跟現有 LLM 呼叫同款)
- **端點**:`POST https://generativelanguage.googleapis.com/v1beta/models/gemini-robotics-er-1.6-preview:generateContent`
  - 金鑰走 **header** `x-goog-api-key: KEY`(不放 URL → 不進 log)。
- **body**:`{"contents":[{"parts":[{"inline_data":{"mime_type":"image/jpeg","data":"<base64>"}},{"text":"<prompt>"}]}],"generationConfig":{"temperature":0}}`
- **prompt 要求回 JSON**,模型回:`[{"label":"中文名","point":[y,x],"box_2d":[ymin,xmin,ymax,xmax]}]`,座標 **正規化 0–1000**(y 垂直、x 水平)。常包在 ```json fence,解析要容錯。

### ✅ 實測結果
- 生一張測試圖(紅圓 / 藍方 / 綠長方)→ HTTP 200、模型**三個全中**:
  ```
  [{"label":"紅色圓形","point":[417,250],"box_2d":[250,125,585,376]},
   {"label":"藍色正方形","point":[511,719],"box_2d":[312,562,710,876]},
   {"label":"綠色長方形","point":[834,532],"box_2d":[750,406,918,658]}]
  ```
  換算 box 後**精準對上**我畫的形狀座標(如紅圓 box→(80,120)-(240,280),完全吻合)。
- **真機(Black Shark)**:相機開 1280×720、每次呼叫 Gemini 200、延遲約 2.4–4.8s。

### 計費 / 雷點
- 計費:輸入 **$1**(text/image)/ 輸出 **$5** 每百萬 token → **每次拍照+問約 0.2–1 美分**,只在問時才花錢(非一直開)。
- ⚠️ **免費額度很緊**:每 4 秒問一次會 `HTTP 429 "You exceeded your current quota"`。已放慢到 12 秒一次;**要連續用得在 Google Cloud 提額/開帳單**。
- 對 Kebbi:能「看懂 + 指認 + 轉向 + 講出來」(接現有 NeckZ/FaceFully/DOA);**不能抓取**(無夾爪、手臂搆不到任意點),Robotics-ER 的抓取軌跡輸出用不上。

---

## 3. 即時翻譯 — `gemini-3.5-live-translate-preview`(📋 已研究,未落地)

- 同 Live API WebSocket。setup 在 `generationConfig` 內加:
  `"translationConfig":{"targetLanguageCode":"<BCP-47>","echoTargetLanguage":true}` + `responseModalities:["AUDIO"]` + input/outputAudioTranscription。
- ⚠️ **語言碼用語言層級,不是 locale**:印尼語 = `id`(不是 `id-ID`)、繁中 = `zh-Hant`(不是 `zh-TW`)。直塞 `id-ID/zh-TW` 會被拒。來源語自動偵測。
- 只吃**音訊輸入**;輸出 24kHz PCM + 雙語逐字稿。延遲「比說話者慢幾秒」。
- 用途:Kebbi 當即時口譯機(老師中文↔學生印尼語)。工程量同 Live 對話(共用串流)。

---

## 4. 兩機語音對話(STT 語意端點,本輪前段,非 Gemini)— ✅ 真機跑通

- 架構:cascaded **Azure STT(印尼語/中文)→ OpenAI/Claude LLM → Azure TTS**;**內容走空氣(麥克風)、端點 Kebbi 自己用「LLM 語意完整度 + 靜音」判斷,零網路 token**。
- 第二支手機「扮真人」(persona+goal、會遲疑、用 `…` 標停頓分段 TTS 逼對方判端點)。
- 實測:Black Shark(Kebbi)↔ ASUS(扮真人),**連續 ~7 分鐘不卡死、~8 來回、主題連貫**(問路→指路→確認→道謝);台灣國語版亦通。
- 取捨:cascaded 批次 STT 使**每輪延遲 ~30–60s**(慢)→ 這正是後來改用 Gemini Live(串流、原生 turn-taking)的動機。
- 詳見 `對話實測_2026-06-20.md`(完整逐字稿)。

---

## 5. 基礎設施 / 工程發現

- **Unity 2022.3.62f3、IL2CPP、ARM64**;`build-apk.sh <target>` 出 APK。targets:`menu`(功能選單)/`live`/`robovision`/`conversestt`/`g4`…
- **金鑰鏈**:env → `KebbiSecrets.asset`(打包進 APK)→ build 後清空(磁碟不留 key);log 只印長度、URL 不帶 key。
- **WebSocket**:`System.Net.WebSockets.ClientWebSocket` **在 IL2CPP/Android 可直接用**(Google 公開 CA,預設驗證即可),不需 NativeWebSocket;receive/send 各跑背景 Task(內不碰 UnityEngine),音訊用 SPSC ring buffer + `OnAudioFilterRead`。
- **麥克風**:`Microphone.Start(dev,true,1,16000)` + `GetPosition` 輪詢(環形 wraparound),需要時重採樣到 16k。
- **功能選單**:`KebbiMenuBehaviour`——一個功能一個按鈕,點選 → 生成 `KebbiAppBehaviour` 跑該 Mode(裝一支 menu APK 即可在手機上點選,免每模式各裝一支)。
- **自測**:主控台 hand-rolled 測試 **431/431 綠**(含 Gemini 協定組裝/解析、語意端點、PCM 轉換/重採樣等,免金鑰可驗)。

## 6. 真機 / MIUI 雷點(實測踩過)

- **Black Shark(MIUI)權限框會暫停 Unity app**:首次用麥克風/相機跳 `RECORD_AUDIO`/`CAMERA` 授權框,蓋住 app → Unity `onPause` → 串流停。
  - 解:Android 端用 `UnityEngine.Android.Permission.RequestUserPermission` 觸發正規授權框(不是 `Application.RequestUserAuthorization`);adb 端 `pm grant` + `appops set ... allow`;`appops` 的 uid-mode `ignore` 只有走「真實授權流程」才會翻成 allow。
  - 保持螢幕亮:`svc power stayon true`(否則螢幕關→app pause→串流停)。
- 部分裝置字型**無 emoji 字符**→選單 emoji 顯示空白,已改純文字標籤。
- 測試手機:**Black Shark DLT-A0**(Android 11, 192.168.1.108,主測機)、**ASUS Z011D**(Android 6.0.1, 192.168.1.115)。

## 7. 待你決策 / 後續

- **額度/計費**:Robotics-ER preview 免費額度緊;要連續 demo 視覺/語音,需在 Google Cloud 開帳單或提額。
- **下一步可選**:(a) Live 對話接 Kebbi 動作(邊講邊轉頭/表情);(b) 視覺認物接「轉頭指物 + 講出名稱」;(c) 即時翻譯落地;(d) 全雙工 barge-in(需 AEC/耳機)。
- 程式都在分支 `auto/dev-loop`,已 commit/push。
