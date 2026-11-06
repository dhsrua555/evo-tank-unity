// 생물 모양 메시: 유전자 + 그 칸의 실제 모양(Morph, 온도·포식 신호 반영) + 환경 상태(삼투 부풂·쭈글, 빛 순응 엽록체)로 절차적으로 만든다.
// 몸길이 = 1(정규화), +Z = 앞(헤엄 방향). 화면은 몸길이(m) / 상자 크기로 키운다.
// 꼭짓점 정보: 색(rgb, a = 밝기·불투명), uv0 = (몸 따라 u, 움직임 종류, 위상, 진폭), uv1 = 흔들 방향 — Evo 셰이더가 편모·섬모·꼬리를 움직인다.
using System;
using System.Collections.Generic;
using UnityEngine;
using Evo;

public static class Bodies
{
    // 움직임 종류(셰이더와 같음): 0 없음 1 편모 2 섬모 3 몸 물결(꼬리) 4 맥동(수축포·갓) 5 흔들림(촉수·해조)
    const float FLAG = 1, CIL = 2, UND = 3, PULSE = 4, SWAY = 5;

    sealed class MB
    {
        public readonly List<Vector3> v = new(), n = new(), d = new(); public readonly List<Color> c = new(); public readonly List<Vector4> a = new(); public readonly List<int> t = new();
        public int V(Vector3 p, Vector3 nn, Color col, Vector4 anim, Vector3 dir) { v.Add(p); n.Add(nn); c.Add(col); a.Add(anim); d.Add(dir); return v.Count - 1; }
        public void Tri(int i, int j, int k) { t.Add(i); t.Add(j); t.Add(k); }
        public readonly List<(string, Vector3)> parts = new();   // 부위 이름표 자리(메시 좌표). 같은 이름은 처음 것만
        public void Mark(string name, Vector3 p) { foreach (var (n0, _) in parts) if (n0 == name) return; parts.Add((name, p)); }
        public Mesh Mesh()
        {
            var m = new Mesh(); if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetUVs(0, a); m.SetUVs(1, d); m.SetTriangles(t, 0); m.RecalculateBounds();
            return m;
        }
    }

    static readonly Dictionary<long, Mesh> cache = new();
    static readonly Dictionary<Mesh, (string name, Vector3 p)[]> partsOf = new();
    public static (string name, Vector3 p)[] Parts(Mesh m) => m && partsOf.TryGetValue(m, out var a) ? a : Array.Empty<(string, Vector3)>();
    public static int Count => cache.Count;
    public static void Clear() { foreach (var x in cache.Values) UnityEngine.Object.Destroy(x); cache.Clear(); partsOf.Clear(); }   // 새 세계: 종·계통 번호가 처음부터 다시 쓰인다

