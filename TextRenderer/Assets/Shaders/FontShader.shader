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

            float evaluateGlyph(float2 uv, float pixelsPerUVX, uint startLoca, uint bezierCount)
            {
                float fraction = 0.0; //enables horizontal anti-aliasing

                for (uint j = 0; j < bezierCount; j++)
                {
                    Bezier bezier = _GlyphDataBuffer[startLoca + j];

                    float y1 = bezier.start.y - uv.y;
                    float y2 = bezier.middle.y - uv.y;
                    float y3 = bezier.end.y - uv.y;

                    float a = y1 - 2 * y2 + y3;
                    float b = y1 - y2;
                    float c = y1;

                    #define EPSILON 0.0001

                    float discriminant = max(b * b - a * c, 0.0);

                    uint lookupIndex = (y1 < 0.0) + (y2 < 0.0) * 2 + (y3 < 0.0) * 4;

                    uint firstRootTable = 0x74;

                    if ((firstRootTable >> lookupIndex) & 1) //first root is eligible
                    {
                        float t;
                        if (abs(a) > EPSILON)
                        {
                            t = (b - sqrt(discriminant)) / a; //possible intersection
                        }
                        else
                        {
                            t = c / (2 * b);
                        }
                        
                        if (t >= 0.0 && t < 1.0)
                        {
                            fraction += saturate((calculateBezierX(bezier.start.x, bezier.middle.x, bezier.end.x, t) - uv.x) * pixelsPerUVX + 0.5);
                        }
                    }

                    uint secondRootTable = 0x2E;

                    if ((secondRootTable >> lookupIndex) & 1) //second root is eligible
                    {
                        float t;
                        if (abs(a) > EPSILON)
                        {
                            t = (b + sqrt(discriminant)) / a; //possible intersection
                        }
                        else
                        {
                            t = c / (2 * b);
                        }

                        if (t >= 0.0 && t < 1.0)
                        {
                            fraction -= saturate((calculateBezierX(bezier.start.x, bezier.middle.x, bezier.end.x, t) - uv.x) * pixelsPerUVX + 0.5);
                        }
                    }
                }

                return saturate(fraction); //saturated to prevent leaking artefacts
            }

            float4 frag (v2f i) : SV_Target
            {
                //amount of change in uv coordinates per one pixel
                float2 uvChangePerPixel = fwidth(i.uv); //sqrt(ddx()^2+ddy()^2) would be accurate but this works well enough
                float2 pixelsPerUV = 1.0 / uvChangePerPixel; //amount of pixels that can fit in the uv range (0 to 1)

                uint glyphIndex = _TextBuffer[i.instanceID].index;

                uint startLoca = _GlyphLocaBuffer[glyphIndex];
                uint bezierCount = (_GlyphLocaBuffer[glyphIndex + 1] - startLoca);

                float sum = 0.0;

                #define VERTICAL_SAMPLES 3

                //e.g. VERTICAL_SAMPLES == 3 -> offset = -1/3, 0/3, 1/3    VERTICAL_SAMPLES == 4 -> offset = -1.5/4, -0.5/4, 0.5/4, 1.5/4
                float offsetDelta = (1.0 / VERTICAL_SAMPLES) * uvChangePerPixel.y;
                float offset = ((VERTICAL_SAMPLES - 1) / -2.0) * offsetDelta;

                for (int j = 0; j < VERTICAL_SAMPLES; j++)
                {
                    sum += evaluateGlyph(i.uv + float2(0.0, offset), pixelsPerUV.x, startLoca, bezierCount);
                    offset += offsetDelta;
                }

                return float4(1, 1, 1, sum / VERTICAL_SAMPLES);
            }
            ENDCG
        }
    }
}
