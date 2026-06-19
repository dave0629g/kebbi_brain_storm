#!/usr/bin/env bash
# 驗證下載到的 ASUS 韌體 zip 是不是「ZE601KL (代號 Z00T) WW」的正確檔。
# 針對三胞胎陷阱(Z00T 共用於 ZE601KL / ZE551KL / ZD551KL Selfie)做檔內 CSC 判別。
# 用法: tools/verify-ze601kl-firmware.sh <firmware.zip>
#
# 你機(adb 實讀)的 ground truth:
#   ro.product.model=ASUS_Z011D  ro.product.device=ASUS_Z011  ro.product.name=WW_Z011
#   ro.build.csc.version=WW_ZE601KL_...   →  檔內 CSC 必須是 WW_ZE601KL 才對
set -u
ZIP="${1:-}"
[ -n "$ZIP" ] || { echo "用法: $0 <firmware.zip>"; exit 2; }
[ -f "$ZIP" ] || { echo "找不到檔案: $ZIP"; exit 2; }

warn=0; fail=0; fmt="?"
ok(){ echo "  ✅ $1"; }
wn(){ echo "  ⚠️  $1"; warn=$((warn+1)); }
no(){ echo "  ❌ $1"; fail=$((fail+1)); }

echo "============================================================"
echo "驗證: $ZIP"
echo "============================================================"

# [1] 大小
sz=$(stat -f%z "$ZIP" 2>/dev/null || stat -c%s "$ZIP" 2>/dev/null)
gb=$(awk "BEGIN{printf \"%.2f\", $sz/1073741824}")
echo "[1] 檔案大小: $sz bytes (~${gb} GB)"
if   [ "$sz" -lt 300000000 ]; then no "太小,八成是廣告頁 HTML 或殘檔,丟掉重抓"
elif [ "$sz" -ge 1400000000 ] && [ "$sz" -le 2200000000 ]; then ok "落在 1.4–2.2GB 合理範圍"
else wn "不在典型範圍,續驗(user.zip 與 raw 版大小本就不同)"; fi

# [2] zip 完整性
echo "[2] zip 完整性 (unzip -t)"
if unzip -t "$ZIP" >/dev/null 2>&1; then ok "無 CRC 錯誤,檔案完整"
else no "zip 損壞/下載不完整,重抓"; fi

# [3] 封裝格式: 本機更新 vs raw
echo "[3] 封裝格式"
listing=$(unzip -l "$ZIP" 2>/dev/null)
if echo "$listing" | grep -qiE 'META-INF/com/google/android/updater-script'; then
  ok "本機更新格式(有 updater-script) → stock recovery 可吃、保留資料、不需電腦"
  fmt="local"
elif echo "$listing" | grep -qiE '\.img( |$)|rawprogram|flashall|\.raw'; then
  wn "raw/AFT 格式(.img/rawprogram) → 要 AsusFlashTool/fastboot 整機刷、會清資料、風險較高"
  fmt="raw"
else wn "格式無法判定(內容可能壓在 .dat)"; fi

# [4][5] 最關鍵: 檔內 CSC 三胞胎判別 (不能靠檔名!)
echo "[4/5] 機型 CSC(靠檔內字串,非檔名)"
csc=$(LC_ALL=C grep -aoE 'WW_(ZE601KL|ZE551KL|ZE600KL|ZD551KL)_[0-9.]+' "$ZIP" 2>/dev/null | sort -u)
if [ -n "$csc" ]; then echo "$csc" | sed 's/^/        /'; fi
if echo "$csc" | grep -q 'WW_ZE601KL'; then
  if echo "$csc" | grep -qE 'WW_(ZE551KL|ZE600KL|ZD551KL)'; then
    no "同時含其他機型 CSC,可疑,別刷"
  else ok "只含 WW_ZE601KL → 機型正確(對上你機 ro.build.csc.version)"; fi
elif echo "$csc" | grep -qE 'WW_(ZE551KL|ZD551KL|ZE600KL)'; then
  no "是雙生機 ($csc),不是 ZE601KL,丟掉"
else
  wn "抓不到 CSC 明文字串(可能壓在 system.new.dat) → 改靠 [6] 版本號 + [8] 雜湊輔判,或交給我用其他方式拆驗"
fi

# [6] 版本號語意
echo "[6] 版本號語意(輔助快篩)"
base=$(basename "$ZIP")
if   echo "$base" | grep -qE '21\.40\.1220\.'; then ok "21.40.1220.* = ZE601KL/Laser 的 Marshmallow(對)"
elif echo "$base" | grep -qE '21\.40\.0\.';    then no "21.40.0.* = ZD551KL Selfie,拿錯機型"
elif echo "$base" | grep -qE '1\.1[0-9]\.40\.'; then wn "1.1x.40.* 還是 Lollipop,不是 Marshmallow"
else wn "檔名看不出版本,以 [5] CSC 為準"; fi

# [7] SKU
echo "[7] SKU"
if echo "$base$csc" | grep -qE 'JP_|_JP|CN_|_CN'; then no "疑似 JP/CN 版,你是 WW 台灣機,別拿錯"
else ok "未見 JP/CN 標記(應為 WW)"; fi

# [8] 雜湊: 自算 + 已知反例自保
echo "[8] 雜湊(自算 → 拿去和你下載鏡像頁所列值逐字比對)"
md5v=$(md5 -q "$ZIP" 2>/dev/null || md5sum "$ZIP" 2>/dev/null | awk '{print $1}')
sha=$(shasum -a 256 "$ZIP" 2>/dev/null | awk '{print $1}')
echo "        MD5    = $md5v"
echo "        SHA256 = $sha"
case "$md5v" in
  8451978749e6b45bea4c02b2a3ce9d93) no "此 MD5 = ZE551KL .1615 → 拿錯機型!" ;;
  e57fa2a8b3496c759fecd1084f168830) no "此 MD5 = ZD551KL .661 Selfie → 拿錯機型!" ;;
  4b5fb1d52d70f31fc762fc4d7c2c39c9) ok "此 MD5 = rebyte 所列 ZE601KL .1698(單一鏡像,僅供參考)" ;;
  *) wn "未知 MD5 → 和你下載鏡像頁標值比對;最好第二鏡像下同檔,兩邊 md5 一致才採信" ;;
esac

echo "============================================================"
echo "結論: 硬性錯誤 fail=$fail, 提醒 warn=$warn"
if [ "$fail" -gt 0 ]; then
  echo "❌ 有硬性問題,不要刷這個檔。"; exit 1
elif [ "$fmt" = "raw" ]; then
  echo "⚠️ 檔像是對的,但 raw 格式要 AsusFlashTool/fastboot(風險高、清資料)。"
  echo "   建議改找同機型的 UL-Z00T-WW-<ver>-user.zip 本機更新版。"; exit 0
else
  echo "✅ 看起來是對的 ZE601KL 本機更新檔。"
  echo "   ⚠️ 刷前提醒:你機現在 5.0.2(1.11.40.421),不能直升 MM——"
  echo "      先用手機『系統更新』把 Lollipop 升到最新 L 版,再套用此 MM 檔。"; exit 0
fi
