using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KebbiBrain.Hardware;

namespace KebbiBrain.App
{
    // 說話接力故事圈:小朋友圍著凱比一人一句編故事,凱比當主持人。純 C#(IVoice/ILlm/IKebbiBody 抽象)→ 主控台可測。
    // 「點名制」:輪替狀態機決定下一位是誰(不靠 DOA 猜誰在講,只用 DOA 轉頭面向被點到的那位)→ 規避多人同講的 DOA 歧義。
    // 端點偵測(這孩子講完沒)沿用 ConversationStt 的設計重點;凱比每隔幾位也貢獻一句推進故事。

    public sealed class StoryParticipant
    {
        public string Name; public float DoaDeg; public bool IsKebbi;
        public StoryParticipant(string name, float doaDeg = 0f, bool isKebbi = false) { Name = name; DoaDeg = doaDeg; IsKebbi = isKebbi; }
    }

    // 純輪替狀態機:小朋友 round-robin,凱比每 KebbiEvery 位小朋友後插一句。無 I/O → 可斷言。
    public sealed class StoryCircleCore
    {
        private readonly List<StoryParticipant> _kids;
        private readonly StoryParticipant _kebbi;
        private readonly List<string> _story = new List<string>();
        private int _kidIdx, _sinceKebbi;
        public int KebbiEvery;
        public StoryParticipant Current { get; private set; }
        public int TurnIndex { get; private set; } = -1;
        public IReadOnlyList<string> Story => _story;

        public StoryCircleCore(IReadOnlyList<StoryParticipant> kids, int kebbiEvery = 2, string kebbiName = "凱比")
        {
            _kids = new List<StoryParticipant>(kids ?? new List<StoryParticipant>());
            _kebbi = new StoryParticipant(kebbiName, 0f, true);
            KebbiEvery = kebbiEvery;
        }

        // 下一位發言者(點名)。凱比每 KebbiEvery 位小朋友後輪到;小朋友 round-robin 跨凱比延續(不重頭)。
        public StoryParticipant Next()
        {
            if (KebbiEvery > 0 && _sinceKebbi >= KebbiEvery) { _sinceKebbi = 0; Current = _kebbi; }
            else if (_kids.Count > 0) { Current = _kids[_kidIdx]; _kidIdx = (_kidIdx + 1) % _kids.Count; _sinceKebbi++; }
            else Current = _kebbi;
            TurnIndex++;
            return Current;
        }

        public bool IsKebbiTurn => Current != null && Current.IsKebbi;

        // 把目前發言者的這句接進故事。空句不接。
        public void Add(string sentence)
        {
            if (Current != null && !string.IsNullOrWhiteSpace(sentence))
                _story.Add(Current.Name + ":" + sentence.Trim());
        }

        public string StoryText() => string.Join(" ", _story);
    }

    public sealed class StoryCircleGame
    {
        private readonly StoryCircleCore _core;
        private readonly IVoice _voice;
        private readonly ILlm _llm;
        private readonly IKebbiBody _body;
        private readonly Action<string> _log;

        public StoryCircleCore Core => _core;

        public StoryCircleGame(IReadOnlyList<StoryParticipant> kids, IVoice voice, ILlm llm, IKebbiBody body,
                               int kebbiEvery = 2, Action<string> log = null)
        {
            _core = new StoryCircleCore(kids, kebbiEvery);
            _voice = voice; _llm = llm; _body = body; _log = log;
        }

        public static string KebbiSystem()
            => "你是說故事接力的主持人凱比。讀目前的故事接龍,只加『一句』把故事往前推進,保留童趣、開放讓小朋友接下去。" +
               "只用繁體中文(台灣),只輸出那一句、簡短。";

        // 跑 turns 回合:點名→(小朋友)轉頭面向他+聽他一句 /(凱比)生一句說出。皆接進故事。
        public async Task RunTurnsAsync(int turns)
        {
            for (int i = 0; i < turns; i++)
            {
                var who = _core.Next();
                if (!who.IsKebbi && _body != null) { try { KebbiHead.FaceFully(_body, who.DoaDeg); } catch { } }  // 點名:轉頭面向這位

                if (who.IsKebbi)
                {
                    string line;
                    try { line = (await _llm.AskAsync(KebbiSystem(), _core.StoryText() + "\n凱比:") ?? "").Trim(); }
                    catch { line = ""; }
                    if (line.Length == 0) line = "後來呢?換你接下去!";
                    _core.Add(line);
                    if (_voice != null) { try { await _voice.SpeakAsync(line, "zh-TW"); } catch { } }
                    _log?.Invoke("📖 凱比: " + line);
                }
                else
                {
                    string heard = "";
                    if (_voice != null) { try { heard = await _voice.ListenAsync("zh-TW"); } catch { } }
                    _core.Add(heard);
                    _log?.Invoke("🧒 " + who.Name + ": " + heard);
                }
            }
        }
    }
}
