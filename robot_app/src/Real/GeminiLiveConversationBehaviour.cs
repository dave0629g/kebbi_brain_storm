// Gemini Live API 即時語音對話(整檔 #if UNITY)。
// 麥克風 → 16kHz PCM 串流上行 → 模型原生 turn-taking/VAD → 收 24kHz 語音串流播放 + 雙語字幕。
// 模型 gemini-3.1-flash-live-preview。協定組裝/解析在純 C# 的 App.GeminiLiveProtocol(主控台已測)。
//
// WebSocket:用 System.Net.WebSockets.ClientWebSocket(Google 公開 CA → IL2CPP 預設驗證即可,不需自訂 callback)。
//   receive/send 各跑一個背景 Task(內絕不碰 UnityEngine);麥克風在主緒 Update 讀;音訊用 ring buffer + OnAudioFilterRead。
// 回授處理(無 AEC):預設「Kebbi 講話時不送麥克風」(gateMicWhileSpeaking)避免聽到自己;戴耳機可關掉做真全雙工。
#if UNITY
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Android;
using KebbiBrain.App;

namespace KebbiBrain.Real
{
    public sealed class GeminiLiveConversationBehaviour : MonoBehaviour
    {
        public string apiKey = "";
        public string model = GeminiLiveProtocol.DefaultModel;
        public string systemInstruction = "你是凱比,一個親切的教育機器人。只用繁體中文(台灣)講話,每次一兩句、簡短自然、會鼓勵小朋友。";
        public bool gateMicWhileSpeaking = true;  // Kebbi 講話時不送麥克風(無 AEC 防回授);戴耳機可設 false 做全雙工

        // ── WebSocket ──
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<string> _sendQ = new ConcurrentQueue<string>();
        private volatile bool _setupDone;
        private volatile string _status = "啟動中…";
        private volatile string _userText = "", _kebbiText = "";
        private int _turns;

        // ── 播放 ring buffer(SPSC:receive 緒寫、audio 緒讀)──
        private float[] _ring;
        private long _wIdx, _rIdx;     // 絕對計數(Interlocked/volatile 讀寫)
        private int _outRate = 48000;
        private AudioSource _src;

