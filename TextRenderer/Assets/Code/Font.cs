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

    public Glyph[] glyphs;
    public CharacterMapper CharacterMapper;

    public ComputeBuffer GlyphDataBuffer { get; private set; }
    public ComputeBuffer GlyphLocaBuffer { get; private set; }

    public int Ascent;
    public int Descent;
    public int LineGap;

    private void InitializeBuffers()
    {
        GlyphDataBuffer?.Release(); //get rid of old buffers
        GlyphLocaBuffer?.Release();

        List<Bezier> bezierData = new List<Bezier>();
        uint[] locations = new uint[glyphs.Length + 1]; //extra location at the end to calculate the count of beziers in glyph

        //first location not set because it's 0 by default anyway

        for (int glyphIndex = 0; glyphIndex < glyphs.Length; glyphIndex++)
        {
            Glyph glyph = glyphs[glyphIndex];

            Vector2 min = glyph.Min;
            Vector2 max = glyph.Max;
            Vector2 size = max - min;

            var splineData = glyph.SplineData;

            for (int i = 0; i < splineData.Length; i+=3)
            {
                Bezier bezier;
                bezier.start = (splineData[i] - min) / size; //from 0 to 1 values
                bezier.middle = (splineData[i + 1] - min) / size;
                bezier.end = (splineData[i + 2] - min) / size;

                bezierData.Add(bezier);
            }

            locations[glyphIndex + 1] = (uint)bezierData.Count;
        }

        GlyphDataBuffer = new ComputeBuffer(bezierData.Count, System.Runtime.InteropServices.Marshal.SizeOf<Bezier>(), ComputeBufferType.Structured);
        GlyphDataBuffer.SetData(bezierData);

        GlyphLocaBuffer = new ComputeBuffer(locations.Length, sizeof(uint));
        GlyphLocaBuffer.SetData(locations);
    }
}