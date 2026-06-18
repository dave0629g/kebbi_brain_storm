using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.Sim
{
    // 文字型語音模擬器：說＝印出；聽＝從腳本佇列取出（沒有就回空字串）。
    public sealed class SimVoice : IVoice
    {
        private readonly Action<string> _out;
        private readonly Queue<string> _heard = new Queue<string>();

        public SimVoice(Action<string> output) { _out = output ?? Console.WriteLine; }

        // 測試腳本用：預先排入「學生會說的話」
        public void EnqueueHeard(string text) => _heard.Enqueue(text);

        public Task SpeakAsync(string text, string lang = "id-ID")
        {
            _out($"   🗣️  [Kebbi 說/{lang}] 「{text}」");
            return Task.CompletedTask;
        }

        public Task<string> ListenAsync(string lang = "id-ID")
        {
            string heard = _heard.Count > 0 ? _heard.Dequeue() : "";
            _out($"   🎧 [聽到/{lang}] 「{heard}」");
            return Task.FromResult(heard);
        }
    }
}
