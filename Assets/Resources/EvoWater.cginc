// 물빛·햇빛 공통(Macro·Sky·Surface·Post): 보는 방향마다 물빛이 다르다(위가 밝고, 머리 위엔 스넬 창). 빛물결은 실제 m 좌표로
float4 _FogCol;    // 전역: 물빛(rgb)
float4 _Sigma;     // 전역: 화면 단위 거리당 빨강·초록·파랑 감쇠
float4 _LightCol;  // 전역: 이 깊이까지 내려온 햇빛 색(깊을수록 청록)
float4 _Obs;       // 전역: 관찰자 위치(m, y = 위쪽이 +: −수심), w = 화면 단위/m
float4 _Sky;       // 전역: x 스넬 창 밝기, y 빛물결 세기, z 빛줄기 세기
float _Sun;        // 전역: 이 깊이의 햇빛 0–1

// 보는 방향 d(단위 벡터)의 물빛: 아래는 어둡고 위는 밝다. 머리 위 97° 원뿔 = 수면 너머 하늘(스넬 창), 물결에 일렁인다
float3 WaterCol(float3 d)
{
    float3 c = _FogCol.rgb * lerp(0.4, 1.55, smoothstep(-0.75, 0.95, d.y));
    float t = _Time.y, wob = 0.03 * sin(d.x * 23 + t * 1.3) * sin(d.z * 19 - t * 1.1);
    float win = smoothstep(0.62, 0.74, d.y + wob);
    return c + win * _Sky.x * _LightCol.rgb * float3(0.75, 0.9, 1);
}

// 빛물결: 수면 물결이 모은 햇빛이 바닥·몸 위에 그물 무늬로 일렁인다. p = 수평 위치(m)
float Caustic(float2 p, float t)
{
    p *= 7;
    float2 q = p + 0.6 * float2(sin(p.y * 0.9 + t * 0.7), sin(p.x * 0.8 - t * 0.6));
    float a = abs(sin(q.x * 1.9 + t * 0.5) + sin(q.y * 2.2 - t * 0.45));
    float b = abs(sin((q.x + q.y) * 1.4 - t * 0.55) + sin((q.x - q.y) * 1.7 + t * 0.35));
    return pow(saturate(1 - 0.55 * min(a, b)), 5);
}
