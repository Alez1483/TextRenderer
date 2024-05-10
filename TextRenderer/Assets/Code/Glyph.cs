using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Glyph
{
    public Vector2[] SplineData { get; private set; }
    public Vector2 Min { get; private set; }
    public Vector2 Max { get; private set; }
    public int AdvanceWidth { get; set; }
    public int LeftSideBearing { get; set; }

    public Glyph(Vector2[] splineData, Vector2 min, Vector2 max)
    {
        SplineData = splineData;
        Min = min;
        Max = max;
    }
}
