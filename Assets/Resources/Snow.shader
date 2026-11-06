// 떠다니는 입자(유기물 부스러기·바다눈) 한 타일: 꼭짓점 = 타일 안 위치(0–1), uv = 네 모서리, uv2.x = 순번.
// 크기는 실제 반지름(화면 단위)이고, 1픽셀보다 작아지면 넓이를 지키며 흐려진다. 관찰자에서 멀면(보이는 구 밖) 사라진다
Shader "Evo/Snow"
{
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
            float4 _Off;   // xyz 타일 모서리(관찰자 기준, 화면 단위), w 주기(화면 단위)
            float4 _Par;   // x 반지름 y 알파 z 보이는 반지름 w 거리 1에서 1픽셀의 화면 단위
            float4 _Mot;   // x 가라앉기(주기/초) y 떨림(주기 비율) z 바닥 높이 w 수면 높이
            float4 _Col;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float a : TEXCOORD1; };
            v2f vert (appdata v)
            {
                v2f o; float t = _Time.y, k = v.uv2.x * 131.0;
                float3 j = float3(sin(t * 5.3 + k), sin(t * 4.1 + k * 1.7), cos(t * 6.7 + k * 0.6)) * _Mot.y;
                float3 c = frac(v.vertex.xyz + float3(0, -t * _Mot.x, 0) + j);
                float3 w = _Off.xyz + c * _Off.w;
                float4 vp = mul(UNITY_MATRIX_V, float4(w, 1));
                float z = max(0.01, -vp.z), r = max(_Par.x, 1.3 * z * _Par.w);
                vp.xy += v.uv * r;
                o.pos = mul(UNITY_MATRIX_P, vp);
                float fade = saturate((_Par.z - length(w)) / (_Par.z * 0.35)) * step(_Mot.z, w.y) * step(w.y, _Mot.w);
                o.a = _Par.y * fade * (_Par.x / r) * (_Par.x / r);
                o.uv = v.uv; return o;
            }
            fixed4 frag (v2f i) : SV_Target { float a = saturate(1 - dot(i.uv, i.uv)); return fixed4(_Col.rgb, a * a * i.a); }
            ENDCG
        }
    }
}
