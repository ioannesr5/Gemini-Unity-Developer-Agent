using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// コードの変更内容をプレビューし、開発者が承認または破棄を選択できる差分確認ウィンドウ
/// (用于预览代码修改内容，允许开发者选择批准或放弃的差异确认窗口)
/// </summary>
public class GeminiDiffViewerWindow : EditorWindow
{
    private string targetFilePath;
    private string originalCode;
    private string modifiedCode;
    private Vector2 scrollPosOriginal;
    private Vector2 scrollPosModified;

    /// <summary>
    /// 差分確認ウィンドウを表示するメソッド
    /// (显示差异确认窗口的方法)
    /// </summary>
    public static void ShowWindow(string filePath, string oldCode, string newCode)
    {
        // 确保窗口具有焦点且不可忽视 (Ensure window gets focus)
        GeminiDiffViewerWindow window = GetWindow<GeminiDiffViewerWindow>("Code Diff Viewer", true);
        window.targetFilePath = filePath;
        window.originalCode = oldCode;
        window.modifiedCode = newCode;
        window.minSize = new Vector2(900, 600);
        window.ShowUtility();
    }

    private void OnGUI()
    {
        GUILayout.Label($"変更対象ファイル (Target File): {targetFilePath}", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        // 左側：元のコード (Left: Original Code)
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2f - 10f));
        GUILayout.Label("変更前 (Original)", EditorStyles.boldLabel);
        scrollPosOriginal = EditorGUILayout.BeginScrollView(scrollPosOriginal, "box");
        EditorGUILayout.TextArea(originalCode, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // 右側：変更後のコード (Right: Modified Code)
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2f - 10f));
        GUILayout.Label("変更後 (Modified)", EditorStyles.boldLabel);
        scrollPosModified = EditorGUILayout.BeginScrollView(scrollPosModified, "box");
        EditorGUILayout.TextArea(modifiedCode, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 承認と破棄ボタン (Approve and Reject Buttons)
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("破棄 (Reject)", GUILayout.Height(40)))
        {
            Debug.Log($"[Gemini Agent] 変更が破棄されました (Modification rejected): {targetFilePath}");
            Close();
        }

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("承認して適用 (Approve & Apply)", GUILayout.Height(40)))
        {
            ApplyChanges();
            Close();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 承認された変更をファイルに書き込み、バックアップを作成してAssetDatabaseを更新する
    /// (将批准的更改写入文件，创建备份并更新AssetDatabase)
    /// </summary>
    private void ApplyChanges()
    {
        try
        {
            // バックアップの作成 (Create Backup)
            string backupPath = targetFilePath + ".bak";
            File.WriteAllText(backupPath, originalCode, System.Text.Encoding.UTF8);

            // 新しいコードの書き込み (Write new code)
            File.WriteAllText(targetFilePath, modifiedCode, System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[Gemini Agent] ✅ コードが適用されました (Code applied): {targetFilePath} \n(Backup saved at: {backupPath})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Gemini Agent] ファイルの保存に失敗しました (Failed to save file): {ex.Message}");
        }
    }
}