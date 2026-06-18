using System;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.Sim
{
    // 決定式 LLM 模擬器：不需金鑰、輸出可預測，方便自測。
    // 真正的 LLM（Claude/OpenAI）在 real 後端用 UnityWebRequest 串接。
    public sealed class SimLlm : ILlm
    {
        private readonly Action<string> _out;
        public SimLlm(Action<string> output) { _out = output ?? Console.WriteLine; }

        public Task<string> AskAsync(string system, string user)
        {
            // 簡單規則：依 user 內含的關鍵字回固定但合宜的印尼語提示（模擬糾錯）。
            string reply;
            if (user.Contains("salah") || user.Contains("錯"))
                reply = "Ingat: 'kanan' itu sebelah kanan saya, bukan kananmu. Coba lihat dari sisi saya ya!";
            else
                reply = "Bagus! Sekarang coba arah yang lain.";
            _out($"   🤖 [LLM(sim)] {reply}");
            return Task.FromResult(reply);
        }
    }
}
