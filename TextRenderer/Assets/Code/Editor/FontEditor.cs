using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Font))]
public class FontEditor : Editor
{
    SerializedProperty fontAssetProperty;

    void OnEnable()
    {
        fontAssetProperty = serializedObject.FindProperty("FontAsset");
    }

    public override void OnInspectorGUI()
    {
        Font font = target as Font;

        serializedObject.Update();
        EditorGUILayout.PropertyField(fontAssetProperty);

        if (serializedObject.hasModifiedProperties)
        {
            Object asset = fontAssetProperty.objectReferenceValue;
            string path = AssetDatabase.GetAssetPath(asset);

            if (IsTtfFile(path))
            {
                serializedObject.ApplyModifiedProperties();
                Undo.RecordObject(font, "Font Asset Changed");
                FontReader.LoadFontAsset(font);
            }
            else
            {
                Debug.LogError("Given Asset is not a TrueType Font File");
            }
        }
    }

    private bool IsTtfFile(string path)
    {
        if (path == null)
        {
            return false;
        }
        return path.EndsWith(".ttf");
    }
}
