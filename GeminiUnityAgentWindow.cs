using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ユーザーインターフェースの描画とユーザー操作の受付のみを担当する軽量化されたウィンドウ
/// </summary>
public class GeminiUnityAgentWindow : EditorWindow
{
    private GeminiModelRouter _modelRouter = new GeminiModelRouter();
    private GeminiCacheManager _cacheManager = new GeminiCacheManager();

    private string apiKey = "";
    private string proxyUrl = "";
    private string userPrompt = "";
    private string statusMessage = "準備完了";
    private bool isProcessing = false;

    private bool enableAutoRouting = true;
    private int selectedModelIndex = 0;

    private bool autoIncludeSelectionContext = true;
    private bool autoIncludeDirectoryContext = true;
    private bool autoIncludeSceneContext = true;
    private bool autoIncludeScriptContext = true;
    private string scriptScanFolderPath = "Assets/Scripts";

    private List<ChatLogItem> chatHistory = new List<ChatLogItem>();
    private Vector2 scrollPosition = Vector2.zero;
    private bool scrollToBottom = false;
    private const string CHAT_HISTORY_PREF_KEY = "GeminiAgent_ChatHistory";

    [MenuItem("Tools/Gemini Unity Agent (Refactored)")]
    public static void ShowWindow()
    {
        GeminiUnityAgentWindow window = GetWindow<GeminiUnityAgentWindow>("Gemini Agent");
        window.minSize = new Vector2(500, 750);
    }

    private void OnEnable()
    {
        apiKey = EditorPrefs.GetString("GeminiAgent_APIKey", "");
        proxyUrl = EditorPrefs.GetString("GeminiAgent_ProxyURL", "");
        enableAutoRouting = EditorPrefs.GetBool("GeminiAgent_AutoRouting", true);
        selectedModelIndex = EditorPrefs.GetInt("GeminiAgent_ModelIndex", 0);
        scriptScanFolderPath = EditorPrefs.GetString("GeminiAgent_ScanPath", "Assets/Scripts");

        LoadChatHistory();

        if (!string.IsNullOrEmpty(apiKey))
        {
            _ = UpdateModelListAsync();
        }
    }

    private async System.Threading.Tasks.Task UpdateModelListAsync()
    {
        isProcessing = true;
        statusMessage = "モデル一覧を取得中...";
        Repaint();

        bool success = await _modelRouter.FetchModelsAsync(apiKey, proxyUrl);
        statusMessage = success ? "モデル一覧の更新完了" : "モデル取得失敗";

        isProcessing = false;
        Repaint();
    }

