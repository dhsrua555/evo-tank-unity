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
            #include "EvoWater.cginc"   // 물빛·감쇠(_Sigma: 작게 보면 물이 투명)·햇빛 색·빛물결
            float _Fade, _Sel;
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
                if (_Sky.y > 0) dif *= 1 + _Sky.y * Caustic(_Obs.xz + i.wp.xz / _Obs.w, _Time.y) * saturate(n.y * 0.8 + 0.4);   // 빛물결: 위를 향한 면에
                float rim = pow(1 - abs(dot(n, vd)), 3);
                float3 c = i.col.rgb * (0.25 + 0.9 * dif * (0.3 + 0.7 * _Sun)) * _LightCol.rgb + rim * (0.25 + 0.5 * _Sel) * float3(0.7, 0.9, 1);
                float3 T = exp(-distance(_WorldSpaceCameraPos, i.wp) * _Sigma.rgb) * _Fade;
                return fixed4(c * T + WaterCol(-vd) * (1 - T), 1);   // 그 방향 배경과 같은 물빛으로 녹는다
            }
            ENDCG
        }
    }
}
