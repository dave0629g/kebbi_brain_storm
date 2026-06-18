using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KebbiBrain.Hardware
{
    // 在 IRobotLink 上提供「送出 → await 等一則符合條件的回覆，帶逾時」的 request/response 助手。
    // 為什麼需要它:多機編排(FinaleShowGame cue→ACK/DONE、未來 RemoteVoiceProxy 播畢 DONE)要「等對方回覆才往下」。
    //   • 舊寫法「送出後『同步』讀旗標」只在 Sim(SimRobotBus 同步遞送)成立;真機 UDP 非同步 → 回覆在讀取之後才到 → 全誤判。
    //   • 本助手用 TaskCompletionSource + Task.Delay 逾時,Sim 與 real 兩種遞送都正確。
    // ⚠ 用法鐵則:必須「先 WaitForAsync 取得 Task,再送出觸發訊息」。因為 Sim 同步遞送下,回覆會在 SendAsync 當下就抵達;
    //   若先送再 WaitForAsync,回覆早於等待者註冊 → 漏接 → 誤逾時。WaitForAsync 在第一個 await 前就同步把等待者登記好,故
    //   只要「var t = WaitForAsync(...); await link.SendAsync(...); var r = await t;」這個順序即安全。
    // OnMessage 是「單一 handler、後者覆寫」語意 → 本助手在建構式註冊唯一 handler;不符任何等待者的訊息轉交 alsoHandle。
    public sealed class LinkAwaiter
    {
        private sealed class Waiter
        {
            public Func<string, string, bool> Match;
            public TaskCompletionSource<string> Tcs;
        }

        private readonly object _gate = new object();
        private readonly List<Waiter> _waiters = new List<Waiter>();

        public LinkAwaiter(IRobotLink link, Action<string, string> alsoHandle = null)
        {
            link.OnMessage((from, text) =>
            {
                Waiter hit = null;
                lock (_gate)
                {
                    for (int i = 0; i < _waiters.Count; i++)
                        if (_waiters[i].Match(from, text)) { hit = _waiters[i]; _waiters.RemoveAt(i); break; }
                }
                if (hit != null) hit.Tcs.TrySetResult(text);   // 命中等待者 → 完成其 Task
                else alsoHandle?.Invoke(from, text);            // 沒人等 → 當一般訊息轉交
            });
        }

        // 等一則 (from,text) 符合 match 的訊息;timeoutMs 內沒等到回 null。回覆內容(text)為回傳值。
        // RunContinuationsAsynchronously:避免在 SendAsync 同步遞送鏈內就地跑續接造成重入。
        public async Task<string> WaitForAsync(Func<string, string, bool> match, int timeoutMs)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var w = new Waiter { Match = match, Tcs = tcs };
            lock (_gate) { _waiters.Add(w); }   // 註冊發生在第一個 await 之前(同步)→ 鐵則成立

            using (var cts = new CancellationTokenSource())
            {
                var delay = Task.Delay(timeoutMs, cts.Token);
                var winner = await Task.WhenAny(tcs.Task, delay);
                if (winner == tcs.Task)
                {
                    cts.Cancel();               // 取消逾時計時器(已命中)
                    return tcs.Task.Result;
                }
                // 逾時:撤掉等待者。邊界競態(可接受):若回覆恰在此刻抵達,handler 可能已先命中並移走 w(此處 Remove 回 false),
                // 該則回覆被消費但丟棄、本方法仍回 null —— 該站本就判逾時,丟棄遲到回覆語意正確。
                lock (_gate) { _waiters.Remove(w); }
                return null;
            }
        }
    }
}
