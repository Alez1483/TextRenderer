using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TextRenderer))]
public class TextRendererEditor : Editor
{
    private SerializedProperty fontProperty;
    private SerializedProperty colorProperty;
    private SerializedProperty scaleProperty;
    private SerializedProperty textProperty;
    private SerializedProperty horizontalAlignmenProperty;

    //private Font oldFontValue;
    private Color oldColorValue;
    private float oldScaleValue;

    public override void OnInspectorGUI()
    {
        TextRenderer textRenderer = target as TextRenderer;

        Font oldFontValue = (Font)fontProperty.objectReferenceValue;

        serializedObject.Update();

        EditorGUILayout.PropertyField(fontProperty);
        EditorGUILayout.PropertyField(colorProperty);
        EditorGUILayout.PropertyField(scaleProperty);
        EditorGUILayout.PropertyField(horizontalAlignmenProperty);
        EditorGUILayout.PropertyField(textProperty);

        if (serializedObject.ApplyModifiedProperties())
        {
            Font newFontValue = (Font)fontProperty.objectReferenceValue;
            if (newFontValue != oldFontValue)
            {
                if (Application.isPlaying)
                {
                    if (newFontValue != null)
                    {
                        newFontValue.ActiveRenderers++;
                    }
                    if (oldFontValue != null)
                    {
                        oldFontValue.ActiveRenderers--;
                    }
                }
                textRenderer.FontChanged();

                oldFontValue = newFontValue;
            }
            if (colorProperty.colorValue != oldColorValue)
            {
                textRenderer.ColorChanged();
                oldColorValue = colorProperty.colorValue;
            }
            if (scaleProperty.floatValue != oldScaleValue)
            {
                textRenderer.ScaleChanged();
                oldScaleValue = scaleProperty.floatValue;
            }
        }
    }

    private void OnEnable()
    {
        fontProperty = serializedObject.FindProperty("_font");
        colorProperty = serializedObject.FindProperty("_color");
        scaleProperty = serializedObject.FindProperty("_scale");
        textProperty = serializedObject.FindProperty("Text");
        horizontalAlignmenProperty = serializedObject.FindProperty("CenterHorizontally");
    }
}
