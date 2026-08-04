using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 標準的なGameObjectおよびプレハブ、UIコンポーネントの操作を処理するハンドラー
/// (处理标准 GameObject 及预制体、UI 组件生成与修改的处理器)
/// </summary>
public class GameObjectCommandHandler : IUnityCommandHandler
{
    private const string MATERIAL_SAVE_PATH = "Assets/Materials/Generated/";

    public string[] SupportedActionTypes => new[] {
        "UNPACK_PREFAB", "CREATE_PREFAB_VARIANT", "SET_PROPERTY",
        "CREATE_UI_ELEMENT", "CREATE_MATERIAL", "INSTANTIATE_PREFAB",
        "DELETE_OBJECT", "CREATE_OBJECT", "MODIFY_TRANSFORM", "ADD_COMPONENT"
    };

    public string Execute(DeveloperCommandData command)
    {
        switch (command.actionType)
        {
            case "UNPACK_PREFAB": return ExecuteUnpackPrefab(command);
            case "CREATE_PREFAB_VARIANT": return ExecuteCreatePrefabVariant(command);
            case "SET_PROPERTY": return ExecuteSetSerializedProperty(command);
            case "CREATE_UI_ELEMENT": return ExecuteCreateUIElement(command);
            case "CREATE_MATERIAL": return ExecuteCreateMaterial(command);
            case "INSTANTIATE_PREFAB": return ExecuteInstantiatePrefab(command);
            case "DELETE_OBJECT": return ExecuteDeleteObject(command);
            case "CREATE_OBJECT":
            case "MODIFY_TRANSFORM":
            case "ADD_COMPONENT":
            default: return ExecuteStandardGameObjectOperation(command);
        }
    }

    private static string ExecuteUnpackPrefab(DeveloperCommandData command)
    {
        GameObject targetObj = GeminiCommandUtils.ResolveTargetGameObject(command.targetObjectName, command.childPath);
        if (targetObj == null) return $"⚠️ 対象なし (Target not found): {command.targetObjectName}";

        if (!PrefabUtility.IsAnyPrefabInstanceRoot(targetObj))
            return $"⚠️ 対象はプレハブルー卜ではありません (Target is not a prefab root): {targetObj.name}";

        PrefabUnpackMode mode = command.unpackMode == "Completely" ? PrefabUnpackMode.Completely : PrefabUnpackMode.OutermostRoot;

        Undo.RegisterFullObjectHierarchyUndo(targetObj, "Unpack Prefab");
        PrefabUtility.UnpackPrefabInstance(targetObj, mode, InteractionMode.AutomatedAction);

        return $"📦 <b>プレハブ解体 (Unpack Prefab):</b> {targetObj.name} [{mode}]";
    }

    private static string ExecuteCreatePrefabVariant(DeveloperCommandData command)
    {
        if (string.IsNullOrEmpty(command.prefabAssetPath) || string.IsNullOrEmpty(command.variantSavePath))
            return "⚠️ 元プレハブのパスまたはバリアント保存パスが不足しています。";

        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(command.prefabAssetPath);
        if (basePrefab == null) return $"⚠️ 元プレハブが見つかりません: {command.prefabAssetPath}";

        string targetDir = Path.GetDirectoryName(command.variantSavePath);
        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        GameObject variant = PrefabUtility.SaveAsPrefabAssetAndConnect(instance, command.variantSavePath, InteractionMode.AutomatedAction, out bool success);

        if (success)
        {
            Selection.activeGameObject = instance;
            return $"🧬 <b>バリアント作成 (Create Prefab Variant):</b> <color=cyan>{command.variantSavePath}</color>";
        }
        else
        {
            Undo.DestroyObjectImmediate(instance);
            return $"❌ バリアントの作成に失敗しました (Failed to create variant): {command.variantSavePath}";
        }
    }

