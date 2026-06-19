# tools — 多機網路診斷

## udplisten.c — UDP 廣播/單播接收驗證器

實測發現:**有些 WiFi AP 會丟棄無線客戶端之間的廣播幀**(unicast 通、255.255.255.255 與子網廣播都不通)。
這會讓只靠 UDP 廣播的多機發現失效。此工具用來在「不能跑我們 APK 的第二台裝置」上驗證網路層收包。

### 用途情境
2026-06-19 兩台實體 Android 對測:小米(Android 11)能跑 app、ASUS ZenFone 2(Android 5.0.2 / API 21 < APK minSdk 22)跑不了 app。
用本工具把 ASUS 當純網路接收端,證明:
- Mac/小米 → ASUS **unicast 收得到**
- Mac/小米 → ASUS **broadcast(255 與子網)收不到** ← AP 丟廣播
- 小米 app 加了 `UnityRobotLink` 的 unicast 備援後 → ASUS **收到 app 的 RobotLink 封包**(問題解除)

### 編譯(NDK,動態 + sysv hash,可跑在 Android 5.0.2 的舊 linker)
```sh
CC=~/Library/Android/sdk/ndk/<ver>/toolchains/llvm/prebuilt/darwin-x86_64/bin/aarch64-linux-android21-clang
"$CC" -O2 -Wl,--hash-style=sysv -o udplisten udplisten.c
```
> 註:`-static` 會踩 NDK r23+ 的「TLS segment underaligned」bug，舊機拒載；
> 改用「動態 + `--hash-style=sysv`」：API 21 的 bionic 不認 gnu hash 但認 sysv，動態又避開靜態 TLS bug。

### 用法
```sh
adb push udplisten /data/local/tmp/ && adb shell chmod 755 /data/local/tmp/udplisten
adb shell /data/local/tmp/udplisten 4    # 收 4 包後結束;印出來源 IP + payload
```

### 競賽現場提醒
若場地 AP 也丟廣播,兩台凱比要靠 `UnityRobotLink` 的 **unicast 備援**:
build 時設 `KEBBI_PEER_IP=<對方IP>`(逗號分隔多台),或讓雙方先以靜態 IP 互指。
