using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 各コマンドハンドラー(Strategy)が共通で利用するユーティリティクラス
/// (各个命令处理器共同使用的共享工具类，解决寻找目标和父节点的问题)
/// </summary>
public static class GeminiCommandUtils
{
    /// <summary>
    /// アクティブ・非アクティブを問わず、対象のGameObjectを解決する
    /// (解析目标游戏对象，支持查找未激活的节点)
    /// </summary>
    public static GameObject ResolveTargetGameObject(string objectName, string childPath = null)
    {
        GameObject rootObj = null;
        if (string.IsNullOrEmpty(objectName)) return null;

        if (objectName.Equals("SELECTED_OBJECT", StringComparison.OrdinalIgnoreCase) ||
            objectName.Equals("SELECTED", StringComparison.OrdinalIgnoreCase))
        {
            rootObj = Selection.activeGameObject;
        }
        else
        {
            // 非アクティブなオブジェクトも含めて検索 (检索所有对象，包含未激活)
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allObjects)
            {
                if (go.hideFlags == HideFlags.None && go.scene.IsValid() && go.name == objectName)
                {
                    rootObj = go;
                    break;
                }
            }
        }

        // 子オブジェクトのパスが指定されている場合は、Transformから検索 (深度寻址)
        if (rootObj != null && !string.IsNullOrEmpty(childPath))
        {
            Transform child = rootObj.transform.Find(childPath);
            return child != null ? child.gameObject : null;
        }
        return rootObj;
    }

    /// <summary>
    /// SELECTED_OBJECT キーワードを判定し、親 Transform を解体取得
    /// (解析 SELECTED_OBJECT 关键字，获取实际的父 Transform)
    /// </summary>
    public static Transform ResolveParentTransform(string parentName, Transform fallbackCanvasTransform = null)
    {
        if (string.IsNullOrEmpty(parentName)) return fallbackCanvasTransform;

        if (parentName.Equals("SELECTED_OBJECT", StringComparison.OrdinalIgnoreCase) ||
            parentName.Equals("SELECTED", StringComparison.OrdinalIgnoreCase))
            return Selection.activeTransform ?? fallbackCanvasTransform;

        GameObject parentObj = GameObject.Find(parentName);
        return parentObj != null ? parentObj.transform : fallbackCanvasTransform;
    }
}