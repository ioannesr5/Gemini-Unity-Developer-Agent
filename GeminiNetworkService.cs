using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// API通信の結果をカプセル化するDTO (封装 API 通信结果的数据传输对象)
/// </summary>
public class GeminiNetworkResult
{
    public bool IsSuccess;
    public string ResponseText; // APIからの生テキストまたはJSON (API 返回的原始文本或命令 JSON)
    public string ErrorMessage;
    public int StatusCode;
}

/// <summary>
/// Gemini API とのHTTP通信を専任する純粋なネットワークサービスクラス
/// (专职负责与 Gemini API 进行 HTTP 通信的纯净网络服务类)
/// </summary>
public static class GeminiNetworkService
{
    /// <summary>
    /// POSTリクエストを送信する (发送 POST 请求，主要用于生成内容)
    /// </summary>
    public static async Task<GeminiNetworkResult> SendPostRequestAsync(string url, string jsonPayload, string proxyUrl, int timeoutSeconds = 60, bool extractFunctionCall = true)
    {
        GeminiNetworkResult result = new GeminiNetworkResult();

        try
        {
            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true };
            if (!string.IsNullOrEmpty(proxyUrl?.Trim()))
            {
                handler.Proxy = new WebProxy(proxyUrl.Trim());
                handler.UseProxy = true;
            }

            using (HttpClient client = new HttpClient(handler))
            {
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(url, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                result.StatusCode = (int)response.StatusCode;
                result.IsSuccess = response.IsSuccessStatusCode;

                if (result.IsSuccess)
                {
                    result.ResponseText = extractFunctionCall ? ExtractTextFromResponse(responseBody, true) : responseBody;
                }
                else
                {
                    result.ErrorMessage = responseBody;
                }
            }
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// GETリクエストを送信する (发送 GET 请求，主要用于获取模型列表)
    /// </summary>
    public static async Task<GeminiNetworkResult> SendGetRequestAsync(string url, string proxyUrl, int timeoutSeconds = 15)
    {
        GeminiNetworkResult result = new GeminiNetworkResult();

        try
        {
            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true };
            if (!string.IsNullOrEmpty(proxyUrl?.Trim()))
            {
                handler.Proxy = new WebProxy(proxyUrl.Trim());
                handler.UseProxy = true;
            }

            using (HttpClient client = new HttpClient(handler))
            {
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                HttpResponseMessage response = await client.GetAsync(url);
                string responseBody = await response.Content.ReadAsStringAsync();

                result.StatusCode = (int)response.StatusCode;
                result.IsSuccess = response.IsSuccessStatusCode;
                result.ResponseText = responseBody;
                if (!result.IsSuccess) result.ErrorMessage = responseBody;
            }
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private static string ExtractTextFromResponse(string rawApiResponse, bool checkFunctionCall)
    {
        GeminiResponseWrapper wrapper = JsonUtility.FromJson<GeminiResponseWrapper>(rawApiResponse);
        if (wrapper != null && wrapper.candidates != null && wrapper.candidates.Length > 0)
        {
            var part = wrapper.candidates[0].content.parts[0];

            if (checkFunctionCall && part.functionCall != null && !string.IsNullOrEmpty(part.functionCall.name))
            {
                return JsonUtility.ToJson(part.functionCall.args);
            }

            string text = part.text;
            if (!string.IsNullOrEmpty(text))
            {
                if (text.Contains("```json"))
                {
                    int start = text.IndexOf("```json") + 7;
                    int end = text.LastIndexOf("```");
                    if (end > start) text = text.Substring(start, end - start);
                }
                return text.Trim();
            }
        }
        return checkFunctionCall ? "{}" : "ERROR";
    }
}