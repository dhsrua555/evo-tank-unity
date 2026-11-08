// 바닥(모래·흙): 화면 축척에 맞춘 네 겹 무늬(알갱이·물결 자국·얼룩)를 실제 m 좌표로 그린다.
// 겹마다 크기가 2배씩이고, 확대·축소하면 한 겹이 사라지는 만큼 새 겹이 서서히 나타나 무늬가 튀지 않는다(무게 합 일정)
Shader "Evo/Floor"
{
    Properties { _Base ("Base", Color) = (0.55, 0.48, 0.36, 1) _Kind ("Kind", Float) = 0 }
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
            #include "EvoWater.cginc"
            float4 _Base, _OX, _OZ, _L, _W; float _Kind, _FR;   // _FR: 바닥 판이 끝나는 거리(화면 단위) — 그 앞에서 물빛으로 녹인다   // _OX/_OZ: 겹마다 관찰자 위치의 무늬 주기 안 자리(0–1), _L: 겹 칸 크기(m), _W: 겹 무게
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; };
            v2f vert (float4 v : POSITION) { v2f o; o.pos = UnityObjectToClipPos(v); o.wp = mul(unity_ObjectToWorld, v).xyz; return o; }
            float Hash(float2 c) { c -= 64 * floor(c / 64); return frac(sin(dot(c, float2(127.1, 311.7))) * 43758.5453); }
            float VN(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }
            // 한 겹의 높이(칸 단위): 모래는 휘어진 물결 자국 + 알갱이, 흙은 덩어리진 얼룩
            float Layer(float2 c)
            {
                float n = VN(c), g = VN(c * 3.1 + 17);
                float2 wv = c + 2.2 * float2(VN(c * 0.21 + 3), VN(c * 0.21 + 11));                          // 물결 자국은 휘고 끊긴다
                float rip = (0.5 + 0.5 * sin(6.2832 * (wv.x * 0.45 + 0.3 * wv.y) + 6 * VN(c * 0.17 + 5))) * smoothstep(0.2, 0.6, VN(c * 0.13 + 23));
                return _Kind < 0.5 ? 0.45 * rip * rip + 0.35 * n + 0.2 * g : 0.6 * n * n + 0.4 * g;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 lm = i.wp.xz / _Obs.w;   // 관찰자에서 잰 수평 거리(m)
                float h = 0, dx = 0, dz = 0, hv = 0, e = 0.08;
                [unroll] for (int j = 0; j < 4; j++)
                {
                    float2 c = float2(_OX[j], _OZ[j]) * 64 + lm / _L[j];
                    float a = Layer(c); h += _W[j] * a; hv += _W[j] * VN(c * 0.37 + 31);
                    dx += _W[j] * (Layer(c + float2(e, 0)) - a) / e; dz += _W[j] * (Layer(c + float2(0, e)) - a) / e;
                }
                float3 n = normalize(float3(-dx * 0.35, 1, -dz * 0.35));
                float dif = saturate(dot(n, normalize(float3(0.25, 1, 0.15))) * 0.8 + 0.2);
                float2 rp = _Obs.xz + lm;
                if (_Sky.y > 0) dif *= 1 + _Sky.y * Caustic(rp, _Time.y);
                float3 col = _Base.rgb * (0.7 + 0.6 * h) * lerp(float3(0.9, 0.95, 1.06), float3(1.08, 1.02, 0.92), hv);   // 알갱이 빛깔이 조금씩 다르다
                float3 c = col * (0.25 + 0.9 * dif * (0.3 + 0.7 * _Sun)) * _LightCol.rgb;
                float3 vd = normalize(_WorldSpaceCameraPos - i.wp), T = exp(-distance(_WorldSpaceCameraPos, i.wp) * _Sigma.rgb);
                T *= 1 - smoothstep(0.3, 1, length(i.wp.xz) / _FR);   // 판 가장자리가 수평선처럼 또렷이 잘리지 않게
                return fixed4(c * T + WaterCol(-vd) * (1 - T), 1);
            }
            ENDCG
        }
    }
}
