using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Agent通信とコマンド実行に使用される構造化データ定義群(DTO)
/// (Agent 通信与命令执行所使用的结构化数据传输对象集合)
/// </summary>

[Serializable]
public class ChatLogItem
{
    public string role;
    public string apiText;
    public string displayText;
    public string timestamp;
    public string usedModel;

    // Human-in-the-Loop 用の保留状態 (用于 Human-in-the-Loop 的挂起状态)
    public bool isPendingExecution;
    public string pendingCommandJson;
    public bool isRejected;
}

[Serializable]
public class DeveloperCommandBatch
{
    public List<DeveloperCommandData> actions = new List<DeveloperCommandData>();
}

[Serializable]
public class DeveloperCommandData
{
    public string actionType;
    public string targetObjectName;
    public string parentName;
    public string primitiveType = "Cube";

    public Vector3Data position;
    public Vector3Data rotation;
    public Vector3Data scale;
    public string addComponent;

    public Vector4Data quaternionRotation;
    public string transformSpace;
    public bool isRelativeTransform;
    public string alignPoint;
    public string alignTargetName;

    public string directoryPath;
    public string searchFilter;
    public string sourceFilePath;
    public string targetFilePath;

    public string editMode;
    public string searchPattern;
    public string replacementCode;

    public string uiElementType;
    public string uiTextContent;
    public RectTransformData rectTransform;

    public string scriptClassName;
    public string scriptContent;
    public string materialName;
    public string materialColorHex;
    public string propertyTargetComponent;
    public string propertyName;
    public string propertyValueString;

    public string prefabAssetPath;
    public string childPath;
    public string unpackMode;
    public string variantSavePath;
}

[Serializable]
public class RectTransformData
{
    public Vector2Data anchorMin;
    public Vector2Data anchorMax;
    public Vector2Data anchoredPosition;
    public Vector2Data sizeDelta;
    public Vector2Data pivot;
}

[Serializable]
public class Vector2Data { public float x, y; public Vector2 ToVector2() => new Vector2(x, y); }

[Serializable]
public class Vector3Data { public float x, y, z; public Vector3 ToVector3() => new Vector3(x, y, z); }

[Serializable]
public class Vector4Data { public float x, y, z, w; public Quaternion ToQuaternion() => new Quaternion(x, y, z, w); }

[Serializable]
public class GeminiCacheResponse { public string name; public string expireTime; }

[Serializable]
public class GeminiRequestWithCache
{
    public string cachedContent;
    public GeminiRequestMessage[] contents;
}

#region Gemini API JSON Response Wrappers
[Serializable]
public class GeminiResponseWrapper { public Candidate[] candidates; }
[Serializable]
public class Candidate { public Content content; }
[Serializable]
public class Content { public Part[] parts; }
[Serializable]
public class Part
{
    public string text;
    public FunctionCallData functionCall;
}
[Serializable]
public class FunctionCallData
{
    public string name;
    public DeveloperCommandBatch args;
}

[Serializable]
public class GeminiRequestMessage
{
    public string role;
    public GeminiRequestPart[] parts;
}

[Serializable]
public class GeminiRequestPart { public string text; }

[Serializable]
public class ChatHistoryWrapper { public List<ChatLogItem> history = new List<ChatLogItem>(); }

// モデル一覧取得用のDTO (用于获取模型列表的数据结构)
[Serializable]
public class ModelListResponse { public List<ModelInfo> models; }

[Serializable]
public class ModelInfo
{
    public string name;
    public string displayName;
    public string description;
    public List<string> supportedGenerationMethods;
}
#endregion