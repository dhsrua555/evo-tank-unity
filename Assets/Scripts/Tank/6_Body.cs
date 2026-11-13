// 6단계 — 몸 그물: 그 종 대표 계통의 세포 배치(Body.cells, μm)와 형질(Traits)로 세포 하나하나를 그린다. 미리 정한 생물 모양은 없다.
// 단위 μm, 무게중심이 원점, +Z = 앞(헤엄 방향). 꼭짓점: 색(a = 불투명), uv0 = (몸 따라 u, 움직임 종류, 위상, 진폭), uv1 = 흔들 방향 — EvoAnim.cginc가 움직인다.
// 보이는 것(형질 → 모양):
//   세포막 = 종 색 반투명 껍질, 벽이 약하면 출렁인다 · 세포벽(벽 단백질 몫) = 바깥 겹 껍질 · 유전체 덩이 = 가운데 옅은 푸른 덩이
//   광계 알갱이: 산소 광합성(광계 산화력 ≥ 0.92 V)은 청록, 아니면 자주(박테리오클로로필) — 광계 몫이 클수록 많다
//   황 알갱이: 황화물을 쓰는 화학합성(공여체 선호 0.48–0.75)은 세포 안에 S⁰ 방울을 쌓는다(Schulz & Jørgensen 2001)
//   녹 껍질: Fe²⁺를 쓰는 화학합성(선호 > 0.75)은 겉에 산화철이 앉는다(Emerson et al. 2010) · 먹이 주머니: 포식 막 몫
//   편모: 편모 몫이 클수록 길고 많다(홑세포는 뒤쪽 끝에서, 덩이는 겉 세포가 바깥쪽으로)
using System;
using System.Collections.Generic;
using Life;
using UnityEngine;

namespace Tank
{
    public sealed partial class Tank
    {
        const float FLAG = 1, WOB = 6, STREAM = 7;   // 움직임 종류(EvoAnim.cginc와 같음): 편모, 막 출렁임, 세포질 흐름
        // 종마다 그물 하나: 대표 계통의 몸이 바뀌면 다시 만들되 같은 종은 1초에 한 번까지(중립 표류·정착으로 몸이 사진마다 바뀌기도 한다).
        // 예전엔 몸마다 따로 두어 표가 금세 400을 넘어 통째로 비우고 보이는 종을 다 다시 만들었다: 사용자 지구에서 5초에 300번, 프레임 20–40 ms,
        // 그물 재료 할당 ~200 MB/s → Boehm GC가 시뮬 스레드까지 늦춤(GC를 끄면 시뮬 +50 %). 재료 줄(MB)도 하나를 비워 다시 쓴다
        sealed class BM { public Body body; public Mesh m; public float built; public int used; }
        readonly Dictionary<int, BM> bodyMeshes = new(); readonly MB mb = new(); readonly List<int> stale = new();

        sealed class MB
        {
            public readonly List<Vector3> v = new(), n = new(), d = new(); public readonly List<Color> c = new(); public readonly List<Vector4> a = new(); public readonly List<int> t = new();
            public void V(Vector3 p, Vector3 nn, Color col, Vector4 anim, Vector3 dir) { v.Add(p); n.Add(nn); c.Add(col); a.Add(anim); d.Add(dir); }
            public void Tri(int i, int j, int k) { t.Add(i); t.Add(j); t.Add(k); }
            public void Clear() { v.Clear(); n.Clear(); d.Clear(); c.Clear(); a.Clear(); t.Clear(); }
            public Mesh Mesh()
            {
                var m = new Mesh(); if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetUVs(0, a); m.SetUVs(1, d); m.SetTriangles(t, 0); m.RecalculateBounds();
                return m;
            }
        }

