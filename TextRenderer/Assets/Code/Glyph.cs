using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Glyph
{
    public char Character { get; set; }
    public Vector2[] SplineData { get; private set; }
    public Vector2 Min { get; private set; }
    public Vector2 Max { get; private set; }

    public Glyph(Vector2[] splineData, Vector2 min, Vector2 max)
    {
        SplineData = splineData;
        Min = min;
        Max = max;
    }

    public override int GetHashCode()
    {
        return Character;
    }
}