    public static Mesh Get(Strain st, int k, in Morph m, float swell, float chl)
    {
        int sq = Mathf.Clamp(Mathf.RoundToInt(m.spines * 7), 0, 7), cq = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(Mathf.Max(1, m.C), 2)), 0, 63);
        int sw = swell > 0.3f ? 2 : swell < -0.3f ? 1 : 0, ch = Mathf.Clamp(Mathf.RoundToInt(chl * 2), 0, 3);
        var sp = Sp?.Invoke(st.sp); VS = sp?.vis ?? Zero; HU = sp?.hue ?? HU;
        long key = ((long)st.sp << 42) | ((long)st.id << 20) | ((long)k << 17) | ((long)sq << 14) | ((long)cq << 8) | ((long)sw << 6) | ((long)ch << 4);
        if (cache.TryGetValue(key, out var mesh)) return mesh;
        if (cache.Count > 700) { foreach (var x in cache.Values) UnityEngine.Object.Destroy(x); cache.Clear(); partsOf.Clear(); }
        var b = new MB(); var r = new System.Random(st.id * 31 + k);
        Build(b, st, k, m, sw == 2 ? 1 : sw == 1 ? -1 : 0, ch / 2f, r); Sig(b, st.dev);
        mesh = b.Mesh(); cache[key] = mesh; partsOf[mesh] = b.parts.ToArray(); return mesh;
    }

    // 종 표지(Species.vis, 화면이 종 → 표지를 찾는 길). 그리는 동안 VS·HU에 둔다
    public static Func<int, Species> Sp;
    static readonly float[] Zero = new float[8];
    static float[] VS = Zero; static float HU;

    // 다 그린 몸에 종 표지를 입힌다: 몸 비율(길쭉·뭉툭), 색조를 조금 돌림(엽록체 초록도 황록·청록으로), 무늬(가로띠·세로줄·점·없음).
    // 같은 몸 설계라도 종마다 눈에 띄게 다르다(실제 종 구별도 무늬·비율·부속지 수 같은 진단 형질로 한다)
    // 종 표지(Species.vis)로 몸 전체를 고친다: 길쭉함·납작함·달걀꼴(앞뒤 굵기)·단면 골(이랑 수)·색조·무늬(띠/줄/점)와 그 진하기.
    // 같은 몸 설계라도 종이 다르면 윤곽이 확실히 달라 보이게(발생 생물은 몸 틀이 무너지지 않게 조금 약하게)
    static void Sig(MB b, bool dev)
    {
        float e = 1 + (dev ? 0.25f : 0.4f) * VS[0], sx = 1 / Mathf.Sqrt(e), rot = VS[1] * 18;
        float fl = 1 + (dev ? 0.15f : 0.25f) * VS[6], tp = (dev ? 0.15f : 0.3f) * VS[5];
        int lobes = 3 + Mathf.RoundToInt(2 * (VS[3] + 1)); float la = (dev ? 0.025f : 0.06f) * (VS[7] + 1);
        int kind = VS[2] < -0.5f ? 0 : VS[2] < 0 ? 1 : VS[2] < 0.5f ? 2 : 3;
        float f = 2.5f + 1.5f * (VS[3] + 1), amp = 0.3f + 0.2f * (VS[4] + 1), ph = VS[3] * 7;
        float zr = 1e-6f; foreach (var p in b.v) zr = Mathf.Max(zr, Mathf.Abs(p.z));
        Vector3 Sc(Vector3 p) { float rad = (1 + tp * p.z / zr) * (1 + la * Mathf.Sin(Mathf.Atan2(p.y, p.x) * lobes + ph)); return new Vector3(sx * fl * rad, sx / fl * rad, e); }
        for (int i = 0; i < b.v.Count; i++)
        {
            var p = b.v[i]; float pat = kind switch
            {
                1 => Mathf.SmoothStep(0, 1, Mathf.Sin((p.z * f + ph) * Mathf.PI * 2) * 2.5f),                                    // 가로띠
                2 => Mathf.SmoothStep(0, 1, Mathf.Sin(Mathf.Atan2(p.y, p.x) * Mathf.Round(f + 1) + ph) * 2.5f),                // 세로줄
                3 => Mathf.SmoothStep(0, 1, (Mathf.Sin(p.x * f * 9 + ph) * Mathf.Sin(p.y * f * 9) * Mathf.Sin(p.z * f * 9 + 1) - 0.25f) * 4),   // 점
                _ => 0
            };
            var c = b.c[i]; Color.RGBToHSV(c, out float h, out float sa, out float va);
            if (sa > 0.08f) h = Mathf.Repeat(h + rot / 360f, 1);
            var c2 = Color.HSVToRGB(h, Mathf.Clamp01(sa * (1 + 0.8f * amp * pat) + 0.25f * amp * pat), va * (1 - 0.8f * amp * pat));
            c2.a = c.a + (1 - c.a) * 0.6f * amp * pat; b.c[i] = c2;   // 비치는 세포도 무늬 자리는 진해져 보인다
            var S = Sc(p);
            b.v[i] = Vector3.Scale(p, S);
            var n = b.n[i]; b.n[i] = new Vector3(n.x / S.x, n.y / S.y, n.z / S.z).normalized;
            b.d[i] = Vector3.Scale(b.d[i], S);
        }
        for (int i = 0; i < b.parts.Count; i++) { var (nm, p) = b.parts[i]; b.parts[i] = (nm, Vector3.Scale(p, Sc(p))); }
    }

    static Vector3 Mk(MB b, string name, Vector3 p) { b.Mark(name, p); return p; }   // 그 자리에 이름표를 달고 자리를 그대로 돌려준다

    // ---------- 색 ----------
    static Color Hue(float h, float s, float v, float a) { var c = Color.HSVToRGB(Mathf.Repeat(h / 360f, 1), s, v); c.a = a; return c; }
    static Color Col(float r, float g, float b, float a) => new Color(r, g, b, a);
    static Color Cyto(float[] g, float hue) => Color.Lerp(Hue(hue, 0.4f, 0.95f, 0.35f), Col(1, 0.5f, 0.3f, 0.4f), g[Gn.Pigment] * 0.7f);

    // ---------- 기본 도형 ----------
    // 회전타원체(울퉁불퉁 bump: 방향 → 반지름 배율)
    static void Ellipsoid(MB b, Vector3 ctr, Vector3 rad, Quaternion q, Color col, int seg, Func<Vector3, float> bump = null, Vector4 anim = default)
    {
        int la = seg, lo = seg * 2, i0 = b.v.Count;
        for (int i = 0; i <= la; i++)
        {
            float th = Mathf.PI * i / la;
            for (int j = 0; j <= lo; j++)
            {
                float ph = 2 * Mathf.PI * j / lo;
                var u = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th));
                float f = bump == null ? 1 : bump(u);
                var p = Vector3.Scale(u, rad) * f;
                var nn = new Vector3(u.x / rad.x, u.y / rad.y, u.z / rad.z).normalized;
                b.V(ctr + q * p, q * nn, col, anim, q * nn);
            }
        }
        for (int i = 0; i < la; i++) for (int j = 0; j < lo; j++)
        {
            int p0 = i0 + i * (lo + 1) + j, p1 = p0 + lo + 1;
            b.Tri(p0, p1, p0 + 1); b.Tri(p0 + 1, p1, p1 + 1);
        }
    }

    // 관(편모·자루·촉수·가지): 점들을 잇고 반지름 rad(u)
    static void Tube(MB b, IList<Vector3> pts, Func<float, float> rad, Color col, int sides, float type, float amp, float phase, Vector3 dir, Func<float, Color> colAt = null)
    {
        int n = pts.Count, i0 = b.v.Count;
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)(n - 1);
            var fw = (pts[Mathf.Min(n - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]).normalized;
            var s1 = Vector3.Cross(fw, Mathf.Abs(fw.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var s2 = Vector3.Cross(fw, s1);
            float rr = rad(u); var cc = colAt != null ? colAt(u) : col;
            for (int j = 0; j <= sides; j++)
            {
                float ph = 2 * Mathf.PI * j / sides; var nn = s1 * Mathf.Cos(ph) + s2 * Mathf.Sin(ph);
                b.V(pts[i] + nn * rr, nn, cc, new Vector4(u, type, phase, amp), dir);
            }
        }
        for (int i = 0; i < n - 1; i++) for (int j = 0; j < sides; j++)
        {
            int p0 = i0 + i * (sides + 1) + j, p1 = p0 + sides + 1;
            b.Tri(p0, p1, p0 + 1); b.Tri(p0 + 1, p1, p1 + 1);
        }
    }

    // 회전체(껍데기·갓·해면·몸통): 축 z0→z1, 반지름 rad(z), 단면 납작함 flat(y/x). 몸 물결 진폭 und
    static void Lathe(MB b, Vector3 ctr, Quaternion q, float z0, float z1, Func<float, float> rad, Func<float, Color> col, int segZ, int segR, float flat = 1, float type = 0, float amp = 0, Func<float, float> wide = null)
    {
        int i0 = b.v.Count;
        for (int i = 0; i <= segZ; i++)
        {
            float z = Mathf.Lerp(z0, z1, i / (float)segZ), rr = rad(z), ww = wide != null ? wide(z) : rr;
            float u = Mathf.InverseLerp(z1, z0, z);   // 앞(머리) 0 → 뒤(꼬리) 1
            var cc = col(z);
            for (int j = 0; j <= segR; j++)
            {
                float ph = 2 * Mathf.PI * j / segR, cx = Mathf.Cos(ph), sy = Mathf.Sin(ph);
                var p = new Vector3(cx * ww, sy * rr * flat, z);
                var nn = new Vector3(cx / Mathf.Max(1e-4f, ww), sy / Mathf.Max(1e-4f, rr * flat), 0).normalized;
                var c2 = cc; if (col != null && sy > 0.3f) c2 = cc * 0.85f; c2.a = cc.a;
                b.V(ctr + q * p, q * nn, c2, new Vector4(u, type, 0, amp), q * Vector3.right);
            }
        }
        for (int i = 0; i < segZ; i++) for (int j = 0; j < segR; j++)
        {
            int p0 = i0 + i * (segR + 1) + j, p1 = p0 + segR + 1;
            b.Tri(p0, p0 + 1, p1); b.Tri(p0 + 1, p1 + 1, p1);
        }
    }

    // 납작한 막(지느러미·잎): 다각형 부채꼴. 양면(셰이더 Cull Off)
    static void Fan(MB b, Vector3[] poly, Color col, float type, float amp, Vector3 dir, Func<Vector3, float> uOf)
    {
        int i0 = b.v.Count; var ctr = Vector3.zero; foreach (var p in poly) ctr += p; ctr /= poly.Length;
        var nn = Vector3.Cross(poly[1] - poly[0], poly[2] - poly[0]).normalized;
        b.V(ctr, nn, col, new Vector4(uOf(ctr), type, 0, amp), dir);
        foreach (var p in poly) { var c2 = col; c2.a *= 0.8f; b.V(p, nn, c2, new Vector4(uOf(p), type, 0, amp), dir); }
        for (int i = 0; i < poly.Length; i++) b.Tri(i0, i0 + 1 + i, i0 + 1 + (i + 1) % poly.Length);
    }

    static Vector3 Fib(int i, int n) { float y = 1 - 2 * (i + 0.5f) / n, rr = Mathf.Sqrt(1 - y * y), th = i * 2.39996f; return new Vector3(Mathf.Cos(th) * rr, y, Mathf.Sin(th) * rr); }
    static Vector3 RandIn(System.Random r) { Vector3 p; do p = new Vector3((float)r.NextDouble() * 2 - 1, (float)r.NextDouble() * 2 - 1, (float)r.NextDouble() * 2 - 1); while (p.sqrMagnitude > 1); return p; }

    // ---------- 세포 하나 ----------
    // 단세포 몸 설계: 유전자 조합으로 정한다. 실제 원생생물 무리의 몸 구조를 따른다(그 밖은 기본 세포)
    public static Plan PlanOf(Strain st) => Taxa.PlanOf(st);   // 단세포 몸 설계(분류는 Sim의 Taxa)

    // len = 세포 길이(메시 단위), detail 2 전부 · 1 막+엽록체+편모 · 0 막만
    const float WOB = 6, STREAM = 7;   // 막 출렁임, 세포질 흐름(셰이더 EvoAnim)
    static void Cell(MB b, Strain st, in Morph m, Vector3 ctr, float len, Quaternion q, int detail, float swell, float chl, System.Random r, bool appendages = true)
    {
        var g = st.g; float a = st.aspect; var plan = PlanOf(st);
        Vector3 rad = a >= 1 ? new Vector3(0.5f / a, 0.5f / a, 0.5f) * len : new Vector3(0.5f, 0.5f * a, 0.5f) * len;
        if (swell > 0) rad = Vector3.Lerp(rad, Vector3.one * (rad.x + rad.y + rad.z) / 3, 0.5f) * 1.12f;   // 민물에서 물이 들어와 부푼다
        float amoe = plan == Plan.Amoeba ? 0.15f + (g[Gn.Phago] - 0.55f) * 0.9f : 0;   // 위족(아메바)
        int s1 = r.Next(1000);
        bool solo = ctr == Vector3.zero && detail == 2, stiff = g[Gn.Wall] > 0.3f || plan == Plan.Diatom;
        var wob = solo && !stiff ? new Vector4(0, WOB, s1 * 0.01f, len * (plan == Plan.Amoeba ? 0.04f : plan == Plan.Euglena ? 0.025f : 0.012f)) : default;
        // 세포질 흐름: 알갱이가 긴 축(납작하면 위아래 축) 둘레를 저마다 다른 빠르기로 돈다
        Vector4 Flow() => solo && plan != Plan.Diatom ? new Vector4(0, STREAM, (float)r.NextDouble() * 6.3f, (a >= 1 ? 1 : -1) * (0.15f + 0.35f * (float)r.NextDouble())) : default;
        Func<Vector3, float> bump = null;
        if (amoe > 0 || swell < 0 || plan == Plan.Dino || plan == Plan.Ciliate)
            bump = u => (1 + amoe * Mathf.Max(0, Mathf.Sin(u.x * 3.1f + s1) * Mathf.Sin(u.y * 2.7f + s1 * 0.3f) * Mathf.Sin(u.z * 2.3f)) * 1.6f
                          + (swell < 0 ? 0.07f * Mathf.Sin(u.x * 17 + u.y * 13) * Mathf.Sin(u.z * 15) : 0))   // 고염에서 물이 빠져 쭈글쭈글
                      * (plan == Plan.Dino ? 1 - 0.18f * Mathf.Exp(-u.z * u.z / 0.008f) - 0.1f * Mathf.Exp(-u.x * u.x / 0.02f) * Mathf.Clamp01(-u.y * 3) * Mathf.Clamp01(0.3f - u.z * 3) : 1)   // 띠 홈 + 배 쪽 세로 홈
                      * (plan == Plan.Ciliate ? 1 - 0.28f * Mathf.Exp(-(u.y - 0.25f * u.z) * (u.y - 0.25f * u.z) / 0.03f) * Mathf.Clamp01(-u.x * 3) * Mathf.Clamp01(u.z * 2.5f + 0.4f) : 1);   // 구강 홈(비스듬히)
        var cyto = Cyto(g, HU);
        float rmin = Mathf.Min(rad.x, Mathf.Min(rad.y, rad.z));
        Func<float, float> eug = null;   // 유글레나 단면 반지름(z): 앞은 둥글고 뒤는 뾰족한 방추형
        if (plan == Plan.Diatom) Frustule(b, ctr, rad, q, a, detail);
        else if (plan == Plan.Euglena && detail > 0)
        {
            eug = z => rad.x * 1.2f * Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01((z + rad.z) / (2 * rad.z)), 1.5f));
            int i0 = b.v.Count; Lathe(b, ctr, q, -rad.z, rad.z, eug, _ => cyto, 18, 12);
            Radial(b, i0, ctr, q, wob); b.Mark("펠리클", ctr + q * new Vector3(eug(0), 0, 0));
            // 펠리클 줄무늬: 몸을 비스듬히 감는 띠(겉막 단백질 띠가 미끄러져 몸을 바꾼다)
            for (int j = 0, nj = detail == 2 ? 10 : 5; j < nj; j++)
            {
                var pts = new Vector3[16];
                for (int i = 0; i < 16; i++) { float t = i / 15f, z = Mathf.Lerp(-rad.z, rad.z, t), an = j * 2 * Mathf.PI / nj + t * 4.2f, rr = eug(z) * 1.01f; pts[i] = ctr + q * new Vector3(Mathf.Cos(an) * rr, Mathf.Sin(an) * rr, z); }
                int k0 = b.v.Count; Tube(b, pts, _ => len * 0.004f, Col(0.85f, 1, 0.85f, 0.3f), 3, 0, 0, 0, Vector3.zero); Radial(b, k0, ctr, q, wob);
                if (j == 0) b.Mark("펠리클 줄무늬", pts[5]);
            }
        }
        else
        {
            Ellipsoid(b, ctr, rad, q, cyto, detail == 0 ? 4 : plan == Plan.Plain ? 8 : 10, bump, wob);
            b.Mark(g[Gn.Wall] > 0.15f && detail > 0 && plan != Plan.Radiolarian ? (plan == Plan.Dino ? "갑옷판" : "세포벽") : "세포막", ctr + q * new Vector3(rad.x, 0, 0));
            if (g[Gn.Wall] > 0.15f && detail > 0 && plan != Plan.Radiolarian) Ellipsoid(b, ctr, rad * 1.07f, q, Col(0.85f, 1, 0.85f, 0.12f + 0.25f * g[Gn.Wall]), 8, plan == Plan.Dino ? bump : null);
        }
        // 엽록체: 어두운 곳일수록 많이(광순응, Geider 1997). 돌말·와편모충은 황갈색(푸코잔틴·페리디닌)
        bool brown = plan == Plan.Diatom || plan == Plan.Dino;
        if (g[Gn.Photo] > 0.05f && detail > 0)
        {
            var green = brown ? Col(0.8f, 0.6f, 0.22f, 0.85f) : Color.Lerp(Col(0.3f, 0.95f, 0.35f, 0.85f), Col(0.75f, 0.6f, 0.2f, 0.85f), g[Gn.Pigment] * 0.6f);
            if (plan == Plan.Diatom && a >= 1.5f)   // 깃형 돌말: 띠 쪽 벽에 붙은 판 모양 둘
                for (int s = -1; s <= 1; s += 2) { var p = new Vector3(0, s * rad.x * 0.62f, 0); Ellipsoid(b, ctr + q * p, new Vector3(0.55f * rad.x, 0.09f * rad.x, 0.7f * rad.z), q, green, 6); b.Mark("엽록체", ctr + q * p); }
            else if (plan == Plan.Diatom)   // 원형 돌말: 가장자리에 작은 원반 여럿
            {
                var qa = a >= 1 ? q : q * Quaternion.Euler(-90, 0, 0); float R = rad.x, hz = a >= 1 ? rad.z : rad.y; int nc = detail == 1 ? 6 : 12;
                for (int i = 0; i < nc; i++) { float an = i * 2.39996f; var p = new Vector3(Mathf.Cos(an) * R * 0.75f, Mathf.Sin(an) * R * 0.75f, ((float)r.NextDouble() * 2 - 1) * hz * 0.6f); Ellipsoid(b, ctr + qa * p, new Vector3(0.04f, 0.11f, 0.11f) * R, qa * Quaternion.Euler(0, 0, an * Mathf.Rad2Deg), green, 4); if (i == 0) b.Mark("엽록체", ctr + qa * p); }
            }
            else
            {
                int nc = Mathf.Clamp(Mathf.RoundToInt(g[Gn.Photo] * (2 + m.esd / 4) * (0.6f + chl)), 1, detail == 1 ? 2 : 24);
                for (int i = 0; i < nc; i++)
                {
                    var p = Vector3.Scale(RandIn(r) * 0.55f, rad);
                    Ellipsoid(b, ctr + q * p, new Vector3(0.2f, 0.08f, 0.15f) * rmin * (nc == 1 ? 2.2f : 1.3f), q * Quaternion.Euler(r.Next(180), r.Next(180), 0), green, 5, null, brown ? default : Flow()); b.Mark("엽록체", ctr + q * p);
                }
            }
        }
        if (detail == 2)
        {
            if (plan == Plan.Ciliate)   // 큰 핵(유전자 일꾼) + 작은 핵(생식용)
            {
                var pn = new Vector3(0.1f * rad.x, 0.1f * rad.y, -0.05f * rad.z);
                Ellipsoid(b, ctr + q * pn, new Vector3(0.38f * rad.x, 0.32f * rad.y, 0.42f * rad.z), q, Col(0.6f, 0.65f, 1, 0.5f), 8); b.Mark("대핵", ctr + q * pn);
                var pm = pn + new Vector3(0.42f * rad.x, 0, 0.15f * rad.z); Ellipsoid(b, ctr + q * pm, Vector3.one * rmin * 0.09f, q, Col(0.55f, 0.6f, 1, 0.7f), 4); b.Mark("소핵", ctr + q * pm);
            }
            else if (plan == Plan.Radiolarian)   // 중심낭: 핵과 속 세포질을 감싼 주머니
            {
                Ellipsoid(b, ctr, rad * 0.5f, q, Col(0.95f, 0.72f, 0.38f, 0.55f), 8); b.Mark("중심낭", ctr + q * new Vector3(0, rad.y * 0.5f, 0));
                Ellipsoid(b, ctr, Vector3.one * rmin * 0.18f, q, Col(0.65f, 0.7f, 1, 0.6f), 6); b.Mark("핵", ctr);
            }
            else { var pn = new Vector3(0, 0, -0.1f * rad.z); Ellipsoid(b, ctr + q * pn, Vector3.one * rmin * 0.32f, q, Col(0.65f, 0.7f, 1, 0.5f), 6); b.Mark("핵", ctr + q * pn); }
            if (g[Gn.Vacuole] > 0.1f && plan != Plan.Diatom)   // 수축포: 맥동. 섬모충은 앞뒤 둘, 별 모양 수로가 물을 모은다
                for (int v = 0, nv = plan == Plan.Ciliate ? 2 : 1; v < nv; v++)
                {
                    var pv = new Vector3(0.3f * rad.x, plan == Plan.Ciliate ? 0.4f * rad.y : 0, (v == 0 ? -0.55f : 0.5f) * rad.z); float vr = rmin * (0.15f + 0.25f * g[Gn.Vacuole]);
                    b.Mark("수축포", ctr + q * pv);
                    Ellipsoid(b, ctr + q * pv, Vector3.one * vr, q, Col(0.8f, 0.9f, 1, 0.3f), 6, null, new Vector4(0, PULSE, v * 3.1f, rmin * 0.08f));
                    if (plan == Plan.Ciliate)
                        for (int c = 0; c < 7; c++) { float an = c * 0.8976f; var d = q * new Vector3(Mathf.Cos(an), Mathf.Sin(an) * 0.6f, Mathf.Sin(an) * 0.8f); Tube(b, new[] { ctr + q * pv + d * vr, ctr + q * pv + d * vr * 3.2f }, u => rmin * 0.035f * (1 - 0.6f * u), Col(0.8f, 0.9f, 1, 0.3f), 3, PULSE, -rmin * 0.03f, v * 3.1f + 3.1f, d); }
                }
            if (g[Gn.Phago] > 0.4f && plan != Plan.Diatom)   // 먹이 주머니
                for (int i = 0, nf = 1 + (int)(g[Gn.Phago] * 3); i < nf; i++) { var pp = ctr + q * Vector3.Scale(RandIn(r) * 0.5f, rad); Ellipsoid(b, pp, Vector3.one * rmin * 0.16f, q, Col(0.85f, 0.65f, 0.4f, 0.55f), 4, null, Flow()); b.Mark("먹이 주머니", pp); }
            if (g[Gn.Buoy] > 0.2f)   // 기포(가스 소포)·기름방울
                for (int i = 0, nb = (int)(g[Gn.Buoy] * 14); i < nb; i++) { var pp = ctr + q * Vector3.Scale(RandIn(r) * 0.7f, rad); Ellipsoid(b, pp, Vector3.one * rmin * 0.07f, q, Col(1, 1, 0.9f, 0.9f), 3, null, Flow()); b.Mark("기포", pp); }
            if (g[Gn.Pigment] > 0.3f)   // 붉은 색소 방울(카로티노이드)
                for (int i = 0, np = (int)(g[Gn.Pigment] * 6); i < np; i++) { var pp = ctr + q * Vector3.Scale(RandIn(r) * 0.6f, rad); Ellipsoid(b, pp, Vector3.one * rmin * 0.08f, q, Col(1, 0.35f, 0.15f, 0.8f), 3, null, Flow()); b.Mark("색소 방울", pp); }
            if (plan == Plan.Euglena)   // 파라밀론(저장 녹말) 막대
                for (int i = 0; i < 5; i++) { var pp = ctr + q * Vector3.Scale(RandIn(r) * 0.45f, rad); Ellipsoid(b, pp, new Vector3(0.12f, 0.12f, 0.3f) * rmin, q * Quaternion.Euler(r.Next(60), r.Next(360), 0), Col(1, 1, 0.95f, 0.6f), 3, null, Flow()); if (i == 0) b.Mark("파라밀론", pp); }
            if (plan == Plan.Amoeba)   // 과립 세포질: 위족 쪽으로 흘러간다
                for (int i = 0; i < 36; i++) { var pp = ctr + q * Vector3.Scale(RandIn(r) * 0.7f, rad); Ellipsoid(b, pp, Vector3.one * rmin * 0.035f, q, Col(0.95f, 0.9f, 0.75f, 0.6f), 2, null, Flow()); if (i == 0) b.Mark("과립", pp); }
        }
        if (plan == Plan.Amoeba && detail > 0)   // 가장 길게 뻗은 위족에 이름표
        {
            Vector3 best = Vector3.forward; float bf = 0;
            for (int i = 0; i < 64; i++) { var u = Fib(i, 64); float f = bump(u); if (f > bf) { bf = f; best = u; } }
            b.Mark("위족", ctr + q * Vector3.Scale(best, rad) * bf);
        }
        if (plan == Plan.Ciliate && detail > 0)   // 구강 홈 끝에서 세포 안으로 들어가는 목구멍(세포인두)
        {
            var u = new Vector3(-0.92f, 0, 0.1f).normalized; var p0 = ctr + q * Vector3.Scale(u, rad) * bump(u); b.Mark("구강 홈", p0);
            Tube(b, new[] { p0, ctr + q * new Vector3(-0.45f * rad.x, 0.05f * rad.y, -0.1f * rad.z), ctr + q * new Vector3(-0.15f * rad.x, 0.1f * rad.y, -0.3f * rad.z) }, uu => rmin * (0.12f - 0.07f * uu), Col(0.75f, 0.8f, 0.95f, 0.45f), 5, 0, 0, 0, Vector3.zero);
        }
        // 눈: 안점(빨강) → 컵눈(검은 컵) → 렌즈 눈
        if (g[Gn.Eye] > 0.08f && detail > 0)
        {
            var ep = ctr + q * new Vector3(rad.x * 0.55f, rad.y * 0.3f, rad.z * 0.6f);
            float er = rmin * (0.12f + 0.25f * g[Gn.Eye]); b.Mark(g[Gn.Eye] > 0.65f ? "렌즈 눈" : g[Gn.Eye] > 0.35f ? "컵눈" : "안점", ep);
            Ellipsoid(b, ep, Vector3.one * er, q, Col(1, 0.15f, 0.05f, 0.9f), 4);
            if (g[Gn.Eye] > 0.35f) Ellipsoid(b, ep + q * Vector3.right * er * 0.4f, new Vector3(0.5f, 1, 1) * er * 1.2f, q, Col(0.15f, 0.05f, 0.05f, 0.9f), 4);
            if (g[Gn.Eye] > 0.65f) Ellipsoid(b, ep + q * Vector3.right * er * 0.9f, Vector3.one * er * 0.6f, q, Col(0.9f, 0.95f, 1, 1), 5);
        }
        // 껍데기(각): 꽃병 모양, 입이 앞으로 열림 (유종섬모충 lorica)
        if (g[Gn.Lorica] > 0.15f && detail > 0)
        {
            float zl0 = -rad.z * 1.35f, zl1 = rad.z * 0.75f, rl = Mathf.Max(rad.x, rad.y) * 1.25f;
            var amber = Col(0.85f, 0.65f, 0.35f, 0.25f + 0.4f * g[Gn.Lorica]); b.Mark("껍데기", ctr + q * new Vector3(rl * 0.9f, 0, zl0 * 0.5f));
            Lathe(b, ctr, q, zl0, zl1, z => { float u = Mathf.InverseLerp(zl0, zl1, z); return rl * Mathf.Sqrt(Mathf.Max(0.02f, Mathf.Sin(Mathf.Min(1, u * 1.6f) * Mathf.PI / 2))) * (1 + 0.25f * Mathf.Max(0, u - 0.8f) * 5); },
                z => { float u = Mathf.InverseLerp(zl0, zl1, z); var c2 = amber; if (Mathf.Repeat(u * 7, 1) < 0.18f) c2 *= 1.4f; c2.a = amber.a; return c2; }, 10, 10);
        }
        if (!appendages) return;
        // 방산충: 격자 껍질(그물처럼 이어진 규산 골격) + 사방으로 뻗은 가는 축족
        if (plan == Plan.Radiolarian && detail > 0)
        {
            int nl = detail == 2 ? 42 : 20; var nodes = new Vector3[nl]; var done = new HashSet<long>();
            for (int i = 0; i < nl; i++) nodes[i] = Vector3.Scale(Fib(i, nl), rad) * 1.15f;
            for (int i = 0; i < nl; i++)
            {
                var near = new List<int>(); for (int j = 0; j < nl; j++) if (j != i) near.Add(j);
                near.Sort((x, y) => (nodes[x] - nodes[i]).sqrMagnitude.CompareTo((nodes[y] - nodes[i]).sqrMagnitude));
                for (int k = 0; k < 4; k++) { int j = near[k]; if (!done.Add(Math.Min(i, j) * 1000L + Math.Max(i, j))) continue; Tube(b, new[] { ctr + q * nodes[i], ctr + q * nodes[j] }, _ => len * 0.007f, Col(0.92f, 0.96f, 1, 0.55f), 3, 0, 0, 0, Vector3.zero); }
            }
            b.Mark("격자 껍질", ctr + q * nodes[nl / 3]);
            for (int i = 0, na = detail == 2 ? 48 : 16; i < na; i++)
            {
                var u = Fib(i, na); var p0 = ctr + q * Vector3.Scale(u, rad) * 0.5f; var p1 = ctr + q * Vector3.Scale(u, rad) * 2.1f;
                Tube(b, new[] { p0, Vector3.Lerp(p0, p1, 0.5f), p1 }, uu => len * 0.004f * (1 - 0.7f * uu), Col(0.9f, 0.95f, 1, 0.3f), 3, SWAY, len * 0.03f, i, q * Vector3.Cross(u, Vector3.up).normalized);
                if (i == 0) b.Mark("축족", p1);
            }
        }
        // 가시: 방사형 원뿔 (포식 신호가 있으면 더 길게 — 유도 방어). 와편모충은 대신 뿔 셋(케라티움처럼)
        if (m.spines > 0.05f && plan != Plan.Dino)
        {
            int ns = Mathf.Clamp(Mathf.RoundToInt(4 + m.spines * 12), 4, 16); float sl = (0.4f + 0.8f * m.spines) * len;
            for (int i = 0; i < ns; i++)
            {
                var u = Fib(i, ns); Vector3 p0 = ctr + q * Vector3.Scale(u, rad) * 0.95f, p1 = p0 + q * u * sl; b.Mark("가시", p1);
                Tube(b, new[] { p0, p1 }, uu => rmin * 0.09f * (1 - uu), Col(0.9f, 0.95f, 1, 0.6f), 4, 0, 0, 0, Vector3.zero);
            }
        }
        if (plan == Plan.Dino && detail > 0)
        {
            if (m.spines > 0.05f)
            {
                float hl = (0.4f + 0.9f * m.spines) * len;
                var horns = new[] { (new Vector3(0, 0, 0.9f), new Vector3(0, 0, 1), 1f), (new Vector3(0.35f, 0, -0.8f), new Vector3(0.25f, 0, -1), 0.75f), (new Vector3(-0.3f, 0, -0.85f), new Vector3(-0.1f, 0, -1), 0.5f) };
                foreach (var (o, d, f) in horns)
                {
                    var p0 = ctr + q * Vector3.Scale(o, rad); var pts = new Vector3[6];
                    for (int i = 0; i < 6; i++) pts[i] = p0 + q * d.normalized * hl * f * i / 5f;
                    Tube(b, pts, u => rmin * 0.17f * (1 - 0.9f * u), Col(0.9f, 0.92f, 0.8f, 0.5f), 6, 0, 0, 0, Vector3.zero); b.Mark("뿔", pts[4]);
                }
            }
            // 가로 편모: 띠 홈을 따라 감겨 물결친다(물결이 둘레를 돌아 몸을 굴린다)
            int i0 = b.v.Count, nr = 33; var ring = new Vector3[nr];
            for (int i = 0; i < nr; i++) { float an = 2 * Mathf.PI * i / (nr - 1); ring[i] = ctr + q * new Vector3(Mathf.Cos(an) * rad.x * 0.86f, Mathf.Sin(an) * rad.y * 0.86f, 0); }
            Tube(b, ring, _ => len * 0.016f, Col(0.95f, 0.97f, 1, 0.8f), 3, CIL, 0, 0, q * Vector3.forward);
            for (int i = i0; i < b.v.Count; i++) b.a[i] = new Vector4(b.a[i].x, CIL, b.a[i].x * 8 * Mathf.PI, len * 0.03f);
            b.Mark("가로 편모", ring[5]);
            // 세로 편모: 배 쪽 세로 홈에서 뒤로 뻗는다
            float fl = (0.8f + 0.5f * g[Gn.Flagella]) * len; var pf = new Vector3[12]; var b0 = ctr + q * new Vector3(0, -rad.y * 0.85f, -0.1f * rad.z); var df = q * new Vector3(0, -0.25f, -1).normalized;
            for (int i = 0; i < 12; i++) pf[i] = b0 + df * fl * i / 11f;
            Tube(b, pf, u => len * 0.018f * (1 - 0.5f * u), Col(0.85f, 0.9f, 1, 0.55f), 3, FLAG, fl * 0.12f, 0, q * Vector3.right); b.Mark("세로 편모", pf[7]);
        }
        // 깃편모충: 편모 둘레를 미세융모 깃이 깔때기처럼 둘러싼다(물살 속 세균을 걸러 먹는다)
        if (plan == Plan.Choano && detail > 0)
        {
            int nv = detail == 2 ? 20 : 10;
            for (int i = 0; i < nv; i++)
            {
                float an = i * 2 * Mathf.PI / nv; var c = new Vector3(Mathf.Cos(an), Mathf.Sin(an), 0);
                var p0 = ctr + q * (c * rad.x * 0.45f + Vector3.forward * rad.z * 0.85f); var p1 = ctr + q * (c * rad.x * 0.9f + Vector3.forward * (rad.z + len * 0.6f));
                Tube(b, new[] { p0, p1 }, _ => len * 0.006f, Col(0.85f, 0.92f, 1, 0.45f), 3, 0, 0, 0, Vector3.zero);
                if (i == 0) b.Mark("깃(미세융모)", Vector3.Lerp(p0, p1, 0.7f));
            }
        }
        // 편모: 앞에서 나와 물결친다(길이 ~ 세포 1.5배). 유글레나·깃편모충은 하나
        if (g[Gn.Flagella] > 0.05f && detail > 0 && plan != Plan.Dino && plan != Plan.Diatom && plan != Plan.Radiolarian)
        {
            int nfl = plan == Plan.Euglena || plan == Plan.Choano ? 1 : g[Gn.Flagella] > 0.55f ? 2 : 1;
            for (int f = 0; f < nfl; f++)
            {
                float fl = (plan == Plan.Euglena ? 1 : 1.1f + g[Gn.Flagella]) * len * (1 + 0.35f * VS[7]); var pts = new Vector3[12];   // 유글레나 편모 ≈ 몸길이
                var dirF = q * (f == 0 ? Vector3.forward : new Vector3(0.6f, 0, -0.8f));
                var base0 = ctr + q * new Vector3(0, 0, f == 0 ? rad.z : 0.2f * rad.z) + q * (f == 0 ? Vector3.zero : Vector3.right * rad.x);
                for (int i = 0; i < 12; i++) pts[i] = base0 + dirF * fl * i / 11f;
                b.Mark("편모", pts[7]);
                Tube(b, pts, u => len * 0.018f * (1 - 0.5f * u), Col(0.85f, 0.9f, 1, 0.55f), 3, FLAG, fl * 0.12f, f * 1.7f, q * (f == 0 ? Vector3.right : Vector3.up));
            }
        }
        // 섬모: 몸 전체에 짧은 털, 물결처럼 차례로 친다(metachronal)
        if (g[Gn.Cilia] > 0.1f && detail == 2 && plan != Plan.Diatom)
        {
            int nh = (int)(30 + 150 * g[Gn.Cilia]);
            for (int i = 0; i < nh; i++)
            {
                var u = Fib(i, nh); var p0 = ctr + q * Vector3.Scale(u, rad) * (bump == null ? 1 : bump(u)); var nn = q * Vector3.Scale(u, new Vector3(1 / rad.x, 1 / rad.y, 1 / rad.z)).normalized;
                var side = Vector3.Cross(nn, q * Vector3.forward).normalized * len * 0.006f; float hl = len * 0.11f; b.Mark("섬모", p0 + nn * hl);
                int i0 = b.v.Count; var an = new Vector4(0, CIL, u.z * 6, hl * 0.5f); var cc = Col(0.85f, 0.9f, 1, 0.4f);
                b.V(p0 - side, nn, cc, new Vector4(0, CIL, an.z, 0), q * Vector3.forward); b.V(p0 + side, nn, cc, new Vector4(0, CIL, an.z, 0), q * Vector3.forward);
                b.V(p0 + nn * hl - side, nn, cc, an, q * Vector3.forward); b.V(p0 + nn * hl + side, nn, cc, an, q * Vector3.forward);
                b.Tri(i0, i0 + 2, i0 + 1); b.Tri(i0 + 1, i0 + 2, i0 + 3);
            }
        }
        // 부착: 뒤쪽 자루 + 바닥 원반 (종벌레 Vorticella처럼)
        if (g[Gn.Attach] > 0.5f && detail > 0)
        {
            var pts = new Vector3[8]; float sl = len * 1.6f;
            for (int i = 0; i < 8; i++) pts[i] = ctr + q * new Vector3(Mathf.Sin(i * 0.9f) * len * 0.05f, 0, -rad.z - sl * i / 7f);
            Tube(b, pts, u => len * 0.03f, Col(0.8f, 0.85f, 0.75f, 0.55f), 4, SWAY, len * 0.1f, 0, q * Vector3.right); b.Mark("자루", pts[3]); b.Mark("부착 원반", pts[7]);
            Ellipsoid(b, pts[7], new Vector3(0.18f, 0.18f, 0.04f) * len, q, Col(0.7f, 0.7f, 0.55f, 0.6f), 5);
        }
    }

    // 꼭짓점 i0부터: 움직임 an을 주고, 흔들 방향을 몸 축에서 바깥쪽(단면 방사)으로
    static void Radial(MB b, int i0, Vector3 ctr, Quaternion q, Vector4 an)
    {
        var iq = Quaternion.Inverse(q);
        for (int i = i0; i < b.v.Count; i++) { var l = iq * (b.v[i] - ctr); b.a[i] = an; b.d[i] = q * new Vector3(l.x, l.y, 0).normalized; }
    }

    // 돌말 규산 껍질(피각): 원형(알약 상자, 방사 줄무늬) · 깃형(배 모양, 가로 줄무늬 + 가운데 봉선)
    static void Frustule(MB b, Vector3 ctr, Vector3 rad, Quaternion q, float a, int detail)
    {
        var glass = Col(0.92f, 0.88f, 0.65f, 0.3f); var stria = Col(0.5f, 0.45f, 0.3f, 0.75f);
        if (a < 1.5f)
        {
            var qa = a >= 1 ? q : q * Quaternion.Euler(-90, 0, 0); float R = rad.x, hz = a >= 1 ? rad.z : rad.y;
            Lathe(b, ctr, qa, -hz, hz, z => R * Mathf.Pow(Mathf.Max(1e-4f, 1 - Mathf.Pow(Mathf.Abs(z) / hz, 6)), 1 / 6f),
                z => Mathf.Abs(z) < hz * 0.8f && Mathf.Repeat(z / hz * 3, 1) < 0.12f ? stria : glass, 20, detail == 2 ? 24 : 12);
            b.Mark("규산 껍질", ctr + qa * new Vector3(R, 0, hz * 0.5f)); b.Mark("띠(거들)", ctr + qa * new Vector3(-R, 0, 0));
            if (detail < 2) return;
            for (int s = -1; s <= 1; s += 2) for (int i = 0; i < 28; i++)   // 껍질 면의 방사 줄무늬(구멍 줄)
            {
                float an = i * 2 * Mathf.PI / 28, z = s * hz * 1.005f; var d = new Vector3(Mathf.Cos(an), Mathf.Sin(an), 0);
                Tube(b, new[] { ctr + qa * (d * R * 0.12f + Vector3.forward * z), ctr + qa * (d * R * 0.9f + Vector3.forward * z) }, _ => R * 0.012f, stria, 3, 0, 0, 0, Vector3.zero);
                if (s == 1 && i == 0) b.Mark("방사 줄무늬", ctr + qa * (d * R * 0.6f + Vector3.forward * z));
            }
        }
        else
        {
            float W = rad.x * 1.3f, Hh = rad.x * 0.75f, Z = rad.z; q *= Quaternion.Euler(0, 0, 90);   // 껍질 면이 옆(x)을 본다
            Lathe(b, ctr, q, -Z, Z, z => Hh * Mathf.Sqrt(Mathf.Max(1e-4f, 1 - z * z / (Z * Z))), z => Mathf.Repeat(z / Z * 12, 1) < 0.3f ? stria : glass, 32, 12, 1, 0, 0,
                z => W * Mathf.Pow(Mathf.Max(1e-4f, 1 - z * z / (Z * Z)), 0.75f));
            b.Mark("규산 껍질", ctr + q * new Vector3(W, 0, 0.3f * Z)); b.Mark("가로 줄무늬", ctr + q * new Vector3(-W * 0.8f, 0, -0.5f * Z));
            if (detail < 2) return;
            for (int s = -1; s <= 1; s += 2)   // 봉선: 껍질 가운데를 앞뒤로 가르는 틈(가운데 결절에서 끊긴다)
            {
                var pts = new Vector3[8];
                for (int i = 0; i < 8; i++) { float z = s * Mathf.Lerp(0.07f, 0.9f, i / 7f) * Z; pts[i] = ctr + q * new Vector3(0, Hh * Mathf.Sqrt(Mathf.Max(0, 1 - z * z / (Z * Z))) * 1.02f, z); }
                Tube(b, pts, _ => Hh * 0.05f, Col(0.45f, 0.4f, 0.3f, 0.7f), 3, 0, 0, 0, Vector3.zero);
            }
            b.Mark("봉선", ctr + q * new Vector3(0, Hh, 0.5f * Z));
        }
    }

    // ---------- 전체 ----------
    static void Build(MB b, Strain st, int k, in Morph m, float swell, float chl, System.Random r)
    {
        var lk = Taxa.Of(st);
        if (m.C <= 1) { Cell(b, st, m, Vector3.zero, m.cellL / Mathf.Max(m.cellL, m.Leff / (1 + 1.5f * m.spines)), Quaternion.identity, 2, swell, chl, r); return; }
        bool larva = st.dev && k == 0 && st.K > 1;
        if (!st.dev || lk <= Look.Blob || larva) { Colony(b, st, m, swell, chl, r, larva); return; }
        switch (lk)   // 몸 설계는 부품 조합에서 읽고(Taxa), 그리는 것도 부품으로
        {
            case Look.Seaweed: Seaweed(b, st, m, r); break;
            case Look.Sponge: Sponge(b, st, m, r); break;
            case Look.Polyp: case Look.Coral: case Look.Jelly: Cnidarian(b, st, m, r); break;
            case Look.Echino: Echino(b, st, m, r); break;
            default: if (lk >= Look.Lancelet) Chordate(b, st, m, r, lk); else Bilaterian(b, st, m, r, lk); break;
        }
    }

    // 군체: 실(사슬) · 판 · 속 빈 공(볼복스). 세포가 많으면 일부만 그리고 크기를 맞춘다. 분업하면 안쪽에 큰 생식세포
    static void Colony(MB b, Strain st, in Morph m, float swell, float chl, System.Random r, bool larva)
    {
        var g = st.g; int C = Mathf.Max(2, Mathf.RoundToInt(m.C)), n = Mathf.Min(C, 220);
        int det = n <= 16 ? 2 : n <= 64 ? 1 : 0;
        float D = larva ? 3 : m.D;
        if (D < 1.5f)   // 실: 몸길이 1 안에 n개
        {
            float cl = 1f / n;
            for (int i = 0; i < n; i++)
            {
                float z = -0.5f + (i + 0.5f) * cl; float cv = 1 + 1.5f * VS[6]; var p = new Vector3(Mathf.Sin(z * (5 + 3 * VS[5]) + 1) * 0.04f * cv, Mathf.Sin(z * 3) * 0.03f * cv, z);   // 사슬 굽이: 종마다 다르다
                Cell(b, st, m, p, cl * 1.05f, Quaternion.identity, det, swell, chl, r, i == n - 1);
            }
        }
        else if (D < 2.4f)   // 판: 육각 배열 원반
        {
            int ring = Mathf.CeilToInt(Mathf.Sqrt(n / 3f)); float cl = 1f / (2 * ring + 1); int placed = 0;
            for (int qx = -ring; qx <= ring && placed < n; qx++) for (int qy = -ring; qy <= ring && placed < n; qy++)
            {
                int qz = -qx - qy; if (Mathf.Abs(qz) > ring) continue;
                var p = new Vector3((qx + qy * 0.5f) * cl, 0, qy * 0.866f * cl);
                Cell(b, st, m, p, cl * 1.0f, Quaternion.identity, det, swell, chl, r, Mathf.Abs(qx) + Mathf.Abs(qy) + Mathf.Abs(qz) >= 2 * ring - 1); placed++;
            }
        }
        else   // 공: 겉에 세포, 편모는 바깥으로
        {
            float R = 0.45f, cl = Mathf.Min(0.3f, 2.2f * R / Mathf.Sqrt(n));
            Ellipsoid(b, Vector3.zero, Vector3.one * R * 0.98f, Quaternion.identity, Col(0.6f, 0.8f, 0.9f, 0.08f), 10); b.Mark("젤리 기질", new Vector3(0.25f, -0.2f, 0) * R);   // 젤리 기질
            for (int i = 0; i < n; i++) { var u = Fib(i, n); Cell(b, st, m, u * R, cl, Quaternion.LookRotation(u, Mathf.Abs(u.y) < 0.9f ? Vector3.up : Vector3.right), det, swell, chl, r, det > 0); }
            if (g[Gn.Diff] > 0.3f)
                for (int i = 0, ng = 2 + (int)(g[Gn.Diff] * 6); i < ng; i++)
                    Ellipsoid(b, Mk(b, "생식세포", RandIn(r) * R * 0.5f), Vector3.one * cl * 1.3f, Quaternion.identity, g[Gn.Photo] > 0.2f ? Col(0.3f, 0.9f, 0.35f, 0.7f) : Col(0.9f, 0.8f, 0.6f, 0.6f), 6);
            if (larva && g[Gn.Cilia] + g[Gn.Neuro] > 0.3f) { var fake = st.g; }   // 유생: 겉 세포의 섬모·편모가 이미 그려짐
        }
    }

    // 해조류: 실 모양이면 갈라지는 가지, 판 모양이면 줄기 + 잎(다시마), 부력 있으면 기포 주머니, 붙어 살면 뿌리
    static void Seaweed(MB b, Strain st, in Morph m, System.Random r)
    {
        var g = st.g;
        if (g[Gn.Dry] >= 0.4f)   // 이끼(육상): 키 작은 줄기 여럿이 쿠션처럼 빽빽이 모여 물을 머금는다. 잎은 줄기에 붙은 작은 비늘
        {
            for (int i = 0; i < 34; i++)
            {
                float a = (float)r.NextDouble() * 6.28f, rr = 0.32f * Mathf.Sqrt((float)r.NextDouble()), h = 0.3f + 0.12f * (float)r.NextDouble() - 0.5f * rr;
                var p0 = new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, -0.5f); var lean = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * rr * 0.3f;
                var pts = new[] { p0, p0 + lean * 0.4f + Vector3.forward * h * 0.5f, p0 + lean + Vector3.forward * h };
                var c = Col(0.28f, 0.5f + 0.15f * (float)r.NextDouble(), 0.18f, 1);
                Tube(b, pts, u => 0.007f, c * 0.75f, 4, SWAY, 0.005f, i, Vector3.right);
                for (int j = 2; j <= 10; j++)
                {
                    var q = Vector3.Lerp(pts[0], pts[2], j / 10.5f); float la = j * 2.4f + i;
                    var ld = new Vector3(Mathf.Cos(la), Mathf.Sin(la), 1.1f).normalized;
                    Ellipsoid(b, q + ld * 0.014f, new Vector3(0.007f, 0.003f, 0.018f) * (1.15f - 0.5f * j / 10f), Quaternion.LookRotation(ld, Vector3.forward), c * (0.9f + 0.02f * j), 4);
                }
            }
            b.Mark("줄기", new Vector3(0, 0, -0.2f)); b.Mark("잎", new Vector3(0.05f, 0, -0.1f)); return;
        }
        var col = Color.Lerp(Col(0.25f, 0.75f, 0.3f, 0.85f), Col(0.75f, 0.25f, 0.3f, 0.85f), g[Gn.Pigment]);   // 색소가 많으면 홍조류
        if (g[Gn.Pigment] > 0.3f && g[Gn.Pigment] < 0.6f) col = Col(0.55f, 0.45f, 0.2f, 0.85f);                 // 중간이면 갈조류 빛
        col = Color.Lerp(col, Hue(HU, 0.55f, 0.75f, 0.85f), 0.2f);                                              // 종마다 조금씩 다른 빛
        float th = 0.012f + 0.02f * g[Gn.Form];
        void Branch(Vector3 p, Vector3 dir, float len, int depth)
        {
            var pts = new Vector3[6]; var side = Vector3.Cross(dir, Vector3.right).normalized; if (side == Vector3.zero) side = Vector3.up;
            for (int i = 0; i < 6; i++) pts[i] = p + dir * len * i / 5f + side * Mathf.Sin(i * 0.8f + depth) * len * 0.06f;
            Tube(b, pts, u => th * (1 - 0.4f * u) * Mathf.Pow(0.7f, 3 - depth), col, 4, SWAY, len * 0.15f, depth, Vector3.right); b.Mark(depth == 3 ? "주축" : "가지", pts[3]);
            if (depth == 0) return;
            int nb = Mathf.Max(1, 2 + r.Next(2) + Mathf.RoundToInt(1.4f * VS[5]));   // 종마다 가지 수·벌림·길이가 다르다
            for (int i = 0; i < nb; i++)
            {
                var d2 = (dir + RandIn(r) * 0.8f * (1 + 0.6f * VS[6])).normalized; if (d2.z < 0.1f) d2.z = 0.3f;
                Branch(pts[2 + r.Next(4)], d2.normalized, len * (0.6f + 0.12f * VS[7]), depth - 1);
            }
        }
        if (g[Gn.Form] < 0.4f) Branch(new Vector3(0, 0, -0.5f), Vector3.forward, 0.45f, 3);
        else
        {
            var stipe = new[] { new Vector3(0, 0, -0.5f), new Vector3(0.02f, 0, -0.25f), new Vector3(0, 0.01f, -0.05f) };
            Tube(b, stipe, u => 0.02f, col * 0.8f, 5, SWAY, 0.02f, 0, Vector3.right); b.Mark("줄기", stipe[1]);
            int nl = Mathf.Max(2, 3 + (int)(g[Gn.Form] * 4) + Mathf.RoundToInt(2.5f * VS[5]));
            for (int i = 0; i < nl; i++)
            {
                float ang = i * 2.4f, w = (0.08f + 0.06f * g[Gn.Form]) * (1 + 0.4f * VS[6]); var dir = new Vector3(Mathf.Cos(ang) * 0.35f, Mathf.Sin(ang) * 0.35f, 1).normalized;
                var basep = stipe[2]; var tip = basep + dir * 0.5f; var side = Vector3.Cross(dir, Vector3.up).normalized * w;
                var poly = new Vector3[8];
                for (int j = 0; j < 4; j++) { float u = (j + 1) / 5f, wv = Mathf.Sin(u * Mathf.PI) * (1 + 0.15f * Mathf.Sin(u * 9)); poly[j] = Vector3.Lerp(basep, tip, u) + side * wv; poly[7 - j] = Vector3.Lerp(basep, tip, u) - side * wv; }
                Fan(b, poly, col, SWAY, 0.06f, Vector3.right, p => Mathf.Clamp01(p.z + 0.5f)); b.Mark("잎", Vector3.Lerp(basep, tip, 0.6f));
                if (g[Gn.Buoy] > 0.3f) Ellipsoid(b, Mk(b, "기포 주머니", Vector3.Lerp(basep, tip, 0.15f)), Vector3.one * 0.03f, Quaternion.identity, Col(0.7f, 0.65f, 0.3f, 0.9f), 5);
            }
        }
        if (g[Gn.Attach] > 0.5f) b.Mark("부착기", new Vector3(0, 0, -0.52f));
        if (g[Gn.Attach] > 0.5f) for (int i = 0; i < 5; i++) { var d = new Vector3(Mathf.Cos(i * 1.26f), Mathf.Sin(i * 1.26f), -0.4f).normalized; Tube(b, new[] { new Vector3(0, 0, -0.5f), new Vector3(0, 0, -0.5f) + d * 0.06f }, u => 0.01f, col * 0.6f, 3, 0, 0, 0, Vector3.zero); }
    }

    // 해면: 꽃병 몸통, 위에 큰 구멍(출수공), 겉에 작은 구멍(입수공). 바닥에 붙어 산다
    static void Sponge(MB b, Strain st, in Morph m, System.Random r)
    {
        var g = st.g; var col = Color.Lerp(Col(0.9f, 0.8f, 0.45f, 0.9f), Col(0.95f, 0.45f, 0.3f, 0.9f), g[Gn.Pigment]);
        float wd = 0.18f + 0.15f * g[Gn.Form];
        Lathe(b, Vector3.zero, Quaternion.identity, -0.5f, 0.5f, z => wd * (0.75f + 0.3f * Mathf.Sin((z + 0.5f) * 2.6f)) * (z > 0.45f ? 0.85f : 1), z => col, 14, 14);
        Lathe(b, Vector3.zero, Quaternion.identity, 0.0f, 0.49f, z => wd * 0.6f, z => col * 0.45f, 4, 12);   // 안쪽 빈 곳
        b.Mark("출수공", new Vector3(0, 0, 0.5f)); b.Mark("몸통", new Vector3(wd * 0.95f, 0, -0.15f));
        for (int i = 0; i < 40; i++)
        {
            float z = -0.4f + 0.8f * (float)r.NextDouble(), ph = (float)r.NextDouble() * 6.28f, rr = wd * (0.75f + 0.3f * Mathf.Sin((z + 0.5f) * 2.6f));
            Ellipsoid(b, Mk(b, "입수공", new Vector3(Mathf.Cos(ph) * rr, Mathf.Sin(ph) * rr, z)), Vector3.one * 0.015f, Quaternion.identity, col * 0.35f, 3);
        }
    }

    // 자포동물형: 붙어 살면 폴립(기둥 + 촉수 고리), 떠다니면 해파리(맥동하는 갓 + 늘어진 촉수)
    static void Cnidarian(MB b, Strain st, in Morph m, System.Random r)
    {
        var g = st.g; var col = Hue(HU, 0.35f, 0.95f, 0.5f); int nt = Mathf.Max(4, 6 + (int)(10 * g[Gn.Gut]) + Mathf.RoundToInt(4 * VS[5])); float tl = 1 + 0.5f * VS[7], bh = 1 + 0.35f * VS[6];
        if (m.sessile)
        {
            void Polyp(Vector3 o, float sc, int i0)
            {
                Lathe(b, o, Quaternion.identity, -0.5f * sc, 0.15f * sc, z => (0.07f + 0.03f * Mathf.Sin(z / sc * 4)) * sc, z => col, 10, 10);
                for (int i = 0; i < nt; i++)
                {
                    float a = i * 2 * Mathf.PI / nt; var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0); var pts = new Vector3[8];
                    for (int j = 0; j < 8; j++) { float u = j / 7f; pts[j] = o + (new Vector3(0, 0, 0.15f) + d * (0.07f + 0.25f * u * tl) + Vector3.forward * (0.3f * tl * Mathf.Sin(u * 2))) * sc; }
                    Tube(b, pts, u => 0.012f * (1 - 0.6f * u) * sc, col, 4, SWAY, 0.06f * sc, i + i0, d); if (i0 == 0) b.Mark("촉수", pts[5]);
                }
            }
            if (g[Gn.Mineral] > 0.4f)   // 산호: 석회 골격 위에 작은 폴립 여럿(군체)
            {
                b.Mark("석회 골격", new Vector3(0.3f, 0, -0.38f));
                Ellipsoid(b, new Vector3(0, 0, -0.42f), new Vector3(0.38f, 0.38f, 0.12f), Quaternion.identity, Col(0.93f, 0.9f, 0.82f, 1), 10, u => 1 + 0.08f * Mathf.Sin(u.x * 9) * Mathf.Sin(u.y * 7));
                for (int p = 0; p < 6; p++) Polyp(p == 0 ? new Vector3(0, 0, -0.12f) : new Vector3(Mathf.Cos(p * 1.26f) * 0.22f, Mathf.Sin(p * 1.26f) * 0.22f, -0.2f), 0.5f, p * 3);
                b.Mark("폴립", new Vector3(0.05f, 0, -0.2f));
            }
            else { Polyp(Vector3.zero, 1, 0); b.Mark("입", new Vector3(0, 0, 0.16f)); b.Mark("몸기둥", new Vector3(0.08f, 0, -0.2f)); }
        }
        else
        {
            int ib = b.v.Count; Lathe(b, Vector3.zero, Quaternion.identity, 0.05f, 0.05f + 0.45f * bh, z => 0.32f / Mathf.Sqrt(bh) * Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow((z - 0.05f) / (0.45f * bh), 2))), z => col, 10, 16, 1, PULSE, 0.06f);   // 갓: 종마다 높이·너비가 다르다
            for (int i = ib; i < b.v.Count; i++) { var p = b.v[i]; b.d[i] = -new Vector3(p.x, p.y, 0).normalized * b.a[i].x; }   // 갓 가장자리가 안쪽으로 오므라든다(이때 물을 밀어낸다)
            b.Mark("갓", new Vector3(0.24f, 0, 0.32f));
            for (int i = 0; i < nt; i++)
            {
                float a = i * 2 * Mathf.PI / nt; var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0); var pts = new Vector3[10];
                for (int j = 0; j < 10; j++) { float u = j / 9f; pts[j] = d * 0.3f / Mathf.Sqrt(bh) + new Vector3(0, 0, 0.05f - 0.55f * tl * u); }
                Tube(b, pts, u => 0.006f, col * 0.9f, 3, SWAY, 0.08f, i * 0.7f, d); b.Mark("촉수", pts[6]);
            }
            Tube(b, new[] { new Vector3(0, 0, 0.1f), new Vector3(0, 0, -0.15f) }, u => 0.04f * (1 - u * 0.5f), col * 1.1f, 6, SWAY, 0.02f, 0, Vector3.right); b.Mark("입자루", new Vector3(0, 0, -0.12f));
        }
    }

    // 붙어 살면 메시 +Z가 위를 본다(EvoTank.Spawn). 위를 +Y로 그린 몸을 그쪽으로 돌린다
    static void Turn(MB b, int i0, Quaternion q) { for (int i = i0; i < b.v.Count; i++) { b.v[i] = q * b.v[i]; b.n[i] = q * b.n[i]; b.d[i] = q * b.d[i]; } }

    // 좌우대칭 무척추: 몸통 하나를 부품(마디·외골격·광물·부속지·턱·눈·독·투명)으로 꾸민다. 껍데기 연체는 Mollusc
    static void Bilaterian(MB b, Strain st, in Morph m, System.Random r, Look lk)
    {
        if (lk == Look.Snail || lk == Look.Clam) { Mollusc(b, st, m, lk); return; }
        var g = st.g; int i0 = b.v.Count;
        bool arth = lk >= Look.Arthropod && lk <= Look.Trilobite, tri = lk == Look.Trilobite, cope = lk == Look.Copepod, arrow = lk == Look.ArrowWorm;
        int ns = Mathf.Clamp(Mathf.RoundToInt(g[Gn.Seg] * (arth ? 2 : 4)), 0, 24);   // 그릴 마디 수
        float flat = lk == Look.Flatworm || tri ? 0.3f : 1;
        bool flatw = lk == Look.Flatworm;
        float th = tri ? 0.24f : cope ? 0.15f : arrow ? 0.05f : arth ? 0.1f : flatw ? 0.12f : 0.5f / Mathf.Max(3, st.aspect * 2);
        float al = 0.92f * (1 - 0.8f * m.transp);   // 투명한 몸은 흐리게(화살벌레·요각류)
        var col = arth ? Color.Lerp(Hue(HU + 25, 0.6f, 0.7f, al), Col(0.8f, 0.75f, 0.62f, al), g[Gn.Mineral] * 0.7f) : Hue(HU, 0.45f, 0.9f, al);
        bool warn = g[Gn.Toxin] > 0.4f;   // 독이 있으면 노란 경고 띠
        Func<float, float> seg = z => ns > 1 ? Mathf.Abs(Mathf.Sin((z + 0.5f) * ns * Mathf.PI)) : 1;   // 마디 안 자리(마디 사이 = 0)
        Func<float, float> prof = cope ? (Func<float, float>)(z => { float u = z + 0.5f; return u > 0.4f ? Mathf.Sqrt(Mathf.Max(0.03f, 1 - Mathf.Pow((u - 0.68f) / 0.3f, 2))) : 0.28f * (0.6f + u); })   // 굵은 머리가슴 + 가는 배
            : tri ? (Func<float, float>)(z => Mathf.Pow(Mathf.Max(0.02f, 1 - Mathf.Pow(z / 0.5f, 2)), 0.35f))   // 넓은 타원 판
            : z => Mathf.Sqrt(Mathf.Max(0.02f, 1 - Mathf.Pow(z / 0.5f, 2)));
        float ra = arth ? 0.1f : 0.07f;
        Lathe(b, Vector3.zero, Quaternion.identity, -0.5f, 0.5f, z => th * prof(z) * (ns > 1 ? 1 - ra + ra * seg(z) : 1),
            z => { var c = col; if (ns > 1 && seg(z) < 0.3f) c *= 0.75f; if (warn && Mathf.Sin(z * 28) > 0.55f) c = Col(0.95f, 0.78f, 0.1f, al); c.a = al; return c; }, 48, 12, flat, 0, 0,
            flatw ? z => th * 2.2f * Mathf.Pow(Mathf.Max(0.02f, 1 - Mathf.Pow(z / 0.5f, 2)), 0.6f) * (1 + 0.06f * Mathf.Sin(z * 45)) : null);   // 납작벌레: 넓은 잎, 물결 가장자리
        if (flatw)
        {
            for (int s = -1; s <= 1; s += 2) Ellipsoid(b, new Vector3(s * th * 0.35f, th * flat * 0.9f, 0.36f), Vector3.one * th * 0.12f, Quaternion.identity, Col(0.05f, 0.05f, 0.05f, 1), 4);
            b.Mark("눈점", new Vector3(th * 0.35f, th * flat, 0.36f)); b.Mark("물결 가장자리", new Vector3(th * 2.2f, 0, -0.05f));
        }
        b.Mark("머리", new Vector3(0, 0, 0.48f)); if (ns > 1) b.Mark(arth ? "마디(외골격)" : "마디", new Vector3(th * prof(-0.1f), 0, -0.1f));
        if (warn) b.Mark("경고색(독)", new Vector3(th * prof(0.1f), 0, 0.1f));
        if (m.transp > 0.3f)   // 비치는 소화관
        {
            Tube(b, new[] { new Vector3(0, 0, 0.42f), new Vector3(0, 0, 0), new Vector3(0, 0, -0.4f) }, u => th * 0.25f, Col(0.65f, 0.5f, 0.3f, 0.6f), 5, 0, 0, 0, Vector3.zero);
            b.Mark("소화관(비침)", new Vector3(0, 0, -0.1f));
        }
        if (tri)   // 삼엽충: 세 엽(가운데 볼록한 축), 머리 방패, 볼가시, 겹눈
        {
            Lathe(b, new Vector3(0, th * flat * 0.55f, 0), Quaternion.identity, -0.45f, 0.4f, z => th * 0.28f * prof(z) * (0.85f + 0.15f * seg(z)), z => col * 0.85f, 30, 8, 0.8f);
            b.Mark("가운데 엽", new Vector3(0, th * flat * 0.9f, 0));
            Ellipsoid(b, Mk(b, "머리 방패", new Vector3(0, th * flat * 0.25f, 0.33f)), new Vector3(th * 1.3f, th * flat * 0.55f, 0.2f), Quaternion.identity, col * 1.05f, 10, u => u.z < -0.2f ? 0.85f : 1);
            for (int s = -1; s <= 1; s += 2)
            {
                Tube(b, new[] { new Vector3(s * th, 0, 0.22f), new Vector3(s * th * 1.05f, 0, 0.05f), new Vector3(s * th * 0.95f, 0, -0.12f) }, u => th * 0.06f * (1 - u), col, 4, 0, 0, 0, Vector3.zero);
                Ellipsoid(b, new Vector3(s * th * 0.45f, th * flat * 0.8f, 0.36f), new Vector3(th * 0.18f, th * 0.12f, th * 0.3f), Quaternion.identity, Col(0.15f, 0.13f, 0.1f, 1), 5);
            }
            b.Mark("볼가시", new Vector3(th * 1.05f, 0, 0.05f)); b.Mark("겹눈", new Vector3(th * 0.45f, th * flat * 0.9f, 0.36f));
        }
        float legL = th * (cope ? 1.2f : tri ? 0.9f : 2.2f) * (0.5f + 0.7f * g[Gn.Limb]);
        if (arth)   // 마디 다리(요각류는 배 밑 헤엄다리)
        {
            int nl = Mathf.Clamp(cope ? 5 : ns, 3, 14);
            for (int i = 0; i < nl; i++)
            {
                float z = cope ? 0.3f - 0.35f * i / (nl - 1) : 0.3f - 0.65f * i / (nl - 1), w = th * prof(z);
                for (int s = -1; s <= 1; s += 2)
                {
                    var p0 = new Vector3(s * w * 0.8f, -th * flat * 0.3f, z); var p1 = p0 + new Vector3(s * legL * 0.55f, legL * 0.15f, 0.01f);
                    Tube(b, cope ? new[] { p0, p0 + new Vector3(s * legL * 0.2f, -legL * 0.6f, -0.03f), p0 + new Vector3(s * legL * 0.3f, -legL, -0.08f) } : new[] { p0, p1, p1 + new Vector3(s * legL * 0.2f, -legL * 0.75f, -0.02f) },
                        u => th * (tri ? 0.08f : 0.07f) * (1 - 0.5f * u), col * 0.9f, 4, SWAY, legL * 0.25f, i * 0.9f, Vector3.forward);
                    if (i == nl / 2 && s == 1) b.Mark(cope ? "헤엄다리" : "마디 다리", p1);
                }
            }
        }
        else if (g[Gn.Limb] >= 0.3f)   // 갯지렁이: 마디마다 옆다리(부채) + 강모 다발
        {
            int nl = Mathf.Clamp(ns, 4, 20);
            for (int i = 0; i < nl; i++)
            {
                float z = 0.4f - 0.85f * i / (nl - 1), w = th * prof(z);
                for (int s = -1; s <= 1; s += 2)
                {
                    var p0 = new Vector3(s * w * 0.9f, 0, z);
                    Ellipsoid(b, p0 + new Vector3(s * th * 0.55f, 0, 0), new Vector3(th * 0.6f, th * 0.22f, Mathf.Min(th * 0.5f, 0.35f / nl)), Quaternion.identity, col * 1.08f, 4);
                    Tube(b, new[] { p0 + new Vector3(s * th * 0.9f, 0, 0), p0 + new Vector3(s * th * 1.9f, 0, -0.01f) }, u => th * 0.04f, Col(0.9f, 0.85f, 0.6f, al), 3, UND, 0.05f, 0, Vector3.right);
                    if (i == nl / 2 && s == 1) b.Mark("옆다리·강모", p0 + new Vector3(th * 1.2f, 0, 0));
                }
            }
        }
        else if (g[Gn.Skel] > 0.2f && !arrow)   // 강모만
            for (int i = 0; i < 20; i++) { float z = -0.4f + 0.8f * i / 19f; for (int s = -1; s <= 1; s += 2) Tube(b, new[] { new Vector3(s * th * 0.9f, 0, z), new Vector3(s * th * 1.9f, 0, z - 0.01f) }, u => th * 0.05f, col * 1.1f, 3, UND, 0.05f, 0, Vector3.right); }
        if (arth || lk == Look.Polychaete)   // 더듬이(요각류는 몸만큼 긴 첫째 더듬이를 옆으로 편다)
        {
            float aL = cope ? 0.75f : tri ? 0.22f : 0.4f;
            for (int s = -1; s <= 1; s += 2)
            {
                var p0 = new Vector3(s * th * 0.3f, th * flat * 0.3f, 0.48f); var pts = new Vector3[6];
                for (int j = 0; j < 6; j++) { float u = j / 5f; pts[j] = cope ? p0 + new Vector3(s * aL * u, aL * 0.08f * Mathf.Sin(u * 3), -aL * 0.25f * u * u) : p0 + new Vector3(s * aL * 0.45f * u, aL * 0.1f * u, aL * u); }
                Tube(b, pts, u => th * (cope ? 0.05f : 0.04f) * (1 - 0.7f * u), col, 3, SWAY, aL * 0.08f, s, Vector3.up);
                if (s == 1) b.Mark(cope ? "첫째 더듬이" : "더듬이", pts[3]);
            }
        }
        if (cope)
        {
            for (int s = -1; s <= 1; s += 2) Tube(b, new[] { new Vector3(s * 0.01f, 0, -0.48f), new Vector3(s * 0.04f, 0, -0.6f) }, u => 0.006f, col, 3, SWAY, 0.02f, s, Vector3.up);
            b.Mark("꼬리 갈래", new Vector3(0.04f, 0, -0.58f));
            Ellipsoid(b, Mk(b, "가운데 눈", new Vector3(0, th * 0.55f, 0.44f)), Vector3.one * th * 0.15f, Quaternion.identity, Col(0.8f, 0.1f, 0.05f, 1), 4);
        }
        else if (g[Gn.Eye] > 0.2f && !tri && !flatw)
            for (int s = -1; s <= 1; s += 2) Ellipsoid(b, s == 1 ? Mk(b, "눈", new Vector3(th * 0.4f, th * flat * 0.7f, 0.42f)) : new Vector3(-th * 0.4f, th * flat * 0.7f, 0.42f), Vector3.one * th * (0.15f + 0.2f * g[Gn.Eye]), Quaternion.identity, Col(0.05f, 0.05f, 0.05f, 1), 4);
        if (arrow)   // 화살벌레: 머리의 잡는 가시 다발, 옆지느러미, 꼬리지느러미
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4; var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                Tube(b, new[] { new Vector3(0, 0, 0.48f) + d * th * 0.6f, new Vector3(0, 0, 0.54f) + d * th * 1.8f, new Vector3(0, 0, 0.6f) + d * th * 1.2f }, u => th * 0.16f * (1 - 0.7f * u), Col(0.9f, 0.9f, 0.85f, 0.9f), 3, 0, 0, 0, Vector3.zero);
            }
            b.Mark("잡는 가시", new Vector3(th * 1.8f, 0, 0.54f));
            for (int s = -1; s <= 1; s += 2) Fan(b, new[] { new Vector3(s * th * 0.5f, 0, -0.05f), new Vector3(s * th * 3.5f, 0, -0.2f), new Vector3(s * th * 0.5f, 0, -0.32f) }, col, UND, 0.05f, Vector3.right, p => Mathf.Clamp01(0.5f - p.z));
            Fan(b, new[] { new Vector3(0, 0, -0.42f), new Vector3(th * 2.5f, 0, -0.55f), new Vector3(0, 0, -0.6f), new Vector3(-th * 2.5f, 0, -0.55f) }, col, UND, 0.05f, Vector3.right, p => Mathf.Clamp01(0.5f - p.z));
            b.Mark("옆지느러미", new Vector3(th * 3, 0, -0.2f)); b.Mark("꼬리지느러미", new Vector3(th * 2, 0, -0.55f));
        }
        else if (g[Gn.Jaw] >= 0.4f)   // 큰턱(절지동물) · 턱(갯지렁이)
        {
            for (int s = -1; s <= 1; s += 2) Tube(b, new[] { new Vector3(s * th * 0.3f, -th * flat * 0.2f, 0.47f), new Vector3(s * th * 0.5f, -th * flat * 0.3f, 0.55f), new Vector3(s * th * 0.12f, -th * flat * 0.3f, 0.6f) }, u => th * 0.1f * (1 - 0.6f * u), Col(0.25f, 0.15f, 0.08f, 1), 4, 0, 0, 0, Vector3.zero);
            b.Mark(arth ? "큰턱" : "턱", new Vector3(th * 0.5f, -th * flat * 0.3f, 0.55f));
        }
        if (!arth)   // 말랑한 몸은 붙은 것까지 한 물결로 흔든다(머리 0 → 꼬리 1)
            for (int i = i0; i < b.v.Count; i++) if (b.a[i].y == 0 || b.a[i].y == UND) { b.a[i] = new Vector4(Mathf.Clamp01(0.5f - b.v[i].z), UND, 0, 0.05f); b.d[i] = Vector3.right; }
        if (m.burrow > 0.5f) b.Mark("굴 파는 몸", new Vector3(th * prof(-0.3f), -th * flat * 0.5f, -0.3f));
    }

    // 연체동물: 고둥(배발 + 나선 껍데기 + 촉수) · 조개(두 장 껍데기 + 발 + 수관). 위 = +Y로 그리고 붙어 살면 돌린다
    static void Mollusc(MB b, Strain st, in Morph m, Look lk)
    {
        var g = st.g; int i0 = b.v.Count;
        var shell = Color.Lerp(Hue(HU + 30, 0.35f, 0.85f, 1), Col(0.92f, 0.88f, 0.8f, 1), 0.5f * g[Gn.Mineral]);
        var band = Color.Lerp(shell, Hue(HU, 0.6f, 0.4f, 1), 0.55f + 0.4f * g[Gn.Pigment]);
        var skin = Hue(HU + 10, 0.3f, 0.75f, 0.9f);
        if (lk == Look.Snail)
        {
            Ellipsoid(b, Mk(b, "배발", new Vector3(0.14f, -0.16f, 0)), new Vector3(0.14f, 0.05f, 0.48f), Quaternion.identity, skin, 8);
            Ellipsoid(b, new Vector3(0, -0.06f, 0.05f), new Vector3(0.1f, 0.08f, 0.26f), Quaternion.identity, skin, 6);   // 내장 덩어리(발과 껍데기를 잇는다)
            Ellipsoid(b, Mk(b, "머리", new Vector3(0, -0.1f, 0.4f)), new Vector3(0.07f, 0.06f, 0.09f), Quaternion.identity, skin, 5);
            var sp = new Vector3[64];   // 나선 껍데기: 꼭지(위)에서 입구(아래)로 감기며 굵어지는 관 하나
            for (int i = 0; i < 64; i++) { float u = i / 63f, a = u * 3.2f * 2 * Mathf.PI, R = 0.02f + 0.11f * Mathf.Pow(u, 1.2f); sp[i] = new Vector3(Mathf.Cos(a) * R, 0.42f - 0.3f * u, -0.05f + Mathf.Sin(a) * R); }
            Tube(b, sp, u => 0.008f + 0.13f * Mathf.Pow(u, 1.3f), shell, 10, 0, 0, 0, Vector3.zero, u => Mathf.Sin(u * 60) > 0.6f ? band : shell);
            b.Mark("나선 껍데기", sp[40] + new Vector3(0.1f, 0, 0)); b.Mark("껍데기 입구", sp[63]);
            for (int s = -1; s <= 1; s += 2)
            {
                var p0 = new Vector3(s * 0.05f, -0.12f, 0.42f); var tip = p0 + new Vector3(s * 0.08f, 0.12f, 0.12f);
                Tube(b, new[] { p0, (p0 + tip) / 2 + new Vector3(0, 0.02f, 0), tip }, u => 0.015f * (1 - 0.5f * u), skin, 4, SWAY, 0.02f, s, Vector3.up);
                if (g[Gn.Eye] > 0.2f) Ellipsoid(b, tip, Vector3.one * 0.015f, Quaternion.identity, Col(0.05f, 0.05f, 0.05f, 1), 4);
                if (s == 1) b.Mark("촉수", tip);
            }
            if (g[Gn.Jaw] >= 0.3f || g[Gn.Herb] >= 0.3f) b.Mark("치설(긁는 혀)", new Vector3(0, -0.17f, 0.47f));
        }
        else
        {
            for (int s = -1; s <= 1; s += 2) Valve(b, s, shell, band);
            Ellipsoid(b, new Vector3(0, -0.01f, 0), new Vector3(0.022f, 0.28f, 0.43f), Quaternion.identity, skin * 0.6f, 8);   // 두 껍데기 사이로 보이는 외투막
            b.Mark("껍데기(두 장)", new Vector3(0.12f, 0, 0)); b.Mark("성장선", new Vector3(0.1f, -0.2f, -0.1f));
            Ellipsoid(b, Mk(b, "각정", new Vector3(0, 0.29f, 0.08f)), new Vector3(0.07f, 0.05f, 0.08f), Quaternion.identity, shell * 0.9f, 6);
            Ellipsoid(b, Mk(b, "발", new Vector3(0, -0.3f, 0.22f)), new Vector3(0.035f, 0.09f, 0.12f), Quaternion.identity, skin, 6);
            float sl = 0.08f + 0.14f * Mathf.Clamp01(m.burrow);   // 파묻혀 살수록 수관이 길다
            for (int s = -1; s <= 1; s += 2)
                Tube(b, new[] { new Vector3(0, 0.05f * s + 0.1f, -0.42f), new Vector3(0, 0.05f * s + 0.1f + sl * 0.5f, -0.42f - sl * 0.6f), new Vector3(0, 0.05f * s + 0.1f + sl, -0.42f - sl * 0.8f) }, u => 0.026f * (1 - 0.3f * u), skin, 5, SWAY, 0.02f, s, Vector3.up);
            b.Mark("수관", new Vector3(0, 0.15f + sl * 0.5f, -0.42f - sl * 0.6f));
        }
        if (m.sessile) Turn(b, i0, Quaternion.FromToRotation(Vector3.up, Vector3.forward));
    }

    // 조개 껍데기 한 장: 얕은 돔, 각정(위 앞)에서 퍼지는 동심원 성장선 색 띠
    static void Valve(MB b, int s, Color c0, Color c1)
    {
        const int NR = 24, NA = 48; int i0 = b.v.Count; var um = new Vector2(0.9f, 0.15f);
        for (int i = 0; i <= NR; i++) for (int j = 0; j <= NA; j++)
        {
            float rho = i / (float)NR, a = 2 * Mathf.PI * j / NA, Y = rho * Mathf.Cos(a), Z = rho * Mathf.Sin(a), q = Mathf.Sqrt(Mathf.Max(0.04f, 1 - rho * rho));
            var p = new Vector3(s * (0.015f + 0.085f * q), 0.3f * Y, 0.46f * Z);
            var nn = new Vector3(s, 0.085f * Y / q / 0.3f, 0.085f * Z / q / 0.46f).normalized;
            b.V(p, nn, Mathf.Repeat(Vector2.Distance(new Vector2(Y, Z), um) * 5, 1) < 0.3f ? c1 : c0, default, nn);
        }
        for (int i = 0; i < NR; i++) for (int j = 0; j < NA; j++) { int p0 = i0 + i * (NA + 1) + j, p1 = p0 + NA + 1; b.Tri(p0, p1, p0 + 1); b.Tri(p0 + 1, p1, p1 + 1); }
    }

    // 극피동물: 다섯 갈래 팔(불가사리) 또는 가시 공(성게). 위에 광물 골판 혹, 아래에 관족
    static void Echino(MB b, Strain st, in Morph m, System.Random r)
    {
        var g = st.g; int i0 = b.v.Count;
        var col = Hue(HU + 15, 0.55f, 0.85f, 1); var knob = Color.Lerp(col, Col(0.95f, 0.9f, 0.8f, 1), 0.5f);
        if (g[Gn.Limb] >= 0.3f)
        {
            Ellipsoid(b, Mk(b, "가운데 원반", new Vector3(0, 0.06f, 0)), new Vector3(0.15f, 0.06f, 0.15f), Quaternion.identity, col, 8);
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.PI / 2 + i * 2 * Mathf.PI / 5; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var pts = new Vector3[6]; for (int j = 0; j < 6; j++) { float u = j / 5f; pts[j] = d * (0.05f + 0.45f * u) + Vector3.up * (0.02f * Mathf.Sin(u * 3 + i)); }
                Tube(b, pts, u => 0.07f * (1 - 0.8f * u), col, 6, SWAY, 0.015f, i, Vector3.up);
                for (int j = 1; j < 5; j++) { float u = j / 5f; Ellipsoid(b, pts[j] + Vector3.up * 0.065f * (1 - 0.8f * u), Vector3.one * 0.018f, Quaternion.identity, knob, 3); }
                for (int j = 1; j < 6; j++) { var p = d * (0.05f + 0.08f * j); float ar = 0.07f * (1 - 0.8f * (0.08f * j / 0.45f)); Tube(b, new[] { p - Vector3.up * ar * 0.6f, p - Vector3.up * (ar + 0.03f) }, u => 0.006f, Col(0.95f, 0.85f, 0.7f, 0.9f), 3, SWAY, 0.01f, j + i, Vector3.forward); }
                if (i == 0) { b.Mark("팔", pts[3]); b.Mark("골판 혹", pts[2] + Vector3.up * 0.06f); b.Mark("관족", d * 0.25f - Vector3.up * 0.07f); }
            }
        }
        else
        {
            Ellipsoid(b, Mk(b, "골판 껍데기", new Vector3(0.25f, 0.05f, 0)), new Vector3(0.25f, 0.18f, 0.25f), Quaternion.identity, col, 10);
            for (int i = 0; i < 70; i++)
            {
                var u = Fib(i, 80); var p = Vector3.Scale(u, new Vector3(0.25f, 0.18f, 0.25f));
                Tube(b, new[] { p, p + u * (0.16f + 0.1f * g[Gn.Mineral]) }, t => 0.012f * (1 - 0.8f * t), knob, 3, SWAY, 0.02f, i, Vector3.up);
                if (i == 10) b.Mark("가시", p + u * 0.12f);
            }
            for (int i = 0; i < 10; i++) { float a = i * 0.63f; var p = new Vector3(Mathf.Cos(a) * 0.15f, -0.17f, Mathf.Sin(a) * 0.15f); Tube(b, new[] { p, p - Vector3.up * 0.05f }, u => 0.006f, Col(0.95f, 0.85f, 0.7f, 0.9f), 3, SWAY, 0.01f, i, Vector3.forward); }
            b.Mark("관족", new Vector3(0.15f, -0.2f, 0));
        }
        b.Mark("입(아래)", new Vector3(0, -0.08f, 0));
        if (m.sessile) Turn(b, i0, Quaternion.FromToRotation(Vector3.up, Vector3.forward));
    }

    // 척삭동물·어류: 몸통 하나에 부품. 창고기(옆으로 납작, 지느러미막) → 어류(방추형, 지느러미). 칠성장어는 턱 없이 빨판 입·짝지느러미 없음,
    // 갑주어·판피어는 머리 갑옷, 상어는 잿빛 + 위 꼬리가 긴 꼬리 + 아가미 틈, 경골어류는 아가미덮개. 지느러미 크기는 부속지 유전자
    static void Chordate(MB b, Strain st, in Morph m, System.Random r, Look lk)
    {
        var g = st.g; bool fish = lk >= Look.Jawless, shark = lk == Look.Shark, jawless = lk == Look.Jawless || lk == Look.Armored, armor = lk == Look.Armored || lk == Look.Placoderm;
        float fishness = fish ? 1 : Mathf.Clamp01((g[Gn.Skel] - 0.4f) / 0.5f) * 0.5f;
        float depth = Mathf.Lerp(0.07f, 0.13f + 0.06f * g[Gn.Form], fishness) * (lk == Look.Jawless ? 0.6f : 1), width = depth * Mathf.Lerp(0.45f, 0.55f, fishness) * (armor ? 1.3f : 1);
        float al = 1 - 0.7f * m.transp;
        Color top = shark ? Col(0.42f, 0.47f, 0.52f, al) : Hue(HU, 0.5f, 0.45f, al), belly = Col(0.92f, 0.94f, 0.97f, al), mid = shark ? Col(0.55f, 0.6f, 0.65f, al) : Hue(HU, 0.25f, 0.85f, al);
        bool eel = lk == Look.Jawless;   // 칠성장어: 뱀장어 같은 긴 몸
        if (armor) { top = Color.Lerp(top, Col(0.5f, 0.45f, 0.38f, al), 0.6f); mid = Color.Lerp(mid, Col(0.75f, 0.7f, 0.6f, al), 0.5f); }
        if (!fish) { top = Hue(HU, 0.2f, 0.85f, 0.75f * al); mid = Col(0.92f, 0.9f, 0.86f, 0.75f * al); }   // 창고기: 반투명한 옅은 몸
        float stripes = g[Gn.Pigment];
        Func<float, float> prof = z =>
        {
            float u = (z + 0.5f);   // 0 꼬리 → 1 머리
            float body = eel ? Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.4f) : fish ? Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.75f)), 0.9f) : Mathf.Sin(Mathf.PI * u);
            float ped = fish && !eel ? Mathf.Lerp(0.25f, 1, Mathf.SmoothStep(0, 1, u / 0.3f)) : 1;   // 꼬리자루(Unity SmoothStep은 from·to 보간이라 0–1로 넣는다)
            return Mathf.Max(0.004f, body * ped);
        };
        Lathe(b, Vector3.zero, Quaternion.identity, -0.5f, 0.5f, z => depth * prof(z), z =>
        {
            var c = Color.Lerp(mid, top, 0.5f); if (stripes > 0.3f && Mathf.Sin(z * 40) > 0.7f) c = Color.Lerp(c, top * 0.5f, stripes); c.a = al; return c;
        }, 36, 14, 1, UND, 0.06f, z => width * prof(z));
        // 배 쪽 은빛: 아래 반쪽을 밝게 칠한 두 번째 띠
        Lathe(b, new Vector3(0, -depth * 0.25f, 0), Quaternion.identity, -0.42f, 0.45f, z => depth * prof(z) * 0.72f, z => belly, 20, 10, 1, UND, 0.06f, z => width * prof(z) * 0.95f);
        if (fish) b.Mark("은빛 배", new Vector3(width * 0.7f, -depth * 0.55f, 0.05f));
        if (!fish) b.Mark("척삭", new Vector3(0, depth * 0.2f, -0.1f));
        if (!fish) Tube(b, new[] { new Vector3(0, depth * 0.2f, -0.48f), new Vector3(0, depth * 0.2f, 0), new Vector3(0, depth * 0.2f, 0.45f) }, u => depth * 0.08f, Col(1, 1, 0.9f, 1), 4, UND, 0.06f, 0, Vector3.right);
        if (armor)
        {
            Color bone = Col(0.72f, 0.66f, 0.55f, 1), boneD = Col(0.45f, 0.4f, 0.33f, 1);
            if (lk == Look.Armored)   // 갑주어: 넓적한 말굽 머리 방패
            {
                b.Mark("머리 방패", new Vector3(width * 2f, depth * 0.15f, 0.3f));
                Ellipsoid(b, new Vector3(0, depth * 0.15f, 0.3f), new Vector3(width * 2.1f, depth * 0.55f, 0.22f), Quaternion.identity, bone, 10, u => u.z < -0.5f ? 0.9f : 1, new Vector4(0.15f, UND, 0, 0.06f));
            }
            else   // 판피어: 머리 + 가슴을 두른 갑옷판, 판 사이 이음선
            {
                b.Mark("머리 갑옷", new Vector3(width * 1.3f, depth * 0.4f, 0.38f));
                Ellipsoid(b, new Vector3(0, 0, 0.36f), new Vector3(width * prof(0.36f) * 1.25f, depth * prof(0.36f) * 1.12f, 0.15f), Quaternion.identity, bone, 10, null, new Vector4(0.12f, UND, 0, 0.06f));
                Lathe(b, Vector3.zero, Quaternion.identity, 0.04f, 0.26f, z => depth * prof(z) * 1.1f, z => Mathf.Repeat(z * 14, 1) < 0.12f ? boneD : bone, 10, 14, 1, UND, 0.06f, z => width * prof(z) * 1.15f);
                b.Mark("가슴 갑옷", new Vector3(width * prof(0.15f) * 1.15f, 0, 0.15f));
            }
        }
        Func<Vector3, float> U = p => Mathf.Clamp01(0.5f - p.z);
        float fin = Mathf.Lerp(0.3f, 1, fishness) * (0.5f + 0.5f * g[Gn.Skel]) * (0.7f + 0.6f * g[Gn.Limb]);
        var finC = shark ? Col(0.4f, 0.45f, 0.5f, 0.85f * al) : Hue(HU, 0.4f, 0.7f, 0.75f * al);
        // 꼬리지느러미(상어는 위쪽 잎이 길다)
        float tz = -0.5f, th2 = depth * 1.4f * fin; b.Mark("꼬리지느러미", new Vector3(0, fish ? th2 * 0.6f : depth * 0.3f, tz - (fish ? 0.1f : 0.02f)));
        Fan(b, eel ? new[] { new Vector3(0, depth * 0.5f, tz + 0.15f), new Vector3(0, depth * 0.75f, tz + 0.02f), new Vector3(0, 0, tz - 0.05f), new Vector3(0, -depth * 0.6f, tz + 0.02f), new Vector3(0, -depth * 0.4f, tz + 0.12f) }
            : fish ? new[] { new Vector3(0, 0, tz + 0.04f), new Vector3(0, th2 * (shark ? 1.35f : 1), tz - (shark ? 0.17f : 0.12f)), new Vector3(0, th2 * 0.25f, tz - 0.06f), new Vector3(0, -th2 * 0.25f, tz - 0.06f), new Vector3(0, -th2 * (shark ? 0.6f : 1), tz - (shark ? 0.08f : 0.12f)) }
            : new[] { new Vector3(0, depth * 0.6f, tz + 0.15f), new Vector3(0, 0, tz - 0.06f), new Vector3(0, -depth * 0.6f, tz + 0.15f) }, finC, UND, 0.06f, Vector3.right, U);
        if (fish)
        {
            b.Mark("등지느러미", new Vector3(0, depth * (0.85f + 0.6f * fin), 0)); b.Mark("뒷지느러미", new Vector3(0, -depth * (0.7f + 0.4f * fin), -0.19f));
            if (eel) Fan(b, new[] { new Vector3(0, depth * 0.9f, 0.05f), new Vector3(0, depth * 1.5f, -0.1f), new Vector3(0, depth * 1.35f, -0.3f), new Vector3(0, depth * 0.6f, -0.36f) }, finC, UND, 0.06f, Vector3.right, U);   // 낮고 긴 등지느러미
            else
            {
                Fan(b, new[] { new Vector3(0, depth * prof(0.14f) * 0.88f, 0.14f), new Vector3(0, depth * (0.85f + (shark ? 1.3f : 0.9f) * fin), shark ? 0.05f : 0.0f), new Vector3(0, depth * prof(-0.16f) * 0.88f, -0.16f) }, finC, UND, 0.06f, Vector3.right, U);   // 등지느러미
                Fan(b, new[] { new Vector3(0, -depth * 0.7f, -0.12f), new Vector3(0, -depth * (0.7f + 0.6f * fin), -0.2f), new Vector3(0, -depth * 0.55f, -0.26f) }, finC, UND, 0.06f, Vector3.right, U);   // 뒷지느러미
            }
            if (lk > Look.Armored)   // 칠성장어·갑주어는 짝지느러미가 없다
            {
                b.Mark("가슴지느러미", new Vector3(width * (0.9f + 1.4f * fin), -depth * 0.6f, 0.13f)); b.Mark("배지느러미", new Vector3(width * (0.6f + 0.7f * fin), -depth * 1.05f, -0.04f));
                float pf = shark ? 1.5f : 1;
                for (int s = -1; s <= 1; s += 2)
                {
                    Fan(b, new[] { new Vector3(s * width * 0.8f, -depth * 0.3f, 0.22f), new Vector3(s * width * (0.9f + 2 * fin * pf), -depth * 0.7f, 0.1f), new Vector3(s * width * 0.8f, -depth * 0.5f, 0.15f) }, finC, SWAY, 0.03f, Vector3.up, p => 0.5f);   // 가슴지느러미
                    Fan(b, new[] { new Vector3(s * width * 0.5f, -depth * 0.8f, 0.0f), new Vector3(s * width * (0.6f + fin), -depth * 1.2f, -0.06f), new Vector3(s * width * 0.5f, -depth * 0.8f, -0.05f) }, finC, UND, 0.06f, Vector3.right, U);   // 배지느러미
                }
            }
        }
        else { b.Mark("지느러미막", new Vector3(0, depth * 1.25f, -0.1f)); Fan(b, new[] { new Vector3(0, depth * 0.95f, 0.3f), new Vector3(0, depth * 1.3f, 0.0f), new Vector3(0, depth * 1.3f, -0.3f), new Vector3(0, depth * 0.5f, -0.48f) }, finC, UND, 0.06f, Vector3.right, U); }
        // 아가미: 경골어류·판피어는 덮개 선, 상어·칠성장어·창고기는 틈(구멍)
        if (g[Gn.Circ] > 0.3f)
        {
            bool cover = fish && !shark && !jawless; int ng = cover ? 1 : lk == Look.Jawless ? 7 : 5;
            for (int i = 0; i < ng; i++) for (int s = -1; s <= 1; s += 2)
            {
                float z = cover ? 0.3f : 0.32f - i * (lk == Look.Jawless ? 0.03f : 0.04f), rr = depth * prof(z) * (cover ? 0.9f : lk == Look.Jawless ? 0.15f : 0.6f);
                if (s == 1 && i == 0) b.Mark(cover ? "아가미덮개" : "아가미 틈", new Vector3(width * prof(z), 0, z));
                Tube(b, new[] { new Vector3(s * width * prof(z) * 1.01f, -rr, z), new Vector3(s * width * prof(z) * 1.05f, 0, z + 0.02f * (rr > 0.01f ? 1 : 0)), new Vector3(s * width * prof(z) * 1.01f, rr * 0.7f + 0.002f, z) }, u => depth * 0.03f, Col(0.5f, 0.15f, 0.15f, 1), 3, UND, 0.06f, 0, Vector3.right);
            }
        }
        // 입: 턱 없는 무리는 둥근 빨판 입, 턱이 있으면 입꼬리 선
        if (eel) { Ellipsoid(b, Mk(b, "빨판 입", new Vector3(0, -depth * 0.1f, 0.5f)), new Vector3(depth * 0.45f, depth * 0.45f, 0.015f), Quaternion.identity, Col(0.6f, 0.35f, 0.32f, 1), 8); Ellipsoid(b, new Vector3(0, -depth * 0.1f, 0.506f), new Vector3(depth * 0.25f, depth * 0.25f, 0.01f), Quaternion.identity, Col(0.2f, 0.08f, 0.08f, 1), 6); }
        else if (fish && !jawless)
        {
            for (int s = -1; s <= 1; s += 2) Tube(b, new[] { new Vector3(s * width * prof(0.47f) * 1.02f, -depth * 0.12f, 0.49f), new Vector3(s * width * prof(0.42f) * 1.02f, -depth * 0.28f, 0.42f) }, u => depth * (lk == Look.Placoderm ? 0.07f : 0.03f), lk == Look.Placoderm ? Col(0.45f, 0.4f, 0.33f, 1) : Col(0.2f, 0.1f, 0.1f, 1), 3, UND, 0.06f, 0, Vector3.right);
            b.Mark(lk == Look.Placoderm ? "턱(뼈 날)" : "턱", new Vector3(width * prof(0.44f), -depth * 0.22f, 0.44f));
        }
        // 눈
        if (!fish) Ellipsoid(b, Mk(b, "안점", new Vector3(0, depth * 0.3f, 0.47f)), Vector3.one * depth * 0.12f, Quaternion.identity, Col(0.1f, 0.1f, 0.1f, 1), 4, null, new Vector4(0.03f, UND, 0, 0.06f));   // 창고기는 눈 대신 색소 점
        else if (g[Gn.Eye] > 0.25f)
            for (int s = -1; s <= 1; s += 2)
            {
                float ez = 0.38f, er = depth * (0.12f + 0.2f * g[Gn.Eye]) * (shark || lk == Look.Armored ? 0.6f : eel || lk == Look.Placoderm ? 0.7f : 1);
                var ep = lk == Look.Armored ? new Vector3(s * width * 0.45f, depth * 0.68f, 0.4f) : new Vector3(s * width * prof(ez) * (lk == Look.Placoderm ? 1.3f : 0.9f), depth * 0.25f, ez); b.Mark("눈", new Vector3(width * prof(ez) * 0.9f, depth * 0.25f, ez));
                Ellipsoid(b, ep, new Vector3(0.5f, 1, 1) * er, Quaternion.identity, Col(0.9f, 0.85f, 0.5f, 1), 5, null, new Vector4(0.12f, UND, 0, 0.06f));
                Ellipsoid(b, ep + new Vector3(s * er * 0.3f, 0, 0), new Vector3(0.4f, 0.65f, 0.65f) * er, Quaternion.identity, Col(0.02f, 0.02f, 0.03f, 1), 5, null, new Vector4(0.12f, UND, 0, 0.06f));
            }
    }
}