        Mesh BodyMesh(SpView s)
        {
            float now = Time.unscaledTime; int f = Time.frameCount;
            if (bodyMeshes.TryGetValue(s.id, out var e) && (e.body == s.body || now - e.built < 1)) { e.used = f; return e.m; }
            long t0 = Perf.T(); try {
            if (e == null && bodyMeshes.Count > 400)   // 2초 넘게 안 그린 종의 그물을 버린다
            {
                stale.Clear(); foreach (var kv in bodyMeshes) if (now - kv.Value.built > 2 && f - kv.Value.used > 60) stale.Add(kv.Key);
                foreach (int k in stale) { Destroy(bodyMeshes[k].m); bodyMeshes.Remove(k); }
            }
            var t = s.t; var c = s.body.cells; int n = Mathf.Clamp(Mathf.RoundToInt((float)t.MeanCells), 1, c.Length); float um = (float)s.body.rc;   // 배치는 세포 반지름 1 단위 → μm
            Vector3 cen = Vector3.zero; for (int i = 0; i < n; i++) cen += new Vector3(c[i].x, c[i].y, c[i].z) * um; cen /= n;
            int detail = n <= 8 ? 2 : n <= 64 ? 1 : 0;
            var b = mb; b.Clear(); var r = new System.Random(s.id * 31 + n); Color col = Draw.Of(t, s.id);
            for (int i = 0; i < n; i++)
            {
                var o = new Vector3(c[i].x, c[i].y, c[i].z) * um - cen;
                bool inner = n > 1 && c[i].outer < 0.5f;
                Color cc = inner ? Color.Lerp(col, new Color(0.95f, 0.85f, 0.4f), (float)t.diff) : col;   // 분화한 속 세포는 노르스름
                var q = n == 1 ? Quaternion.identity : Quaternion.Euler(r.Next(360), r.Next(360), 0);
                Vector3 outDir = n == 1 || o.sqrMagnitude < 1e-6f ? Vector3.back : o.normalized;
                float asp = n == 1 ? (float)t.aspect : 1 + ((float)t.aspect - 1) * 0.4f;
                Cell(b, o, c[i].r * um, asp, q, detail, t, cc, r, n == 1, outDir, !inner && detail > 0);
            }
            var m = b.Mesh();
            if (e == null) bodyMeshes[s.id] = e = new BM(); else Destroy(e.m);
            e.body = s.body; e.m = m; e.built = now; e.used = f; return m;
            } finally { Perf.Add(6, t0); }
        }

        // 세포 하나: 반지름 R(μm), 길쭉함 asp(부피는 같게), detail 2 전부 · 1 막+알갱이+편모 · 0 막만
        static void Cell(MB b, Vector3 ctr, float R, float asp, Quaternion q, int detail, Traits t, Color col, System.Random r, bool solo, Vector3 outDir, bool flag)
        {
            var rad = new Vector3(R * Mathf.Pow(asp, -1 / 3f), R * Mathf.Pow(asp, -1 / 3f), R * Mathf.Pow(asp, 2 / 3f));
            float rmin = rad.x, wall = (float)t.Sat(G.WALL, 0.05), photo = (float)t.Sat(G.PHOTO, 0.05), chemo = (float)t.Sat(G.CHEMO, 0.05), phago = (float)t.Sat(G.PHAGO, 0.05), mot = (float)t.Sat(G.MOT, 0.01);
            // 세포막: 벽이 약하면 출렁이고, 포식 막이 많으면 울퉁불퉁(위족)
            var wob = solo && wall < 0.5f ? new Vector4(0, WOB, r.Next(1000) * 0.01f, R * (0.012f + 0.04f * phago)) : default;
            int s1 = r.Next(1000);
            Func<Vector3, float> bump = phago > 0.5f && wall < 0.5f ? u => 1 + 0.25f * (phago - 0.5f) * Mathf.Max(0, Mathf.Sin(u.x * 3.1f + s1) * Mathf.Sin(u.y * 2.7f + s1 * 0.3f) * Mathf.Sin(u.z * 2.3f)) * 3 : null;
            var cyto = col; cyto.a = 0.35f;
            Ellipsoid(b, ctr, rad, q, cyto, detail == 0 ? 5 : detail == 1 ? 8 : 12, bump, wob);
            if (wall > 0.3f && detail > 0) Ellipsoid(b, ctr, rad * 1.08f, q, new Color(0.85f, 1, 0.85f, 0.06f + 0.14f * wall), 8);
            Vector4 Flow() => solo ? new Vector4(0, STREAM, (float)r.NextDouble() * 6.3f, (asp >= 1 ? 1 : -1) * (0.15f + 0.35f * (float)r.NextDouble())) : default;   // 알갱이가 긴 축 둘레를 돈다
            if (detail > 0 && photo > 0.2f)
            {
                bool oxy = t.eox >= 0.92;
                var pig = oxy ? new Color(0.3f, 0.95f, 0.45f, 0.85f) : new Color(0.75f, 0.35f, 0.75f, 0.85f);
                int nc = Mathf.Clamp(Mathf.RoundToInt(photo * (2 + R / 2)), 1, detail == 1 ? 3 : 20);
                for (int i = 0; i < nc; i++)
                    Ellipsoid(b, ctr + q * Vector3.Scale(RandIn(r) * 0.55f, rad), new Vector3(0.2f, 0.08f, 0.15f) * rmin * (nc == 1 ? 2.2f : 1.3f), q * Quaternion.Euler(r.Next(180), r.Next(180), 0), pig, 5, null, Flow());
            }
            if (detail > 0 && chemo > 0.3f && t.dnr >= 0.48 && t.dnr < 0.75)   // 황 방울
                for (int i = 0, ns = 2 + (int)(chemo * 6); i < ns; i++)
                    Ellipsoid(b, ctr + q * Vector3.Scale(RandIn(r) * 0.6f, rad), Vector3.one * rmin * 0.12f, q, new Color(1, 0.97f, 0.75f, 0.9f), 4, null, Flow());
            if (detail > 0 && chemo > 0.3f && t.dnr >= 0.75)   // 겉의 산화철
                for (int i = 0, nf = 3 + (int)(chemo * 5); i < nf; i++)
                {
                    var u = Fib(i, nf); var p = ctr + q * Vector3.Scale(u, rad) * 1.06f;
                    Ellipsoid(b, p, Vector3.one * rmin * 0.1f, q, new Color(0.85f, 0.42f, 0.15f, 0.5f), 3);
                }
            if (detail == 2)
            {
                Ellipsoid(b, ctr + q * new Vector3(0, 0, -0.1f * rad.z), Vector3.one * rmin * 0.32f, q, new Color(0.65f, 0.7f, 1, 0.4f), 6, null, Flow());   // 유전체 덩이
                if (phago > 0.3f)
                    for (int i = 0, nf = 1 + (int)(phago * 3); i < nf; i++)
                        Ellipsoid(b, ctr + q * Vector3.Scale(RandIn(r) * 0.5f, rad), Vector3.one * rmin * 0.16f, q, new Color(0.85f, 0.65f, 0.4f, 0.55f), 4, null, Flow());
            }
            // 편모: 몸길이의 2–5배, 뒤로 끌고 가며 민다
            if (flag && mot > 0.3f)
            {
                int nf = solo ? (mot > 0.7f ? 2 : 1) : 1; float len = 2 * rad.z * (2 + 3 * mot), w = Mathf.Max(rmin * 0.05f, 0.03f);
                Vector3 axis = solo ? q * Vector3.back : outDir, root = solo ? ctr + q * new Vector3(0, 0, -rad.z) : ctr + outDir * R;
                var side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                for (int k = 0; k < nf; k++)
                {
                    var ax = nf == 1 ? axis : Quaternion.AngleAxis(k == 0 ? 18 : -18, side) * axis;
                    var pts = new Vector3[14]; for (int i = 0; i < pts.Length; i++) pts[i] = root + ax * (len * i / (pts.Length - 1f));
                    Tube(b, pts, u => w * (1 - 0.5f * u), new Color(0.85f, 0.95f, 1, 0.6f), 4, FLAG, len * 0.12f, k * 1.7f + (float)r.NextDouble(), Vector3.Cross(ax, side));
                }
            }
        }

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
                    var p = Vector3.Scale(u, rad) * (bump == null ? 1 : bump(u));
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

