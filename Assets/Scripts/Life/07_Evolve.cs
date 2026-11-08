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
        public double[] pruneDbg = new double[3];   // 탐침: 지운 계통 나이 [0일, 1–9일, 10일+]
        public double[] adhDbg = new double[6], evoDbg = new double[4];   // evoDbg: [정착, 그중 r≤0, 지운 어린 계통 중 rate>0, 지운 어린 계통]   // 탐침: 붙음 돌연변이 [평가, 위로, 위로 정착, 아래로, 아래로 정착, 위로 sc 합]
        public readonly List<double> jumps = new();   // 정착한 돌연변이의 몸 모양 변화(최근 것부터 덮어씀)
        double[] supW = new double[0]; int[] supI = new int[0];

        // 몸 모양 지표: log2 크기, log2 세포 수, 차원, 겉 세포 비율
        // 몸 모양 변화: 크기·세포 수·배치 차원·겉 세포 몫. 차원은 8세포 넘는 배치에서만 뜻이 있다(그 밑은 3으로 두는 이름표라 7→8세포에서 거짓 도약 2가 났다)
        public static double ShapeDist(Strain a, Traits tb, Body bb)
        {
            double d1 = Math.Log(bb.size / a.b.size, 2), d2 = Math.Log(tb.MeanCells / a.t.MeanCells, 2), d3 = a.b.cells.Length >= 8 && bb.cells.Length >= 8 ? bb.dim - a.b.dim : 0, d4 = 2 * (bb.exposed - a.b.exposed);
            return Math.Sqrt(d1 * d1 + d2 * d2 + d3 * d3 + d4 * d4);
        }

        // 그 칸에서의 순증가율(/일): 생리 성장 × 자원 나눔 − 죽음 − 먹힘
        public double Fit(Traits t, Body b, int v, int self, double lim)
        {
            var e = EnvAt(v);
            double hb = binB[v * 125 + t.vbin], vm = Cfg.VIRUS * hb / (hb + Cfg.VIRUS_K);
            var r = Cell.Grow(t, b, e, Intake(t, b, v, self), vm);
            return Math.Max(0, r.g) * lim + Math.Min(0, r.g) - r.mort - Eaten(t, b, v, self);
        }

        // 먹이 섭취(세포 C 1 mol당 /일, 똥 뺀 몫)
        public double Intake(Traits t, Body b, int v, int self)
        {
            double a = Attack(t, b), E = 0; int mySp = strains[self]?.species ?? -1;
            for (int i = 0; i < alive.Count; i++)
            {
                var s = alive[i]; if (s.slot == self || (s.species == mySp && mySp >= 0)) continue; double bi = B[s.slot * V + v]; if (bi <= Cfg.BMIN) continue;
                E += a * Pref(s.b.size, b.feed, t.lr) * Vul(s.t) * bi;
            }
            for (int k = 0; k < 3; k++) E += a * Pref(Cfg.POC_SIZE[k], b.feed, t.lr) * pool[P.POC0 + k][v];
            return (1 - Cfg.EGEST) * E / (1 + Cfg.HANDLE / Allo(b) * E);
        }

        // 먹힘률(/일): 그 칸 포식자들의 지금 섭취 배율로
        public double Eaten(Traits t, Body b, int v, int self)
        {
            double vul = Vul(t), loss = 0; int mySp = strains[self]?.species ?? -1;
            for (int j = 0; j < alive.Count; j++)
            {
                var s = alive[j]; if (s.slot == self || (s.species == mySp && mySp >= 0)) continue; int sj = s.slot * V + v; double bj = B[sj]; if (bj <= Cfg.BMIN || eatScale[sj] <= 0) continue;
                loss += Attack(s.t, s.b) * Pref(b.size, s.b.feed, s.t.lr) * vul * eatScale[sj] * bj;
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
                bool shape = gene == G.SIZE || gene == G.ASPECT || gene == G.BRANCH || gene == G.ANGLE || gene == G.PROP || gene == G.ADH || gene == G.DIFF || gene < G.NA;
                var body = shape ? Body.Of(g, t) : par.b;
                double l = limA[si] > 0 ? limA[si] : 1;
                double rp = Fit(par.t, par.b, v, slot, l), rm = Fit(t, body, v, slot, l);
                double gen = Math.Max(0.05, grow[si]);   // 한 세대(1/분열 속도)당 선택 계수
                double per = Math.Min(10, E / Cfg.EVAL);   // ponytail: 후보 하나가 대표하는 변이체 수를 10으로 묶음 — 중립 변이가 칸을 다 채우지 않게. 공급이 적을 땐 그대로
                // 그 칸에서 스스로 늘지 못하는(r ≤ 0) 변이는 드물 때 반드시 사라진다(가지치기 과정): 부모가 이웃에서 흘러와 버티는 칸이어도
                double sc = (rm - rp) / gen, Pk = rm > 0 ? Math.Min(1, Pfix(sc) * per) : 0;
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
                // 표류 장벽: |s| < 1/N 인 변이는 부모와 구별되지 않는다(Lynch). 따로 계통을 만들지 않아야 칸이 쓸모 있는 변이에 남는다
                if (sc < 1 / Cfg.NFOUND) continue;
                bool up = g[G.ADH] > par.g[G.ADH];
                if (gene == G.ADH) { adhDbg[0]++; adhDbg[up ? 1 : 3]++; if (up) adhDbg[5] += sc; }
                if (rng.U() >= Pk) continue;
                if (gene == G.ADH) adhDbg[up ? 2 : 4]++;
                evoDbg[0]++; if ((dest == v ? rm : Fit(t, body, dest, slot, 1)) <= 0) evoDbg[1]++;
                var ch = NewStrain(g, slot, gene, t, body); if (ch == null) continue;
                double f = B[si] * Cfg.FOUND; B[si] -= f; B[ch.slot * V + dest] += f * vol[v] / vol[dest];   // 농도(μM)를 옮기므로 부피 비로(몰 보존)
                ch.jump = ShapeDist(par, t, body); ch.sel = sc; ch.prev = f * vol[v] * 1e-6;
                if (jumps.Count >= 4096) jumps.RemoveAt(0); jumps.Add(ch.jump);
                keptToday++;
            }
        }

        // 하루 한 번: 너무 적어진 계통을 지우고(남은 몸은 녹은 유기물로), 칸이 모자라면 늘지 않는 계통 중 작은 것부터.
        // 생물권보다 빨리 느는 계통(갓 생긴 좋은 변이 포함)은 칸이 모자라도 남긴다: 작아도 자랄 시간을 준다
        double allPrev;
        void Prune()
        {
            double all = 0;
            foreach (var s in alive)
            {
                double tot = 0; for (int v = 0; v < V; v++) tot += B[s.slot * V + v] * vol[v] * 1e-6;
                s.total = tot; all += tot;
            }
            double g0 = allPrev > 0 && all > 0 ? Math.Log(all / allPrev) : 0; allPrev = all;
            foreach (var s in alive)
            {
                double d = s.prev > 0 && s.total > 0 ? Math.Log(s.total / s.prev) - g0 : 0;
                s.rate = double.IsNaN(s.rate) ? d : 0.8 * s.rate + 0.2 * d; s.prev = s.total;
            }
            double Score(Strain s) => Math.Log(Math.Max(s.total, 1e-300)) + (s.rate > 0 ? 1e3 : 0);   // ponytail: 5일 지수 평균으로 '느는 중'을 가림. 포식·피식 순환이 길면 잠깐 줄어드는 드문 종이 먼저 지워질 수 있음
            var order = new List<Strain>(alive); order.Sort((a, b) => Score(a).CompareTo(Score(b)));
            int free = Cfg.SMAX - alive.Count, need = Math.Max(0, 96 - free), k = 0;
            foreach (var s in order)
            {
                bool tiny = s.total <= Cfg.RARE * all;   // 생물권의 이 몫보다 적으면 사라진 것으로 친다(셀 수 없는 꼬리)
                if (!tiny && k >= need) continue;
                int age = (int)Day - s.born; pruneDbg[age < 1 ? 0 : age < 10 ? 1 : 2]++; if (age < 10) { evoDbg[3]++; if (s.rate > 0) evoDbg[2]++; }
                Remove(s, tiny ? null : Kin(s)); k++;
            }
            RebuildAlive();
        }

        // 칸이 모자라 따로 세지 않게 된 계통은 죽은 게 아니다: 가장 가까운 산 친척(부모, 없으면 같은 종에서 유전체가 가장 가까운 것)으로 합친다
        Strain Kin(Strain s)
        {
            Strain best = null; double bd = double.MaxValue;
            foreach (var o in alive)
            {
                if (o == s || o.slot < 0) continue;
                if (o.id == s.parent) return o;
                if (o.species != s.species) continue; double d = Genome.Dist(o.g, s.g); if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        void Remove(Strain s, Strain into = null)
        {
            for (int v = 0; v < V; v++) { int si = s.slot * V + v; if (B[si] == 0) continue; if (into != null) B[into.slot * V + v] += B[si]; else ToDoc(pool, v, B[si]); B[si] = 0; }
            for (int q = 0; q < NINC; q++) for (int v = 0; v < V; v++) inc[(q * Cfg.SMAX + s.slot) * V + v] = 0;
            strains[s.slot] = null; s.slot = -1;
        }

        void RebuildAlive() { alive.Clear(); for (int i = 0; i < Cfg.SMAX; i++) if (strains[i] != null) alive.Add(strains[i]); }
    }
}
