using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;

public class TextRenderer : MonoBehaviour
{
    [SerializeField]
    private Font _font;
    public Font Font
    {
        get { return _font; }
        set
        {
            _font = value;
            FontChanged();
        }
    }

    [SerializeField]
    private float _scale = 1f;
    public float Scale
    {
        get { return _scale; }
        set
        { 
            _scale = value;
            ScaleChanged();
        }
    }

    [SerializeField]
    private Color _color = Color.white;
    public Color Color
    {
        get { return _color; }
        set
        {
            _color = value;
            ColorChanged();
        }
    }

    public bool CenterHorizontally;
    private bool previousCentering;

    [TextArea(0, 40)]
    public string Text;
    private string previousText = "";
    private int glyphCount;

    private ComputeBuffer textBuffer;
    private int bufferMaxSize = 1;

    private Material glyphMaterial;
    private Mesh quadMesh;
    private RenderParams renderParams;
    private GraphicsBuffer commandBuffer;
    private GraphicsBuffer.IndirectDrawIndexedArgs[] indirectDrawArgs;

    private readonly int matrixId = Shader.PropertyToID("_ObjectToWorld");

    void Awake()
    {
        glyphMaterial = new Material(Shader.Find("Unlit/FontShader"));
        renderParams = new RenderParams(glyphMaterial);
        renderParams.worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e10f); //put something more reasonable later maybe?
        renderParams.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderParams.receiveShadows = false;
        renderParams.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderParams.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        
        quadMesh = new Mesh();
        quadMesh.vertices = new Vector3[] { Vector3.zero, Vector3.up, Vector2.one, Vector3.right };
        quadMesh.triangles = new int[] { 0, 1, 3, 1, 2, 3 };
        quadMesh.uv = new Vector2[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        quadMesh.normals = new Vector3[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };

        commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
        indirectDrawArgs = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        indirectDrawArgs[0].indexCountPerInstance = quadMesh.GetIndexCount(0);
    }

    void Update()
    {
        if (transform.hasChanged)
        {
            glyphMaterial.SetMatrix(matrixId, Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one)); //transform.localToWorldMatrix without scale
            transform.hasChanged = false;
        }

        if (Font == null)
        {
            Debug.LogError("No Font Asset is Attatched!");
            return;
        }
        if (Font.FontAsset == null)
        {
            Debug.LogError("Attatched Font Asset doesn't include a Font File!");
            return;
        }
        if (Text == null || Text.Length == 0)
        {
            return;
        }

        if (Text != previousText || CenterHorizontally != previousCentering)
        {
            UpdateTextBuffer();
        }

