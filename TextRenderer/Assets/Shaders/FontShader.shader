Shader "Unlit/FontShader"
{
    Properties
    {

    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            struct Glyph
            {
                uint index;
                float2 pos;
                float2 size;
            };

            StructuredBuffer<Glyph> _TextBuffer;
            StructuredBuffer<float2> _GlyphDataBuffer;
            StructuredBuffer<uint> _GlyphLocaBuffer;

            v2f vert (appdata v, uint svInstanceID : SV_InstanceID)
            {
                v2f o;
                Glyph glyph = _TextBuffer[svInstanceID];
                float3 worldPos = float3(v.vertex.xy * glyph.size + glyph.pos, 0.0);
                o.vertex = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.uv = v.uv;
                return o;
            }

            float dot2(float2 v) { return dot(v, v); }

            float sdBezier(float2 pos, float2 A, float2 B, float2 C)
            {
                float2 a = B - A;
                float2 b = A - 2.0 * B + C;
                float2 c = a * 2.0;
                float2 d = A - pos;
                float kk = 1.0 / dot(b, b);
                float kx = kk * dot(a, b);
                float ky = kk * (2.0 * dot(a, a) + dot(d, b)) / 3.0;
                float kz = kk * dot(d, a);
                float res = 0.0;
                float p = ky - kx * kx;
                float p3 = p * p * p;
                float q = kx * (2.0 * kx * kx - 3.0 * ky) + kz;
                float h = q * q + 4.0 * p3;
                if (h >= 0.0)
                {
                    h = sqrt(h);
                    float2 x = (float2(h, -h) - q) / 2.0;
                    float2 uv = sign(x) * pow(abs(x), float2(1.0 / 3.0, 1.0 / 3.0));
                    float t = clamp(uv.x + uv.y - kx, 0.0, 1.0);
                    res = dot2(d + (c + b * t) * t);
                }
                else
                {
                    float z = sqrt(-p);
                    float v = acos(q / (p * z * 2.0)) / 3.0;
                    float m = cos(v);
                    float n = sin(v) * 1.732050808;
                    float3  t = clamp(float3(m + m, -n - m, n - m) * z - kx, 0.0, 1.0);
                    res = min(dot2(d + (c + b * t.x) * t.x),
                        dot2(d + (c + b * t.y) * t.y));
                    // the third root cannot be the closest
                    // res = min(res,dot2(d+(c+b*t.z)*t.z));
                }
                return sqrt(res);
            }

            float4 frag (v2f i, uint svInstanceID : SV_InstanceID) : SV_Target
            {
                Glyph glyph = _TextBuffer[svInstanceID];
                uint startLoca = _GlyphLocaBuffer[glyph.index];
                uint curvePointCount = (_GlyphLocaBuffer[glyph.index + 1] - startLoca);

                float smallestDist = 1000;

                for (int j = 0; j < curvePointCount; j++)
                {
                    float dist = distance(i.uv, _GlyphDataBuffer[startLoca + j]);
                    //float dist = sdBezier(i.uv, _GlyphDataBuffer[startLoca + j], _GlyphDataBuffer[startLoca + j + 1], _GlyphDataBuffer[startLoca + j + 2]);
                    if (dist < smallestDist)
                    {
                        smallestDist = dist;
                    }
                }
                //return float4(i.uv.xy, 0.0, 1.0);
                return float4(smallestDist.xxx, 1);
            }
            ENDCG
        }
    }
}
