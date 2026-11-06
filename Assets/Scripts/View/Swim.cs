// 자유 이동: 관찰자가 지도에서 고른 해역(reg) 물기둥 안의 수심 py(m)에 있고 헤엄친다. px·pz는 화면용(물빛·입자가 흘러가게)일 뿐 해역은 지도로 옮긴다. 휠 = 내 크기(시야 폭 B).
// 화면은 관찰자 둘레(시야 폭 B의 구)만 그리고, 그 위치의 밀도는 위아래 두 층(층 가운데 사이 선형)을 섞어서 얻는다.
// 물빛·떠다니는 입자는 실제 크기(m)로: 작아지면 물이 투명하고 입자가 커 보이며, 커지면 멀리가 푸르게 흐려진다.
using System;
using UnityEngine;
using Evo;

public partial class EvoTank
{
    double px = 5, py = 1.25, pz = 5;   // 관찰자 위치(m): py = 수심 0(수면)–해역 바닥
    int reg = -1, obsCell = -1, regSeen; // 관찰자가 있는 해역(처음·새 지도면 가장 얕은 곳)과 지도 칸(지형이 바뀌어 해역을 다시 나눠도 그 칸의 바다에 남는다)
    Vector3 dotOff;                     // 작은 생물 점 무늬 어긋남(시야 비율): 헤엄치면 흘러간다
    readonly (int v, float w)[] cw = new (int, float)[2]; int vNear;
    float yF = -1e9f, yS = 1e9f;        // 바닥·수면 높이(화면 단위, 관찰자 기준)
    Material mSnow; Mesh snow; MaterialPropertyBlock mpbS;
    Rect gauge, mapR; Texture2D mapT; int mapKey = -1;

    static double Frac(double x) => x - Math.Floor(x);
    double Floor => w.geo.rDepth[reg];

    // 관찰자 수심 위아래 두 층과 무게, 가장 가까운 칸, 바닥·수면 높이
    void Cells()
    {
        if (regSeen != w.regridN && obsCell >= 0) { regSeen = w.regridN; reg = NearestSea(obsCell); }
        if (reg < 0 || reg >= w.NR) { reg = 0; for (int c = 1; c < w.NR; c++) if (w.geo.rDepth[c] < w.geo.rDepth[reg]) reg = c; }
        if (obsCell < 0 || w.geo.reg[obsCell] != reg) obsCell = Array.IndexOf(w.geo.reg, reg);
        py = Math.Min(py, Floor);
        int n = w.nyR[reg], y0 = 0;
        while (y0 < n - 2 && w.zc[(y0 + 1) * World.LS + reg] <= py) y0++;
        int y1 = Math.Min(y0 + 1, n - 1), v0 = y0 * World.LS + reg, v1 = y1 * World.LS + reg;
        float t = y1 == y0 ? 0 : Mathf.Clamp01((float)((py - w.zc[v0]) / (w.zc[v1] - w.zc[v0])));
        cw[0] = (v0, 1 - t); cw[1] = (v1, t); vNear = t < 0.5f ? v0 : v1;
        float u = BOX / B; yF = -(float)(Floor - py) * u; yS = (float)py * u;
    }
    double Dens(int s, int k) { double d = 0; foreach (var (v, wg) in cw) d += wg * w.cnt[World.Ix(s, v, k)] / w.vol[v]; return d; }   // 마리/mL
    double Mix(double[] a) { double d = 0; foreach (var (v, wg) in cw) d += wg * a[v]; return d; }
    float Mix(float[] a) { float d = 0; foreach (var (v, wg) in cw) d += wg * a[v]; return d; }

    // 다른 해역으로(지도·혈통 찾기): 시야의 개체는 새로 뽑는다
    void GoTo(int r, double depth, int cell = -1) { reg = r; obsCell = cell; py = depth; avs.Clear(); sel = null; tRes = 0; Cells(); }
    int NearestSea(int cell)
    {
        int best = -1; float bd = 1e9f;
        for (int i = 0; i < Geo.G * Geo.G; i++) if (w.geo.reg[i] >= 0) { int dx = i % Geo.G - cell % Geo.G, dz = i / Geo.G - cell / Geo.G; float d = dx * dx + dz * dz; if (d < bd) { bd = d; best = i; } }
        if (best >= 0) obsCell = best; return best >= 0 ? w.geo.reg[best] : 0;
    }

