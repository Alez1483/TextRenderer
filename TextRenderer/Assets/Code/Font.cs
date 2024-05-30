using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using UnityEditor;

[CreateAssetMenu(fileName = "Font", menuName = "Font Asset", order = 2)]
[PreferBinarySerialization]
public class Font : ScriptableObject
{
    private int _activeRenderers = 0;
    public int ActiveRenderers
    {
        get
        {
            return _activeRenderers;
        }
        set
        {
            if (value == 0) // font not used anymore
            {
                GlyphDataBuffer?.Release();
                GlyphLocaBuffer?.Release();
                Debug.Log("Release buffers for font " + name);
            }
            else if (_activeRenderers == 0 && value > 0) // some renderer started using the font
            {
                InitializeBuffers();
                Debug.Log("Initialized buffers for font " + name);
            }
            _activeRenderers = value;
        }
    }

    public UnityEngine.Object FontAsset;

    public GlyphMetrics[] glyphs;
    public CharacterMapper CharacterMapper;

    public Bezier[] glyphBezierData;
    public ComputeBuffer GlyphDataBuffer { get; private set; }

    public uint[] glyphLocaData;
    public ComputeBuffer GlyphLocaBuffer { get; private set; }

    public int Ascent;
    public int Descent;
    public int LineGap;

    private void InitializeBuffers()
    {
        GlyphDataBuffer?.Release(); //get rid of old buffers
        GlyphLocaBuffer?.Release();

        GlyphDataBuffer = new ComputeBuffer(glyphBezierData.Length, System.Runtime.InteropServices.Marshal.SizeOf<Bezier>(), ComputeBufferType.Structured);
        GlyphDataBuffer.SetData(glyphBezierData);

        GlyphLocaBuffer = new ComputeBuffer(glyphLocaData.Length, sizeof(uint));
        GlyphLocaBuffer.SetData(glyphLocaData);
    }
}