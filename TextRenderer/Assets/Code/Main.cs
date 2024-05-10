using UnityEngine;
using System.IO;
using UnityEditor;

public class Main : MonoBehaviour
{
    public Gradient debugGradient;
    [SerializeField]
    private string fontName;
    private Font font;
    [Range(0, 500)]
    public int BezierCount = 0;
    [Range(0, 0.1f)]
    public float gizmoSize = 0.1f;

    [TextArea]
    public string text;

    void Start()
    {
        font = new Font(Path.Combine(Application.dataPath, "Fonts", fontName));
    }
    private void OnDrawGizmos()
    {
        if (font == null || font.glyphs == null)
        {
            return;
        }
        DrawString(text);
    }

    private void DrawGlyph(Glyph glyph, Vector2 offset)
    {
        if (gizmoSize > 0f)
        {
            for (int i = 0; i < glyph.SplineData.Length && i / 3 < BezierCount; i++)
            {
                Vector2 point = glyph.SplineData[i] + offset;
                float handleSize = HandleUtility.GetHandleSize(point);
                Gizmos.color = i % 3 == 1? Color.red : Color.green;
                Gizmos.DrawSphere(point, handleSize * gizmoSize);
            }
        }
        for (int i = 0, j = 0; i < glyph.SplineData.Length && j < BezierCount; i += 3, j++)
        {
            DrawQuadraticBezier(glyph.SplineData[i], glyph.SplineData[i + 1], glyph.SplineData[i + 2], offset, Color.cyan);
        }
    }
    private void DrawString(string s)
    {
        Vector2 penPoint = Vector2.zero;

        for (int i = 0; i < text.Length; i++)
        {
            Glyph g = font.glyphs[font.CharacterMapper.CharToGlyphIndex(text[i])];
            DrawGlyph(g, penPoint + new Vector2(g.LeftSideBearing - g.Min.x, 0f));
            penPoint.x += g.AdvanceWidth;
        }
    }
    private void DrawQuadraticBezier(Vector2 start, Vector2 control, Vector2 end, Vector2 offset, Color color)
    {
        start += offset;
        control += offset;
        end += offset;
        Handles.DrawBezier(start, end, Vector2.Lerp(start, control, 2f / 3f), Vector2.Lerp(end, control, 2f / 3f), color, Texture2D.whiteTexture, 1f);
    }
}
