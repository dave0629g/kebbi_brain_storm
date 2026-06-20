using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace KebbiBrain.Hardware
{
    // 一個被 Gemini 偵測到的物體:label + 中心點(point,0-1000 正規化)和/或邊界框(box_2d,0-1000)。
    public struct Detection
    {
        public string Label;
        public bool HasPoint; public float Y, X;                    // point = [y, x]
        public bool HasBox;   public float Ymin, Xmin, Ymax, Xmax;  // box_2d = [ymin, xmin, ymax, xmax]
    }

    // Gemini Robotics-ER 視覺:組請求 + 解析回應。純 C#(無 UnityEngine)→ 主控台可單測、Unity 也直接用。
    //   模型 gemini-robotics-er-1.6-preview,走一般 REST generateContent(非串流)。
    //   請求:contents[0].parts = [ {inline_data: image/jpeg base64}, {text: prompt} ]。
    //   回應:模型在 text 裡吐 JSON 陣列 [{"label":..,"point":[y,x],"box_2d":[ymin,xmin,ymax,xmax]}],座標 0-1000。
    // 來源:ai.google.dev/gemini-api/docs/robotics-overview、google-gemini/robotics-samples。
    public static class GeminiRoboticsProtocol
    {
        public const string DefaultModel = "gemini-robotics-er-1.6-preview";
        public const string ApiKeyHeader = "x-goog-api-key";  // 金鑰走 header(不放 URL → 不會進 log/歷史)

        public static string Endpoint(string model)
            => "https://generativelanguage.googleapis.com/v1beta/models/" +
               (string.IsNullOrEmpty(model) ? DefaultModel : model) + ":generateContent";

        // 預設探索 prompt:要求只回中文 label + point + box 的純 JSON(方便疊在畫面上)。
        public const string DefaultPrompt =
            "偵測這張影像中最明顯的幾個物體(最多 8 個)。只回傳一個 JSON 陣列,不要任何其他文字或說明。" +
            "每個物體一個物件,格式:{\"label\":\"繁體中文名稱\",\"point\":[y,x],\"box_2d\":[ymin,xmin,ymax,xmax]}。" +
            "座標都是 0 到 1000 的正規化整數,y 是垂直(0=最上)、x 是水平(0=最左)。";

        // 組 generateContent 請求 body(inline JPEG + 文字 prompt)。
        public static string BuildRequestBody(string base64Jpeg, string prompt)
        {
            string p = JsonEscape(string.IsNullOrEmpty(prompt) ? DefaultPrompt : prompt);
            var sb = new StringBuilder();
            sb.Append("{\"contents\":[{\"parts\":[");
            sb.Append("{\"inline_data\":{\"mime_type\":\"image/jpeg\",\"data\":\"").Append(base64Jpeg ?? "").Append("\"}},");
            sb.Append("{\"text\":\"").Append(p).Append("\"}");
            sb.Append("]}],\"generationConfig\":{\"temperature\":0}}");
            return sb.ToString();
        }

        // 從「模型輸出文字」或「整個 generateContent 回應 JSON」解析出 Detection 清單。
        // 容錯:先把跳脫引號(\") 還原,再用 regex 抓每個扁平物件(含 label 且含 point 或 box_2d)。
        public static List<Detection> ParseDetections(string responseOrText)
        {
            var list = new List<Detection>();
            if (string.IsNullOrEmpty(responseOrText)) return list;
            string s = responseOrText.Replace("\\\"", "\"").Replace("\\n", " ").Replace("\\r", " ");
            foreach (Match m in Regex.Matches(s, "\\{[^{}]*\\}"))
            {
                string obj = m.Value;
                if (obj.IndexOf("label", StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool hasPoint = obj.IndexOf("\"point\"", StringComparison.Ordinal) >= 0;
                bool hasBox = obj.IndexOf("box_2d", StringComparison.Ordinal) >= 0;
                if (!hasPoint && !hasBox) continue;
                var d = new Detection();
                var ml = Regex.Match(obj, "\"label\"\\s*:\\s*\"([^\"]*)\"");
                d.Label = ml.Success ? ml.Groups[1].Value : "?";
                var mp = Regex.Match(obj, "\"point\"\\s*:\\s*\\[\\s*(-?[0-9.]+)\\s*,\\s*(-?[0-9.]+)\\s*\\]");
                if (mp.Success) { d.HasPoint = true; d.Y = ParseF(mp.Groups[1].Value); d.X = ParseF(mp.Groups[2].Value); }
                var mb = Regex.Match(obj, "box_2d\"\\s*:\\s*\\[\\s*(-?[0-9.]+)\\s*,\\s*(-?[0-9.]+)\\s*,\\s*(-?[0-9.]+)\\s*,\\s*(-?[0-9.]+)\\s*\\]");
                if (mb.Success) { d.HasBox = true; d.Ymin = ParseF(mb.Groups[1].Value); d.Xmin = ParseF(mb.Groups[2].Value); d.Ymax = ParseF(mb.Groups[3].Value); d.Xmax = ParseF(mb.Groups[4].Value); }
                list.Add(d);
            }
            return list;
        }

        // 從整個 generateContent 回應抽出模型文字(Unity 端可改用 JsonUtility;此為純 C# 備援/顯示推理用)。
        public static string ExtractModelText(string fullResponseJson)
        {
            if (string.IsNullOrEmpty(fullResponseJson)) return "";
            var sb = new StringBuilder();
            foreach (Match m in Regex.Matches(fullResponseJson, "\"text\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""))
                sb.Append(m.Groups[1].Value.Replace("\\\"", "\"").Replace("\\n", " ")).Append(' ');
            return sb.ToString().Trim();
        }

        private static float ParseF(string s)
            => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;

        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
