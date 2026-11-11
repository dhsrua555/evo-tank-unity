// 발생: 세포 하나에서 시작해 유전자 규칙대로 분열·붙음을 되풀이한 배치. 정해 둔 몸 형태는 없다.
// 가지치기 β(1 = 사슬, 0.5 = 두 갈래 나무, 0 = 한 세포 둘레에 다닥다닥)와 분열 각 θ(0 = 곧은 실, 큼 = 뭉침)가 연속이라
// 사슬 → 가지 → 덩이가 매끄럽게 이어진다. 생태에는 배치에서 잰 물리값(크기·겉 세포 비율)만 넘긴다.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Life
{
    public struct CellPos { public float x, y, z, r, outer; public int parent; }

    public sealed class Body
    {
        public const int NDRAW = 256;            // 배치를 실제로 계산하는 세포 수 상한(넘으면 크기만 늘린다)
        public CellPos[] cells;                  // 그릴 배치(첫 세포가 원점, 단위 μm)
        public double size;                      // 몸 크기(μm): 평균 덩이의 지름
        public double exposed;                   // 겉에 드러난 세포 비율(빛·영양을 바로 받는 몫)
        public double dim;                       // 배치의 프랙털 차원(1 실 · 2 판 · 3 덩이)
        public double esd;                       // 같은 부피 공의 지름(μm) = 세포 하나 크기 × 세포 수^(1/3). 포식의 크기(먹는 쪽·먹히는 쪽)는 이것으로 센다: 크기비 자료(Hansen 1994)가 ESD로 잰 값
        public double feed;                      // 먹을 때의 크기(μm): 분화 없는 덩이는 세포 하나씩 삼키고(세포 크기), 분화할수록 몸 전체로 잡는다(입·소화 공간)
        public double share;                     // 나눠 쓰는 몫: 몸 전체의 확산·끌림 반지름 ÷ (세포 수 × 세포 반지름). 홀로 있는 세포 1, 붙을수록 이웃과 같은 물을 나눠 작아진다.
                                                 // 영양 흡수의 확산 상한(05)과 가라앉는 속도(04)에 같이 쓴다: 실은 세포마다 물에 닿아 덩이보다 덜 나눠 쓰고, 덜 가라앉는다

        // 배치 기억: 배치(와 Kirkwood 합)는 가지·각·세포 크기·길쭉함 유전자와 세포 수로만 정해진다. 돌연변이 대부분(배분 유전자)은 이것을 안 바꾼다.
        // 계산이 결정적이라 어느 스레드가 먼저 넣든 결과는 다시 계산한 것과 같다. 세포 65개 몸이면 몸 하나에 48 μs → 기억에서 꺼내면 그 몇 분의 1
        // 진화 후보 중 배치 유전자를 바꾼 것(한 번 쓰고 버려지는 배치)은 넣지 않는다(remember = false): 안 그러면 표가 금세 차서 비워진다
        // ponytail: 8192개를 넘으면 통째로 비운다(배치 하나 ≈ 세포 수 × 40 B, 65세포면 2.6 KB)
        static readonly ConcurrentDictionary<(float, float, float, float, int), (CellPos[] cells, double[] inv)> memo = new();

        public static Body Of(float[] g, Traits t, bool remember = true)
        {
            var b = new Body();
            int n = (int)Math.Clamp(Math.Ceiling(t.MeanCells), 1, NDRAW);   // 평균 덩이를 사이에 끼울 만큼(크기를 세포 수 사이에서 이어 붙이려고)
            int nd = (int)Math.Clamp(Math.Round(t.nA), 1, NDRAW), nl = Math.Max(n, nd);
            var key = (g[G.BRANCH], g[G.ANGLE], g[G.SIZE], g[G.ASPECT], nl);
            if (!memo.TryGetValue(key, out var L))
            {
                var c = Layout(g, t, nl); L = (c, Inv(c, t.dCell / 2));
                if (remember) { if (memo.Count > 8192) memo.Clear(); memo[key] = L; }
            }
            b.cells = L.cells;
            double R(int m)   // 앞 m개 세포의 회전 반지름
            {
                double cx = 0, cy = 0, cz = 0; for (int i = 0; i < m; i++) { cx += b.cells[i].x; cy += b.cells[i].y; cz += b.cells[i].z; }
                cx /= m; cy /= m; cz /= m; double s = 0;
                for (int i = 0; i < m; i++) { double dx = b.cells[i].x - cx, dy = b.cells[i].y - cy, dz = b.cells[i].z - cz; s += dx * dx + dy * dy + dz * dz; }
                return Math.Sqrt(s / m);
            }
            int N = b.cells.Length; double mean = t.MeanCells, r0 = t.dCell * Math.Cbrt(t.aspect) / 2;
            b.dim = N >= 8 ? Math.Clamp(Math.Log(N / (N / 4.0)) / Math.Log(Math.Max(1.01, R(N) / Math.Max(1e-3, R(Math.Max(2, N / 4))))), 1, 3) : 3;
            if (mean > N)   // 배치보다 큰 몸: N → mean 사이의 차원을 직접 잰 회전 반지름(GeoOf, 반지름 1 단위)에서 낸다. 아래 크기·겉·확산 반지름이 이 차원으로 늘어난다
            {
                var rgU = GeoOf(g, t.aspect).rg; int k0 = Math.Min(rgU.Length - 2, (int)mean); double rm = rgU[k0] + (rgU[k0 + 1] - rgU[k0]) * Math.Clamp(mean - k0, 0, 1);
                b.dim = Math.Clamp(Math.Log(mean / N) / Math.Log(Math.Max(1.0001, rm / rgU[N])), 1, 3);
            }
            // 평균 덩이 크기: 계산한 배치 안이면 그 회전 반지름, 넘으면 차원 법칙으로 늘린다
            // 세포 수가 정수 사이일 때는 이웃 정수의 회전 반지름 사이를 잇는다: 붙음을 조금 늘리면 몸도 조금 커진다(계단 없음)
            int m0 = Math.Clamp((int)Math.Floor(mean), 1, N), m1 = Math.Min(N, m0 + 1);
            double rg = mean <= N ? R(m0) + (R(m1) - R(m0)) * Math.Clamp(mean - m0, 0, 1) : R(N) * Math.Pow(mean / N, 1 / b.dim);
            b.size = Math.Max(t.dCell * Math.Cbrt(t.aspect), 2 * (rg + r0));
            int outer = 0; foreach (var c in b.cells) if (c.outer > 0.5f) outer++;
            double exN = (double)outer / N;
            double cl = t.dCell; b.esd = cl * Math.Cbrt(Math.Max(1, mean)); b.feed = cl * Math.Pow(b.esd / cl, t.diff);   // 세포 하나의 ESD = dCell(세포 탄소를 π/6·dCell³에서 내므로)
            b.exposed = mean <= N ? exN : Math.Min(1, exN * Math.Pow(N / mean, 1 - 1 / b.dim));   // 큰 덩이일수록 속 세포가 많다(실은 그대로)
            // 확산·끌림 반지름: Kirkwood 근사 1/C = (1/m²)(m/r + Σ_{i≠j} 1/d_ij) — 공 하나면 r, 맞닿은 둘이면 1.33 r(정확한 값 1.39 r), 곧은 실 m개면 ≈ m·r/(1 + ln m).
            // 확산 포집 반지름과 Stokes 끌림 반지름이 거의 같다(Hubbard & Douglas 1993). 배치 밖으로 큰 덩이는 차원 법칙으로 늘린다
            double rc = t.dCell / 2; var inv = L.inv;
            double Cap(int m) => (double)m * m / (m / rc + 2 * inv[m]);
            double cap = mean <= N ? Cap(m0) + (Cap(m1) - Cap(m0)) * Math.Clamp(mean - m0, 0, 1) : Cap(N) * Math.Pow(mean / N, 1 / b.dim);
            b.share = Math.Min(1, cap / (mean * rc));
            return b;
        }

        // inv[m] = Σ_{i<j<m} 1/d_ij (Kirkwood 합, 거리는 세포 반지름 rc 밑으로 안 내려감)
        static double[] Inv(CellPos[] cells, double rc)
        {
            int N = cells.Length; var inv = new double[N + 1];
            for (int j = 1; j < N; j++)
            {
                double sum = 0; var cj = cells[j];
                for (int i = 0; i < j; i++) { double dx = cj.x - cells[i].x, dy = cj.y - cells[i].y, dz = cj.z - cells[i].z; sum += 1 / Math.Max(rc, Math.Sqrt(dx * dx + dy * dy + dz * dz)); }
                inv[j + 1] = inv[j] + sum;
            }
            return inv;
        }

        // 세포 i의 부모 = floor((i−1)·β). 방향은 부모 방향을 θ만큼 꺾되 꺾는 축을 황금각씩 돌린다(잎차례처럼 3차원으로 퍼짐)
        // 덩이 파쇄: 딸세포가 정해진 각으로 단단히 붙어 못 움직이면, 덩이가 커질수록 안쪽 세포끼리 겹쳐 눌리고(포장 변형) 어느 크기에서 부러진다.
        // 눈송이 효모: 둥근 세포(장단축비 ≈ 1.2) 덩이는 ~100세포에서 부러지고, 세포가 길수록 덜 빽빽해 더 크게 자란다(Jacobeen 2018 Nat Phys). 붙는 힘은 진화 내내 거의 그대로.
        // 그래서 붙음 단백질을 늘려도 덩이는 이 세포 수를 못 넘는다. 잣대: 풀기 전 배치의 겹침 에너지 U(m) = Σ_{i<j<m} max(0, 1.6r − d)²/r²가
        // 기준 배치(두 갈래 β 0.5, 꺾임 54°, 장단축비 1.2) 100세포의 U에 이르는 세포 수. 256세포 안에서 안 부러지면 1024세포까지 재고, 그래도 안 부러지면 512 → 1024 세포의 U 멱법칙으로 늘린다.
        // 크기와 무관(반지름 1로 잼). 유전자 셋으로만 정해지니 기억해 둔다(ponytail: 4096개 넘으면 통째로 비움 — 값은 유전자로만 정해져 다시 재도 같다)
        static readonly ConcurrentDictionary<(float, float, float), (double cap, double[] q, double ex, double[] rg)> frac = new();
        static readonly double UC = Strain(0.5, 0.3, 1.2, 100).U[100];

        // U[m] = 앞 m개(≤ nU) 세포의 겹침 에너지(풀기 전), rg[m] = 회전 반지름(푼 뒤, 반지름 1 단위, ≤ n)
        static (double[] U, double[] rg) Strain(double beta, double ang, double aspect, int n, int nU = int.MaxValue)
        {
            var c = Place(beta, Math.PI * ang, 2 * Math.Cbrt(aspect) * 0.95, 1, n); nU = Math.Min(n, nU); var U = new double[nU + 1]; var rg = new double[n + 1]; double sx = 0, sy = 0, sz = 0, s2 = 0;
            for (int i = 0; i < nU; i++)
            {
                double s = 0;
                for (int j = 0; j < i; j++) { double dx = c[i].x - c[j].x, dy = c[i].y - c[j].y, dz = c[i].z - c[j].z, d = Math.Sqrt(dx * dx + dy * dy + dz * dz); if (d < 1.6) s += (1.6 - d) * (1.6 - d); }
                U[i + 1] = U[i] + s;
            }
            Relax(c, 1);   // 몸 길이는 겹침을 푼 배치(그리는 몸과 같은 것)에서 잰다: 풀기 전 꼬불꼬불한 실은 풀면 곧게 펴진다
            for (int i = 0; i < n; i++)
            {
                sx += c[i].x; sy += c[i].y; sz += c[i].z; s2 += c[i].x * c[i].x + c[i].y * c[i].y + c[i].z * c[i].z; int m = i + 1;
                rg[m] = Math.Sqrt(Math.Max(0, s2 / m - (sx * sx + sy * sy + sz * sz) / ((double)m * m)));
            }
            return (U, rg);
        }

        // 배치 하나에서 파쇄 상한과 난류 짐 q(m)(아래 Shear)을 같이 잰다
        static (double cap, double[] q, double ex, double[] rg) Geo(double beta, double ang, double aspect, int M, int R)   // 겹침은 M세포까지, 회전 반지름은 R세포까지 잰다
        {
            var (U, rg) = Strain(beta, ang, aspect, R, M); double cap = double.NaN;
            for (int m = 2; m <= M && double.IsNaN(cap); m++) if (U[m] > UC) cap = m - 1 + (UC - U[m - 1]) / (U[m] - U[m - 1]);   // 넘는 자리 사이를 이어 계단 없이
            if (double.IsNaN(cap))
            {
                double k = U[M / 2] > 0 ? Math.Max(1, Math.Log(U[M] / U[M / 2]) / Math.Log(2)) : 2;
                cap = U[M] <= 0 ? Cfg.CELLS_MAX : Math.Min(Cfg.CELLS_MAX, M * Math.Pow(UC / U[M], 1 / k));
            }
            double lu = 2 * Math.Cbrt(aspect) * 0.95; var q = new double[R + 1];
            for (int m = 2; m <= R; m++) q[m] = Math.Max(q[m - 1], Math.Pow(12, 1.5) * rg[m] * rg[m] * rg[m] / (m * lu));   // 누적 최댓값: 처음 넘는 자리를 이분 탐색으로
            double D = Math.Clamp(Math.Log(4) / Math.Log(Math.Max(1.0001, rg[R] / rg[R / 4])), 1, 3);
            return (cap, q, 3 / D - 1, rg);
        }

        // 256세포 안에서 안 부러지면 1024까지 직접(겹침 늘림 오차 최대 40 % → 확인 표), 그래도 안 부러지면 회전 반지름만 CELLS_MAX까지 직접 잰다:
        // 몸이 NDRAW보다 크면 크기를 이 값으로 정한다(02 Of). 256세포에서 잰 차원으로 늘리면 갈래 0.99 실이 4096세포에서 5배 크게 나왔음(갈래가 100세포마다 늘어 빗자루가 됨)
        static (double cap, double[] q, double ex, double[] rg) GeoOf(float[] g, double aspect)
        {
            if (frac.Count > 4096) frac.Clear();   // 항목 하나가 회전 반지름·난류 짐 표 최대 4097칸 × 2라 많아야 ~270 MB(65536이었을 때 16시드 동시 실행에서 한 프로세스 1.8 GB까지 불어 메모리가 바닥남)
            return frac.GetOrAdd((g[G.BRANCH], g[G.ANGLE], g[G.ASPECT]), _ =>
            {
                float b = g[G.BRANCH], an = g[G.ANGLE]; var a = Geo(b, an, aspect, NDRAW, NDRAW);
                if (a.cap > NDRAW) a = Geo(b, an, aspect, 4 * NDRAW, 4 * NDRAW);
                return a.cap > 4 * NDRAW ? Geo(b, an, aspect, 4 * NDRAW, Cfg.CELLS_MAX) : a;
            });
        }

        public static double Fracture(float[] g, double aspect) => GeoOf(g, aspect).cap;   // 부러지기 전 최대 세포 수(겹침이 안 쌓이는 실은 CELLS_MAX)
        public static double FractureAt(double beta, double ang, double aspect, int M) => Geo(beta, ang, aspect, M, M).cap;

        // 난류 끊김: 물이 몸을 늘이는 힘은 길이 L의 제곱으로 크고(가는 막대, Batchelor 1971), 그 힘을 가운데 단면의 결합들이 나눠 진다.
        // 단면의 세포 수 k ≈ m × 결합 길이 / L이라 결합 하나의 짐 ∝ L³/m: 곧은 실은 길이²로 커지고, 판은 √m로, 덩어리는 거의 그대로(안 끊김).
        // 곧은 실이 Cfg.ETA(바다 표층 콜모고로프 길이 ≈ 3 mm)에서 끊기게 맞춘다 — 떠 있는 실 실측 최대(Trichodesmium·Aphanizomenon ~mm, 수백 세포)와 같은 크기.
        // L = √12 × 회전 반지름(곧은 막대면 길이 그대로). ponytail: 끊김 문턱 자료는 없고(난류가 세면 실이 짧아진다는 실험만, Xiao 2016), 모든 칸에 같은 ε. 칸별 섞임 세기가 생기면 칸마다 η로
        public static double Shear(float[] g, Traits t)
        {
            var (_, q, ex, _) = GeoOf(g, t.aspect); int M = q.Length - 1; double r = t.dCell / 2, thr = Cfg.ETA * Cfg.ETA / (r * r);
            if (q[M] <= thr) return ex <= 0.01 || q[M] <= 0 ? Cfg.CELLS_MAX : Math.Min(Cfg.CELLS_MAX, M * Math.Pow(thr / q[M], 1 / ex));
            int lo = 2, hi = M; while (lo < hi) { int mid = (lo + hi) / 2; if (q[mid] > thr) hi = mid; else lo = mid + 1; }
            return lo - 1 + (thr - q[lo - 1]) / (q[lo] - q[lo - 1]);
        }

        static CellPos[] Place(double beta, double theta, double len, double r, int n)   // 풀기 전 배치: 딸세포를 부모에서 len 떨어진 곳에 θ로 꺾어 붙인다
        {
            var c = new CellPos[n]; var dir = new (double x, double y, double z)[n];
            c[0] = new CellPos { r = (float)r, parent = -1 }; dir[0] = (1, 0, 0);
            var kids = new int[n];
            for (int i = 1; i < n; i++)
            {
                int p = Math.Min(i - 1, (int)Math.Floor((i - 1) * beta)); int k = kids[p]++;
                var d = dir[p]; double phi = 2.39996 * (i + k), sgn = (k % 2 == 0) ? 1 : -1;
                // 부모 방향에 수직인 축 하나를 황금각으로 돌려 골라 θ만큼 꺾는다
                var (ux, uy, uz) = Perp(d, phi); double ang = theta * sgn * (1 + 0.5 * k);
                double cs = Math.Cos(ang), sn = Math.Sin(ang);
                var nd = (d.x * cs + ux * sn, d.y * cs + uy * sn, d.z * cs + uz * sn);
                dir[i] = nd;
                c[i] = new CellPos { x = (float)(c[p].x + nd.Item1 * len), y = (float)(c[p].y + nd.Item2 * len), z = (float)(c[p].z + nd.Item3 * len), r = (float)r, parent = p };
            }
            return c;
        }

        static CellPos[] Layout(float[] g, Traits t, int n)
        {
            double r = t.dCell / 2; var c = Place(g[G.BRANCH], Math.PI * g[G.ANGLE], t.dCell * Math.Cbrt(t.aspect) * 0.95, r, n); Relax(c, r);
            // 겉 세포: 이웃(2.2 r 안)이 적은 세포
            for (int i = 0; i < n; i++)
            {
                int nb = 0;
                for (int j = 0; j < n; j++) { if (i == j) continue; double dx = c[i].x - c[j].x, dy = c[i].y - c[j].y, dz = c[i].z - c[j].z; if (dx * dx + dy * dy + dz * dz < 4.8 * r * r) nb++; }
                c[i].outer = nb < 7 ? 1 : 0;
            }
            return c;
        }

        static void Relax(CellPos[] c, double r)   // 겹침 풀기: 겹친 세포는 밀어낸다(몇 번만)
        {
            int n = c.Length; bool moved = true;   // 한 바퀴 동안 아무것도 안 밀렸으면 그 뒤 바퀴도 같다: 멈춘다(결과 같음)
            for (int it = 0; it < 12 && n > 2 && moved; it++)
            {
                moved = false;
                for (int i = 1; i < n; i++)
                    for (int j = Math.Max(0, i - 24); j < i; j++)
                    {
                        double dx = c[i].x - c[j].x, dy = c[i].y - c[j].y, dz = c[i].z - c[j].z, d2 = dx * dx + dy * dy + dz * dz, min = 1.6 * r;
                        if (d2 >= min * min || d2 < 1e-12) continue;
                        double d = Math.Sqrt(d2), push = (min - d) / d * 0.5; moved = true;
                        c[i].x += (float)(dx * push); c[i].y += (float)(dy * push); c[i].z += (float)(dz * push);
                    }
            }
        }

        static (double, double, double) Perp((double x, double y, double z) d, double phi)
        {
            var a = Math.Abs(d.x) < 0.9 ? (1.0, 0.0, 0.0) : (0.0, 1.0, 0.0);
            double ux = d.y * a.Item3 - d.z * a.Item2, uy = d.z * a.Item1 - d.x * a.Item3, uz = d.x * a.Item2 - d.y * a.Item1;
            double l = Math.Sqrt(ux * ux + uy * uy + uz * uz); ux /= l; uy /= l; uz /= l;
            double vx = d.y * uz - d.z * uy, vy = d.z * ux - d.x * uz, vz = d.x * uy - d.y * ux;
            double c = Math.Cos(phi), s = Math.Sin(phi);
            return (ux * c + vx * s, uy * c + vy * s, uz * c + vz * s);
        }
    }
}
