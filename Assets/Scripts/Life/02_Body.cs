// 발생: 세포 하나에서 시작해 유전자 규칙대로 분열·붙음을 되풀이한 배치. 정해 둔 몸 형태는 없다.
// 가지치기 β(1 = 사슬, 0.5 = 두 갈래 나무, 0 = 한 세포 둘레에 다닥다닥)와 분열 각 θ(0 = 곧은 실, 큼 = 뭉침)가 연속이라
// 사슬 → 가지 → 덩이가 매끄럽게 이어진다. 생태에는 배치에서 잰 물리값(크기·겉 세포 비율)만 넘긴다.
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Collections.Generic;

namespace Life
{
    public struct CellPos { public float x, y, z, r, outer; public int parent; }

    public sealed class Body
    {
        public const int NDRAW = 256;            // 배치를 실제로 계산하는 세포 수 상한(넘으면 크기만 늘린다)
        public CellPos[] cells;                  // 그릴 배치(첫 세포가 원점, 세포 반지름 = 1 단위 — μm는 × rc). 세포 크기와 상관없는 모양이라 크기만 다른 몸끼리 같은 배열을 나눠 쓴다
        public double rc;                        // 세포 반지름(μm) = 배치 1 단위의 길이
        public double size;                      // 몸 크기(μm): 평균 덩이의 지름
        public double exposed;                   // 겉에 드러난 세포 비율(빛·영양을 바로 받는 몫)
        public double dim;                       // 배치의 프랙털 차원(1 실 · 2 판 · 3 덩이)
        public double esd;                       // 같은 부피 공의 지름(μm) = 세포 하나 크기 × 세포 수^(1/3). 포식의 크기(먹는 쪽·먹히는 쪽)는 이것으로 센다: 크기비 자료(Hansen 1994)가 ESD로 잰 값
        public double feed;                      // 먹을 때의 크기(μm): 분화 없는 덩이는 세포 하나씩 삼키고(세포 크기), 분화할수록 몸 전체로 잡는다(입·소화 공간)
        public double share;                     // 나눠 쓰는 몫: 몸 전체의 확산·끌림 반지름 ÷ (세포 수 × 세포 반지름). 홀로 있는 세포 1, 붙을수록 이웃과 같은 물을 나눠 작아진다.
                                                 // 영양 흡수의 확산 상한(05)과 가라앉는 속도(04)에 같이 쓴다: 실은 세포마다 물에 닿아 덩이보다 덜 나눠 쓰고, 덜 가라앉는다

        // 배치 기억: 배치(와 Kirkwood 합)는 반지름 1 단위로 재면 가지·각·길쭉함 유전자와 세포 수로만 정해진다(세포 크기는 길이 배율일 뿐). 돌연변이 대부분(배분 유전자)은 이것을 안 바꾼다.
        // 계산이 결정적이라 어느 스레드가 먼저 넣든 결과는 다시 계산한 것과 같다. 세포 65개 몸이면 몸 하나에 48 μs → 기억에서 꺼내면 그 몇 분의 1
        // 진화 후보 중 배치 유전자를 바꾼 것(한 번 쓰고 버려지는 배치)은 넣지 않는다(remember = false): 안 그러면 표가 금세 차서 비워진다
        // ponytail: 8192개를 넘으면 통째로 비운다(배치 하나 ≈ 세포 수 × 40 B, 65세포면 2.6 KB)
        static readonly ConcurrentDictionary<(float, float, float, int), (CellPos[] cells, double[] inv)> memo = new();
        // 넣은 수는 따로 센다: ConcurrentDictionary.Count는 잠금을 전부 잡아 앱(IL2CPP) 16 스레드가 거기서 줄을 섰다(후보 단계에 일하는 스레드 평균 4개).
        // 대략이어도 된다(같은 키를 두 스레드가 넣으면 둘 셈 → 조금 일찍 비움): 표는 다시 재도 같은 값이라 결과와 무관
        static int memoN, fracN;

