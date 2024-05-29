using UnityEngine;

[System.Serializable]
[PreferBinarySerialization]
public class Glyph
{
    [field: SerializeField] public Vector2[] SplineData { get; private set; }
    [field: SerializeField] public Vector2 Min { get; private set; }
    [field: SerializeField] public Vector2 Max { get; private set; }
    [field: SerializeField] public int AdvanceWidth { get; set; }
    [field: SerializeField] public int LeftSideBearing { get; set; }

    public Glyph(Vector2[] splineData, Vector2 min, Vector2 max)
    {
        SplineData = splineData;
        Min = min;
        Max = max;
    }
}
