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

        if (serializedObject.ApplyModifiedProperties())
        {
            Debug.Log("Font Asset changed");
            Undo.RecordObject(font, "Font Asset Changed");
            FontReader.LoadFontAsset(font);
            Debug.Log("Sure did");
        }

        EditorGUILayout.LabelField(font.ActiveRenderers.ToString());
    }
}