    // 관찰자를 dm(m, 화면 축: y = 위)만큼 옮긴다. 시야보다 멀리 가면 개체를 새로 뽑는다
    void MoveBy(Vector3 dm)
    {
        double ny = Math.Clamp(py - dm.y, 0, Floor); dm.y = (float)(py - ny); py = ny;
        px += dm.x; pz += dm.z;
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

    // 왼쪽 수심계: 이 해역의 층마다 같은 높이 칸(층 두께는 6 m–수 km로 달라서), 빛(밝을수록 위), 지금 수심, 따라가는 혈통의 밀도. 누르면 그 깊이로 간다
    void DepthGauge(float Hh)
    {
        gauge = new Rect(10, 58, 46, Mathf.Max(120, Hh - 58 - 150)); GUI.Box(gauge, GUIContent.none, sBox);
        var bar = new Rect(gauge.x + 8, gauge.y + 22, 12, gauge.height - 34);
        int n = w.nyR[reg]; float hh = bar.height / n; var fs = FollowSt; double fmax = 0;
        double FD(int y) { int v = y * World.LS + reg; double d = 0; for (int k = 0; k < fs.K; k++) d += w.cnt[World.Ix(fs.slot, v, k)]; return d / w.vol[v]; }
        if (fs != null) for (int y = 0; y < n; y++) fmax = Math.Max(fmax, FD(y));
        for (int y = 0; y < n; y++)
        {
            float l = Mathf.Clamp01(w.I[y * World.LS + reg] * 1.6f), y0 = bar.y + hh * y;
            Fill(new Rect(bar.x, y0, bar.width, hh - 1), Color.Lerp(new Color(0.02f, 0.05f, 0.1f), new Color(0.35f, 0.7f, 0.85f), l));
            if (fs != null && fmax > 0) { double d = FD(y); if (d > 0) Fill(new Rect(bar.xMax + 2, y0, 2 + 16 * (float)Math.Sqrt(d / fmax), hh - 1), Color.HSVToRGB(Mathf.Repeat(fs.hue / 360, 1), 0.6f, 1)); }
        }
        int vc = w.Vox(reg, py), yc = vc / World.LS; float top = World.ZE[yc];
        float my = bar.y + hh * (yc + Mathf.Clamp01((float)(py - top) / w.dz[vc])); Fill(new Rect(bar.x - 4, my - 1, bar.width + 8, 3), Color.white);
        GUI.Label(new Rect(gauge.x, gauge.y + 3, gauge.width, 18), "수심", new GUIStyle(sTd) { alignment = TextAnchor.UpperCenter });
        GUI.Label(new Rect(gauge.x, gauge.yMax - 14, gauge.width, 14), $"{Floor:0} m", new GUIStyle(sTd) { alignment = TextAnchor.UpperCenter, fontSize = 10 });
        if (GUI.Button(bar, GUIContent.none, GUIStyle.none))
        {
            float t = Mathf.Clamp01((Event.current.mousePosition.y - bar.y) / bar.height) * n; int y = Mathf.Min((int)t, n - 1);
            MoveBy(new Vector3(0, (float)(py - (World.ZE[y] + (t - y) * w.dz[y * World.LS + reg])), 0));
        }
    }

    // 해역 지도(오른쪽 위): 고도·수심 색, 지금 해역은 노랗게. 누르면 그 해역으로 간다
    void MapBox(Rect r)
    {
        mapR = r; GUI.Box(r, GUIContent.none, sBox); var g = w.geo; int G = Geo.G;
        if (!mapT) mapT = new Texture2D(G, G) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        if (mapKey != w.seed * 64 + reg + w.regridN * 4096 || Time.frameCount % 30 == 0)   // 지형·해수면·바다얼음이 바뀌면 다시
        {
            mapKey = w.seed * 64 + reg + w.regridN * 4096;
            for (int i = 0; i < G * G; i++)
            {
                float d = g.Depth(i);
                var c = d <= 0 ? Color.Lerp(new Color(0.4f, 0.34f, 0.2f), new Color(0.78f, 0.7f, 0.52f), -d / 1500)
                               : Color.Lerp(new Color(0.5f, 0.86f, 0.84f), new Color(0.03f, 0.08f, 0.26f), Mathf.Log(1 + d / 20) / Mathf.Log(251));
                if (d > 0 && g.reg[i] >= 0) c = Color.Lerp(c, new Color(0.92f, 0.96f, 1), w.iceR[g.reg[i]] * 0.8f);   // 바다얼음
                if (d <= 0 && Geo.Lat(i / G) > 45) c = Color.Lerp(c, new Color(0.92f, 0.96f, 1), Mathf.Clamp01(w.ice * 1.5f - (60 - Geo.Lat(i / G)) / 20));   // 빙상: 고위도 땅부터
                if (g.reg[i] == reg) c = Color.Lerp(c, new Color(1, 0.9f, 0.2f), 0.8f);   // 지금 해역: 깊이와 상관없이 밝은 노랑(땅 갈색과 헷갈리지 않게)
                mapT.SetPixel(i % G, i / G, c);   // 아래 줄 = 남쪽(위도 2°), 위 = 북쪽(60°)
            }
            mapT.Apply();
        }
        var img = new Rect(r.x + 8, r.y + 8, r.height - 16, r.height - 16); GUI.DrawTexture(img, mapT);
        float cs = img.width / G; double day = w.tick * w.P.DT / 24.0;
        foreach (var q in w.quakes)   // 최근 사건 표시: 화산 빨강·운석 보라(30일), 융기·침강 주황 테(진행 중)
        {
            if (q.cell < 0) continue; double age = day - q.Day0; bool on = q.kind == Qk.Uplift ? age < q.days : age < 30; if (!on || age < 0) continue;
            var col = q.kind == Qk.Volcano ? new Color(1, 0.25f, 0.1f) : q.kind == Qk.Impact ? new Color(0.8f, 0.4f, 1) : new Color(1, 0.6f, 0.2f);
            float x = img.x + (q.cell % G + 0.5f) * cs, y = img.y + (G - 1 - q.cell / G + 0.5f) * cs, rr = q.kind == Qk.Uplift ? q.radius * cs : cs * 0.6f;
            if (q.kind == Qk.Uplift) { Fill(new Rect(x - rr, y - rr, 2 * rr, 1), col); Fill(new Rect(x - rr, y + rr, 2 * rr, 1), col); Fill(new Rect(x - rr, y - rr, 1, 2 * rr), col); Fill(new Rect(x + rr, y - rr, 1, 2 * rr), col); }
            else Fill(new Rect(x - rr, y - rr, 2 * rr, 2 * rr), col);
        }
        GUI.Label(new Rect(img.xMax + 10, r.y + 6, r.xMax - img.xMax - 16, r.height - 12),
            placing ? $"<b><color=#ffb070>{World.QName[qSel]}</color></b>\n지도에서 일어날 곳을 누르세요.\n\n(자연 탭에서 다시 누르면 취소)"
            : $"<b>{Geo.LName(g.rLat[reg])} {Geo.DName(g.rDepth[reg])}</b>\n북위 {g.rLat[reg]:0}° · 수심 {g.rDepth[reg]:0} m\n넓이 {g.rCells[reg] * 4}만 km²\n표층 {w.sst[reg]:0.0}°C · 혼합층 {w.mld[reg]:0} m{(w.iceR[reg] > 0.05f ? $"\n바다얼음 {w.iceR[reg] * 100:0}%" : "")}\n\n해역 {w.NR}곳 · 지도를 누르면 그 바다로", sS);
        if (GUI.Button(img, GUIContent.none, GUIStyle.none))
        {
            var p = Event.current.mousePosition;
            int x = Mathf.Clamp((int)((p.x - img.x) / img.width * G), 0, G - 1), z = Mathf.Clamp(G - 1 - (int)((p.y - img.y) / img.height * G), 0, G - 1);
            if (placing) FireQ(z * G + x);
            else { int to = g.reg[z * G + x]; if (to >= 0 && to != reg) GoTo(to, py, z * G + x); }
        }
    }
}
