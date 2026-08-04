using UnityEngine;
using UnityEditor;

/// <summary>
/// 空間行列・トランスフォーム関連のコマンドを処理するハンドラー
/// (处理空间矩阵与变换相关命令的处理器)
/// </summary>
public class TransformCommandHandler : IUnityCommandHandler
{
    public string[] SupportedActionTypes => new[] { "MATH_TRANSFORM", "ALIGN_OBJECT" };

    public string Execute(DeveloperCommandData command)
    {
        if (command.actionType == "MATH_TRANSFORM") return ExecuteMathTransform(command);
        if (command.actionType == "ALIGN_OBJECT") return ExecuteAlignObject(command);
        return "⚠️ 未知のトランスフォームコマンド (Unknown transform command)";
    }

    private static string ExecuteMathTransform(DeveloperCommandData command)
    {
        GameObject targetObj = GeminiCommandUtils.ResolveTargetGameObject(command.targetObjectName, command.childPath);
        if (targetObj == null) return $"⚠️ 対象なし (Target not found): {command.targetObjectName}";

        Undo.RecordObject(targetObj.transform, "Math Transform");
        Transform t = targetObj.transform;
        bool isWorld = command.transformSpace == "World";

        if (command.isRelativeTransform)
        {
            // 相対変換 (Relative Matrix Transform)
            Vector3 deltaPos = command.position != null ? command.position.ToVector3() : Vector3.zero;
            Quaternion deltaRot = command.quaternionRotation != null ? command.quaternionRotation.ToQuaternion() :
                                 (command.rotation != null ? Quaternion.Euler(command.rotation.ToVector3()) : Quaternion.identity);

            if (isWorld)
            {
                t.position += deltaPos;
                t.rotation = deltaRot * t.rotation; // World space rotation accumulation
            }
            else
            {
                // ローカル座標系での行列計算 (Local Matrix multiplication)
                Matrix4x4 localDeltaMat = Matrix4x4.TRS(deltaPos, deltaRot, Vector3.one);
                Matrix4x4 currentLocalMat = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
                Matrix4x4 newLocalMat = currentLocalMat * localDeltaMat;

                t.localPosition = newLocalMat.GetColumn(3);
                t.localRotation = newLocalMat.rotation;
            }
        }
        else
        {
            // 絶対設定 (Absolute Assignment)
            if (command.position != null)
            {
                if (isWorld) t.position = command.position.ToVector3();
                else t.localPosition = command.position.ToVector3();
            }

            if (command.quaternionRotation != null)
            {
                if (isWorld) t.rotation = command.quaternionRotation.ToQuaternion();
                else t.localRotation = command.quaternionRotation.ToQuaternion();
            }
            else if (command.rotation != null)
            {
                if (isWorld) t.eulerAngles = command.rotation.ToVector3();
                else t.localEulerAngles = command.rotation.ToVector3();
            }

            if (command.scale != null) t.localScale = command.scale.ToVector3();
        }

        return $"📐 <b>空間変換 (Math Transform):</b> {targetObj.name} [Space: {command.transformSpace}, Relative: {command.isRelativeTransform}]";
    }

    private static string ExecuteAlignObject(DeveloperCommandData command)
    {
        GameObject sourceObj = GeminiCommandUtils.ResolveTargetGameObject(command.targetObjectName, command.childPath);
        GameObject targetObj = GeminiCommandUtils.ResolveTargetGameObject(command.alignTargetName);

        if (sourceObj == null || targetObj == null)
            return $"⚠️ 整列対象またはターゲットが見つかりません (Source or Target not found).";

        Renderer sourceRenderer = sourceObj.GetComponentInChildren<Renderer>();
        Renderer targetRenderer = targetObj.GetComponentInChildren<Renderer>();

        if (sourceRenderer == null || targetRenderer == null)
            return $"⚠️ バウンディングボックス取得失敗: 双方にRendererコンポーネントが必要です。";

        Undo.RecordObject(sourceObj.transform, "Align Object");

        Bounds targetBounds = targetRenderer.bounds;
        Bounds sourceBounds = sourceRenderer.bounds;
        Vector3 offset = Vector3.zero;

        // 指定された基準点 (Min, Center, Max) に基づいてオフセットを計算
        if (command.alignPoint == "Center") offset = targetBounds.center - sourceBounds.center;
        else if (command.alignPoint == "Min") offset = targetBounds.min - sourceBounds.min;
        else if (command.alignPoint == "Max") offset = targetBounds.max - sourceBounds.max;

        sourceObj.transform.position += offset;
        return $"🧲 <b>バウンディングボックス整列 (Align Bounding Box):</b> {sourceObj.name} ➔ {targetObj.name} [Point: {command.alignPoint}]";
    }
}