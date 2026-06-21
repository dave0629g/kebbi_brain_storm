// ILlm 的真雲端實作：Anthropic Claude Messages API（主控台 HttpClient 版）。
// Unity 端用 UnityWebRequest 打同一個 endpoint（見 src/Real）。
#if !UNITY
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.Cloud
{
    public sealed class ClaudeLlm : ILlm
    {
        private readonly string _key;
        private readonly string _model;
        private readonly Action<string> _out;

        public ClaudeLlm(string key, string model, Action<string> output)
        {
            _key = key; _model = model; _out = output ?? Console.WriteLine;
        }

        public async Task<string> AskAsync(string system, string user)
        {
            // 短一句糾錯提示：max_tokens 刻意小、不開 thinking（Opus 4.8 省略即不思考）。
            string body = JsonSerializer.Serialize(new
            {
                model = _model,
                max_tokens = 256,
                system = system,
                messages = new[] { new { role = "user", content = user } }
            });
            Func<HttpRequestMessage> make = () =>
            {
                var r = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
                r.Headers.Add("x-api-key", _key);
                r.Headers.Add("anthropic-version", "2023-06-01");
                r.Content = new StringContent(body, Encoding.UTF8, "application/json");
                return r;
            };
            var resp = await CloudRetry.SendAsync(make, _out);   // 429/5xx/逾時退避重試
            if (resp == null) throw new Exception("Claude 連線失敗(重試用盡)");
            string json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception("Claude API 失敗 " + (int)resp.StatusCode + "：" + json);

            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("stop_reason", out var sr) && sr.GetString() == "refusal")
                    return "(LLM 拒答)";
                if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                {
                    foreach (var block in content.EnumerateArray())
                    {
                        if (block.TryGetProperty("type", out var t) && t.GetString() == "text")
                            return (block.GetProperty("text").GetString() ?? "").Trim();
                    }
                }
                return "";
            }
        }
    }
}
#endif
