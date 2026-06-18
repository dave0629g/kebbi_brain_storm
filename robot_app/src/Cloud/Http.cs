// 主控台「真雲端」後端共用的 HttpClient。整檔 #if !UNITY：Unity 端改用 UnityWebRequest（見 src/Real）。
#if !UNITY
using System;
using System.Net.Http;

namespace KebbiBrain.Cloud
{
    internal static class Http
    {
        internal static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }
}
#endif
