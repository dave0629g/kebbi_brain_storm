using System.Threading.Tasks;

namespace KebbiBrain.Hardware
{
    // LLM 抽象。real 後端用 UnityWebRequest 打第三方 LLM（免任何 Nuwa 授權）。
    public interface ILlm
    {
        Task<string> AskAsync(string system, string user);
    }
}
