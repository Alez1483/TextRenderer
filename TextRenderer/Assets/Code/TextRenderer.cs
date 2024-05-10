using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    [SerializeField]
    private string fontPath;
    private Font font;

    ComputeBuffer normalizedData;
    ComputeBuffer locationBuffer;


    [TextArea]
    public string text;

    void OnDisable()
    {
        normalizedData?.Release();
        locationBuffer?.Release();
    }

    void Start()
    {
        font = new Font(Path.Combine(Application.dataPath, "Fonts", fontPath));

        var glyphs = font.glyphs;

        List<Vector2> normalized = new List<Vector2>();
        int[] locations = new int[glyphs.Length + 1];
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
