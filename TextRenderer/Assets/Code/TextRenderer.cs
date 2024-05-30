using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;

public class TextRenderer : MonoBehaviour
{
    [SerializeField]
    private Font font;

    [TextArea]
    public string text;
    private string previousText = "";
    private int glyphCount;

    private ComputeBuffer textBuffer;
    private int bufferMaxSize = 1;

    private Material glyphMaterial;
    private Mesh quadMesh;
    private RenderParams renderParams;
    private GraphicsBuffer commandBuffer;
    private GraphicsBuffer.IndirectDrawIndexedArgs[] indirectDrawArgs;

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
        if (text == null || text.Length == 0)
        {
            return;
        }
        if (text != previousText)
        {
            previousText = text;

            if (text.Length > bufferMaxSize)
            {
                textBuffer?.Release();

                bufferMaxSize = Mathf.NextPowerOfTwo(text.Length);

                textBuffer = new ComputeBuffer(bufferMaxSize, System.Runtime.InteropServices.Marshal.SizeOf<GlyphData>());
                glyphMaterial.SetBuffer(Shader.PropertyToID("_TextBuffer"), textBuffer);
            }

            Vector2 penPoint = Vector2.zero;

            List<GlyphData> glyphData = new List<GlyphData>(text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    penPoint.x = 0;
                    penPoint.y -= font.LineGap + (font.Ascent - font.Descent);
                    continue;
                }
                int glyphIndex = font.CharacterMapper.CharToGlyphIndex(text[i]);
                GlyphMetrics g = font.glyphs[glyphIndex];

                Vector2 position = penPoint + new Vector2(g.LeftSideBearing, g.Min.y);
                Vector2 size = g.Max - g.Min;
                penPoint.x += g.AdvanceWidth;

                if (font.glyphLocaData[glyphIndex + 1] - font.glyphLocaData[glyphIndex] < 1)
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

        if (glyphCount > 0)
        {
            Graphics.RenderMeshIndirect(renderParams, quadMesh, commandBuffer);
        }
    }

    void OnEnable()
    {
        font.ActiveRenderers++; //initializes the buffers if not done yet

        glyphMaterial.SetBuffer(Shader.PropertyToID("_GlyphDataBuffer"), font.GlyphDataBuffer); //to make sure the buffers are not changed
        glyphMaterial.SetBuffer(Shader.PropertyToID("_GlyphLocaBuffer"), font.GlyphLocaBuffer);
    }
    void OnDisable()
    {
        font.ActiveRenderers--; //releases the buffers if not needed anymore
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
    public Vector2 position;
    public Vector2 size;

    public GlyphData(uint idx, Vector2 pos, Vector2 size)
    {
        glyphIndex = idx;
        position = pos;
        this.size = size;
    }
}