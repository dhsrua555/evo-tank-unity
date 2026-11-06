// 거시생물 관찰(물속): 위에서 오는 햇빛 + 가장자리 빛 + 물 안개. 상자 가장자리(_Fade)는 물빛으로 녹는다
Shader "Evo/Macro"
{
    Properties { _Fade ("Fade", Float) = 1 _Sel ("Selected", Float) = 0 _T0 ("T0", Float) = 0 _Rate ("Rate", Float) = 1 }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "EvoAnim.cginc"
            float _Fade, _Sel;
            float4 _FogCol;    // 전역: 물빛(rgb)
            float4 _Sigma;     // 전역: 화면 단위 거리당 빨강·초록·파랑 감쇠(실제 m당 감쇠 ÷ 화면 단위/m) — 작게 보면 물이 투명하다
            float4 _LightCol;  // 전역: 이 깊이까지 내려온 햇빛 색(깊을수록 청록)
            float _Sun;        // 전역: 이 깊이의 햇빛 0–1
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; float4 uv0 : TEXCOORD0; float3 uv1 : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 wp : TEXCOORD1; float4 col : COLOR; };
            v2f vert (appdata v)
            {
                v2f o; float3 p = EvoAnim(v.vertex.xyz, v.uv0, v.uv1);
                o.pos = UnityObjectToClipPos(float4(p, 1)); o.n = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, float4(p, 1)).xyz; o.col = v.color; return o;
            }
            fixed4 frag (v2f i, fixed face : VFACE) : SV_Target
            {
                float3 n = normalize(i.n) * (face > 0 ? 1 : -1), vd = normalize(_WorldSpaceCameraPos - i.wp);
                float dif = saturate(dot(n, normalize(float3(0.25, 1, 0.15))) * 0.7 + 0.3);
                float rim = pow(1 - abs(dot(n, vd)), 3);
                float3 c = i.col.rgb * (0.25 + 0.9 * dif * (0.3 + 0.7 * _Sun)) * _LightCol.rgb + rim * (0.25 + 0.5 * _Sel) * float3(0.7, 0.9, 1);
                float3 T = exp(-distance(_WorldSpaceCameraPos, i.wp) * _Sigma.rgb) * _Fade;
                return fixed4(c * T + _FogCol.rgb * (1 - T), 1);
            }
            ENDCG
        }
    }
}
