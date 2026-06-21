// 主控台「真雲端」HttpClient.SendAsync 的退避守門(整檔 #if !UNITY)。
// 用 RetryPolicy/RetryLoop:429/5xx/逾時退避重試、尊重 Retry-After、401/403 快速失敗。
// makeReq 每次重試重建 HttpRequestMessage(單次性,不可重用);全部嘗試都拋例外時回 null,呼叫端須判 null。
#if !UNITY
using System;
using System.Net.Http;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.Cloud
{
    internal static class CloudRetry
    {
        internal static Task<HttpResponseMessage> SendAsync(
            Func<HttpRequestMessage> makeReq, Action<string> log = null,
            RetryPolicy policy = null, Func<int, Task> delay = null)
        {
            return RetryLoop.RunAsync(async attempt =>
            {
                try
                {
                    HttpResponseMessage resp = await Http.Client.SendAsync(makeReq());
                    int retryAfter = 0;
                    if (resp.Headers.TryGetValues("Retry-After", out var vals))
                        foreach (var v in vals) { if (int.TryParse(v, out var ra)) { retryAfter = ra; break; } }
                    return new AttemptResult<HttpResponseMessage>(resp, (int)resp.StatusCode, retryAfter);
                }
                catch (Exception e)
                {
                    if (log != null) log("[CloudRetry] 送出例外(視為可重試): " + e.Message);
                    return new AttemptResult<HttpResponseMessage>(null, 0, 0);   // status 0 → 可重試
                }
            }, policy, delay, log);
        }
    }
}
#endif