    private static string ExecuteCreateUIElement(DeveloperCommandData command)
    {
        System.Text.StringBuilder log = new System.Text.StringBuilder();

        Canvas targetCanvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (targetCanvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            targetCanvas = canvasObj.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
            log.AppendLine("🖥️ Canvas 自動生成");
        }

        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystemObj, "Create EventSystem");
            log.AppendLine("⚡ EventSystem 自動生成");
        }

        Transform parentTransform = GeminiCommandUtils.ResolveParentTransform(command.parentName, targetCanvas.transform);
        Font defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf") ?? Font.CreateDynamicFontFromOSFont("Arial", 14);

        GameObject uiObj = new GameObject(string.IsNullOrEmpty(command.targetObjectName) ? "UI_Element" : command.targetObjectName);
        Undo.RegisterCreatedObjectUndo(uiObj, "Create UI Element");
        Undo.SetTransformParent(uiObj.transform, parentTransform, "Set Parent");

        RectTransform rectTransform = uiObj.AddComponent<RectTransform>();

        switch (command.uiElementType)
        {
            case "Panel":
                Image panelImage = uiObj.AddComponent<Image>();
                panelImage.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
                break;
            case "Button":
                uiObj.AddComponent<Image>();
                uiObj.AddComponent<Button>();
                GameObject btnTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
                Undo.SetTransformParent(btnTextObj.transform, uiObj.transform, "Set Text Parent");
                Text btnText = btnTextObj.GetComponent<Text>();
                btnText.font = defaultFont;
                btnText.text = string.IsNullOrEmpty(command.uiTextContent) ? "Button" : command.uiTextContent;
                btnText.alignment = TextAnchor.MiddleCenter;
                btnText.color = Color.black;
                RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
                btnTextRect.anchorMin = Vector2.zero;
                btnTextRect.anchorMax = Vector2.one;
                btnTextRect.sizeDelta = Vector2.zero;
                break;
            case "Text":
                Text textComp = uiObj.AddComponent<Text>();
                textComp.font = defaultFont;
                textComp.text = string.IsNullOrEmpty(command.uiTextContent) ? "New Text" : command.uiTextContent;
                textComp.color = Color.white;
                textComp.fontSize = 14;
                textComp.alignment = TextAnchor.MiddleLeft;
                break;
            case "Image": uiObj.AddComponent<Image>(); break;
            case "Slider": uiObj.AddComponent<Slider>(); break;
            case "Toggle": uiObj.AddComponent<Toggle>(); break;
        }

        if (command.rectTransform != null)
        {
            if (command.rectTransform.anchorMin != null) rectTransform.anchorMin = command.rectTransform.anchorMin.ToVector2();
            if (command.rectTransform.anchorMax != null) rectTransform.anchorMax = command.rectTransform.anchorMax.ToVector2();
            if (command.rectTransform.anchoredPosition != null) rectTransform.anchoredPosition = command.rectTransform.anchoredPosition.ToVector2();
            if (command.rectTransform.sizeDelta != null) rectTransform.sizeDelta = command.rectTransform.sizeDelta.ToVector2();
            if (command.rectTransform.pivot != null) rectTransform.pivot = command.rectTransform.pivot.ToVector2();
        }

        Selection.activeGameObject = uiObj;
        log.AppendLine($"🎨 UGUI要素 <b>'{uiObj.name}'</b> ({command.uiElementType}) 構築 (親: '{parentTransform.name}')");
        return log.ToString().TrimEnd();
    }

    private static string ExecuteCreateMaterial(DeveloperCommandData command)
    {
        if (string.IsNullOrEmpty(command.materialName)) command.materialName = "New_Material";
        if (!Directory.Exists(MATERIAL_SAVE_PATH)) Directory.CreateDirectory(MATERIAL_SAVE_PATH);

        string matPath = Path.Combine(MATERIAL_SAVE_PATH, $"{command.materialName}.mat");
        Material newMat = new Material(Shader.Find("Standard"));

        if (ColorUtility.TryParseHtmlString(command.materialColorHex, out Color parsedColor)) newMat.color = parsedColor;

        AssetDatabase.CreateAsset(newMat, matPath);
        AssetDatabase.SaveAssets();

        GameObject targetObj = GeminiCommandUtils.ResolveTargetGameObject(command.targetObjectName, command.childPath);
        if (targetObj != null && targetObj.TryGetComponent<Renderer>(out var renderer))
        {
            Undo.RecordObject(renderer, "Assign Material");
            renderer.sharedMaterial = newMat;
        }

        return $"🎨 <b>マテリアル作成:</b> {matPath}";
    }

    private static string ExecuteInstantiatePrefab(DeveloperCommandData command)
    {
        if (string.IsNullOrEmpty(command.prefabAssetPath)) return "⚠️ プレハブパスが指定されていません。";

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(command.prefabAssetPath);
        if (prefabAsset == null) return $"⚠️ プレハブが見つかりません: '{command.prefabAssetPath}'";

        GameObject spawnedObj = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
        Undo.RegisterCreatedObjectUndo(spawnedObj, "Instantiate Prefab");

        if (command.position != null) spawnedObj.transform.position = command.position.ToVector3();

        Transform parentTransform = GeminiCommandUtils.ResolveParentTransform(command.parentName);
        if (parentTransform != null) Undo.SetTransformParent(spawnedObj.transform, parentTransform, "Set Parent");

        Selection.activeGameObject = spawnedObj;
        return $"📦 <b>プレハブ生成:</b> '{prefabAsset.name}'";
    }

    private static string ExecuteSetSerializedProperty(DeveloperCommandData command)
    {
        GameObject targetObj = GeminiCommandUtils.ResolveTargetGameObject(command.targetObjectName, command.childPath);
        if (targetObj == null) return $"⚠️ 対象オブジェクト '{command.targetObjectName}' なし。";

        if (string.IsNullOrEmpty(command.propertyTargetComponent) || string.IsNullOrEmpty(command.propertyName))
            return "⚠️ コンポーネント名、またはプロパティ名が不足しています。";

        Component comp = targetObj.GetComponent(command.propertyTargetComponent);
        if (comp == null) return $"⚠️ コンポーネント '{command.propertyTargetComponent}' なし。";

        SerializedObject serializedComp = new SerializedObject(comp);
        SerializedProperty prop = serializedComp.FindProperty(command.propertyName);
        if (prop == null) return $"⚠️ プロパティ '{command.propertyName}' なし。";

        bool isParsed = false;
        try
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Float:
                    if (float.TryParse(command.propertyValueString, out float fVal)) { prop.floatValue = fVal; isParsed = true; }
                    break;
                case SerializedPropertyType.Integer:
                    if (int.TryParse(command.propertyValueString, out int iVal)) { prop.intValue = iVal; isParsed = true; }
                    break;
                case SerializedPropertyType.Boolean:
                    if (bool.TryParse(command.propertyValueString, out bool bVal)) { prop.boolValue = bVal; isParsed = true; }
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = command.propertyValueString; isParsed = true;
                    break;
                case SerializedPropertyType.Color:
                    if (ColorUtility.TryParseHtmlString(command.propertyValueString, out Color cVal)) { prop.colorValue = cVal; isParsed = true; }
                    break;
                case SerializedPropertyType.Vector2:
                    string[] v2 = command.propertyValueString.Split(',');
                    if (v2.Length == 2 && float.TryParse(v2[0], out float v2x) && float.TryParse(v2[1], out float v2y))
                    {
                        prop.vector2Value = new Vector2(v2x, v2y); isParsed = true;
                    }
                    break;
                case SerializedPropertyType.Vector3:
                    string[] v3 = command.propertyValueString.Split(',');
                    if (v3.Length >= 3 && float.TryParse(v3[0], out float v3x) && float.TryParse(v3[1], out float v3y) && float.TryParse(v3[2], out float v3z))
                    {
                        prop.vector3Value = new Vector3(v3x, v3y, v3z); isParsed = true;
                    }
                    break;
                case SerializedPropertyType.Enum:
                    if (int.TryParse(command.propertyValueString, out int eIdx))
                    {
                        prop.enumValueIndex = eIdx; isParsed = true;
                    }
                    else
                    {
                        int index = Array.IndexOf(prop.enumNames, command.propertyValueString);
                        if (index >= 0) { prop.enumValueIndex = index; isParsed = true; }
                    }
                    break;
                default:
                    return $"⚠️ プロパティ '{command.propertyName}' の型 ({prop.propertyType}) は現在サポートされていません。";
            }
        }
        catch (Exception ex)
        {
            return $"❌ プロパティ '{command.propertyName}' の解析エラー: {ex.Message}";
        }

        if (isParsed)
        {
            serializedComp.ApplyModifiedProperties();
            if (PrefabUtility.IsPartOfPrefabInstance(targetObj)) PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
            return $"⚙️ <b>{targetObj.name}.{command.propertyTargetComponent}.{command.propertyName}</b> ➔ '{command.propertyValueString}'";
        }

        return $"⚠️ プロパティ '{command.propertyName}' の値 '{command.propertyValueString}' を正しい型にパースできませんでした。";
    }

    private static string ExecuteDeleteObject(DeveloperCommandData command)
    {
        GameObject targetObj = GeminiCommandUtils.ResolveTargetGameObject(command.targetObjectName, command.childPath);
        if (targetObj != null)
        {
            string objName = targetObj.name;
            Undo.DestroyObjectImmediate(targetObj);
            return $"🗑️ <b>オブジェクト削除:</b> '{objName}'";
        }
        return $"⚠️ 削除対象 '{command.targetObjectName}' なし。";
    }

    private static string ExecuteStandardGameObjectOperation(DeveloperCommandData command)
    {
        System.Text.StringBuilder log = new System.Text.StringBuilder();
        GameObject targetObj = GeminiCommandUtils.ResolveTargetGameObject(command.targetObjectName, command.childPath);

        if (targetObj == null)
        {
            PrimitiveType pType = PrimitiveType.Cube;
            bool isPrimitive = Enum.TryParse(command.primitiveType, true, out pType) && command.primitiveType != "Empty";

            targetObj = isPrimitive ? GameObject.CreatePrimitive(pType) : new GameObject();
            targetObj.name = command.targetObjectName.Equals("SELECTED_OBJECT", StringComparison.OrdinalIgnoreCase) ? "NewObject" : command.targetObjectName;
            Undo.RegisterCreatedObjectUndo(targetObj, "Create GameObject");
            log.AppendLine($"✅ オブジェクト <b>'{targetObj.name}'</b> 作成");
        }

        Transform parentTransform = GeminiCommandUtils.ResolveParentTransform(command.parentName);
        if (parentTransform != null) Undo.SetTransformParent(targetObj.transform, parentTransform, "Set Parent");

        Undo.RecordObject(targetObj.transform, "Modify Transform");
        if (command.position != null) targetObj.transform.localPosition = command.position.ToVector3();
        if (command.rotation != null) targetObj.transform.localEulerAngles = command.rotation.ToVector3();
        if (command.scale != null && command.scale.ToVector3() != Vector3.zero) targetObj.transform.localScale = command.scale.ToVector3();

        if (!string.IsNullOrEmpty(command.addComponent))
        {
            Type componentType = System.Type.GetType($"UnityEngine.{command.addComponent}, UnityEngine") ?? System.Type.GetType(command.addComponent);
            if (componentType != null && targetObj.GetComponent(componentType) == null) Undo.AddComponent(targetObj, componentType);
        }

        Selection.activeGameObject = targetObj;
        return log.ToString().TrimEnd();
    }
}