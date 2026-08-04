using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ファイルシステムおよびディレクトリ操作を処理するハンドラー
/// (处理文件系统及目录操作的处理器)
/// </summary>
public class FileSystemCommandHandler : IUnityCommandHandler
{
    public string[] SupportedActionTypes => new[] { "EXPLORE_DIRECTORY", "SEARCH_ASSETS", "READ_FILE", "MOVE_ASSET", "MOVE_FILE", "CREATE_FOLDER" };

    public string Execute(DeveloperCommandData command)
    {
        switch (command.actionType)
        {
            case "EXPLORE_DIRECTORY": return ExecuteExploreDirectory(command);
            case "SEARCH_ASSETS": return ExecuteSearchAssets(command);
            case "READ_FILE": return ExecuteReadFile(command);
            case "MOVE_ASSET":
            case "MOVE_FILE": return ExecuteMoveFile(command);
            case "CREATE_FOLDER": return ExecuteCreateFolder(command);
            default: return "⚠️ 未知のファイルシステムコマンド";
        }
    }

    private static string ExecuteExploreDirectory(DeveloperCommandData command)
    {
        string targetPath = string.IsNullOrEmpty(command.directoryPath) ? "Assets" : command.directoryPath;
        if (!Directory.Exists(targetPath)) return $"⚠️ 指定フォルダが存在しません: '{targetPath}'";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"📂 <b>ディレクトリ探索結果 [{targetPath}]:</b>");

        foreach (string dir in Directory.GetDirectories(targetPath))
            sb.AppendLine($"  📁 {Path.GetFileName(dir)}/");

        foreach (string file in Directory.GetFiles(targetPath))
        {
            if (!file.EndsWith(".meta")) sb.AppendLine($"  📄 {Path.GetFileName(file)}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string ExecuteSearchAssets(DeveloperCommandData command)
    {
        if (string.IsNullOrEmpty(command.searchFilter)) return "⚠️ 検索フィルター (searchFilter) が指定されていません。";

        string[] guids = AssetDatabase.FindAssets(command.searchFilter);
        if (guids == null || guids.Length == 0) return $"🔍 フィルター '{command.searchFilter}' に一致するアセットなし。";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"🔍 <b>アセット検索結果 ('{command.searchFilter}'):</b>");

        int displayCount = Mathf.Min(guids.Length, 15);
        for (int i = 0; i < displayCount; i++)
        {
            sb.AppendLine($"  • <color=cyan>{AssetDatabase.GUIDToAssetPath(guids[i])}</color>");
        }

        return sb.ToString().TrimEnd();
    }

    private static string ExecuteReadFile(DeveloperCommandData command)
    {
        if (string.IsNullOrEmpty(command.sourceFilePath) || !File.Exists(command.sourceFilePath))
            return $"⚠️ ファイルが存在しません: '{command.sourceFilePath}'";

        try
        {
            string content = File.ReadAllText(command.sourceFilePath, Encoding.UTF8);
            return $"📖 <b>ファイル内容 [{command.sourceFilePath}]:</b>\n```\n{content}\n```";
        }
        catch (Exception ex)
        {
            return $"❌ 読み込み失敗 ({command.sourceFilePath}): {ex.Message}";
        }
    }

    private static string ExecuteMoveFile(DeveloperCommandData command)
    {
        if (string.IsNullOrEmpty(command.sourceFilePath) || string.IsNullOrEmpty(command.targetFilePath))
            return "⚠️ 移動元または移動先のパスが不足しています。";

        string targetDir = Path.GetDirectoryName(command.targetFilePath);
        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

        string errorMsg = AssetDatabase.MoveAsset(command.sourceFilePath, command.targetFilePath);
        if (string.IsNullOrEmpty(errorMsg))
        {
            AssetDatabase.Refresh();
            return $"📁 <b>スクリプト移動完了:</b> <color=cyan>{command.sourceFilePath}</color> ➔ <color=green>{command.targetFilePath}</color>";
        }
        return $"❌ 移動エラー ({command.sourceFilePath}): {errorMsg}";
    }

    private static string ExecuteCreateFolder(DeveloperCommandData command)
    {
        if (string.IsNullOrEmpty(command.targetFilePath))
            return "[CREATE_FOLDER] ❌ エラー: targetFilePath が未指定です。(未指定目标路径)";

        string pathSuffix = command.targetFilePath.StartsWith("Assets/") ? command.targetFilePath.Substring(7) : command.targetFilePath;
        string fullDirectoryPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, pathSuffix);

        if (!System.IO.Directory.Exists(fullDirectoryPath))
        {
            System.IO.Directory.CreateDirectory(fullDirectoryPath);
            UnityEditor.AssetDatabase.Refresh();
            return $"[CREATE_FOLDER] ✅ 成功: フォルダを作成しました ➔ {command.targetFilePath}";
        }
        else
        {
            return $"[CREATE_FOLDER] ⚠️ スキップ: フォルダは既に存在します (文件夹已存在) ➔ {command.targetFilePath}";
        }
    }
}