        if (glyphCount > 0)
        {
            Graphics.RenderMeshIndirect(renderParams, quadMesh, commandBuffer);
        }
    }

    private void UpdateTextBuffer()
    {
        previousText = Text;
        previousCentering = CenterHorizontally;

        if (Text.Length > bufferMaxSize)
        {
            textBuffer?.Release();

            bufferMaxSize = Mathf.NextPowerOfTwo(Text.Length);

            textBuffer = new ComputeBuffer(bufferMaxSize, System.Runtime.InteropServices.Marshal.SizeOf<GlyphData>());
            glyphMaterial.SetBuffer(Shader.PropertyToID("_TextBuffer"), textBuffer);
        }


        int rowCount = CountAmountOfChars(Text, '\n') + 1;
        int linespace = Font.LineGap + (Font.Ascent - Font.Descent);
        int textHeight = rowCount * linespace - Font.LineGap;

        Vector2 penPoint = new Vector2(0f, textHeight / 2.0f - Font.Ascent); // vertical centering

        penPoint.x = CalculatePenStartX(0);

        List<GlyphData> glyphData = new List<GlyphData>(Text.Length);

        for (int i = 0; i < Text.Length; i++)
        {
            if (Text[i] == '\n')
            {
                penPoint.x = CalculatePenStartX(i + 1);
                
                penPoint.y -= linespace;
                continue;
            }
            int glyphIndex = Font.CharacterMapper.CharToGlyphIndex(Text[i]);
            GlyphMetrics g = Font.glyphsMetrics[glyphIndex];

            Vector2 position = penPoint + new Vector2(g.LeftSideBearing, g.Min.y);
            Vector2 size = g.Max - g.Min;
            penPoint.x += g.AdvanceWidth;

            if (Font.glyphLocaData[glyphIndex + 1] - Font.glyphLocaData[glyphIndex] < 1)
            {
                continue;
            }

            glyphData.Add(new GlyphData((uint)glyphIndex, position, size));
        }
        textBuffer.SetData(glyphData);
        glyphCount = glyphData.Count;

        indirectDrawArgs[0].instanceCount = (uint)glyphCount;
        commandBuffer.SetData(indirectDrawArgs);
    }

    //Iterates over line of text and calculates where to put the pen position to produce right alignment mode
    private float CalculatePenStartX(int startIndex)
    {
        if (startIndex >= Text.Length)
        {
            return 0; //text ends at newline
        }
        if (CenterHorizontally)
        {
            float width = 0;

            int i;
            for (i = startIndex; i < Text.Length; i++)
            {
                char c = Text[i];
                if (c == '\n')
                {
                    break;
                }
                width += Font.glyphsMetrics[Font.CharacterMapper.CharToGlyphIndex(c)].AdvanceWidth;
            }

            GlyphMetrics firstGlyph = Font.glyphsMetrics[Font.CharacterMapper.CharToGlyphIndex(Text[startIndex])];
            width -= firstGlyph.LeftSideBearing; //remove first glyph left side bearing

            if (i <= 0) //empty line at the start of text, this avoids idx out of bounds exc
            {
                return 0;
            }

            GlyphMetrics lastGlyph = Font.glyphsMetrics[Font.CharacterMapper.CharToGlyphIndex(Text[i - 1])];
            width -= lastGlyph.AdvanceWidth - lastGlyph.LeftSideBearing - (lastGlyph.Max.x - lastGlyph.Min.x); //remove last glyph right side bearing

            return width / -2.0f - firstGlyph.LeftSideBearing;
        }
        else
        {
            return -Font.glyphsMetrics[Font.CharacterMapper.CharToGlyphIndex(Text[startIndex])].LeftSideBearing;
        }
    }

    private int CountAmountOfChars(string s, char c)
    {
        int count = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == c)
            {
                count++;
            }
        }
        return count;
    }

    public void FontChanged()
    {
        if (Font == null || Font.FontAsset == null || glyphMaterial == null)
        {
            return;
        }
        glyphMaterial.SetBuffer(Shader.PropertyToID("_GlyphDataBuffer"), Font.GlyphDataBuffer);
        glyphMaterial.SetBuffer(Shader.PropertyToID("_GlyphLocaBuffer"), Font.GlyphLocaBuffer);
    }
            
    public void ScaleChanged()
    {
        if (Font == null || Font.FontAsset == null || glyphMaterial == null)
        {
            return;
        }
        glyphMaterial.SetFloat("_Scale", Scale / Font.UnitsPerEm);
    }
    public void ColorChanged()
    {
        if (Font == null || Font.FontAsset == null || glyphMaterial == null)
        {
            return;
        }
        glyphMaterial.SetColor("_Color", _color);
    }

    void OnEnable()
    {
        if (Font == null)
        {
            Debug.LogError("No Font Asset is Attatched!");
            return;
        }
        if (Font.FontAsset == null)
        {
            Debug.LogError("Attatched Font Asset doesn't include a Font File!");
            return;
        }
        Font.ActiveRenderers++; //initializes the buffers if not done yet

        glyphMaterial.SetBuffer(Shader.PropertyToID("_GlyphDataBuffer"), Font.GlyphDataBuffer); //buffer data might have changed
        glyphMaterial.SetBuffer(Shader.PropertyToID("_GlyphLocaBuffer"), Font.GlyphLocaBuffer);
        ScaleChanged();

        ColorChanged();
    }
    void OnDisable()
    {
        if (Font == null)
        {
            Debug.LogError("No Font Asset is Attatched!");
            return;
        }
        if (Font.FontAsset == null)
        {
            Debug.LogError("Attatched Font Asset doesn't include a Font File!");
            return;
        }
        Font.ActiveRenderers--; //releases the buffers if not needed anymore
    }

    void OnDestroy()
    {
        textBuffer?.Release();
        commandBuffer.Release();
        Destroy(quadMesh);
        Destroy(glyphMaterial);
    }
}

internal struct GlyphData
{
    public uint glyphIndex;
    public Vector2 position; //position of the left bottom corner
    public Vector2 size;

    public GlyphData(uint idx, Vector2 pos, Vector2 size)
    {
        glyphIndex = idx;
        position = pos;
        this.size = size;
    }
}