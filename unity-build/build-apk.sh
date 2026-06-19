#!/usr/bin/env bash
# 🔐 從 env 注入金鑰、build APK 的安全 wrapper。
#
# 金鑰只透過「環境變數」傳給 Unity process：KebbiBuild.InjectSecretsFromEnv() 讀 env →
#   寫進 Assets/KebbiSecrets.asset → 打包進 APK → build 後 ClearSecretsAsset() 清空(key 不落地)。
# 本腳本「不引用、不 echo、不寫檔」任何金鑰值；at-rest 的 .asset 一律空白。
#
# 用法：先在你的 shell export 金鑰（值不會被本腳本印出）：
#     export KEBBI_SPEECH_KEY=...   KEBBI_SPEECH_REGION=southeastasia   KEBBI_LLM_KEY=...
#     ./unity-build/build-apk.sh middleware   # 中介版(一般 Android 測 TTS/STT/LLM/UI)
#     ./unity-build/build-apk.sh real         # 真機版(真凱比)
#     ./unity-build/build-apk.sh verify       # 只驗 env→asset→clear 流程,不 build(~1-2 分)
set -euo pipefail

UNITY="${UNITY:-/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity}"
PROJ="${KEBBI_UNITY_PROJ:-$HOME/Projects/KebbiBrainUnity}"
LOG="${KEBBI_BUILD_LOG:-/tmp/kebbi-build.log}"

case "${1:-middleware}" in
  middleware) METHOD="KebbiBuild.BuildMiddlewareApk" ;;
  real)       METHOD="KebbiBuild.BuildApk" ;;
  linkping)   METHOD="KebbiBuild.BuildLinkPingApk" ;;   # 雙機 UDP 廣播驗證(必測④ 送出端)
  g1director) METHOD="KebbiBuild.BuildG1DirectorApk" ;; # G1 分散式中控(發 BodyCommand BC| 給遠端機)
  controlled) METHOD="KebbiBuild.BuildControlledApk" ;; # 被控機(收 BC| 套到本機 body 執行)
  g5director) METHOD="KebbiBuild.BuildG5DirectorApk" ;; # G5 分散式中控(辯方機身+語音 BC|/VC| 給遠端)
  g2director) METHOD="KebbiBuild.BuildG2DirectorApk" ;; # G2 分散式中控(甲機機身 BC| 給遠端;乙機語音本機)
  verify)     METHOD="KebbiBuild.VerifySecretsRoundTrip" ;;
  *) echo "用法: $0 [middleware|real|linkping|verify]"; exit 2 ;;
esac

# 只檢查「有無」金鑰，不印值
if [ -z "${KEBBI_SPEECH_KEY:-}" ] && [ -z "${KEBBI_LLM_KEY:-}" ]; then
  echo "⚠ KEBBI_SPEECH_KEY / KEBBI_LLM_KEY 未在 env → APK 會無雲端語音/LLM(仍可 build)。"
fi

echo "▶ $METHOD   (proj=$PROJ)"
# 同專案第二次 batchmode 常卡在「Loaded All Assemblies」→ 清快取強制完整匯入
rm -rf "$PROJ/Library" "$PROJ/Temp" "$PROJ/Logs"

# env(含 KEBBI_*)由本 shell 繼承給 Unity；此處不引用任何 key 值。
set +e
"$UNITY" -batchmode -quit -projectPath "$PROJ" -buildTarget Android \
  -executeMethod "$METHOD" -logFile "$LOG"
code=$?
set -e

# 摘要(Log 只印金鑰長度、不含值 → grep 安全)
grep -E "\[Secrets\]|\[KebbiBuild\]|error CS|Exiting batchmode" "$LOG" 2>/dev/null | tail -15 || true
echo "exit=$code"
exit $code
