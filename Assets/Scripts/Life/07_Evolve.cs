// 진화: 분열마다 돌연변이가 생긴다(공급 ∝ 분열 속도 × 개체 수 × 돌연변이율).
// 틱마다 그중 EVAL개를 뽑아 '같은 생리 식'으로 부모와 비교하고, 정착 확률(Kimura)에 따라 새 계통으로 남긴다.
// 목표·적합도 점수는 없다: 그 칸에서 더 빨리 불어나는(덜 죽는) 변이가 남을 뿐이다.
using System;
using System.Collections.Generic;

namespace Life
{
    public sealed partial class Planet
    {
        public readonly List<Strain> alive = new();
        public IReadOnlyList<Strain> Live() => alive;
        public double supply;                     // 지난 틱 돌연변이 공급(개)
        public int triedToday, keptToday;
        public readonly List<double> jumps = new();   // 정착한 돌연변이의 몸 모양 변화(최근 것부터 덮어씀)
        double[] supW = new double[0]; int[] supI = new int[0];

        // 몸 모양 지표: log2 크기, log2 세포 수, 차원, 겉 세포 비율
        public static double ShapeDist(Strain a, Traits tb, Body bb)
        {
            double d1 = Math.Log2(bb.size / a.b.size), d2 = Math.Log2(tb.MeanCells / a.t.MeanCells), d3 = bb.dim - a.b.dim, d4 = 2 * (bb.exposed - a.b.exposed);
            return Math.Sqrt(d1 * d1 + d2 * d2 + d3 * d3 + d4 * d4);
        }

        // 그 칸에서의 순증가율(/일): 생리 성장 × 자원 나눔 − 죽음 − 먹힘
        double Fit(Traits t, Body b, int v, int self, double lim)
        {
            var e = EnvAt(v);
            double hb = binB[v * 125 + t.vbin], vm = Cfg.VIRUS * hb / (hb + Cfg.VIRUS_K);
            var r = Cell.Grow(t, b, e, Intake(t, b, v, self), vm);
            return Math.Max(0, r.g) * lim + Math.Min(0, r.g) - r.mort - Eaten(t, b, v, self);
        }

        // 먹이 섭취(세포 C 1 mol당 /일, 똥 뺀 몫)
        double Intake(Traits t, Body b, int v, int self)
        {
            double a = Attack(t, b), E = 0;
            for (int i = 0; i < alive.Count; i++)
            {
                var s = alive[i]; if (s.slot == self) continue; double bi = B[s.slot * V + v]; if (bi <= Cfg.BMIN) continue;
                E += a * Pref(s.b.size, b.size) * Vul(s.t) * bi;
            }
            for (int k = 0; k < 3; k++) E += a * Pref(Cfg.POC_SIZE[k], b.size) * pool[P.POC0 + k][v];
            return (1 - Cfg.EGEST) * E / (1 + Cfg.HANDLE * E);
        }

        // 먹힘률(/일): 그 칸 포식자들의 지금 섭취 배율로
        double Eaten(Traits t, Body b, int v, int self)
        {
            double vul = Vul(t), loss = 0;
            for (int j = 0; j < alive.Count; j++)
            {
                var s = alive[j]; if (s.slot == self) continue; int sj = s.slot * V + v; double bj = B[sj]; if (bj <= Cfg.BMIN || eatScale[sj] <= 0) continue;
                loss += Attack(s.t, s.b) * Pref(b.size, s.b.size) * vul * eatScale[sj] * bj;
            }
            return loss;
        }

        static double Pfix(double s)
        {
            if (Math.Abs(s) < 1e-6) return 1 / Cfg.NFOUND;
            if (s < -3) return 0;
            return (1 - Math.Exp(-2 * s)) / (1 - Math.Exp(-2 * s * Cfg.NFOUND));
        }

