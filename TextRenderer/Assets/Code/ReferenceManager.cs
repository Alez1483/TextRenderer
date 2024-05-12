using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReferenceManager : MonoBehaviour
{
    public static ReferenceManager Instance;

    [SerializeField] 
    private Shader fontShader;

    public Shader FontShader
    { 
        get 
        { 
            return fontShader; 
        } 
    }

    void Awake()
    {
        Instance = this;
    }
}
