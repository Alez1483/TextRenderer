using UnityEngine;
using System.IO;
using UnityEditor;

public class Main : MonoBehaviour
{
    [SerializeField]
    private string fontName;
    private Font roboto;
    public Gradient debugGradient;
    [Range(0, 500)]
    public int BezierCount = 0;
    [Range(0, 2000)]
    public int GlyphOffset = 0;
    [Range(0, 100)]
    public int ShowGlyphsCount = 0;
    public float PaddingTemporary;
    [Range(0, 1)]
    public float gizmoSize = 0.1f;
    void Start()
    {
        roboto = new Font(Path.Combine(Application.dataPath, "Fonts", fontName));
    }
    private void OnDrawGizmos()
    {
        if (roboto == null || roboto.glyphs == null)
        {
            return;
        }

        //Vector2 min = glyph.Min;
        //Vector2 max = glyph.Max;
        //Gizmos.DrawWireCube((min + max) * 0.5f, max - min);

        ShowGlyphsCount = Mathf.Min(ShowGlyphsCount, roboto.glyphs.Length);

        for(int i = 0; i < ShowGlyphsCount; i++)
        {
            DrawGlyph(roboto.glyphs[Mathf.Min(i + GlyphOffset, roboto.glyphs.Length - 1)], new Vector2(i * PaddingTemporary, 0f));
        }
    }

    private void DrawGlyph(Glyph glyph, Vector2 offset)
    {
        foreach (Vector2 point in glyph.SplineData)
        {
            Gizmos.DrawSphere(point + offset, Vector3.Distance(Camera.current.transform.position, point + offset) * gizmoSize);
        }
        for (int i = 0, j = 0; i < glyph.SplineData.Length && j < BezierCount; i += 3, j++)
        {
            //bezier here!!
            DrawQuadraticBezier(glyph.SplineData[i], glyph.SplineData[i + 1], glyph.SplineData[i + 2], offset);
            //Gizmos.DrawLine(glyph.SplineData[i] + offset, glyph.SplineData[i + 2] + offset);
        }
    }
    private void DrawQuadraticBezier(Vector2 start, Vector2 control, Vector2 end, Vector2 offset)
    {
        start += offset;
        control += offset;
        end += offset;
        Handles.DrawBezier(start, end, Vector2.Lerp(start, control, 2f / 3f), Vector2.Lerp(end, control, 2f / 3f), Color.cyan, Texture2D.whiteTexture, 1f);
    }
}
