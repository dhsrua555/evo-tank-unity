// 너무 작아 몸을 그릴 수 없는 것(세균·작은 미생물·유기물 부스러기): 상자 안을 떠도는 점. 메시 꼭짓점 = 상자 안 위치(0–1)
// uv = 네 모서리(−1–1), uv2.x = 순번(0–1) — _Count보다 크면 안 그린다
Shader "Evo/Dots"
{
    Properties { _Count ("Visible fraction", Float) = 1 _Size ("Size (units)", Float) = 0.05 _Color ("Color", Color) = (1,1,1,0.5) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Count, _Size, _Box; float4 _Color, _Off, _YLim;   // _Off 헤엄친 만큼 어긋남(상자 비율), _YLim 바닥·수면 높이
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float fade : TEXCOORD1; };
            v2f vert (appdata v)
            {
                v2f o; float t = _Time.y, k = v.uv2.x * 97.0;
                float3 c = frac(v.vertex.xyz + float3(sin(t * 0.21 + k) * 0.01, -t * 0.004, cos(t * 0.17 + k * 1.3) * 0.01) - _Off.xyz);
                float3 w = (c - 0.5) * _Box;
                float4 vp = mul(UNITY_MATRIX_V, float4(w, 1)); vp.xy += v.uv * _Size;
                o.pos = v.uv2.x > _Count ? float4(0, 0, -10, 1) : mul(UNITY_MATRIX_P, vp);
                o.fade = saturate((1 - length(c - 0.5) * 2) * 4) * step(_YLim.x, w.y) * step(w.y, _YLim.y);   // 관찰자 둘레 구 안만
                o.uv = v.uv; return o;
            }
            fixed4 frag (v2f i) : SV_Target { float a = saturate(1 - dot(i.uv, i.uv)); return fixed4(_Color.rgb, a * a * _Color.a * i.fade); }
            ENDCG
        }
    }
}
