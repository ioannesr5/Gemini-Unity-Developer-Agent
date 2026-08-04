using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// AIから返却されたJSONコマンドを解析し、適切なハンドラーにルーティングするコアエンジン
/// (负责解析 AI 返回的 JSON 指令包，并将其路由给对应处理器的核心引擎)
/// </summary>
public static class GeminiCommandExecutorCore
{
    // 登録されたすべてのハンドラー (所有已注册的处理器)
    private static readonly List<IUnityCommandHandler> _handlers = new List<IUnityCommandHandler>
    {
        new TransformCommandHandler(),
        new FileSystemCommandHandler(),
        new ScriptCommandHandler(),
        new GameObjectCommandHandler()
    };

    /// <summary>
    /// 複数アクション(Action Array)のチェーン実行・結果ログ集約
    /// (支持多 Action 链式顺序执行并汇总日志)
    /// </summary>
    public static string ExecuteBatchCommands(string jsonText)
    {
        try
        {
            DeveloperCommandBatch batch = JsonUtility.FromJson<DeveloperCommandBatch>(jsonText);

            // 単一コマンドへのフォールバック (单命令降级兼容)
            if (batch == null || batch.actions == null || batch.actions.Count == 0)
            {
                DeveloperCommandData singleCmd = JsonUtility.FromJson<DeveloperCommandData>(jsonText);
                if (singleCmd != null && !string.IsNullOrEmpty(singleCmd.actionType))
                {
                    batch = new DeveloperCommandBatch();
                    batch.actions.Add(singleCmd);
                }
                else return "⚠️ コマンドデータを解析できませんでした。(JSON Format Mismatch)";
            }

            StringBuilder batchLog = new StringBuilder();
            batchLog.AppendLine($"⚡ <b>一括コマンド実行開始 (全 {batch.actions.Count} 件のアクション):</b>");

            for (int i = 0; i < batch.actions.Count; i++)
            {
                var cmd = batch.actions[i];
                // 適切なハンドラーを検索 (查找对应的处理器)
                var handler = _handlers.FirstOrDefault(h => h.SupportedActionTypes.Contains(cmd.actionType));

                if (handler != null)
                {
                    string resultMsg = handler.Execute(cmd);
                    batchLog.AppendLine($"<b>[{i + 1}/{batch.actions.Count}]</b> {resultMsg}");
                }
                else
                {
                    batchLog.AppendLine($"<b>[{i + 1}/{batch.actions.Count}]</b> ⚠️ 未対応のアクション (Unsupported action): {cmd.actionType}");
                }
            }
            return batchLog.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"❌ 操作実行例外 (Execution Exception): {ex.Message}";
        }
    }
}