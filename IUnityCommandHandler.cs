/// <summary>
/// 実行器(Executor)の各コマンド処理を抽象化するインターフェース
/// (抽象化执行器各个命令处理逻辑的接口)
/// </summary>
public interface IUnityCommandHandler
{
    /// <summary>
    /// このハンドラーが処理できるアクションタイプの配列 (该处理器支持的 ActionType 数组)
    /// </summary>
    string[] SupportedActionTypes { get; }

    /// <summary>
    /// コマンドを実行し、結果のログ文字列を返す (执行命令并返回结果日志)
    /// </summary>
    string Execute(DeveloperCommandData command);
}