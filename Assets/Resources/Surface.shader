// 수면(아래에서 본): 물결 기울기에 따라 하늘이 비치거나(스넬 창 안) 물속이 거울처럼 비친다(전반사). 좌표는 실제 m
Shader "Evo/Surface"
{
    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "EvoWater.cginc"
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; };
            v2f vert (float4 v : POSITION) { v2f o; o.pos = UnityObjectToClipPos(v); o.wp = mul(unity_ObjectToWorld, v).xyz; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 m = _Obs.xz + i.wp.xz / _Obs.w; float t = _Time.y;   // 수면 위치(m)
                float2 g = float2(cos(m.x * 9 + t * 1.7) * 0.5 + cos((m.x + m.y) * 23 - t * 2.3) * 0.25, cos(m.y * 11 - t * 1.3) * 0.5 + cos((m.x - m.y) * 19 + t * 2.1) * 0.25);
                float3 n = normalize(float3(g.x * 0.25, -1, g.y * 0.25));   // 아래를 보는 면의 물결 법선
                float3 v = normalize(i.wp - _WorldSpaceCameraPos);
                float3 r = refract(v, n, 1.33);
                float3 rv = reflect(v, n); rv.y = -abs(rv.y);
                float3 under = WaterCol(float3(rv.x, rv.y * 0.35, rv.z) / length(float3(rv.x, rv.y * 0.35, rv.z))) * 1.15;   // 전반사: 물속 얕은 쪽(밝은 물빛)이 거울처럼
                float3 sky = float3(0.75, 0.88, 1) * (0.6 + 0.6 * _Sun) * _LightCol.rgb + 0.4 * pow(saturate(r.y), 8);
                float tir = dot(r, r) < 1e-4 ? 1 : 0;   // 굴절 못 함 = 전반사
                float3 c = lerp(sky, under, tir);
                float3 T = exp(-distance(_WorldSpaceCameraPos, i.wp) * _Sigma.rgb);
                return fixed4(c * T + WaterCol(v) * (1 - T), 1);
            }
            ENDCG
        }
    }
}
