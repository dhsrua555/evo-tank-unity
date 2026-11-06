// 자유 이동: 층·상자 대신 관찰자가 물속 위치(px, py 수심, pz; m)를 갖고 헤엄친다. 휠 = 내 크기(시야 폭 B).
// 화면은 관찰자 둘레(시야 폭 B의 구)만 그리고, 그 위치의 밀도는 둘레 8칸(가로 2×2 기둥 × 위아래 층)을 섞어서 얻는다.
// 물빛·떠다니는 입자는 실제 크기(m)로: 작아지면 물이 투명하고 입자가 커 보이며, 커지면 멀리가 푸르게 흐려진다.
using System;
using UnityEngine;
using Evo;

public partial class EvoTank
{
    double px = 5, py = 1.25, pz = 5;   // 관찰자 위치(m): 가로 0–20(둘레가 이어짐), 수심 0(수면)–10(바닥)
    Vector3 dotOff;                     // 작은 생물 점 무늬 어긋남(시야 비율): 헤엄치면 흘러간다
    readonly (int v, float w)[] cw = new (int, float)[8]; int vNear;
    float yF = -1e9f, yS = 1e9f;        // 바닥·수면 높이(화면 단위, 관찰자 기준)
    Material mSnow; Mesh snow; MaterialPropertyBlock mpbS;
    Rect gauge;

    static double Frac(double x) => x - Math.Floor(x);

    // 관찰자 둘레 8칸과 무게(삼중 선형), 가장 가까운 칸, 바닥·수면 높이
    void Cells()
    {
        float fyf = Mathf.Clamp((float)(py / World.DZ) - 0.5f, 0, World.NY - 1);
        int y0 = Mathf.FloorToInt(fyf), y1 = Mathf.Min(y0 + 1, World.NY - 1); float ty = fyf - y0;
        double fx = px / World.DX - 0.5, fz = pz / World.DX - 0.5;
        int x0 = (int)Math.Floor(fx), z0 = (int)Math.Floor(fz); float tx = (float)(fx - x0), tz = (float)(fz - z0);
        int n = 0; float best = -1;
        for (int a = 0; a < 2; a++) for (int b = 0; b < 2; b++) for (int c = 0; c < 2; c++)
        {
            int ix = ((x0 + a) % World.NX + World.NX) % World.NX, iz = ((z0 + c) % World.NZ + World.NZ) % World.NZ, v = (b == 0 ? y0 : y1) * World.LS + ix + World.NX * iz;
            float wg = (a == 0 ? 1 - tx : tx) * (b == 0 ? 1 - ty : ty) * (c == 0 ? 1 - tz : tz);
            cw[n++] = (v, wg); if (wg > best) { best = wg; vNear = v; }
        }
        float u = BOX / B; yF = -(float)(World.DEPTH - py) * u; yS = (float)py * u;
    }
    double Dens(int s, int k) { double d = 0; foreach (var (v, wg) in cw) d += wg * w.cnt[World.Ix(s, v, k)]; return d; }
    double Mix(double[] a) { double d = 0; foreach (var (v, wg) in cw) d += wg * a[v]; return d; }
    float Mix(float[] a) { float d = 0; foreach (var (v, wg) in cw) d += wg * a[v]; return d; }

    // 관찰자를 dm(m, 화면 축: y = 위)만큼 옮긴다. 시야보다 멀리 가면 개체를 새로 뽑는다
    void MoveBy(Vector3 dm)
    {
        double ny = Math.Clamp(py - dm.y, 0, World.DEPTH); dm.y = (float)(py - ny); py = ny;
        px = Frac((px + dm.x) / World.WIDTH) * World.WIDTH; pz = Frac((pz + dm.z) / World.WIDTH) * World.WIDTH;
        Cells();
        var d = dm * (BOX / B);
        if (d.sqrMagnitude > BOX * BOX) { avs.Clear(); sel = null; tRes = 0; return; }
        foreach (var a in avs) { a.p -= d; if (a.sessile) a.p.y = yF; a.p = Wrap(a.p); }
        dotOff += d / BOX;
    }

    // 시야 밖으로 나간 것은 반대편에서 들어온다(둘레가 이어진 물). 바닥·수면에서는 멈춘다
    Vector3 Wrap(Vector3 p)
    {
        p.x = Mathf.Repeat(p.x + H, BOX) - H; p.z = Mathf.Repeat(p.z + H, BOX) - H;
        if (p.y < -H || p.y > H) p.y = Mathf.Repeat(p.y + H, BOX) - H;
        return new Vector3(p.x, Mathf.Clamp(p.y, yF, yS), p.z);
    }