        void Evolve()
        {
            // 공급 가중치: (계통, 칸)마다 분열 속도 × min(개체 수, NCAP) × 틱 × 돌연변이율
            int m = 0, cap = alive.Count * V;
            if (supW.Length < cap) { supW = new double[cap]; supI = new int[cap]; }
            double E = 0;
            foreach (var s in alive)
                for (int v = 0; v < V; v++)
                {
                    int si = s.slot * V + v; double b = B[si]; if (b <= Cfg.BMIN) continue;
                    double gb = Math.Max(0, grow[si]); if (gb <= 0) continue;
                    double org = b * vol[v] * 1e-6 / (s.t.cellC * s.t.MeanCells);
                    double w = gb * Math.Min(org, Cfg.NCAP) * Cfg.DT * knobs.mu; if (w <= 0) continue;
                    E += w; supW[m] = E; supI[m] = si; m++;
                }
            supply = E;
            if (m == 0 || E <= 0) return;
            if (links == null) BuildLinks();
            for (int k = 0; k < Cfg.EVAL; k++)
            {
                double x = rng.U() * E; int lo = 0, hi = m - 1;
                while (lo < hi) { int mid = (lo + hi) / 2; if (supW[mid] < x) lo = mid + 1; else hi = mid; }
                int si = supI[lo], slot = si / V, v = si % V; var par = strains[slot]; if (par == null) continue;
                var g = Genome.Mutate(par.g, ref rng, out int gene);
                var t = Traits.Of(g);
                bool shape = gene == G.SIZE || gene == G.ASPECT || gene == G.BRANCH || gene == G.ANGLE || gene == G.PROP || gene == G.ADH || gene < G.NA;
                var body = shape ? Body.Of(g, t) : par.b;
                double l = limA[si] > 0 ? limA[si] : 1;
                double rp = Fit(par.t, par.b, v, slot, l), rm = Fit(t, body, v, slot, l);
                double gen = Math.Max(0.05, grow[si]);   // 한 세대(1/분열 속도)당 선택 계수
                double per = Math.Min(10, E / Cfg.EVAL);   // ponytail: 후보 하나가 대표하는 변이체 수를 10으로 묶음 — 중립 변이가 칸을 다 채우지 않게. 공급이 적을 땐 그대로
                double sc = (rm - rp) / gen, Pk = Math.Min(1, Pfix(sc) * per);
                // 이웃 칸으로 흘러간 변이체: 부모가 거기서 못 사는데 변이체는 산다면 새 터전을 연다(서식지 넓히기)
                int dest = v;
                if (nbr[v].Length > 0)
                {
                    int u = nbr[v][(int)(rng.U() * nbr[v].Length)], su = slot * V + u;
                    double lu = limA[su] > 0 ? limA[su] : 1, rpu = Fit(par.t, par.b, u, slot, lu), rmu = Fit(t, body, u, slot, lu);
                    if (rmu > 0 && rmu > rpu)
                    {
                        double s2 = (rmu - Math.Max(0, rpu)) / Math.Max(0.05, rmu), P2 = Math.Min(1, Pfix(s2) * per * 0.1);   // 흘러간 몫(0.1)만큼 공급이 적다
                        if (P2 > Pk) { Pk = P2; sc = s2; dest = u; }
                    }
                }
                triedToday++;
                if (rng.U() >= Pk) continue;
                var ch = NewStrain(g, slot, gene, t, body); if (ch == null) continue;
                double f = B[si] * Cfg.FOUND; B[si] -= f; B[ch.slot * V + dest] += f;
                ch.jump = ShapeDist(par, t, body); ch.sel = sc;
                if (jumps.Count >= 4096) jumps.RemoveAt(0); jumps.Add(ch.jump);
                keptToday++;
            }
        }

        // 하루 한 번: 너무 적어진 계통을 지우고(남은 몸은 녹은 유기물로), 칸이 모자라면 가장 적은 것부터
        void Prune()
        {
            foreach (var s in alive)
            {
                double tot = 0; for (int v = 0; v < V; v++) tot += B[s.slot * V + v] * vol[v] * 1e-6;
                s.total = tot;
            }
            var order = new List<Strain>(alive); order.Sort((a, b) => a.total.CompareTo(b.total));
            int free = Cfg.SMAX - alive.Count, need = Math.Max(0, 96 - free), k = 0;
            foreach (var s in order)
            {
                bool tiny = s.total <= 0;
                if (!tiny && k >= need) break;
                Remove(s); k++;
            }
            RebuildAlive();
        }

        void Remove(Strain s)
        {
            for (int v = 0; v < V; v++) { int si = s.slot * V + v; if (B[si] != 0) { pool[P.DOC][v] += B[si]; B[si] = 0; } }
            for (int q = 0; q < NINC; q++) for (int v = 0; v < V; v++) inc[(q * Cfg.SMAX + s.slot) * V + v] = 0;
            strains[s.slot] = null; s.slot = -1;
        }

        void RebuildAlive() { alive.Clear(); for (int i = 0; i < Cfg.SMAX; i++) if (strains[i] != null) alive.Add(strains[i]); }
    }
}
