// 생물 메시 꼭짓점 움직임(Bodies.cs와 같은 약속): uv0 = (몸 따라 u, 종류, 위상, 진폭), uv1 = 흔들 방향
// 종류: 1 편모(끝으로 갈수록 크게 도는 물결) 2 섬모(파도처럼 차례로) 3 몸 물결(꼬리 쪽이 크게) 4 맥동 5 흔들림(촉수·해조)
//       6 막 출렁임(세포막이 자리마다 다르게 부풀고 꺼짐) 7 세포질 흐름(소기관이 세포 긴 축(진폭>0) 또는 y축(진폭<0) 둘레를 돈다)
float _T0, _Rate;   // 개체마다 시간 어긋남, 움직임 빠르기(MaterialPropertyBlock)
float _Pinch;       // 분열 중(0–1): 몸이 길어지며 가운데가 잘록해진다

float3 EvoAnim(float3 p, float4 a, float3 dir)
{
    float u = a.x, type = a.y, ph = a.z, amp = a.w, t = _Time.y * _Rate + _T0, s = 0;
    if (_Pinch > 0) { p.z *= 1 + 0.45 * _Pinch; p.xy *= 1 - 0.7 * _Pinch * exp(-p.z * p.z / 0.012); }
    if (type < 0.5) return p;
    if (type < 1.5) s = sin(u * 9 - t * 14 + ph) * u;
    else if (type < 2.5) s = sin(t * 9 + ph);
    else if (type < 3.5) s = sin(u * 5.6 - t * 6 + ph) * (0.15 + u * u);
    else if (type < 4.5) s = 0.5 + 0.5 * sin(t * 3 + ph);
    else if (type < 5.5) s = sin(t * 1.3 + ph + u * 2) * u;
    else if (type < 6.5) s = sin(t * 2.3 + dot(p, float3(7.1, 5.3, 3.7)) + ph) * sin(t * 1.7 + dot(p, float3(-4.3, 6.1, 5.9)));
    else
    {
        float an = t * abs(amp) + ph, c = cos(an), sn = sin(an);
        return amp > 0 ? float3(c * p.x - sn * p.y, sn * p.x + c * p.y, p.z) : float3(c * p.x - sn * p.z, p.y, sn * p.x + c * p.z);
    }
    return p + dir * amp * s;
}