    private void OnGUI()
    {
        GUILayout.Label("🤖 Gemini Developer Agent (Modular Architecture)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        apiKey = EditorGUILayout.PasswordField("Gemini API Key:", apiKey);
        if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString("GeminiAgent_APIKey", apiKey);

        EditorGUI.BeginChangeCheck();
        proxyUrl = EditorGUILayout.TextField("Proxy URL (任意):", proxyUrl);
        if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString("GeminiAgent_ProxyURL", proxyUrl);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("モデル設定 (Model Configuration):", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        enableAutoRouting = EditorGUILayout.Toggle("自動モデルルーティング", enableAutoRouting);
        if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool("GeminiAgent_AutoRouting", enableAutoRouting);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(enableAutoRouting || isProcessing || _modelRouter.ModelOptions.Count == 0);
        EditorGUI.BeginChangeCheck();
        if (selectedModelIndex >= _modelRouter.ModelOptions.Count) selectedModelIndex = 0;
        selectedModelIndex = EditorGUILayout.Popup("手動選択モデル:", selectedModelIndex, _modelRouter.ModelOptions.ToArray());
        if (EditorGUI.EndChangeCheck()) EditorPrefs.SetInt("GeminiAgent_ModelIndex", selectedModelIndex);
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("🔄 モデル更新", GUILayout.Width(90)))
        {
            _ = UpdateModelListAsync();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("プロジェクト文脈・解析設定:", EditorStyles.boldLabel);
        autoIncludeSelectionContext = EditorGUILayout.Toggle("現在選択中のオブジェクト情報を送信", autoIncludeSelectionContext);
        autoIncludeDirectoryContext = EditorGUILayout.Toggle("Assets フォルダ構造を送信", autoIncludeDirectoryContext);
        autoIncludeSceneContext = EditorGUILayout.Toggle("現在のUI/シーン構造を送信", autoIncludeSceneContext);
        autoIncludeScriptContext = EditorGUILayout.Toggle("既存C#スクリプト型情報を送信", autoIncludeScriptContext);

        if (autoIncludeScriptContext)
        {
            EditorGUI.BeginChangeCheck();
            scriptScanFolderPath = EditorGUILayout.TextField("解析対象フォルダ:", scriptScanFolderPath);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString("GeminiAgent_ScanPath", scriptScanFolderPath);
        }

        EditorGUILayout.Space();

        DrawCacheMonitorUI();
        DrawChatHistoryArea();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("新しい指示 (Prompt):");
        userPrompt = EditorGUILayout.TextArea(userPrompt, GUILayout.Height(50));

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(isProcessing || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(userPrompt.Trim()));

        if (GUILayout.Button("開発指示を送信 (Send Dev Command)", GUILayout.Height(30)))
        {
            string targetModel = enableAutoRouting
                ? _modelRouter.FindBestMatchingModel("pro", "flash", "gemini-3.5-flash")
                : (_modelRouter.ModelApiNames.Count > selectedModelIndex ? _modelRouter.ModelApiNames[selectedModelIndex] : "gemini-3.5-flash");

            SendAgentRequestAsync(userPrompt.Trim(), targetModel);
        }

        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("履歴クリア", GUILayout.Width(80), GUILayout.Height(30)))
        {
            chatHistory.Clear();
            SaveChatHistory();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        string selectedObjName = Selection.activeGameObject != null ? Selection.activeGameObject.name : "なし";
        EditorGUILayout.HelpBox($"ステータス: {statusMessage} | 選択中: {selectedObjName}", MessageType.Info);
    }

    private void DrawCacheMonitorUI()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("コンテキストキャッシュ監視 (Context Caching Monitor)", EditorStyles.boldLabel);

        bool isValid = _cacheManager.IsCacheValid;
        string statusIcon = isValid ? "🟢 有効 (Active / 命中)" : "🟡 フォールバック (Fallback / 降級)";
        EditorGUILayout.LabelField("ステータス (Status):", statusIcon);

        if (isValid)
        {
            EditorGUILayout.LabelField("キャッシュ識別子:", _cacheManager.CurrentCacheName);
            EditorGUILayout.LabelField("有効期限 (Expires):", _cacheManager.GetExpireTime().ToLocalTime().ToString("HH:mm:ss"));
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔄 キャッシュを手動更新 (Force Update Cache)"))
        {
            _cacheManager.ClearCache();
            statusMessage = "🔄 キャッシュを破棄しました。次回の送信時に再構築されます。";
        }
        if (GUILayout.Button("🗑️ キャッシュを破棄して降級 (Clear & Fallback)"))
        {
            _cacheManager.ClearCache();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private void DrawChatHistoryArea()
    {
        GUIStyle historyBoxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 10, 10) };
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, historyBoxStyle, GUILayout.Height(200));

        foreach (var item in chatHistory)
        {
            bool isUser = item.role == "user";
            GUIStyle bubbleStyle = new GUIStyle(GUI.skin.button) { wordWrap = true, alignment = TextAnchor.MiddleLeft, richText = true };

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(isUser ? $"👤 User [{item.timestamp}]" : $"🤖 Agent [{item.timestamp}]", EditorStyles.miniLabel);
            GUILayout.Box(item.displayText, bubbleStyle);

            if (item.isPendingExecution)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
                if (GUILayout.Button("破棄 (Reject)"))
                {
                    item.isPendingExecution = false;
                    item.isRejected = true;
                    item.displayText = "❌ コマンドは破棄されました";
                    SaveChatHistory();
                }

                GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
                if (GUILayout.Button("承認して実行 (Approve & Execute)"))
                {
                    OnExecuteApprovedCommand(item);
                    SaveChatHistory();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        if (scrollToBottom) { scrollPosition.y = float.MaxValue; scrollToBottom = false; }
        EditorGUILayout.EndScrollView();
    }

    private async void SendAgentRequestAsync(string prompt, string targetModelName)
    {
        isProcessing = true;
        statusMessage = $"{targetModelName} が処理中 (Processing)...";

        string timeNow = DateTime.Now.ToString("HH:mm:ss");
        chatHistory.Add(new ChatLogItem { role = "user", apiText = prompt, displayText = prompt, timestamp = timeNow });
        userPrompt = "";
        scrollToBottom = true;
        Repaint();

        StringBuilder contextBuilder = new StringBuilder();
        if (autoIncludeSelectionContext) contextBuilder.AppendLine(GeminiContextScanner.CaptureSelectionContext());
        if (autoIncludeDirectoryContext) contextBuilder.AppendLine(GeminiContextScanner.CaptureDirectoryStructure("Assets", 0, 3));
        if (autoIncludeSceneContext) contextBuilder.AppendLine(GeminiContextScanner.CaptureSceneContextJson());
        if (autoIncludeScriptContext) contextBuilder.AppendLine(GeminiContextScanner.CaptureProjectScriptsSummary(scriptScanFolderPath, prompt, Selection.activeGameObject));

        string fullContext = contextBuilder.ToString();

        string jsonPayload = $@"{{
            ""contents"": [
                {{""role"": ""user"", ""parts"": [{{""text"": ""{prompt}\n\n{fullContext}""}}]}}
            ]
        }}";

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{targetModelName}:generateContent?key={apiKey}";

        GeminiNetworkResult result = await GeminiNetworkService.SendPostRequestAsync(url, jsonPayload, proxyUrl);

        if (result.IsSuccess)
        {
            string trimmedCommand = result.ResponseText != null ? result.ResponseText.Trim() : "";
            bool isJsonCommand = trimmedCommand.StartsWith("{") || trimmedCommand.StartsWith("[");
            bool hasCommand = isJsonCommand && trimmedCommand != "{}" && trimmedCommand != "[]";

            string initialDisplay = hasCommand ? "⚠️ 実行待機中のコマンドがあります (Commands pending execution)" : trimmedCommand;

            chatHistory.Add(new ChatLogItem
            {
                role = "model",
                apiText = trimmedCommand,
                displayText = initialDisplay,
                timestamp = DateTime.Now.ToString("HH:mm:ss"),
                usedModel = targetModelName,
                isPendingExecution = hasCommand,
                pendingCommandJson = hasCommand ? trimmedCommand : ""
            });
            statusMessage = hasCommand ? "コマンド生成完了 (承認待ち)" : "テキスト応答を受信";
        }
        else
        {
            statusMessage = $"通信エラー (HTTP {result.StatusCode})";
            chatHistory.Add(new ChatLogItem { role = "model", displayText = $"❌ <b>エラー:</b>\n{result.ErrorMessage}", timestamp = DateTime.Now.ToString("HH:mm:ss") });
        }

        SaveChatHistory();
        isProcessing = false;
        scrollToBottom = true;
        Repaint();
    }

    private void OnExecuteApprovedCommand(ChatLogItem item)
    {
        item.isPendingExecution = false;
        string executionResult = GeminiCommandExecutorCore.ExecuteBatchCommands(item.pendingCommandJson);
        item.displayText = executionResult;
    }

    private void SaveChatHistory()
    {
        ChatHistoryWrapper wrapper = new ChatHistoryWrapper { history = this.chatHistory };
        EditorPrefs.SetString(CHAT_HISTORY_PREF_KEY, JsonUtility.ToJson(wrapper));
    }

    private void LoadChatHistory()
    {
        string json = EditorPrefs.GetString(CHAT_HISTORY_PREF_KEY, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                ChatHistoryWrapper wrapper = JsonUtility.FromJson<ChatHistoryWrapper>(json);
                if (wrapper != null && wrapper.history != null)
                {
                    chatHistory = wrapper.history;
                    scrollToBottom = true;
                }
            }
            catch { chatHistory = new List<ChatLogItem>(); }
        }
    }
}