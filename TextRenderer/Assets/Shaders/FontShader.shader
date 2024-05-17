Shader "Unlit/FontShader"
{
    Properties
    {
        //by default half a pixel wide padding around the glyph to avoid aliasing around the edges
        [HideInInspector]_PaddingPixels("hidden", Float) = 0.5
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
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                nointerpolation uint instanceID : TEXCOORD1; //might not work for large instance counts though nointerpolation could help
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
            float _PaddingPixels;

            v2f vert (appdata v, uint svInstanceID : SV_InstanceID)
            {
                v2f o;
                o.instanceID = svInstanceID;
                Glyph glyph = _TextBuffer[svInstanceID];

                float padding;

                if (unity_OrthoParams.w > 0.5) //orthogrpahic
                {
                    padding = unity_OrthoParams.y / _ScreenParams.y * _PaddingPixels * 2.0; // size / screen height
                }
                else //perspective
                {
                    //works because math: eye depth / _m11 / screen height. derived from the fact that fov = atan(1.0 / _m11) * 2
                    padding = max(-mul(UNITY_MATRIX_V, float4(glyph.pos + 0.5 * glyph.size, 0.0, 1.0)).z / unity_CameraProjection._m11 / _ScreenParams.y * _PaddingPixels * 2.0, 0.0);
                }

                float2 scaledCoord = v.vertex.xy * (glyph.size + 2 * padding) - padding;
                float3 worldPos = float3(scaledCoord + glyph.pos, 0.0); //add padding around the mesh (note! only works for quad meshes)
                o.uv = scaledCoord / glyph.size; //object space position is identical to uv coordinate
                o.vertex = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                return o;
            }

            float calculateBezierX(float x1, float x2, float x3, float t) //works for Y as well
            {
                return lerp(lerp(x1, x2, t), lerp(x2, x3, t), t); //could be potentially optimized
            }

            float evaluateGlyph(float2 uv, float pixelsPerUVX, uint startLoca, uint bezierCount)
            {
                float fraction = 0.0; //fractional winding number, enables horizontal anti-aliasing almost for free

                for (uint j = 0; j < bezierCount; j++)
                {
                    Bezier bezier = _GlyphDataBuffer[startLoca + j];

                    //position in question translated to origin
                    float2 p1 = bezier.start - uv;
                    float2 p2 = bezier.middle - uv;
                    float2 p3 = bezier.end - uv;

                    //multipliers of quadratic equation
                    float a = p1.y - 2 * p2.y + p3.y;
                    float b = p1.y - p2.y; //(should be -2(b) but the formula can be simplified this way)
                    float c = p1.y;

                    #define EPSILON 0.0001

                    float discriminant = max(b * b - a * c, 0.0);

                    uint lookupIndex = (p1.y < 0.0) + (p2.y < 0.0) * 2 + (p3.y < 0.0) * 4;

                    uint firstRootTable = 0x74;

                    if ((firstRootTable >> lookupIndex) & 1) //first root is eligible
                    {
                        //simplified from quadratic formula (variable b above is multiplied by -2 from what it should be)
                        //set to c / (2 * b) when straight line (otherwise division by zero occurs). works only when p2 is in the middle of p1 and p3
                        float t = abs(a) > EPSILON? (b - sqrt(discriminant)) / a : c / (2 * b); //possible intersection
                        
                        if (t >= 0.0 && t < 1.0)
                        {
                            fraction += saturate((calculateBezierX(p1.x, p2.x, p3.x, t)) * pixelsPerUVX + 0.5);
                        }
                    }

                    uint secondRootTable = 0x2E;

                    if ((secondRootTable >> lookupIndex) & 1) //second root is eligible
                    {
                        float t = abs(a) > EPSILON? (b + sqrt(discriminant)) / a : c / (2 * b); //possible intersection

                        if (t >= 0.0 && t < 1.0)
                        {
                            fraction -= saturate((calculateBezierX(p1.x, p2.x, p3.x, t)) * pixelsPerUVX + 0.5);
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