        // 관(편모): 점들을 잇고 반지름 rad(u)
        static void Tube(MB b, IList<Vector3> pts, Func<float, float> rad, Color col, int sides, float type, float amp, float phase, Vector3 dir)
        {
            int n = pts.Count, i0 = b.v.Count;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                var fw = (pts[Mathf.Min(n - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]).normalized;
                var s1 = Vector3.Cross(fw, Mathf.Abs(fw.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var s2 = Vector3.Cross(fw, s1);
                for (int j = 0; j <= sides; j++)
                {
                    float ph = 2 * Mathf.PI * j / sides; var nn = s1 * Mathf.Cos(ph) + s2 * Mathf.Sin(ph);
                    b.V(pts[i] + nn * rad(u), nn, col, new Vector4(u, type, phase, amp), dir);
                }
            }
            for (int i = 0; i < n - 1; i++) for (int j = 0; j < sides; j++)
            {
                int p0 = i0 + i * (sides + 1) + j, p1 = p0 + sides + 1;
                b.Tri(p0, p1, p0 + 1); b.Tri(p0 + 1, p1, p1 + 1);
            }
        }

        static Vector3 Fib(int i, int n) { float y = 1 - 2 * (i + 0.5f) / n, rr = Mathf.Sqrt(1 - y * y), th = i * 2.39996f; return new Vector3(Mathf.Cos(th) * rr, y, Mathf.Sin(th) * rr); }
        static Vector3 RandIn(System.Random r) { Vector3 p; do p = new Vector3((float)r.NextDouble() * 2 - 1, (float)r.NextDouble() * 2 - 1, (float)r.NextDouble() * 2 - 1); while (p.sqrMagnitude > 1); return p; }
    }
}