    void SwimKeys(float dt)
    {
        var mv = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) mv.z += 1; if (Input.GetKey(KeyCode.S)) mv.z -= 1;
        if (Input.GetKey(KeyCode.D)) mv.x += 1; if (Input.GetKey(KeyCode.A)) mv.x -= 1;
        if (Input.GetKey(KeyCode.E)) mv.y += 1; if (Input.GetKey(KeyCode.Q)) mv.y -= 1;
        if (Input.GetKeyDown(KeyCode.PageUp)) MoveBy(new Vector3(0, 0.5f, 0));
        if (Input.GetKeyDown(KeyCode.PageDown)) MoveBy(new Vector3(0, -0.5f, 0));
        if (mv == Vector3.zero) return;
        if (sel != null) { sel = null; Unfollow(); }   // 헤엄치면 따라가기를 멈춘다
        float sp = B * 0.5f * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 4 : 1);
        MoveBy((Quaternion.Euler(pitch, yaw, 0) * new Vector3(mv.x, 0, mv.z) + Vector3.up * mv.y) * sp * dt);
    }

    // 그 깊이에서 물 1 m가 빨강·초록·파랑을 얼마나 흐리게 하나(감쇠 계수, 연안 바닷물 근사: 빨강이 먼저 사라진다)
    static readonly Vector3 SIGMA = new(0.45f, 0.11f, 0.08f);

    // 떠다니는 입자(유기물 부스러기·바다눈): 주기 P(m)인 무늬를 시야를 덮을 만큼 타일로 깐다. 크기도 m 단위라 확대하면 커진다
    static readonly float[] SNOW_P = { 1.6e-3f, 1.28e-2f, 0.1024f, 0.8192f };
    static readonly float[] SNOW_SINK = { 1e-6f, 3e-5f, 1e-3f, 1e-2f };   // 가라앉는 속도(m/s)
    void Snow(float u, float sun, bool ms)
    {
        if (!snow) { snow = DotsMesh(600); mSnow = new Material(Shader.Find("Evo/Snow")); mpbS = new MaterialPropertyBlock(); }
        var col = (ms ? new Color(0.8f, 0.9f, 0.85f) : new Color(0.75f, 0.8f, 0.95f)) * (0.35f + 0.65f * sun);
        float amt = Mathf.Clamp((float)Mix(w.pom) / 1.5f, 0.08f, 1.6f);   // 바다눈 양 = 그 자리의 실제 유기물 입자(POM) 농도
        float pxk = 2 * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2) / Screen.height;
        for (int i = 0; i < SNOW_P.Length; i++)
        {
            float P = SNOW_P[i], q = Mathf.Log(B / P);
            float wl = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Mathf.Log(0.06f), Mathf.Log(0.2f), q)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Mathf.Log(1.2f), Mathf.Log(2.5f), q)));
            if (wl < 0.01f) continue;
            float Pu = P * u, R = H;
            var f = new Vector3((float)Frac(px / P), (float)Frac(-py / P), (float)Frac(pz / P));
            mpbS.SetVector("_Par", new Vector4(P / 400 * u, 0.5f * wl * amt, R, pxk));
            mpbS.SetVector("_Mot", new Vector4(SNOW_SINK[i] / P, i == 0 ? 0.004f : 0.0005f, yF, yS));
            mpbS.SetColor("_Col", col);
            int lo = Mathf.FloorToInt(-R / Pu), hi = Mathf.CeilToInt(R / Pu) + 1;
            for (int a = lo; a < hi; a++) for (int b = lo; b < hi; b++) for (int c = lo; c < hi; c++)
            {
                var o = (new Vector3(a, b, c) - f) * Pu;
                var near = new Vector3(Mathf.Clamp(0, o.x, o.x + Pu), Mathf.Clamp(0, o.y, o.y + Pu), Mathf.Clamp(0, o.z, o.z + Pu));
                if (near.sqrMagnitude > R * R) continue;   // 보이는 구와 안 겹치는 타일
                mpbS.SetVector("_Off", new Vector4(o.x, o.y, o.z, Pu));
                Graphics.DrawMesh(snow, Matrix4x4.identity, mSnow, 0, null, 0, mpbS);
            }
        }
    }

    // 왼쪽 수심계: 빛(밝을수록 위), 지금 수심, 따라가는 혈통이 사는 깊이. 누르면 그 깊이로 간다
    void DepthGauge(float Hh)
    {
        gauge = new Rect(10, 58, 46, Mathf.Max(120, Hh - 58 - 150)); GUI.Box(gauge, GUIContent.none, sBox);
        var bar = new Rect(gauge.x + 8, gauge.y + 22, 12, gauge.height - 34);
        int c = vNear % World.LS; var fs = FollowSt; double fmax = 0;
        if (fs != null) for (int y = 0; y < World.NY; y++) for (int k = 0; k < fs.K; k++) fmax = Math.Max(fmax, w.cnt[World.Ix(fs.slot, y * World.LS + c, k)]);
        for (int y = 0; y < World.NY; y++)
        {
            float l = Mathf.Clamp01(w.I[y * World.LS + c] * 1.6f), y0 = bar.y + bar.height * y / World.NY, hh = bar.height / World.NY + 0.5f;
            Fill(new Rect(bar.x, y0, bar.width, hh), Color.Lerp(new Color(0.02f, 0.05f, 0.1f), new Color(0.35f, 0.7f, 0.85f), l));
            if (fs != null && fmax > 0)
            {
                double d = 0; for (int k = 0; k < fs.K; k++) d += w.cnt[World.Ix(fs.slot, y * World.LS + c, k)];
                if (d > 0) Fill(new Rect(bar.xMax + 2, y0, 2 + 16 * (float)Math.Sqrt(d / fmax / fs.K), hh - 1), Color.HSVToRGB(Mathf.Repeat(fs.hue / 360, 1), 0.6f, 1));
            }
        }
        float my = bar.y + bar.height * (float)(py / World.DEPTH); Fill(new Rect(bar.x - 4, my - 1, bar.width + 8, 3), Color.white);
        GUI.Label(new Rect(gauge.x, gauge.y + 3, gauge.width, 18), "수심", new GUIStyle(sTd) { alignment = TextAnchor.UpperCenter });
        GUI.Label(new Rect(gauge.x, gauge.yMax - 14, gauge.width, 14), $"{World.DEPTH:0} m", new GUIStyle(sTd) { alignment = TextAnchor.UpperCenter, fontSize = 10 });
        if (GUI.Button(bar, GUIContent.none, GUIStyle.none))
        {
            float t = Mathf.Clamp01((Event.current.mousePosition.y - bar.y) / bar.height);
            MoveBy(new Vector3(0, (float)(py - t * World.DEPTH), 0));
        }
    }
}
