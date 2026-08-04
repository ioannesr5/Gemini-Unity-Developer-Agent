using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

/// <summary>
/// C#スクリプトの作成、AST(抽象構文木)を用いた編集処理を担当するハンドラー
/// (负责创建 C# 脚本及利用 AST(抽象语法树) 进行代码修改的处理器)
/// </summary>
public class ScriptCommandHandler : IUnityCommandHandler
{
    private const string SCRIPT_SAVE_PATH = "Assets/Scripts/Generated/";

    public string[] SupportedActionTypes => new[] { "EDIT_SCRIPT", "CREATE_SCRIPT" };

    public string Execute(DeveloperCommandData command)
    {
        if (command.actionType == "EDIT_SCRIPT") return ExecuteEditScript(command);
        if (command.actionType == "CREATE_SCRIPT") return ExecuteCreateScript(command);
        return "⚠️ 未知のスクリプトコマンド";
    }

    private static string ExecuteCreateScript(DeveloperCommandData command)
    {
        if (string.IsNullOrEmpty(command.scriptClassName) || string.IsNullOrEmpty(command.scriptContent))
            return "⚠️ C# クラス名またはコード本文が不足しています。";

        if (!Directory.Exists(SCRIPT_SAVE_PATH)) Directory.CreateDirectory(SCRIPT_SAVE_PATH);

        string fullPath = Path.Combine(SCRIPT_SAVE_PATH, $"{command.scriptClassName}.cs");
        File.WriteAllText(fullPath, command.scriptContent, Encoding.UTF8);

        GameObject targetObj = GeminiCommandUtils.ResolveTargetGameObject(command.targetObjectName, command.childPath);
        if (targetObj != null)
        {
            EditorPrefs.SetString("GeminiAgent_PendingAttach_Obj", targetObj.name);
            EditorPrefs.SetString("GeminiAgent_PendingAttach_Class", command.scriptClassName);
        }

        AssetDatabase.Refresh();
        return $"📝 <b>C# スクリプト生成:</b> <color=cyan>{fullPath}</color>";
    }

    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        string targetObjName = EditorPrefs.GetString("GeminiAgent_PendingAttach_Obj", "");
        string className = EditorPrefs.GetString("GeminiAgent_PendingAttach_Class", "");

