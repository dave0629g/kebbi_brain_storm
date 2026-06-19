# Kebbi 功能 ↔ App 對應表（編號・中文名・覆蓋檢查）

> 目的：把 Kebbi 的**每一個功能**都對到一個「有應用價值的場景 app」，給編號＋中文名，作為 loop 的覆蓋檢查清單。
> 目標：**所有功能都有對應的 app**。本表每次 loop 檢查時更新。最後更新 2026-06-20。
> 跑法：純 Sim 示範 `dotnet bin/Debug/net8.0/KebbiBrain.dll --<flag>`；實機 `unity-build/build-apk.sh <target>`。

## 一、應用場景 App（編號・中文名）

| 編號 | 中文名 | 一句話場景 | Sim flag | 實機 build | 主要展示的功能 |
|---|---|---|---|---|---|
| **A1** | 雙機接力闖關 | 學生寫積木指令，兩台真機地板接力繞障、撞牆即失敗 | `--g1` | `g1director`+`controlled` | 底盤移動/旋轉、雙機接力、避障、交棒 |
| **A2** | 幾何證明接力站 | 乙機念理由→甲機走到該邊轉頭指認，逐步把證明外化 | `--g2`/`--g2v`/`--g2h` | `g2director`+`controlled` | 走位、手臂指認、轉頭、雙機 POINT/DONE |
| **A3** | 動作鏡像教練 | 真實關節立體鏡像示範，喊「太快」聲源轉頭降速、「再一次」逐幀回退 | `--g3`/`--g3r`/`--g3f` | (middleware) | 關節逐幀、HoldMs/Loops、DOA 轉頭、鏡像對映 |
| **A4** | Tebak Arah 印尼語方位 | Kebbi 憑聲源定位轉頭指向發話者，印尼語判方位詞對錯 | `--g4t`/`--g4e` | (middleware) | DOA 真值、8 向方位、轉頭、印尼語 TTS/STT/LLM |
| **A5** | 歷史現場法庭辯論 | 兩台分處兩端控辯接力、向中央逼近、轉身點名、投票宣判 | `--g5`/`--g5t` | `g5director`+`controlled` | 雙機機身+遠端語音、逼近、轉身、投票 |
| **A6** | 凱比聯合學園祭（合體彩蛋） | 中控導演機 cue G2/G3/G5 各站接力，離線自動降級，壓軸全體同步 | `--finale` | (多機) | 多機編排 CUE/ACK/DONE、降級備案 |
| **A7** | 印尼語雙人格對話 | 兩台各有人格（Andi 開朗/Budi 沉穩）用印尼語一來一往持續對話 | （Sim 自測 T_Conversation） | `converse`×2 | LLM 人格台詞、TTS、完成信號交棒、握手、自我修正時機 |
| **A8** | 即時狀態 HUD | sim Kebbi 螢幕以文字即時顯示狀態與收/送訊息（鏡像 Debug.Log） | （隨各 build 掛上） | 隨各 APK | OnGUI 文字疊層、可視化多機收送 |
| **A0** | 複合面向示範 | base turn 粗調 + NeckZ±40 細調 → 完整面向任意角，桌型誠實回報最近可達 | `--face` | — | FaceFully、NeckZ 物理可達性判決 |

## 二、功能 → 由哪個 App 覆蓋（確保每個功能都有 app）

| 功能 | 覆蓋的 App | 實機驗證狀態 |
|---|---|---|
| 底盤 move/turn | A1, A2 | 程式路徑驗（真馬達需真 Kebbi，必測③）|
| 關節馬達 SetMotor（手臂/頭） | A1,A2,A3,A5 | ✅ 兩台真機（被控機收 BC\|SM 執行）|
| 複合面向 FaceFully（turn+NeckZ±40） | A0, A4 | ✅ Sim；真馬達角度待量（必測⑥）|
| DOA 聲源定位 | A3, A4 | ⏳ 需真 Kebbi 麥克風陣列（必測⑤）|
| 8 向方位 Direction | A4 | ✅ Sim（含 NaN/Inf 防護）|
| 雲端 TTS（印尼語/中文） | A4,A5,A7 | ✅ 真機（cloud + 對話實播）|
| 雲端 STT | A4 | ✅ 管線通；真人辨識準確度需人耳 |
| 雲端 LLM | A4,A5,A7 | ✅ 真機（對話即時生成印尼語）|
| 多機 UDP link（廣播+unicast 備援） | A1,A2,A5,A6,A7 | ✅ 兩台真機（PeerRegistry 自動學習）|
| 遠端機身 RemoteBodyProxy | A1,A2,A5 | ✅ 兩台真機 |
| 遠端語音 RemoteVoiceProxy | A5 | ⏳ 身體已驗；語音播出需人耳 |
| 說完才交棒（完成信號/握手） | A7 | ✅ 兩台真機（連續 6 分對話）|
| 螢幕可視化 HUD | A8 | ✅ 已掛進 build |

## 三、覆蓋結論
- **每個功能都至少有一個 app 覆蓋** ✅。
- 仍待「真 Kebbi 硬體」才能完整驗的：DOA（A3/A4）、真馬達 move/turn 與角度（必測③⑤⑥）。
- 仍待「人耳/真人」：TTS 發音品質、真人 STT、遠端語音播出（A5）。
- GitHub：本表與各 app 說明在 `auto/dev-loop` 分支；教案 `教案_G1~G5_*.md`、決賽腳本、`成果總覽.md` 對應 A1–A6。

> loop 檢查項：①是否有新功能未對到 app？②各 app 實機驗證狀態是否更新？③能補的覆蓋是否補上？
