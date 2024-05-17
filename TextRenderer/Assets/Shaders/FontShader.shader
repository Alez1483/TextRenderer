Shader "Unlit/FontShader"
{
    Properties
    {
        
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderType"="Transparent"}
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float2 uv : TEXCOORD0;
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                nointerpolation uint instanceID : TEXCOORD1; //might not work for large instance counts
            };

            struct Bezier
            {
                float2 start;
                float2 middle;
                float2 end;
            };

            struct Glyph
            {
                uint index;
                float2 pos;
                float2 size;
            };

            StructuredBuffer<Glyph> _TextBuffer;
            StructuredBuffer<Bezier> _GlyphDataBuffer;
            StructuredBuffer<uint> _GlyphLocaBuffer;

            v2f vert (appdata v, uint svInstanceID : SV_InstanceID)
            {
                v2f o;
                o.instanceID = svInstanceID;
                Glyph glyph = _TextBuffer[svInstanceID];
                float3 worldPos = float3(v.vertex.xy * glyph.size + glyph.pos, 0.0);
                o.vertex = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.uv = v.uv;
                return o;
            }

            float calculateBezierX(float x1, float x2, float x3, float t)
            {
                return lerp(lerp(x1, x2, t), lerp(x2, x3, t), t);
            }

            float4 frag (v2f i) : SV_Target
            {
                Glyph glyph = _TextBuffer[i.instanceID];
                uint startLoca = _GlyphLocaBuffer[glyph.index];
                uint bezierCount = (_GlyphLocaBuffer[glyph.index + 1] - startLoca);

                int windingNumber = 0;

                for (uint j = 0; j < bezierCount; j++)
                {
                    Bezier bezier = _GlyphDataBuffer[startLoca + j];

                    float y1 = bezier.start.y - i.uv.y;
                    float y2 = bezier.middle.y - i.uv.y;
                    float y3 = bezier.end.y - i.uv.y;

                    float a = y1 - 2 * y2 + y3;
                    float b = y1 - y2;
                    float c = y1;

                    float fraction = 0.0; //horizontal anti-aliasing

                    float discriminant = max(b * b - a * c, 0.0);

                    uint lookupIndex = (y1 < 0.0) + (y2 < 0.0) * 2 + (y3 < 0.0) * 4;

                    uint firstRootTable = 0x74;

                    if ((firstRootTable >> lookupIndex) & 1) //first root is eligible
                    {
                        float t;
                        if (abs(a) > 0.0001)
                        {
                            t = (b - sqrt(discriminant)) / a; //possible intersection
                        }
                        else
                        {
                            t =  c / (2 * b);
                        }
                        
                        if (t >= 0.0 && t < 1.0 && calculateBezierX(bezier.start.x, bezier.middle.x, bezier.end.x, t) >= i.uv.x)
                        {
                            windingNumber++;
                        }
                    }

                    uint secondRootTable = 0x2E;

                    if ((secondRootTable >> lookupIndex) & 1) //second root is eligible
                    {
                        float t;
                        if (abs(a) > 0.0001)
                        {
                            t = (b + sqrt(discriminant)) / a; //possible intersection
                        }
                        else
                        {
                            t =  c / (2 * b);
                        }

                        if (t >= 0.0 && t < 1.0 && calculateBezierX(bezier.start.x, bezier.middle.x, bezier.end.x, t) >= i.uv.x)
                        {
                           windingNumber--;
                        }
                    }
                }

                return float4(1, 1, 1, windingNumber != 0);
            }
            ENDCG
        }
    }
}
