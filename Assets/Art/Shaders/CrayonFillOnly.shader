Shader "Unlit/Crayon Fill Only"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Color   ("Tint", Color) = (1,1,1,1)

        // Fill look
        _FillColor ("Fill Color", Color) = (1,1,1,1)

        // Hand-drawn roughness
        _NoiseTex    ("Noise (grayscale)", 2D) = "gray" {}
        _NoiseScale  ("Noise Scale", Range(1, 30)) = 8
        _JitterSpeed ("Jitter Speed", Range(0, 5)) = 0.7
        _StrokeDensity ("Stroke Density", Range(0.5, 6)) = 3.0
        _StrokeBreakup ("Stroke Breakup", Range(0, 1)) = 0.55

        // Frame stepping for choppy "hand-drawn" look
        _FakeFPS ("Stroke FPS", Range(1, 24)) = 4

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

            float4 _FillColor;

            sampler2D _NoiseTex;
            float  _NoiseScale;
            float  _JitterSpeed;
            float  _StrokeDensity;
            float  _StrokeBreakup;

            float  _Cutoff;
            float  _FakeFPS;

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

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 texCol = tex2D(_MainTex, i.uv) * i.color;
                clip(texCol.a - _Cutoff);

                // Quantized (stepped) time for choppy effect
                float steppedTime = floor(_Time.y * _FakeFPS) / _FakeFPS;

                // Jittered noise for hand-drawn breakup
                float2 nUV = i.uv * _NoiseScale + steppedTime * _JitterSpeed;
                float n = tex2D(_NoiseTex, nUV).r;

                float bands = _StrokeDensity;
                float banded = frac(n * bands);
                float stroke = smoothstep(_StrokeBreakup, 0.0, banded);

                float grain = hash21(i.uv * 1024.0);

                // Hand-drawn breakup factor
                float fillAlpha = saturate(0.65 + 0.35*grain) * saturate(0.25 + 0.75*stroke);

                // Final output: noisy fill only
                return fixed4(_FillColor.rgb, texCol.a * fillAlpha * _FillColor.a);
            }
            ENDCG
        }
    }

    FallBack "Unlit/Transparent"
}
