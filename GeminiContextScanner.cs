using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;

/// <summary>
/// Unityプロジェクトの文脈(フォルダ構造、C# API、シーン階層、選択オブジェクト)をスキャン・抽出するアナライザークラス
/// (负责扫描与提取 Unity 项目上下文信息：目录树、按需提取的 C# API 签名、场景层级树与当前选中物体的分析器)
/// </summary>
public static class GeminiContextScanner
{
    // ... [保留原有代码] CaptureSelectionContext, GetGameObjectPath, CaptureDirectoryStructure, CaptureSceneContextJson, DumpGameObjectHierarchy ...

    /// <summary>
    /// 現在ヒエラルキー上で選択されているGameObjectのコンテキスト情報(階層パス、コンポーネント、Transform)をキャプチャ
    /// (抓取当前在 Hierarchy 中选中的 GameObject 上下文信息：全路径、挂载组件、Transform/RectTransform)
    /// </summary>
    public static string CaptureSelectionContext()
    {
        GameObject activeObj = Selection.activeGameObject;
        if (activeObj == null)
        {
            return "[選択オブジェクト: なし (No Object Selected)]";
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"[選択中メインオブジェクト: '{activeObj.name}']");
        sb.AppendLine($"  ・階層フルパス (Hierarchy Path): {GetGameObjectPath(activeObj.transform)}");

        var components = activeObj.GetComponents<Component>();
        var compNames = new System.Collections.Generic.List<string>();
        foreach (var c in components)
        {
            if (c != null) compNames.Add(c.GetType().Name);
        }
        sb.AppendLine($"  ・アタッチ済みコンポーネント: [{string.Join(", ", compNames)}]");

        if (activeObj.TryGetComponent<RectTransform>(out var rectTransform))
        {
            sb.AppendLine($"  ・RectTransform -> pos: {rectTransform.anchoredPosition}, size: {rectTransform.sizeDelta}, anchorMin: {rectTransform.anchorMin}, anchorMax: {rectTransform.anchorMax}");
        }
        else
        {
            sb.AppendLine($"  ・Transform -> localPos: {activeObj.transform.localPosition}, localRot: {activeObj.transform.localEulerAngles}, localScale: {activeObj.transform.localScale}");
        }

        if (Selection.gameObjects.Length > 1)
        {
            sb.AppendLine($"  ・他選択オブジェクト数: {Selection.gameObjects.Length - 1} 件");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Transformからルートまでの階層フルパスを取得 (例: Canvas/MainPanel/ConfirmButton)
    /// (获取从 Root 到当前物体的完整层级路径)
    /// </summary>
    public static string GetGameObjectPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    /// <summary>
    /// Assetsフォルダ配下のディレクトリ構造(木構造)を再帰的にキャプチャ
    /// (递归抓取 Assets 目录下的树状文件夹与文件结构)
    /// </summary>
    public static string CaptureDirectoryStructure(string rootPath, int currentDepth = 0, int maxDepth = 3)
    {
        if (!Directory.Exists(rootPath)) return $"[Directory Not Found: {rootPath}]";

        StringBuilder sb = new StringBuilder();
        string indent = new string(' ', currentDepth * 2);

        try
        {
            string[] directories = Directory.GetDirectories(rootPath);
            foreach (string dir in directories)
            {
                string dirName = Path.GetFileName(dir);
                if (dirName.StartsWith(".") || dirName == "Library" || dirName == "Temp") continue;

                sb.AppendLine($"{indent}📁 {dirName}/");

                if (currentDepth < maxDepth)
                {
                    sb.Append(CaptureDirectoryStructure(dir, currentDepth + 1, maxDepth));
                }
            }

            string[] files = Directory.GetFiles(rootPath);
            foreach (string file in files)
            {
                if (file.EndsWith(".meta")) continue;
                string fileName = Path.GetFileName(file);
                sb.AppendLine($"{indent}  📄 {fileName}");
            }
        }
        catch (System.Exception ex)
        {
            sb.AppendLine($"{indent}[Error scanning {rootPath}: {ex.Message}]");
        }

        return sb.ToString();
    }

    /// <summary>
    /// アクティブシーンの全GameObject階層構造をキャプチャ
    /// (抓取当前激活 Scene 的全部 GameObject 节点与组件挂载结构)
    /// </summary>
    public static string CaptureSceneContextJson()
    {
        StringBuilder contextBuilder = new StringBuilder();
        var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        contextBuilder.AppendLine("Root GameObjects:");
        foreach (var root in rootObjects)
        {
            DumpGameObjectHierarchy(root.transform, contextBuilder, 1);
        }

        return contextBuilder.ToString();
    }

    private static void DumpGameObjectHierarchy(Transform current, StringBuilder builder, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 2);

        var components = current.GetComponents<Component>();
        var compNames = new System.Collections.Generic.List<string>();
        foreach (var c in components)
        {
            if (c != null) compNames.Add(c.GetType().Name);
        }
        string componentsStr = $" [{string.Join(", ", compNames)}]";

        builder.AppendLine($"{indent}- {current.name}{componentsStr}");

        if (indentLevel < 4)
        {
            foreach (Transform child in current)
            {
                DumpGameObjectHierarchy(child, builder, indentLevel + 1);
            }
        }
    }

    /// <summary>
    /// 指定フォルダ内のC#スクリプトを解析し、グローバルシンボル一覧と、文脈に関連するクラスの詳細APIをオンデマンド抽出する
    /// (利用 Roslyn 解析指定目录下的 C# 脚本。引入增量扫描与预过滤机制，避免在超大工程中引发内存与主线程卡顿)
    /// </summary>
    public static string CaptureProjectScriptsSummary(string folderPath, string userPrompt, GameObject activeObject)
    {
        if (!Directory.Exists(folderPath))
        {
            return $"[Scan Path Not Found: {folderPath}]";
        }

        // 1. 文脈キーワードの抽出 (提取上下文关键词，包括用户提示词的单词和选中物体的组件名)
        HashSet<string> contextKeywords = new HashSet<string>();
        if (!string.IsNullOrEmpty(userPrompt))
        {
            var words = Regex.Split(userPrompt, @"\W+").Where(w => w.Length > 2);
            foreach (var w in words) contextKeywords.Add(w);
        }

        if (activeObject != null)
        {
            foreach (var comp in activeObject.GetComponents<Component>())
            {
                if (comp != null) contextKeywords.Add(comp.GetType().Name);
            }
        }

        StringBuilder globalIndexBuilder = new StringBuilder();
        StringBuilder detailedApiBuilder = new StringBuilder();

        globalIndexBuilder.AppendLine("【Global Symbol Index (グローバルクラス一覧)】");
        detailedApiBuilder.AppendLine("【Relevant API Details (関連API詳細)】");

        string[] scriptFiles = Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories);

        foreach (string file in scriptFiles)
        {
            if (file.Contains("Editor") || file.Contains("Generated")) continue;

            // --- 新規追加: ファイルサイズ制限 (新增：跳过大于 500KB 的超大文件，防止内存溢出) ---
            if (new FileInfo(file).Length > 500 * 1024) continue;

            string relativePath = file.Replace("\\", "/");
            int assetsIndex = relativePath.IndexOf("Assets/");
            if (assetsIndex >= 0) relativePath = relativePath.Substring(assetsIndex);

            string codeText = File.ReadAllText(file);

            // --- 新規追加: 事前テキストマッチング (新增：纯文本预匹配，只有命中关键词才进行昂贵的 AST 解析) ---
            bool requiresDeepScan = contextKeywords.Count == 0 || contextKeywords.Any(k => codeText.Contains(k));

            // グローバルインデックス構築のための簡易正規表現マッチ (为全局索引进行快速正则匹配类名，不构建 AST)
            Match classMatch = Regex.Match(codeText, @"class\s+([A-Za-z0-9_]+)");
            if (classMatch.Success)
            {
                globalIndexBuilder.AppendLine($" - {classMatch.Groups[1].Value} ({relativePath})");
            }

            if (!requiresDeepScan) continue; // 関連性がなければAST解析をスキップ (无关文件直接跳过)

            // ここから下は関連ファイルのみASTを構築 (仅对强相关文件进行 Roslyn AST 解析)
            SyntaxTree tree = CSharpSyntaxTree.ParseText(codeText);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var classDecl in classDeclarations)
            {
                string className = classDecl.Identifier.Text;
                if (contextKeywords.Contains(className) || contextKeywords.Any(k => codeText.Contains(k)))
                {
                    detailedApiBuilder.AppendLine($"\n--- Script: {className} ({relativePath}) ---");
                    detailedApiBuilder.AppendLine($"  {classDecl.Modifiers} class {className}");

                    var fields = classDecl.DescendantNodes().OfType<FieldDeclarationSyntax>()
                        .Where(f => f.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword) || f.AttributeLists.ToString().Contains("SerializeField")));
                    foreach (var f in fields) detailedApiBuilder.AppendLine($"    Field: {f.Declaration}");

                    var methods = classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>()
                        .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)));
                    foreach (var m in methods)
                    {
                        detailedApiBuilder.AppendLine($"    Method: {m.Modifiers} {m.ReturnType} {m.Identifier}{m.ParameterList}");
                        var trivia = m.GetLeadingTrivia().Select(i => i.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().FirstOrDefault();
                        if (trivia != null)
                        {
                            string summary = trivia.Content.ToString().Replace("///", "").Replace("\n", " ").Trim();
                            detailedApiBuilder.AppendLine($"      Summary: {summary}");
                        }
                    }
                }
            }
        }
        return globalIndexBuilder.ToString() + "\n" + detailedApiBuilder.ToString();
    }
}