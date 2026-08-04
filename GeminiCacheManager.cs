using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using UnityEngine.Networking;
using System.Text;
using System;

/// <summary>
/// コンテキストキャッシュ (Context Caching) のライフサイクルを管理するクラス。
/// トークン計算、APIへの登録、EditorPrefsを介した永続化(Persistence)を担当します。
/// </summary>
public class GeminiCacheManager
{
    private const string PREF_CACHE_NAME = "GeminiAgent_CacheName";
    private const string PREF_CACHE_EXPIRE = "GeminiAgent_CacheExpireTime";

    // 最低限必要なトークン数の閾値（APIの制限: 32768）(API硬性规定的最低 Token 门槛)
    private const int MIN_TOKEN_THRESHOLD = 32768;
    // デフォルトのTTL設定 (300秒 = 5分) (默认生存时间)
    private const string DEFAULT_TTL = "300s";

    public string CurrentCacheName => EditorPrefs.GetString(PREF_CACHE_NAME, string.Empty);
    public bool IsCacheValid => !string.IsNullOrEmpty(CurrentCacheName) && DateTime.UtcNow < GetExpireTime();

    public DateTime GetExpireTime()
    {
        string expireStr = EditorPrefs.GetString(PREF_CACHE_EXPIRE, string.Empty);
        if (DateTime.TryParse(expireStr, out DateTime expireTime)) return expireTime;
        return DateTime.MinValue;
    }

    /// <summary>
    /// コンテキストキャッシュの構築を試みます (尝试构建上下文缓存)
    /// 既存のDTO依存を避け、直接JSON文字列を構築して通信します。
    /// </summary>
    public async Task<bool> TryBuildCacheAsync(string apiKey, string modelName, string sysInstructionJson, string staticContextText, string toolsJsonArray)
    {
        // 簡易的なトークン推測 (Heuristic Token Estimation: 4文字 ≒ 1トークン)
        int estimatedTokens = staticContextText.Length / 4;
        if (estimatedTokens < MIN_TOKEN_THRESHOLD)
        {
            Debug.Log($"[Gemini Cache] トークン数が少なすぎます({estimatedTokens} < 32k)。APIの制限によりキャッシュ構築をスキップします。(Token amount too low, skipping cache build)");
            ClearCache();
            return false;
        }

        string cleanModelName = modelName.StartsWith("models/") ? modelName : $"models/{modelName}";
        string escapedContext = EscapeJsonString(staticContextText);

        // キャッシュ構築用のペイロード (Cache Payload) を手動で組み立てる
        string jsonPayload = $@"
        {{
            ""model"": ""{cleanModelName}"",
            ""systemInstruction"": {sysInstructionJson},
            ""tools"": {toolsJsonArray},
            ""contents"": [
                {{
                    ""role"": ""user"",
                    ""parts"": [{{ ""text"": ""{escapedContext}"" }}]
                }}
            ],
            ""ttl"": ""{DEFAULT_TTL}""
        }}";

        string url = $"https://generativelanguage.googleapis.com/v1beta/cachedContents?key={apiKey}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<GeminiCacheResponse>(request.downloadHandler.text);
                EditorPrefs.SetString(PREF_CACHE_NAME, response.name);
                EditorPrefs.SetString(PREF_CACHE_EXPIRE, response.expireTime);
                Debug.Log($"[Gemini Cache] ✅ キャッシュ生成成功 (Cache created successfully): {response.name}");
                return true;
            }
            else
            {
                Debug.LogWarning($"[Gemini Cache] ❌ キャッシュの作成に失敗しました (Cache creation failed): {request.error}\nResponse: {request.downloadHandler.text}");
                ClearCache();
                return false;
            }
        }
    }

    public void ClearCache()
    {
        EditorPrefs.DeleteKey(PREF_CACHE_NAME);
        EditorPrefs.DeleteKey(PREF_CACHE_EXPIRE);
    }

    private string EscapeJsonString(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }
}