using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 利用可能なモデルの取得、フィルタリング、およびタスク複雑度に基づく動的ルーティングを担当するクラス
/// (负责获取可用模型、过滤以及基于任务复杂度进行动态路由的类)
/// </summary>
public class GeminiModelRouter
{
    public List<string> ModelApiNames { get; private set; } = new List<string>();
    public List<string> ModelOptions { get; private set; } = new List<string>();

    /// <summary>
    /// 利用可能なモデル一覧を非同期で取得・フィルタリングする
    /// </summary>
    public async Task<bool> FetchModelsAsync(string apiKey, string proxyUrl)
    {
        if (string.IsNullOrEmpty(apiKey)) return false;

        string url = $"[https://generativelanguage.googleapis.com/v1beta/models?key=](https://generativelanguage.googleapis.com/v1beta/models?key=){apiKey}";

        GeminiNetworkResult result = await GeminiNetworkService.SendGetRequestAsync(url, proxyUrl);

        if (result.IsSuccess)
        {
            ParseModelsFromJson(result.ResponseText);
            return ModelApiNames.Count > 0;
        }
        else
        {
            SetFallbackModels();
            return false;
        }
    }

    private void ParseModelsFromJson(string json)
    {
        ModelApiNames.Clear();
        ModelOptions.Clear();

        try
        {
            ModelListResponse response = JsonUtility.FromJson<ModelListResponse>(json);
            if (response != null && response.models != null)
            {
                // 特定ドメインモデルやプレビュー版を除外 (排除特定领域模型和预览版)
                string[] excludedKeywords = new string[]
                {
                    "embedding", "aqa", "tts", "image", "vision",
                    "robotics", "computer-use", "customtools",
                    "preview", "latest", "experimental"
                };

                System.Text.RegularExpressions.Regex iterationRegex = new System.Text.RegularExpressions.Regex(@"-\d{3}$");

                foreach (var model in response.models)
                {
                    bool supportsGenerate = false;
                    if (model.supportedGenerationMethods != null)
                    {
                        foreach (var method in model.supportedGenerationMethods)
                        {
                            if (method == "generateContent") { supportsGenerate = true; break; }
                        }
                    }

                    if (supportsGenerate && model.name.StartsWith("models/gemini"))
                    {
                        string cleanName = model.name.Replace("models/", "");
                        string lowerName = cleanName.ToLower();
                        bool isExcluded = false;

                        foreach (var keyword in excludedKeywords)
                        {
                            if (lowerName.Contains(keyword)) { isExcluded = true; break; }
                        }

                        if (!isExcluded && iterationRegex.IsMatch(lowerName)) isExcluded = true;

                        if (!isExcluded)
                        {
                            ModelApiNames.Add(cleanName);
                            ModelOptions.Add($"{cleanName} ({model.displayName})");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Gemini Router] JSONパース失敗 (JSON parsing failed): {ex.Message}");
        }

        if (ModelApiNames.Count == 0) SetFallbackModels();
    }

    private void SetFallbackModels()
    {
        ModelApiNames = new List<string> { "gemini-3.5-flash", "gemini-3.1-pro", "gemini-3.1-flash-lite", "gemini-2.5-pro" };
        ModelOptions = new List<string>
        {
            "gemini-3.5-flash (Fallback)",
            "gemini-3.1-pro (Fallback)",
            "gemini-3.1-flash-lite (Fallback)",
            "gemini-2.5-pro (Fallback)"
        };
    }

    /// <summary>
    /// パターンマッチングにより最適なモデルを検索する
    /// </summary>
    public string FindBestMatchingModel(string primaryKeyword, string secondaryKeyword, string fallback)
    {
        foreach (var name in ModelApiNames)
            if (name.ToLower().Contains(primaryKeyword.ToLower())) return name;

        if (!string.IsNullOrEmpty(secondaryKeyword))
            foreach (var name in ModelApiNames)
                if (name.ToLower().Contains(secondaryKeyword.ToLower())) return name;

        return ModelApiNames.Count > 0 ? ModelApiNames[0] : fallback;
    }
}