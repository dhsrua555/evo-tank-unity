// 화면 뒤처리: 피사계 심도(초점면 밖 생물은 흐리게: 현미경은 얕고 물속 카메라는 깊다) + 빛줄기 + 가장자리 어둡게.
// _DepthTex = 생물 몸의 카메라 거리(0 = 몸 없음: 배경·떠다니는 입자는 흐리지 않는다 — 입자는 제 셰이더가 스스로 흐린다)
Shader "Evo/Post"
{
    Properties { _MainTex ("", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "EvoWater.cginc"
            sampler2D _MainTex, _DepthTex; float4 _MainTex_TexelSize;
            float4 _DoF;                       // 전역: x 초점 거리, y 조리개(흐림 세기, 픽셀), z 최대 흐림 반지름(픽셀), w 거리 1에서 1픽셀의 화면 단위
            float4 _CamF, _CamR, _CamU;        // 카메라 앞·오른쪽·위(오른쪽·위는 화면 끝까지의 길이)
            float _Micro;                      // 1 = 현미경 모드

            float Coc(float d) { return d <= 0 ? 0 : min(_DoF.z, abs(d - _DoF.x) / d * _DoF.y); }

            static const float2 DISK[16] = {
                float2(0.2770, 0.1520), float2(-0.3790, 0.2950), float2(0.0620, -0.4900), float2(0.6290, -0.2400),
                float2(-0.6710, -0.3010), float2(0.4140, 0.6370), float2(-0.1530, 0.7810), float2(0.8790, 0.2620),
                float2(-0.8930, 0.2510), float2(0.1100, -0.8420), float2(-0.5100, -0.7600), float2(0.7600, -0.6300),
                float2(-0.2100, 0.0900), float2(0.3500, -0.0700), float2(-0.0500, 0.4000), float2(-0.3300, -0.2400) };

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 uv = i.uv, px = _MainTex_TexelSize.xy;
                float d0 = tex2D(_DepthTex, uv).r, c0 = Coc(d0);
                // 앞쪽의 흐린 몸은 가장자리 밖으로도 번진다: 둘레에서 나보다 가까운 것의 흐림 반지름을 빌려 온다
                float R = c0, dz = d0 <= 0 ? 1e9 : d0;
                [unroll] for (int k = 0; k < 8; k++)
                {
                    float2 o = DISK[k] * _DoF.z * px; float d = tex2D(_DepthTex, uv + o).r;
                    if (d > 0 && d < dz) R = max(R, Coc(d) * step(length(DISK[k]) * _DoF.z, Coc(d)));
                }
                float3 col = tex2D(_MainTex, uv).rgb;
                if (R > 0.75)
                {
                    float3 sum = col; float wsum = 1;
                    [unroll] for (int k = 0; k < 16; k++)
                    {
                        float2 o = DISK[k] * R; float d = tex2D(_DepthTex, uv + o * px).r;
                        // 나보다 가까운데 선명한 것은 끌어오지 않는다(선명한 앞 물체가 뒤 배경에 번지지 않게)
                        float w = (d <= 0 || d >= dz - 0.05) ? 1 : saturate(Coc(d) - length(o) + 1);
                        sum += tex2D(_MainTex, uv + o * px).rgb * w; wsum += w;
                    }
                    col = sum / wsum;
                }
                // 빛줄기(물속): 수면에서 내려오는 세로 빛기둥. 위를 볼수록 밝고 머리 위(꼭짓점)로 모인다. 몸 앞쪽 물만큼만 보인다
                float2 s = uv * 2 - 1; float3 dir = normalize(_CamF.xyz + s.x * _CamR.xyz + s.y * _CamU.xyz);
                if (_Sky.z > 0)
                {
                    float ph = atan2(dir.z, dir.x), t = _Time.y;
                    float sh = saturate(0.5 + 0.28 * (sin(ph * 11 + t * 0.15) + sin(ph * 23.3 - t * 0.21) + 0.6 * sin(ph * 47.1 + t * 0.33)));
                    sh = sh * sh * sh * smoothstep(-0.25, 0.75, dir.y) * (1 - smoothstep(0.9, 0.99, dir.y));
                    float far = d0 <= 0 ? 1 : 1 - exp(-d0 / _Obs.w * 0.6);   // 몸까지 물(m)만큼
                    col += sh * far * _Sky.z * _LightCol.rgb * float3(0.8, 0.95, 1);
                }
                // 가장자리: 현미경은 둥근 시야(접안렌즈), 물속은 살짝 어둡게
                float rr = length(float2(s.x * _MainTex_TexelSize.z / _MainTex_TexelSize.w, s.y));   // 화면 높이 단위
                col *= _Micro > 0.5 ? lerp(0.35, 1, smoothstep(1.6, 1.0, rr)) : 1 - 0.09 * rr * rr;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
