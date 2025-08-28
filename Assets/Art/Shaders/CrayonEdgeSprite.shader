Shader "Unlit/Crayon Edge Sprite"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Color   ("Tint", Color) = (1,1,1,1)

        // Edge look
        _EdgeColor   ("Edge Color", Color) = (0,0,0,1)
        _EdgeThickness ("Edge Thickness (px)", Range(0.5, 8)) = 2
        _EdgeIntensity ("Edge Intensity", Range(0.5, 4)) = 1.5
        _EdgeThreshold ("Edge Threshold", Range(0, 1)) = 0.2

        // Hand-drawn roughness
        _NoiseTex    ("Noise (grayscale)", 2D) = "gray" {}
        _NoiseScale  ("Noise Scale", Range(1, 30)) = 8
        _JitterSpeed ("Jitter Speed", Range(0, 5)) = 0.7
        _StrokeDensity ("Stroke Density", Range(0.5, 6)) = 3.0
        _StrokeBreakup ("Stroke Breakup", Range(0, 1)) = 0.55

        // Alpha handling
        _Cutoff ("Alpha Clip", Range(0,1)) = 0.001
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Color;

            float4 _EdgeColor;
            float  _EdgeThickness;
            float  _EdgeIntensity;
            float  _EdgeThreshold;

            sampler2D _NoiseTex;
            float4 _NoiseTex_TexelSize;
            float  _NoiseScale;
            float  _JitterSpeed;
            float  _StrokeDensity;
            float  _StrokeBreakup;

            float  _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            // Sobel edge on alpha (in texture space), thickness in pixels
            float EdgeMaskAlpha(sampler2D tex, float2 uv)
            {
                float2 px = _MainTex_TexelSize.xy * _EdgeThickness;

                // Sample alpha in a 3x3 kernel
                float a00 = tex2D(tex, uv + float2(-px.x, -px.y)).a;
                float a10 = tex2D(tex, uv + float2( 0,     -px.y)).a;
                float a20 = tex2D(tex, uv + float2( px.x,  -px.y)).a;

                float a01 = tex2D(tex, uv + float2(-px.x,  0)).a;
                float a11 = tex2D(tex, uv + float2( 0,      0)).a;
                float a21 = tex2D(tex, uv + float2( px.x,   0)).a;

                float a02 = tex2D(tex, uv + float2(-px.x,  px.y)).a;
                float a12 = tex2D(tex, uv + float2( 0,     px.y)).a;
                float a22 = tex2D(tex, uv + float2( px.x,  px.y)).a;

                float gx = (a20 + 2*a21 + a22) - (a00 + 2*a01 + a02);
                float gy = (a02 + 2*a12 + a22) - (a00 + 2*a10 + a20);

                float g = sqrt(gx*gx + gy*gy);
                // Emphasize edge and threshold
                float edge = saturate((g * _EdgeIntensity) - _EdgeThreshold);
                return edge;
            }

            float hash21(float2 p)
            {
                // tiny hash for subtle per-pixel variation even without noise
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 texCol = tex2D(_MainTex, i.uv) * i.color;
                // Respect sprite transparency
                clip(texCol.a - _Cutoff);

                // Base color (unlit)
                float3 baseRGB = texCol.rgb;

                // Edge mask from alpha
                float edge = EdgeMaskAlpha(_MainTex, i.uv);

                // Hand-drawn breakup using noise + time jitter
                float2 nUV = i.uv * _NoiseScale + _Time.y * _JitterSpeed;
                float n = tex2D(_NoiseTex, nUV).r;

                // Create “strokes” by quantizing noise into bands
                float bands = _StrokeDensity;
                float banded = frac(n * bands);
                float stroke = smoothstep(_StrokeBreakup, 0.0, banded); // 1 in darker bands, 0 in lighter

                // Add a tiny extra randomness so repeated tiles don’t look too uniform
                float grain = hash21(i.uv * 1024.0);

                // Final edge opacity
                float edgeAlpha = saturate(edge * (0.65 + 0.35*grain)) * saturate(0.25 + 0.75*stroke);

                // Composite: draw edge color *around* opaque areas; keep sprite color
                float3 outRGB = baseRGB * (1 - edgeAlpha) + _EdgeColor.rgb * edgeAlpha;
                float outA = texCol.a; // preserve sprite alpha

                return fixed4(outRGB, outA);
            }
            ENDCG
        }
    }

    FallBack "Unlit/Transparent"
}

