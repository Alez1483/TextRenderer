using UnityEngine;

[System.Serializable]
public struct Bezier
{
    public Vector2 start;
    public Vector2 middle;
    public Vector2 end;

    public Bezier(Vector2 s, Vector2 m, Vector2 e)
    {
        start = s;
        middle = m;
        end = e;
    }
}