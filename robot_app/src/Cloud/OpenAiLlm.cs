// ILlm 的 OpenAI 實作(Chat Completions)。主控台 HttpClient 版；Unity 端同樣可用 UnityWebRequest 打同 endpoint。
#if !UNITY
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.Cloud
{
    public sealed class OpenAiLlm : ILlm
    {
        private readonly string _key;
        private readonly string _model;
        private readonly Action<string> _out;

        public OpenAiLlm(string key, string model, Action<string> output)
        {
            _key = key; _model = model; _out = output ?? Console.WriteLine;
        }

        public async Task<string> AskAsync(string system, string user)
        {
            string body = JsonSerializer.Serialize(new
            {
                model = _model,
                max_tokens = 256,
                messages = new[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user }
                }
            });
            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            req.Headers.Add("Authorization", "Bearer " + _key);
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var resp = await Http.Client.SendAsync(req);
            string json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception("OpenAI API 失敗 " + (int)resp.StatusCode + "：" + json);

            using (var doc = JsonDocument.Parse(json))
            {
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
                    return (choices[0].GetProperty("message").GetProperty("content").GetString() ?? "").Trim();
                return "";
            }
        }
    }
}
#endif
