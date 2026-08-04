using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

/// <summary>
/// Unityのコンパイルパイプライン(CompilationPipeline)を監視し、C#コンパイルエラーを自動検知するクラス
/// (监听 Unity 编译管线，自动拦截 C# 编译错误日志并触发回调的组件)
/// </summary>
[InitializeOnLoad]
public static class GeminiCompileErrorWatcher
{
    public static event Action<string> OnCompileErrorDetected;
    public static event Action OnCompileSuccessDetected;

    private const string IS_MONITORING_KEY = "GeminiAgent_CompileWatcher_IsMonitoring";
    private const string MONITOR_START_TIME_KEY = "GeminiAgent_CompileWatcher_StartTime"; // 新規追加: タイムスタンプキー
    public static bool IsMonitoring
    {
        get
        {
            bool isMon = EditorPrefs.GetBool(IS_MONITORING_KEY, false);
            if (!isMon) return false;

            // --- 新規追加: デッドロック回避のためのタイムアウト(30秒)判定 ---
            // (新增：为了避免域重载失败导致的状态锁死，引入 30 秒超时强制解锁机制)
            string timeStr = EditorPrefs.GetString(MONITOR_START_TIME_KEY, "0");
            if (long.TryParse(timeStr, out long startTimeTicks))
            {
                if (DateTime.UtcNow.Ticks - startTimeTicks > TimeSpan.FromSeconds(30).Ticks)
                {
                    Debug.LogWarning("[Gemini Agent] コンパイル監視がタイムアウトしました。状態をリセットします。(Compile watcher timed out. Resetting state.)");
                    EditorPrefs.SetBool(IS_MONITORING_KEY, false);
                    return false;
                }
            }
            return true;
        }
        set
        {
            EditorPrefs.SetBool(IS_MONITORING_KEY, value);
            if (value)
            {
                // 監視開始時に現在のUTCティック数を記録 (开始监控时记录当前时间戳)
                EditorPrefs.SetString(MONITOR_START_TIME_KEY, DateTime.UtcNow.Ticks.ToString());
            }
        }
    }

    static GeminiCompileErrorWatcher()
    {
        // 既存のイベント登録を一旦解除し、重複登録を防止する (先解除已有的事件订阅，防止域重载导致的重复订阅)
        CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
    }

    private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
    {
        List<string> errorLogList = new List<string>();

        foreach (var msg in messages)
        {
            if (msg.type == CompilerMessageType.Error)
            {
                string formattedError = $"• {msg.file}({msg.line},{msg.column}): {msg.message}";
                errorLogList.Add(formattedError);
            }
        }

        if (errorLogList.Count > 0)
        {
            IsMonitoring = false;
            string combinedErrors = string.Join("\n", errorLogList);
            OnCompileErrorDetected?.Invoke(combinedErrors);
        }
        else if (IsMonitoring)
        {
            IsMonitoring = false;
            OnCompileSuccessDetected?.Invoke();
        }
    }
}