        if (!string.IsNullOrEmpty(targetObjName) && !string.IsNullOrEmpty(className))
        {
            EditorPrefs.DeleteKey("GeminiAgent_PendingAttach_Obj");
            EditorPrefs.DeleteKey("GeminiAgent_PendingAttach_Class");

            GameObject targetObj = GameObject.Find(targetObjName);
            if (targetObj != null)
            {
                Type scriptType = Type.GetType($"{className}, Assembly-CSharp");
                if (scriptType == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        scriptType = asm.GetType(className);
                        if (scriptType != null) break;
                    }
                }

                if (scriptType != null && targetObj.GetComponent(scriptType) == null)
                {
                    Undo.AddComponent(targetObj, scriptType);
                    Debug.Log($"[Gemini Agent] ✅ 自動アタッチ成功 (Auto-attach succeeded): '{targetObj.name}' ➔ '{className}'");
                }
            }
        }
    }

    private static string ExecuteEditScript(DeveloperCommandData command)
    {
        string filePath = !string.IsNullOrEmpty(command.targetFilePath) ? command.targetFilePath : command.sourceFilePath;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return $"⚠️ 改修対象のスクリプトファイルが存在しません: '{filePath}'";
        }

        string originalCode = File.ReadAllText(filePath, Encoding.UTF8);
        string modifiedCode = originalCode;
        string mode = string.IsNullOrEmpty(command.editMode) ? "FULL_REWRITE" : command.editMode.ToUpper();

        try
        {
            if (mode == "FULL_REWRITE")
            {
                if (string.IsNullOrEmpty(command.scriptContent)) return "⚠️ 全体リファクタリング用のコード本文 (scriptContent) が指定されていません。";
                modifiedCode = command.scriptContent;
            }
            else
            {
                Microsoft.CodeAnalysis.SyntaxTree tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(originalCode);
                Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

                if (mode == "APPEND_METHOD")
                {
                    var classDecl = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().FirstOrDefault();
                    if (classDecl != null)
                    {
                        var newMethod = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseMemberDeclaration(
                            $"\n    // --- Added by Gemini Agent [{DateTime.Now:HH:mm:ss}] ---\n{command.replacementCode}\n"
                        );

                        if (newMethod != null)
                        {
                            var newClassDecl = classDecl.AddMembers(newMethod);
                            root = root.ReplaceNode(classDecl, newClassDecl);
                            modifiedCode = root.NormalizeWhitespace().ToFullString();
                        }
                        else return "❌ 追加コードのASTパースに失敗しました。(追加代码的AST解析失败)";
                    }
                    else return $"❌ ファイル '{filePath}' 内にクラス宣言が見つかりませんでした。";
                }
                else if (mode == "REPLACE_SNIPPET")
                {
                    if (string.IsNullOrEmpty(command.searchPattern) || string.IsNullOrEmpty(command.replacementCode))
                        return "⚠️ 置換対象の検索パターンまたは置換コードが指定されていません。";

                    GeminiRoslynRewriter rewriter = new GeminiRoslynRewriter(command.searchPattern, command.replacementCode);
                    Microsoft.CodeAnalysis.SyntaxNode newRoot = rewriter.Visit(root);

                    // 🚨 修正: 失敗時に明確なエラーメッセージを返す (返回明确的解析错误以便 AI 进行修复)
                    if (rewriter.IsParseFailed)
                    {
                        return $"❌ 置換コードの構文が不正です。(Replacement code syntax is invalid.)\n提供されたコードが完全なメソッド構造を持っているか確認してください。";
                    }

                    if (rewriter.IsReplaced) modifiedCode = newRoot.NormalizeWhitespace().ToFullString();
                    else return $"⚠️ 検索パターン '{command.searchPattern}' に一致する構文ノードが見つかりませんでした。";
                }
            }
        }
        catch (Exception ex)
        {
            return $"❌ AST構文解析エラー (AST语法解析错误): {ex.Message}";
        }

        // Diff Viewer ウィンドウの呼び出し (呼叫差异查看器窗口)
        EditorApplication.delayCall += () =>
        {
            GeminiDiffViewerWindow.ShowWindow(filePath, originalCode, modifiedCode);
        };

        return $"⏳ <b>C#コード増量改修 [{mode}]:</b>\n<color=yellow>開発者の承認待ちです (Waiting for developer approval)...</color>\n対象: {filePath}";
    }

    public class GeminiRoslynRewriter : Microsoft.CodeAnalysis.CSharp.CSharpSyntaxRewriter
    {
        private readonly string _searchPattern;
        private readonly string _replacementCode;
        public bool IsReplaced { get; private set; }

        // 🚨 修正: パース失敗フラグを追加 (新增：解析失败标志)
        public bool IsParseFailed { get; private set; }

        public GeminiRoslynRewriter(string searchPattern, string replacementCode)
        {
            _searchPattern = searchPattern;
            _replacementCode = replacementCode;
            IsReplaced = false;
            IsParseFailed = false;
        }

        public override Microsoft.CodeAnalysis.SyntaxNode VisitMethodDeclaration(Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax node)
        {
            if (IsReplaced || IsParseFailed) return base.VisitMethodDeclaration(node);

            string nodeText = node.ToFullString();

            if (System.Text.RegularExpressions.Regex.IsMatch(nodeText, _searchPattern) || nodeText.Contains(_searchPattern))
            {
                var newMember = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseMemberDeclaration(_replacementCode);

                if (newMember != null)
                {
                    IsReplaced = true;
                    return newMember.WithLeadingTrivia(node.GetLeadingTrivia());
                }
                else
                {
                    // パースに失敗した場合はフラグを立ててログを出力
                    IsParseFailed = true;
                    Debug.LogWarning("[Gemini Agent] ASTメンバー直接解析に失敗しました。構文の破壊を防ぐため、置換操作を中断します。(AST parsing failed. Aborting to prevent syntax corruption.)");
                }
            }

            return base.VisitMethodDeclaration(node);
        }
    }
}