using UnityEngine;

[System.Serializable]
[PreferBinarySerialization]
public class GlyphMetrics
{
    [field: SerializeField] public Vector2 Min { get; private set; }
    [field: SerializeField] public Vector2 Max { get; private set; }
    [field: SerializeField] public int AdvanceWidth { get; set; }
    [field: SerializeField] public int LeftSideBearing { get; set; }

    public GlyphMetrics(Vector2 min, Vector2 max)
    {
        Min = min;
        Max = max;
    }
}
