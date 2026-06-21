using System;
using System.Threading.Tasks;

namespace KebbiBrain.Hardware
{
    // 雲端呼叫守門(純決策,無 I/O → 可單測)。依 HTTP 狀態碼 + 第幾次嘗試 → 是否重試 + 退避毫秒。
    // 規則:2xx/3xx=成功不重試;401/403=認證錯→快速失敗不重試;其他 4xx(400/404…)=用戶端錯不重試;
    //       429 / 5xx / 0(逾時或網路例外)=可重試到上限。429 優先尊重 Retry-After。
    // 動機:demo 最常見當機點就是雲端偶發 429/逾時,現況「一次失敗就 throw/回空」→ 畫面空白。
    public struct RetryDecision
    {
        public bool ShouldRetry;
        public int DelayMs;
        public RetryDecision(bool retry, int delayMs) { ShouldRetry = retry; DelayMs = delayMs; }
    }

    public sealed class RetryPolicy
    {
        public int MaxAttempts;   // 含第一次的總嘗試次數上限
        public int BaseDelayMs;   // 指數退避基數
        public int MaxDelayMs;    // 單次等待上限(也夾 Retry-After)

        public RetryPolicy(int maxAttempts = 4, int baseDelayMs = 400, int maxDelayMs = 8000)
        { MaxAttempts = maxAttempts; BaseDelayMs = baseDelayMs; MaxDelayMs = maxDelayMs; }

        public static readonly RetryPolicy Default = new RetryPolicy();

        public static bool IsSuccess(int status) => status >= 200 && status < 400;
        public static bool IsRetryable(int status) => status == 0 || status == 429 || status >= 500;

        // attempt 從 1 起算。retryAfterSec>0(429 常帶)時優先用它當等待。
        public RetryDecision Next(int status, int attempt, int retryAfterSec = 0)
        {
            if (IsSuccess(status)) return new RetryDecision(false, 0);
            if (!IsRetryable(status)) return new RetryDecision(false, 0);    // 401/403/其他 4xx → 快速失敗
            if (attempt >= MaxAttempts) return new RetryDecision(false, 0);  // 重試用盡

            int delay = retryAfterSec > 0 ? retryAfterSec * 1000 : BaseDelayMs * (1 << (attempt - 1));
            if (delay < 0 || delay > MaxDelayMs) delay = MaxDelayMs;          // 夾上限 + 溢位保護
            return new RetryDecision(true, delay);
        }
    }

    // 一次嘗試的結果:回傳值 + HTTP 狀態 + Retry-After 秒數。
    public struct AttemptResult<T>
    {
        public T Value;
        public int Status;
        public int RetryAfterSec;
        public AttemptResult(T value, int status, int retryAfterSec = 0) { Value = value; Status = status; RetryAfterSec = retryAfterSec; }
    }

    // 泛型重試迴圈:呼叫 send(attempt) 取結果+狀態 → 套 RetryPolicy 決定是否再送。平台無關(console/Unity 皆可)。
    // delay 可注入(測試傳即時完成);send 應自行 catch I/O 例外並回 Status=0(代表可重試)。
    public static class RetryLoop
    {
        public static async Task<T> RunAsync<T>(
            Func<int, Task<AttemptResult<T>>> send,
            RetryPolicy policy = null, Func<int, Task> delay = null, Action<string> log = null)
        {
            policy = policy ?? RetryPolicy.Default;
            delay = delay ?? (ms => Task.Delay(ms));
            int attempt = 0;
            while (true)
            {
                attempt++;
                AttemptResult<T> r = await send(attempt);
                RetryDecision dec = policy.Next(r.Status, attempt, r.RetryAfterSec);
                if (!dec.ShouldRetry) return r.Value;
                log?.Invoke("[CloudRetry] status=" + r.Status + " 第" + attempt + "次 → 等 " + dec.DelayMs + "ms 後重試");
                await delay(dec.DelayMs);
            }
        }
    }
}