        public static Body Of(float[] g, Traits t, bool remember = true, Body into = null)   // into: 다시 쓸 객체(모든 칸을 덮어씀)
        {
            var b = into ?? new Body();
            int n = (int)Math.Clamp(Math.Ceiling(t.MeanCells), 1, NDRAW);   // 평균 덩이를 사이에 끼울 만큼(크기를 세포 수 사이에서 이어 붙이려고)
            int nd = (int)Math.Clamp(Math.Round(t.nA), 1, NDRAW), nl = Math.Max(n, nd);
            var key = (g[G.BRANCH], g[G.ANGLE], g[G.ASPECT], nl);
            if (!memo.TryGetValue(key, out var L))
            {
                L = Layout(g, t.aspect, nl);
                if (remember) { if (Interlocked.Increment(ref memoN) > 8192) { memo.Clear(); memoN = 0; } memo[key] = L; }
            }
            b.cells = L.cells; double rc = t.dCell / 2; b.rc = rc;
            double R(int m)   // 앞 m개 세포의 회전 반지름(μm)
            {
                double cx = 0, cy = 0, cz = 0; for (int i = 0; i < m; i++) { cx += b.cells[i].x; cy += b.cells[i].y; cz += b.cells[i].z; }
                cx /= m; cy /= m; cz /= m; double s = 0;
                for (int i = 0; i < m; i++) { double dx = b.cells[i].x - cx, dy = b.cells[i].y - cy, dz = b.cells[i].z - cz; s += dx * dx + dy * dy + dz * dz; }
                return rc * Math.Sqrt(s / m);
            }
            int N = b.cells.Length; double mean = t.MeanCells, r0 = t.dCell * Math.Cbrt(t.aspect) / 2;
            b.dim = N >= 8 ? Math.Clamp(Math.Log(N / (N / 4.0)) / Math.Log(Math.Max(1.01, R(N) / Math.Max(1e-3, R(Math.Max(2, N / 4))))), 1, 3) : 3;
            if (mean > N)   // 배치보다 큰 몸: N → mean 사이의 차원을 직접 잰 회전 반지름(GeoOf, 반지름 1 단위)에서 낸다. 아래 크기·겉·확산 반지름이 이 차원으로 늘어난다
            {
                var (_, _, _, rgU, nU) = GeoOf(g, t.aspect, remember, (int)Math.Min(mean, Cfg.CELLS_MAX) + 2); int k0 = Math.Min(nU - 1, (int)mean); double rm = rgU[k0] + (rgU[k0 + 1] - rgU[k0]) * Math.Clamp(mean - k0, 0, 1);
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
            var inv = L.inv;   // 반지름 1 단위의 합: μm로는 ÷ rc
            double Cap(int m) => rc * m * m / (m + 2 * inv[m]);
            double cap = mean <= N ? Cap(m0) + (Cap(m1) - Cap(m0)) * Math.Clamp(mean - m0, 0, 1) : Cap(N) * Math.Pow(mean / N, 1 / b.dim);
            b.share = Math.Min(1, cap / (mean * rc));
            return b;
        }

        // 세포 i의 부모 = floor((i−1)·β). 방향은 부모 방향을 θ만큼 꺾되 꺾는 축을 황금각씩 돌린다(잎차례처럼 3차원으로 퍼짐)
        // 덩이 파쇄: 딸세포가 정해진 각으로 단단히 붙어 못 움직이면, 덩이가 커질수록 안쪽 세포끼리 겹쳐 눌리고(포장 변형) 어느 크기에서 부러진다.
        // 눈송이 효모: 둥근 세포(장단축비 ≈ 1.2) 덩이는 ~100세포에서 부러지고, 세포가 길수록 덜 빽빽해 더 크게 자란다(Jacobeen 2018 Nat Phys). 붙는 힘은 진화 내내 거의 그대로.
        // 그래서 붙음 단백질을 늘려도 덩이는 이 세포 수를 못 넘는다. 잣대: 풀기 전 배치의 겹침 에너지 U(m) = Σ_{i<j<m} max(0, 1.6r − d)²/r²가
        // 기준 배치(두 갈래 β 0.5, 꺾임 54°, 장단축비 1.2) 100세포의 U에 이르는 세포 수. 256세포 안에서 안 부러지면 1024세포까지 재고, 그래도 안 부러지면 512 → 1024 세포의 U 멱법칙으로 늘린다.
        // 크기와 무관(반지름 1로 잼). 유전자 셋으로만 정해지니 기억해 둔다(ponytail: 4096개 넘으면 통째로 비움 — 값은 유전자로만 정해져 다시 재도 같다)
        static readonly ConcurrentDictionary<(float, float, float), (double cap, double[] q, double ex, double[] rg, int n)> frac = new();
        // 황금각 회전 cos·sin(2.39996 m)을 한 번만: 몸표 하나에 세포마다 삼각함수 넷이던 것을 둘 줄임(같은 식이라 값 같음). UC보다 먼저 만든다(정적 필드는 적힌 차례로)
        static readonly double[] GoldC = new double[2 * Cfg.CELLS_MAX], GoldS = Gold(GoldC);
        static double[] Gold(double[] gc) { var gs = new double[gc.Length]; for (int m = 0; m < gc.Length; m++) { double phi = 2.39996 * m; gc[m] = Math.Cos(phi); gs[m] = Math.Sin(phi); } return gs; }
        static readonly double UC = Overlap(0.5, 0.3, 1.2, 0, 100, 100)[100];

        // 몸표를 잴 때 쓰고 버리는 배열(스레드마다 하나). 진화 후보마다 새로 잡으면 20000일 세상에서 하루 12 MB를 버렸다(앱 GC 끊김)
        sealed class Scratch
        {
            public readonly CellPos[] c = new CellPos[Cfg.CELLS_MAX]; public readonly (double x, double y, double z)[] dir = new (double, double, double)[Cfg.CELLS_MAX];
            public readonly int[] kids = new int[Cfg.CELLS_MAX], next = new int[Cfg.CELLS_MAX], cell = new int[3 * Cfg.CELLS_MAX], hj = new int[Cfg.CELLS_MAX], head = new int[HB];
            public readonly double[] U = new double[Cfg.CELLS_MAX + 1], ht = new double[Cfg.CELLS_MAX];
            public readonly bool[] mv = new bool[Cfg.CELLS_MAX];
            public readonly double[] angC = new double[Cfg.CELLS_MAX], angS = new double[Cfg.CELLS_MAX];
        }
        [ThreadStatic] static Scratch scr;
        const int HB = 4096;
        static int Hash(int x, int y, int z) => (x * 73856093 ^ y * 19349663 ^ z * 83492791) & (HB - 1);

        // 세포 from..n을 (반지름 1로) 이어 놓고 U[m] = 앞 m개(≤ nU) 세포의 겹침 에너지(풀기 전). 배치는 이 스레드의 S.c에 남아 Rg가 이어 쓴다.
        // 배치·겹침은 앞 세포만 보므로 앞 from개(이 스레드에 방금 잰 것)에 이어 재도 한 번에 잰 것과 비트까지 같다
        static double[] Overlap(double beta, double ang, double aspect, int from, int n, int nU)
        {
            var S = scr ??= new Scratch(); var c = S.c; relaxedN = 0;
            Place(beta, Math.PI * ang, 2 * Math.Cbrt(aspect) * 0.95, 1, n, c, from); nU = Math.Min(n, nU); var U = S.U;
            // 1.6보다 가까운 앞 세포만 더한다: 한 변 1.61 격자의 이웃 27칸만 보고, 찾은 것을 j 차례로 더한다(모든 쌍을 훑던 것과 비트까지 같다. 1024세포 몸표가 쌍 52만 개 → 수천 개)
            if (from == 0) { U[0] = 0; Array.Fill(S.head, -1); }
            for (int i = from; i < nU; i++)
            {
                int gx = (int)Math.Floor(c[i].x / 1.61), gy = (int)Math.Floor(c[i].y / 1.61), gz = (int)Math.Floor(c[i].z / 1.61), nh = 0;
                for (int ax = gx - 1; ax <= gx + 1; ax++) for (int ay = gy - 1; ay <= gy + 1; ay++) for (int az = gz - 1; az <= gz + 1; az++)
                    for (int j = S.head[Hash(ax, ay, az)]; j >= 0; j = S.next[j])
                    {
                        if (S.cell[3 * j] != ax || S.cell[3 * j + 1] != ay || S.cell[3 * j + 2] != az) continue;   // 같은 통에 섞인 다른 격자 칸
                        double dx = c[i].x - c[j].x, dy = c[i].y - c[j].y, dz = c[i].z - c[j].z, d = Math.Sqrt(dx * dx + dy * dy + dz * dz); if (d >= 1.6) continue;
                        int k = nh++; while (k > 0 && S.hj[k - 1] > j) { S.hj[k] = S.hj[k - 1]; S.ht[k] = S.ht[k - 1]; k--; }
                        S.hj[k] = j; S.ht[k] = (1.6 - d) * (1.6 - d);
                    }
                double s = 0; for (int k = 0; k < nh; k++) s += S.ht[k];
                U[i + 1] = U[i] + s;
                int h = Hash(gx, gy, gz); S.next[i] = S.head[h]; S.head[h] = i; S.cell[3 * i] = gx; S.cell[3 * i + 1] = gy; S.cell[3 * i + 2] = gz;
            }
            return U;
        }

        // 방금 Overlap이 놓은 n개를 풀고 rg[m] = 앞 m개의 회전 반지름(반지름 1 단위)을 rg(n+1칸)에 채운다
        static void Rg(int n, double[] rg)
        {
            var c = scr.c; double sx = 0, sy = 0, sz = 0, s2 = 0;
            Relax(c, 1, n); relaxedN = n;   // 몸 길이는 겹침을 푼 배치(그리는 몸과 같은 것)에서 잰다: 풀기 전 꼬불꼬불한 실은 풀면 곧게 펴진다
            for (int i = 0; i < n; i++)
            {
                sx += c[i].x; sy += c[i].y; sz += c[i].z; s2 += c[i].x * c[i].x + c[i].y * c[i].y + c[i].z * c[i].z; int m = i + 1;
                rg[m] = Math.Sqrt(Math.Max(0, s2 / m - (sx * sx + sy * sy + sz * sz) / ((double)m * m)));
            }
        }

        // 앞 M세포의 겹침 U에서 파쇄 상한
        static double Cap(double[] U, int M)
        {
            for (int m = 2; m <= M; m++) if (U[m] > UC) return m - 1 + (UC - U[m - 1]) / (U[m] - U[m - 1]);   // 넘는 자리 사이를 이어 계단 없이
            double k = U[M / 2] > 0 ? Math.Max(1, Math.Log(U[M] / U[M / 2]) / Math.Log(2)) : 2;
            return U[M] <= 0 ? Cfg.CELLS_MAX : Math.Min(Cfg.CELLS_MAX, M * Math.Pow(UC / U[M], 1 / k));
        }

        // 방금 놓은 R세포 배치에서 회전 반지름과 난류 짐 q(m)(아래 Shear)
        // 진화 후보 것(keep = false)은 이 스레드의 크기별 표 둘을 다시 쓴다: 그 후보를 재는 동안만 읽히고(lastV), 계통이 되면 형질·몸을 받아 가므로 표는 안 남는다.
        // 후보마다 4097칸 표 둘(64 KB)을 새로 잡으면 틱마다 ~5 MB라 앱(Boehm GC)이 자주 멈췄다. 표의 0·1칸은 안 쓰고(0 그대로) 나머지는 매번 다 덮어 결과는 같다
        [ThreadStatic] static double[][] spare;
        // 앞 K칸(K ≤ R)만 잴 수도 있다: 풀기는 앞 K세포를 따로 풀어도 같은 자리(세포는 앞 24칸만 본다)라 q·rg의 앞 K칸은 R칸 다 잰 것과 비트까지 같다. n = 잰 칸 수
        static (double cap, double[] q, double ex, double[] rg, int n) Tables(double cap, double aspect, int R, int K, bool keep)
        {
            double[] q, rg;
            if (keep) { q = new double[R + 1]; rg = new double[R + 1]; }
            else { int k = R == NDRAW ? 0 : R == 4 * NDRAW ? 2 : 4; var sp = spare ??= new double[6][]; q = sp[k] ??= new double[R + 1]; rg = sp[k + 1] ??= new double[R + 1]; }
            Rg(K, rg);
            double lu = 2 * Math.Cbrt(aspect) * 0.95, c12 = Math.Pow(12, 1.5);
            for (int m = 2; m <= K; m++) q[m] = Math.Max(q[m - 1], c12 * rg[m] * rg[m] * rg[m] / (m * lu));   // 누적 최댓값: 처음 넘는 자리를 이분 탐색으로
            double D = Math.Clamp(Math.Log(4) / Math.Log(Math.Max(1.0001, rg[K] / rg[K / 4])), 1, 3);
            return (cap, q, 3 / D - 1, rg, K);
        }

        // 256세포 안에서 안 부러지면 1024까지 직접(겹침 늘림 오차 최대 40 % → 확인 표), 그래도 안 부러지면 회전 반지름만 CELLS_MAX까지 직접 잰다:
        // 몸이 NDRAW보다 크면 크기를 이 값으로 정한다(02 Of). 256세포에서 잰 차원으로 늘리면 갈래 0.99 실이 4096세포에서 5배 크게 나왔음(갈래가 100세포마다 늘어 빗자루가 됨)
        // remember = false(진화 후보가 배치 유전자를 바꾼 것)는 표에 넣지 않고 이 스레드의 마지막 하나로만 둔다: 같은 후보의 형질·끊김·몸이 이어 꺼내 쓴다.
        // 한 번 쓰고 버려지는 배치로 표가 차 통째로 비워지면 살아 있는 계통 것까지 다시 재야 했다
        // need(remember = false일 때만): 앞 몇 칸까지 쓰는지. 진화 후보는 붙음 덩이 A보다 1–2칸 더만 재면 된다(Traits.Of): 끊김이 그 안에서 안 넘으면 어차피 A보다 커서 안 쓰인다.
        // 4096세포 표가 필요한 후보 대부분이 실제로는 수백 세포 덩이라(사용자 지구) 풀기가 몇 분의 1로 준다
        [ThreadStatic] static (float, float, float) lastK; [ThreadStatic] static (double cap, double[] q, double ex, double[] rg, int n) lastV; [ThreadStatic] static int lastR;
        static (double cap, double[] q, double ex, double[] rg, int n) GeoOf(float[] g, double aspect, bool remember = true, int need = int.MaxValue)
        {
            var key = (g[G.BRANCH], g[G.ANGLE], g[G.ASPECT]);
            if (frac.TryGetValue(key, out var a)) return a;
            need = remember ? int.MaxValue : Math.Max(8, need);   // 8칸 이상: 차원 ex가 rg[n/4] > 0에서 나오게
            if (lastV.q != null && lastK == key && (lastV.n == lastR || lastV.n >= need)) return lastV;
            // 겹침 U·부러짐 cap도 앞 need칸까지만: 그 안에서 UC를 넘으면 cap은 끝까지 잰 것과 같고, 안 넘으면 둘 다 need 이상이라 A와의 최솟값에 안 쓰인다
            float b = g[G.BRANCH], an = g[G.ANGLE]; int n1 = Math.Min(NDRAW, need); var U = Overlap(b, an, aspect, 0, n1, n1); double cap = Cap(U, n1); int R = NDRAW;
            if (n1 == NDRAW && cap > NDRAW)
            {
                int n2 = Math.Min(4 * NDRAW, need); Overlap(b, an, aspect, NDRAW, n2, n2); cap = Cap(U, n2); R = 4 * NDRAW;   // 앞 256세포는 그대로 두고 이어 놓는다
                if (n2 == 4 * NDRAW && cap > 4 * NDRAW) { R = Cfg.CELLS_MAX; Overlap(b, an, aspect, 4 * NDRAW, Math.Min(need, R), 0); }
            }
            a = Tables(cap, aspect, R, Math.Min(R, need), remember);
            if (remember) { if (Interlocked.Increment(ref fracN) > 4096) { frac.Clear(); fracN = 0; } frac[key] = a; }   // 항목 하나가 회전 반지름·난류 짐 표 최대 4097칸 × 2라 많아야 ~270 MB(65536이었을 때 16시드 동시 실행에서 한 프로세스 1.8 GB까지 불어 메모리가 바닥남)
            lastK = key; lastV = a; lastR = R; relaxedK = key; return a;
        }

        public static double Fracture(float[] g, double aspect, bool remember = true, int need = int.MaxValue) => GeoOf(g, aspect, remember, need).cap;   // 부러지기 전 최대 세포 수(겹침이 안 쌓이는 실은 CELLS_MAX)
        public static double FractureAt(double beta, double ang, double aspect, int M) => Cap(Overlap(beta, ang, aspect, 0, M, M), M);

        // 난류 끊김: 물이 몸을 늘이는 힘은 길이 L의 제곱으로 크고(가는 막대, Batchelor 1971), 그 힘을 가운데 단면의 결합들이 나눠 진다.
        // 단면의 세포 수 k ≈ m × 결합 길이 / L이라 결합 하나의 짐 ∝ L³/m: 곧은 실은 길이²로 커지고, 판은 √m로, 덩어리는 거의 그대로(안 끊김).
        // 곧은 실이 Cfg.ETA(바다 표층 콜모고로프 길이 ≈ 3 mm)에서 끊기게 맞춘다 — 떠 있는 실 실측 최대(Trichodesmium·Aphanizomenon ~mm, 수백 세포)와 같은 크기.
        // L = √12 × 회전 반지름(곧은 막대면 길이 그대로). ponytail: 끊김 문턱 자료는 없고(난류가 세면 실이 짧아진다는 실험만, Xiao 2016), 모든 칸에 같은 ε. 칸별 섞임 세기가 생기면 칸마다 η로
        // 앞 n칸만 잰 표(n < 실제 크기)에서 n 안에 안 넘으면 돌려주는 값은 n 이상: 진짜 값도 n 이상이라 붙음 A(< n)와의 최솟값은 같다
        public static double Shear(float[] g, Traits t, bool remember = true, int need = int.MaxValue)
        {
            var (_, q, ex, _, M) = GeoOf(g, t.aspect, remember, need); double r = t.dCell / 2, thr = Cfg.ETA * Cfg.ETA / (r * r);
            if (q[M] <= thr) return ex <= 0.01 || q[M] <= 0 ? Cfg.CELLS_MAX : Math.Min(Cfg.CELLS_MAX, M * Math.Pow(thr / q[M], 1 / ex));
            int lo = 2, hi = M; while (lo < hi) { int mid = (lo + hi) / 2; if (q[mid] > thr) hi = mid; else lo = mid + 1; }
            return lo - 1 + (thr - q[lo - 1]) / (q[lo] - q[lo - 1]);
        }

        static void Place(double beta, double theta, double len, double r, int n, CellPos[] c, int from = 0)   // 풀기 전 배치(c의 from..n칸, 앞은 이미 놓임): 딸세포를 부모에서 len 떨어진 곳에 θ로 꺾어 붙인다
        {
            var S = scr ??= new Scratch(); var dir = S.dir; var kids = S.kids; Array.Clear(kids, from, n - from);
            if (from == 0) { c[0] = new CellPos { r = (float)r, parent = -1 }; dir[0] = (1, 0, 0); }
            int na = 0;   // 꺾는 각 cos·sin은 몇째 딸(k)로만 정해진다: 이 호출에서 처음 나온 k만 계산(같은 식이라 값 같음)
            for (int i = Math.Max(1, from); i < n; i++)
            {
                int p = Math.Min(i - 1, (int)Math.Floor((i - 1) * beta)); int k = kids[p]++;
                var d = dir[p];
                // 부모 방향에 수직인 축 하나를 황금각으로 돌려 골라 θ만큼 꺾는다
                var (ux, uy, uz) = Perp(d, GoldC[i + k], GoldS[i + k]);
                for (; na <= k; na++) { double sg = (na % 2 == 0) ? 1 : -1, ag = theta * sg * (1 + 0.5 * na); S.angC[na] = Math.Cos(ag); S.angS[na] = Math.Sin(ag); }
                double cs = S.angC[k], sn = S.angS[k];
                var nd = (d.x * cs + ux * sn, d.y * cs + uy * sn, d.z * cs + uz * sn);
                dir[i] = nd;
                c[i] = new CellPos { x = (float)(c[p].x + nd.Item1 * len), y = (float)(c[p].y + nd.Item2 * len), z = (float)(c[p].z + nd.Item3 * len), r = (float)r, parent = p };
            }
        }

        // 이 스레드의 S.c에 방금 GeoOf가 푼 배치(반지름 1)가 앞 relaxedN칸 있으면(가지·각·길쭉함 유전자 relaxedK) 몸 배치는 그 앞부분을 베낀다:
        // 놓기·풀기가 같은 식·같은 수라(앞 칸은 뒤 칸과 상관없이 풀림) 다시 놓고 푼 것과 비트까지 같다. 배치 유전자를 바꾼 진화 후보마다 한 번 덜 푼다
        [ThreadStatic] static (float, float, float) relaxedK; [ThreadStatic] static int relaxedN;
        static (CellPos[] cells, double[] inv) Layout(float[] g, double aspect, int n)
        {
            const double r = 1; var c = new CellPos[n];
            if (scr != null && relaxedN >= n && relaxedK == (g[G.BRANCH], g[G.ANGLE], g[G.ASPECT])) Array.Copy(scr.c, c, n);
            else { Place(g[G.BRANCH], Math.PI * g[G.ANGLE], 2 * Math.Cbrt(aspect) * 0.95, r, n, c); Relax(c, r, n); }
            // 겉 세포: 이웃(2.2 r 안)이 적은 세포. Kirkwood 합 inv[m] = Σ_{i<j<m} 1/d_ij(거리는 세포 반지름 r 밑으로 안 내려감)과 같은 쌍 거리로 한 번에 센다
            // (쌍마다 한 번: dx 부호만 바뀌어 거리 같음 → 이웃 수·합 모두 따로 재던 것과 같다)
            Span<int> nb = stackalloc int[n]; double lim = 4.8 * r * r; var inv = new double[n + 1];
            for (int j = 1; j < n; j++)
            {
                double sum = 0; var cj = c[j];
                for (int i = 0; i < j; i++)
                {
                    double dx = cj.x - c[i].x, dy = cj.y - c[i].y, dz = cj.z - c[i].z, d2 = dx * dx + dy * dy + dz * dz;
                    if (d2 < lim) { nb[i]++; nb[j]++; }
                    sum += 1 / Math.Max(r, Math.Sqrt(d2));
                }
                inv[j + 1] = inv[j] + sum;
            }
            for (int i = 0; i < n; i++) c[i].outer = nb[i] < 7 ? 1 : 0;
            return (c, inv);
        }

        static void Relax(CellPos[] c, double r, int n)   // 겹침 풀기(앞 n칸): 겹친 세포는 밀어낸다(몇 번만)
        {
            // 세포 i는 자기와 앞 24칸만 보고 자기만 움직인다: 지난 바퀴에 안 밀렸고 이번 바퀴에 앞 24칸도 안 밀렸으면 보는 값이 그대로라 또 안 밀린다 → 건너뛴다
            // (결과 비트까지 같음. 4096세포 실에서 셈이 ⅓로)
            var mv = (scr ??= new Scratch()).mv; bool moved = true; double min = 1.6 * r, min2 = min * min;   // 한 바퀴 동안 아무것도 안 밀렸으면 그 뒤 바퀴도 같다: 멈춘다(결과 같음)
            for (int it = 0; it < 12 && n > 2 && moved; it++)
            {
                moved = false; int last = -25;   // 이번 바퀴에 마지막으로 밀린 세포
                for (int i = 1; i < n; i++)
                {
                    if (it > 0 && !mv[i] && last < i - 24) continue;
                    mv[i] = false;
                    float xi = c[i].x, yi = c[i].y, zi = c[i].z; bool mi = false;   // 세포 i를 지역 변수로 들고 밀고 끝에 한 번 적는다(같은 float 셈이라 값 같음)
                    for (int j = Math.Max(0, i - 24); j < i; j++)
                    {
                        double dx = xi - c[j].x, dy = yi - c[j].y, dz = zi - c[j].z, d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 >= min2 || d2 < 1e-12) continue;
                        double d = Math.Sqrt(d2), push = (min - d) / d * 0.5; mi = true;
                        xi += (float)(dx * push); yi += (float)(dy * push); zi += (float)(dz * push);
                    }
                    if (mi) { moved = mv[i] = true; last = i; c[i].x = xi; c[i].y = yi; c[i].z = zi; }
                }
            }
        }

        static (double, double, double) Perp((double x, double y, double z) d, double c, double s)
        {
            var a = Math.Abs(d.x) < 0.9 ? (1.0, 0.0, 0.0) : (0.0, 1.0, 0.0);
            double ux = d.y * a.Item3 - d.z * a.Item2, uy = d.z * a.Item1 - d.x * a.Item3, uz = d.x * a.Item2 - d.y * a.Item1;
            double l = Math.Sqrt(ux * ux + uy * uy + uz * uz); ux /= l; uy /= l; uz /= l;
            double vx = d.y * uz - d.z * uy, vy = d.z * ux - d.x * uz, vz = d.x * uy - d.y * ux;
            return (ux * c + vx * s, uy * c + vy * s, uz * c + vz * s);
        }
    }
}