        // ── 麥克風 ──
        private string _micDev;
        private AudioClip _micClip;
        private int _micRead, _micRate;
        private byte[] _pcmAccum; private int _pcmLen;
        private float[] _tmp = new float[16000];

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(apiKey)) { _status = "⚠ 沒有 Gemini 金鑰(KEBBI_GEMINI_KEY)"; Debug.LogWarning("[Live] " + _status); yield break; }
            _outRate = AudioSettings.outputSampleRate;
            _ring = new float[_outRate * 2];               // 2 秒緩衝
            _pcmAccum = new byte[GeminiLiveProtocol.InputRate * 2];   // 1 秒 16-bit
            SetupPlayer();

            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                _status = "請允許麥克風…"; Permission.RequestUserPermission(Permission.Microphone);
                float t = 0; while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && t < 12f) { yield return new WaitForSeconds(0.5f); t += 0.5f; }
            }
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone)) { _status = "⚠ 沒有麥克風權限"; yield break; }

            float wd = 0; while (Microphone.devices.Length == 0 && wd < 4f) { yield return new WaitForSeconds(0.3f); wd += 0.3f; }
            if (Microphone.devices.Length == 0) { _status = "⚠ 找不到麥克風"; yield break; }
            _micDev = Microphone.devices[0];
            _micClip = Microphone.Start(_micDev, true, 1, GeminiLiveProtocol.InputRate);
            _micRate = _micClip != null ? _micClip.frequency : GeminiLiveProtocol.InputRate;
            _micRead = 0;

            _cts = new CancellationTokenSource();
            ConnectAndRun();   // async void:連線 + 收/送背景緒
            _status = "連線中…(對著手機講話,Kebbi 會回你)";
        }

        private async void ConnectAndRun()
        {
            try
            {
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(new Uri(GeminiLiveProtocol.WsUrl(apiKey)), _cts.Token);
                _connected = true;
                await SendRawAsync(GeminiLiveProtocol.BuildSetupJson(model, systemInstruction));
                _ = Task.Run(ReceiveLoop);
                _ = Task.Run(SendLoop);
                _status = "已連線,等握手…";
            }
            catch (Exception e) { _status = "✗ 連線失敗: " + e.Message; Debug.LogError("[Live] " + _status); }
        }

        private async Task SendLoop()
        {
            try
            {
                while (!_cts.IsCancellationRequested && _ws != null && _ws.State == WebSocketState.Open)
                {
                    if (_sendQ.TryDequeue(out var json)) await SendRawAsync(json);
                    else await Task.Delay(5);
                }
            }
            catch (Exception e) { _status = "送出中止: " + e.Message; }
        }

        private async Task SendRawAsync(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
        }

        private async Task ReceiveLoop()
        {
            var buf = new byte[1 << 16];
            var ms = new System.IO.MemoryStream();
            try
            {
                while (!_cts.IsCancellationRequested && _ws.State == WebSocketState.Open)
                {
                    ms.SetLength(0);
                    WebSocketReceiveResult res;
                    do
                    {
                        res = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), _cts.Token);
                        if (res.MessageType == WebSocketMessageType.Close) { _status = "伺服器關閉連線"; return; }
                        ms.Write(buf, 0, res.Count);
                    } while (!res.EndOfMessage);
                    HandleServer(Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length));
                }
            }
            catch (Exception e) { if (!_cts.IsCancellationRequested) { _status = "接收中止: " + e.Message; Debug.LogError("[Live] " + _status); } }
        }

        // 背景緒:解析 + 寫 ring/字幕。絕不碰 UnityEngine(_outRate 已在主緒快取)。
        private void HandleServer(string json)
        {
            var m = GeminiLiveProtocol.TryParseServer(json);
            if (m.SetupComplete) { _setupDone = true; _status = "握手完成,開始對話 🎙️"; }
            if (m.Interrupted) { Interlocked.Exchange(ref _rIdx, Interlocked.Read(ref _wIdx)); } // barge-in:丟掉還沒播的
            if (!string.IsNullOrEmpty(m.InputText)) _userText = m.InputText;
            if (!string.IsNullOrEmpty(m.OutputText)) _kebbiText = (_kebbiText + m.OutputText);
            if (!string.IsNullOrEmpty(m.AudioBase64)) EnqueueAudio(m.AudioBase64);
            if (m.TurnComplete) { _turns++; _userText = ""; }
        }

        private void EnqueueAudio(string b64)
        {
            byte[] pcm;
            try { pcm = Convert.FromBase64String(b64); } catch { return; }
            var f = new float[pcm.Length / 2];
            int n = GeminiLiveProtocol.Pcm16ToFloat(pcm, pcm.Length, f);
            float[] outp = GeminiLiveProtocol.Resample(f, n, GeminiLiveProtocol.OutputRate, _outRate);
            long w = Interlocked.Read(ref _wIdx), r = Interlocked.Read(ref _rIdx);
            int cap = _ring.Length;
            for (int i = 0; i < outp.Length; i++)
            {
                if (w - r >= cap) { r = Interlocked.Read(ref _rIdx); if (w - r >= cap) break; } // 滿了就停(別覆蓋)
                _ring[(int)(w % cap)] = outp[i]; w++;
            }
            Interlocked.Exchange(ref _wIdx, w);
        }

        private void SetupPlayer()
        {
            _src = gameObject.AddComponent<AudioSource>();
            var silent = AudioClip.Create("live", _outRate, 1, _outRate, false); // 1 秒無聲 looping 載體
            _src.clip = silent; _src.loop = true; _src.spatialBlend = 0f; _src.Play();
        }

        // audio 緒:從 ring 拉資料填到輸出(mono → 各聲道)。零配置、零 Unity API、underrun 補靜音。
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (_ring == null) return;
            long r = Interlocked.Read(ref _rIdx), w = Interlocked.Read(ref _wIdx);
            int cap = _ring.Length, frames = data.Length / channels, di = 0;
            for (int i = 0; i < frames; i++)
            {
                float s = 0f;
                if (r < w) { s = _ring[(int)(r % cap)]; r++; }
                for (int c = 0; c < channels; c++) data[di++] = s;
            }
            Interlocked.Exchange(ref _rIdx, r);
        }

        private bool KebbiSpeaking => Interlocked.Read(ref _wIdx) - Interlocked.Read(ref _rIdx) > _outRate / 20; // ring 還有 >50ms 要播

        private void Update()
        {
            if (_micClip == null || !_setupDone) return;
            // 讀麥克風新增的 sample(環形,處理 wraparound)
            int pos = Microphone.GetPosition(_micDev);
            int clipLen = _micClip.samples;
            int avail = pos - _micRead; if (avail < 0) avail += clipLen;
            if (avail <= 0) return;
            if (avail > _tmp.Length) avail = _tmp.Length;
            ReadMic(_micRead, avail);
            _micRead = (_micRead + avail) % clipLen;

            if (gateMicWhileSpeaking && KebbiSpeaking) { _pcmLen = 0; return; } // 講話時不送(防回授)

            // _micRate→16k(若需要)後轉 PCM16 累積,湊滿 ~100ms(3200 byte)就送
            float[] src = _tmp; int srcN = avail;
            if (_micRate != GeminiLiveProtocol.InputRate)
            { src = GeminiLiveProtocol.Resample(_tmp, avail, _micRate, GeminiLiveProtocol.InputRate); srcN = src.Length; }
            int wrote = GeminiLiveProtocol.FloatToPcm16(src, srcN, _scratch);
            for (int i = 0; i < wrote && _pcmLen < _pcmAccum.Length; i++) _pcmAccum[_pcmLen++] = _scratch[i];
            int chunk = GeminiLiveProtocol.InputRate / 10 * 2; // 100ms = 3200 byte
            if (_pcmLen >= chunk)
            {
                _sendQ.Enqueue(GeminiLiveProtocol.BuildAudioChunkJson(_pcmAccum, _pcmLen));
                _pcmLen = 0;
            }
        }

        private byte[] _scratch = new byte[16000 * 2];
        private void ReadMic(int start, int count)
        {
            int clipLen = _micClip.samples;
            if (start + count <= clipLen) { _micClip.GetData(_tmp, start); }
            else
            {
                int tail = clipLen - start;
                var a = new float[tail]; _micClip.GetData(a, start);
                var b = new float[count - tail]; _micClip.GetData(b, 0);
                Array.Copy(a, 0, _tmp, 0, tail); Array.Copy(b, 0, _tmp, tail, count - tail);
            }
        }

        private void OnGUI()
        {
            int sw = Screen.width, sh = Screen.height;
            int fs = Mathf.Clamp(sh / 28, 22, 52);
            // 中央狀態/提示
            var sc = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Clamp(sh / 50, 16, 30), alignment = TextAnchor.UpperCenter, normal = { textColor = Color.white }, wordWrap = true };
            GUI.Label(new Rect(20, 20, sw - 40, 60), "🎙️ Gemini Live 對話 · " + _status + " · 第" + _turns + "輪", sc);
            // 使用者字幕(上)
            if (!string.IsNullOrEmpty(_userText))
            {
                var us = new GUIStyle(GUI.skin.label) { fontSize = fs, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.7f, .9f, 1f) }, wordWrap = true };
                GUI.Label(new Rect(30, sh * 0.42f, sw - 60, sh * 0.18f), "你:" + _userText, us);
            }
            // Kebbi 字幕(下)
            if (!string.IsNullOrEmpty(_kebbiText))
            {
                var ks = new GUIStyle(GUI.skin.label) { fontSize = fs, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.6f, 1f, .7f) }, wordWrap = true };
                GUI.Label(new Rect(30, sh * 0.64f, sw - 60, sh * 0.3f), "凱比:" + Tail(_kebbiText, 120), ks);
            }
        }

        private static string Tail(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s.Substring(s.Length - n);

        private void OnDestroy()
        {
            try { _cts?.Cancel(); } catch { }
            try { if (!string.IsNullOrEmpty(_micDev)) Microphone.End(_micDev); } catch { }
            try { _ws?.Abort(); _ws?.Dispose(); } catch { }
        }
    }
}
#endif
