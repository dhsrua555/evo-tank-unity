// 발생: 세포 하나에서 시작해 유전자 규칙대로 분열·붙음을 되풀이한 배치. 정해 둔 몸 형태는 없다.
// 가지치기 β(1 = 사슬, 0.5 = 두 갈래 나무, 0 = 한 세포 둘레에 다닥다닥)와 분열 각 θ(0 = 곧은 실, 큼 = 뭉침)가 연속이라
// 사슬 → 가지 → 덩이가 매끄럽게 이어진다. 생태에는 배치에서 잰 물리값(크기·겉 세포 비율)만 넘긴다.
using System;
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

        public static Body Of(float[] g, Traits t)
        {
            var b = new Body();
            int n = (int)Math.Clamp(Math.Round(t.MeanCells), 1, NDRAW);
            int nd = (int)Math.Clamp(Math.Round(t.nA), 1, NDRAW);
            b.cells = Layout(g, t, Math.Max(n, nd));
            double R(int m)   // 앞 m개 세포의 회전 반지름
            {
                double cx = 0, cy = 0, cz = 0; for (int i = 0; i < m; i++) { cx += b.cells[i].x; cy += b.cells[i].y; cz += b.cells[i].z; }
                cx /= m; cy /= m; cz /= m; double s = 0;
                for (int i = 0; i < m; i++) { double dx = b.cells[i].x - cx, dy = b.cells[i].y - cy, dz = b.cells[i].z - cz; s += dx * dx + dy * dy + dz * dz; }
                return Math.Sqrt(s / m);
            }
            int N = b.cells.Length;
            b.dim = N >= 8 ? Math.Clamp(Math.Log(N / (N / 4.0)) / Math.Log(Math.Max(1.01, R(N) / Math.Max(1e-3, R(Math.Max(2, N / 4))))), 1, 3) : 3;
            double mean = t.MeanCells, r0 = t.dCell * Math.Cbrt(t.aspect) / 2;
            // 평균 덩이 크기: 계산한 배치 안이면 그 회전 반지름, 넘으면 차원 법칙으로 늘린다
            double rg = mean <= N ? R(Math.Max(1, (int)Math.Round(mean))) : R(N) * Math.Pow(mean / N, 1 / b.dim);
            b.size = Math.Max(t.dCell * Math.Cbrt(t.aspect), 2 * (rg + r0));
            int outer = 0; foreach (var c in b.cells) if (c.outer > 0.5f) outer++;
            double exN = (double)outer / N;
            b.exposed = mean <= N ? exN : Math.Min(1, exN * Math.Pow(N / mean, 1 - 1 / b.dim));   // 큰 덩이일수록 속 세포가 많다(실은 그대로)
            return b;
        }

        // 세포 i의 부모 = floor((i−1)·β). 방향은 부모 방향을 θ만큼 꺾되 꺾는 축을 황금각씩 돌린다(잎차례처럼 3차원으로 퍼짐)
        static CellPos[] Layout(float[] g, Traits t, int n)
        {
            var c = new CellPos[n]; var dir = new (double x, double y, double z)[n];
            double beta = g[G.BRANCH], theta = Math.PI * g[G.ANGLE], len = t.dCell * Math.Cbrt(t.aspect) * 0.95, r = t.dCell / 2;
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
            // 겹침 풀기: 붙은 짝은 거리 len을 지키고, 겹친 세포는 밀어낸다(몇 번만)
            for (int it = 0; it < 12 && n > 2; it++)
            {
                for (int i = 1; i < n; i++)
                    for (int j = Math.Max(0, i - 24); j < i; j++)
                    {
                        double dx = c[i].x - c[j].x, dy = c[i].y - c[j].y, dz = c[i].z - c[j].z, d2 = dx * dx + dy * dy + dz * dz, min = 1.6 * r;
                        if (d2 >= min * min || d2 < 1e-12) continue;
                        double d = Math.Sqrt(d2), push = (min - d) / d * 0.5;
                        c[i].x += (float)(dx * push); c[i].y += (float)(dy * push); c[i].z += (float)(dz * push);
                    }
            }
            // 겉 세포: 이웃(2.2 r 안)이 적은 세포
            for (int i = 0; i < n; i++)
            {
                int nb = 0;
                for (int j = 0; j < n; j++) { if (i == j) continue; double dx = c[i].x - c[j].x, dy = c[i].y - c[j].y, dz = c[i].z - c[j].z; if (dx * dx + dy * dy + dz * dz < 4.8 * r * r) nb++; }
                c[i].outer = nb < 7 ? 1 : 0;
            }
            return c;
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
