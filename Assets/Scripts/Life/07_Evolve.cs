// 진화: 분열마다 돌연변이가 생긴다(공급 ∝ 분열 속도 × 개체 수 × 돌연변이율).
// 틱마다 그중 EVAL개를 뽑아 '같은 생리 식'으로 부모와 비교하고, 정착 확률(Kimura)에 따라 새 계통으로 남긴다.
// 목표·적합도 점수는 없다: 그 칸에서 더 빨리 불어나는(덜 죽는) 변이가 남을 뿐이다.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Life
{
    public sealed partial class Planet
    {
        public readonly List<Strain> alive = new();
        public IReadOnlyList<Strain> Live() => alive;
        public double supply;                     // 지난 틱 돌연변이 공급(개)
        public int triedToday, keptToday;
        public double driftN, driftShape;          // 중립 표류로 계통 유전체가 바뀐 횟수(누적), 그중 몸 꼴이 바뀐 것
        [NonSerialized] public bool drift = true;  // 시험용 스위치(LifeTest NODRIFT): 끄면 예전처럼 중립 변이는 버린다
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
        public double Fit(Traits t, Body b, int v, int self, double lim) => FitWith(t, b, v, lim, Intake(t, b, v, self), Eaten(t, b, v, self));
        double FitWith(Traits t, Body b, int v, double lim, double intake, double eaten)
        {
            var r = Cell.Grow(t, b, EnvAt(v), intake, VirusMort(t, b, v));
            return Math.Max(0, r.g) * lim + Math.Min(0, r.g) - r.mort - eaten;
        }

        // 먹이 섭취(세포 C 1 mol당 /일, 똥 뺀 몫). 바닥 경계층에서 만나는 이웃 칸도(06 Bio와 같게): 퇴적면은 위 물의 떠 있는 먹이·사체를 위 물 농도로,
        // 맨 아래 물 칸은 퇴적면 생물을 경계층 몫(vol 퇴적면/vol 이 칸)만큼
        public double Intake(Traits t, Body b, int v, int self)
        {
            double a = Attack(t, b), me = Mot(t), E = 0, lf = Math.Log10(b.feed), cap = Cfg.PREY_MAX * b.feed; int mySp = strains[self]?.species ?? -1;
            void From(int u, double w, bool poc)
            {
                if (presOn)   // 진화 단계: 칸에 베껴 둔 줄(아래와 같은 식·차례)
                {
                    var ps = pres[u];
                    for (int q = 0, n = nPres[u]; q < n; q++)
                    {
                        ref var s = ref ps[q]; if (s.slot == self || (s.sp == mySp && mySp >= 0) || s.esd > cap) continue; double bi = s.bm; if (bi <= Cfg.BMIN) continue;
                        double x = (s.lsz - lf + t.lr) / Cfg.PREY_SD; if (x * x <= 9) E += a * Math.Exp(-0.5 * x * x) * s.vul * Enc(me, s.mot) * bi * w;
                    }
                }
                else foreach (var s in alive)
                {
                    if (s.slot == self || (s.species == mySp && mySp >= 0) || s.b.esd > cap) continue; double bi = B[s.slot * V + u]; if (bi <= Cfg.BMIN) continue;
                    double x = (s.lsz - lf + t.lr) / Cfg.PREY_SD; if (x * x <= 9) E += a * Math.Exp(-0.5 * x * x) * s.vul * Enc(me, s.mot) * bi * w;   // = Pref(s.b.size, b.feed, t.lr), 로그는 계통에 둔 값
                }
                if (poc) for (int k = 0; k < 3; k++) E += a * Pref(Cfg.POC_SIZE[k], b.feed, t.lr) * Enc(me, 0) * pool[P.POC0 + k][u] * w;
            }
            From(v, 1, true);
            if (v >= NF0) { int h = Bottom(v - NF0); if (h >= 0) From(h, 1, true); }
            else { int f = Under(v); if (f >= 0) From(f, vol[f] / vol[v], false); }
            return (1 - Cfg.EGEST) * E / (1 + Cfg.HANDLE / Allo(t, b) * E);
        }

        // 먹힘률(/일): 그 칸(과 바닥 경계층에서 만나는 이웃 칸) 포식자들의 지금 섭취 배율로
        public double Eaten(Traits t, Body b, int v, int self)
        {
            double vul = Vul(t), me = Mot(t), loss = 0, ls = Math.Log10(b.esd); int mySp = strains[self]?.species ?? -1;
            void By(int u, double w)
            {
                if (presOn)   // 진화 단계: 칸에 베껴 둔 줄(아래와 같은 식·차례)
                {
                    var ps = pres[u];
                    for (int q = 0, n = nPres[u]; q < n; q++)
                    {
                        ref var s = ref ps[q]; if (s.slot == self || (s.sp == mySp && mySp >= 0) || b.esd > Cfg.PREY_MAX * s.feed) continue; double bj = s.bm; if (bj <= Cfg.BMIN || s.eat <= 0) continue;
                        double x = (ls - s.lfeed + s.lr) / Cfg.PREY_SD; if (x * x <= 9) loss += s.att * Math.Exp(-0.5 * x * x) * vul * Enc(s.mot, me) * s.eat * bj * w;
                    }
                }
                else foreach (var s in alive)
                {
                    if (s.slot == self || (s.species == mySp && mySp >= 0) || b.esd > Cfg.PREY_MAX * s.b.feed) continue; int sj = s.slot * V + u; double bj = B[sj]; if (bj <= Cfg.BMIN || eatScale[sj] <= 0) continue;
                    double x = (ls - s.lfeed + s.t.lr) / Cfg.PREY_SD; if (x * x <= 9) loss += s.att * Math.Exp(-0.5 * x * x) * vul * Enc(s.mot, me) * eatScale[sj] * bj * w;   // = Attack × Pref, 계통에 둔 값으로
                }
            }
            By(v, 1);
            if (v >= NF0) { int h = Bottom(v - NF0); if (h >= 0) By(h, 1); }
            else { int f = Under(v); if (f >= 0) By(f, vol[f] / vol[v]); }
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
            // 1) 고르기·돌연변이: 난수는 한 줄로 뽑는다(같은 시드 = 같은 결과)
            // 돌연변이율이 1을 넘으면 후보를 그만큼 더 뽑는다(최대 10배): 공급이 늘면 같은 변이를 더 많이 세는 게 아니라 다른 변이가 더 많이 생긴다.
            // 후보 하나가 대표하는 수(per)는 그만큼 줄여 전체 공급(후보 수 × per)은 돌연변이율에 비례한 그대로. 진화 단계 계산도 그 배수만큼 든다
            double mx = Math.Clamp(knobs.mu, 1, 10); int ne = (int)(Cfg.EVAL * mx);
            int nc = 0;
            for (int k = 0; k < ne; k++)
            {
                double x = rng.U() * E; int lo = 0, hi = m - 1;
                while (lo < hi) { int mid = (lo + hi) / 2; if (supW[mid] < x) lo = mid + 1; else hi = mid; }
                int si = supI[lo], v = si % V; var par = strains[si / V]; if (par == null) continue;
                ref var c = ref cand[nc++];
                c.si = si; c.pg = par.g; c.g = Genome.Mutate(par.g, ref rng, out c.gene);
                c.u = nbr[v].Length > 0 ? nbr[v][(int)(rng.U() * nbr[v].Length)] : -1;
            }
            // 2) 부모와 변이체를 같은 식으로 잰다(병렬). 모두 이 틱 시작의 상태로 잰다: 앞 후보가 정착해도 뒤 후보가 보는 이웃은 그대로(한 틱 = 8시간 안의 근사)
            // 부모 값은 (계통, 칸)마다 한 번만 잰다: 후보 여럿이 같은 부모를 고른다(돌연변이율 10이면 후보 1600개에 부모 칸은 수백 개).
            // 섭취·먹힘(산 계통 전체를 훑는 값)은 변이체의 먹고 먹히는 값이 부모와 똑같으면 부모 것을 쓴다 — 결과는 다시 센 것과 비트까지 같다
            int SV = Cfg.SMAX * V; if (pR == null || pR.Length != SV) { pR = new double[SV]; pI = new double[SV]; pE = new double[SV]; pStamp = new int[SV]; pKeys = new int[cand.Length * 2]; }
            stamp++; int nk = 0; BuildPresent(); presOn = true;
            void Need(int key) { if (pStamp[key] != stamp) { pStamp[key] = stamp; pKeys[nk++] = key; } }
            for (int k = 0; k < nc; k++) { Need(cand[k].si); if (cand[k].u >= 0) Need(cand[k].si / V * V + cand[k].u); }
            Parallel.For(0, nk, po, k =>
            {
                int key = pKeys[k], slot = key / V, v = key % V; var par = strains[slot]; double l = limA[key] > 0 ? limA[key] : 1;
                pI[key] = Intake(par.t, par.b, v, slot); pE[key] = Eaten(par.t, par.b, v, slot); pR[key] = FitWith(par.t, par.b, v, l, pI[key], pE[key]);
            });
            Parallel.For(0, nc, po, k =>
            {
                ref var c = ref cand[k]; int slot = c.si / V, v = c.si % V; var par = strains[slot]; int gene = c.gene;
                c.t = Traits.Of(c.g, gene != G.BRANCH && gene != G.ANGLE && gene != G.ASPECT);
                bool shape = gene == G.SIZE || gene == G.ASPECT || gene == G.BRANCH || gene == G.ANGLE || gene == G.PROP || gene == G.ADH || gene == G.DIFF || gene < G.NA;
                c.body = shape ? Body.Of(c.g, c.t, gene != G.BRANCH && gene != G.ANGLE && gene != G.SIZE && gene != G.ASPECT) : par.b;
                bool sameI = Attack(c.t, c.body) == par.att && c.body.feed == par.b.feed && c.t.lr == par.t.lr && Mot(c.t) == par.mot, sameE = Vul(c.t) == par.vul && c.body.esd == par.b.esd && Mot(c.t) == par.mot;
                double l = limA[c.si] > 0 ? limA[c.si] : 1;
                c.rp = pR[c.si]; c.rm = FitWith(c.t, c.body, v, l, sameI ? pI[c.si] : Intake(c.t, c.body, v, slot), sameE ? pE[c.si] : Eaten(c.t, c.body, v, slot));
                if (c.u >= 0) { int su = slot * V + c.u; double lu = limA[su] > 0 ? limA[su] : 1; c.rpu = pR[su]; c.rmu = FitWith(c.t, c.body, c.u, lu, sameI ? pI[su] : Intake(c.t, c.body, c.u, slot), sameE ? pE[su] : Eaten(c.t, c.body, c.u, slot)); }
            });
            presOn = false;
            // 3) 정착: 차례대로(난수·계통 칸을 쓴다)
            // 후보 하나가 대표하는 변이체 수를 10 × 돌연변이율로 묶는다 — 중립 변이가 칸을 다 채우지 않게. 공급이 적을 땐 그대로.
            // ponytail: 큰 개체군에서는 이 상한이 정착 수를 정하므로 돌연변이율에 비례하게 둔다(그래야 손잡이가 듣는다). 실제로는 클론 간섭으로 비례보다 덜 는다
            double per = Math.Min(10 * knobs.mu / mx, E / ne);
            for (int k = 0; k < nc; k++)
            {
                ref var c = ref cand[k]; int si = c.si, slot = si / V, v = si % V, gene = c.gene; var par = strains[slot]; var g = c.g; var t = c.t; var body = c.body;
                double rp = c.rp, rm = c.rm;
                double gen = Math.Max(0.05, grow[si]);   // 한 세대(1/분열 속도)당 선택 계수
                // 그 칸에서 스스로 늘지 못하는(r ≤ 0) 변이는 드물 때 반드시 사라진다(가지치기 과정): 부모가 이웃에서 흘러와 버티는 칸이어도
                double sc = (rm - rp) / gen, Pk = rm > 0 ? Math.Min(1, Pfix(sc) * per) : 0;
                // 이웃 칸으로 흘러간 변이체: 부모가 거기서 못 사는데 변이체는 산다면 새 터전을 연다(서식지 넓히기)
                int dest = v;
                if (c.u >= 0 && c.rmu > 0 && c.rmu > c.rpu)
                {
                    double s2 = (c.rmu - Math.Max(0, c.rpu)) / Math.Max(0.05, c.rmu), P2 = Math.Min(1, Pfix(s2) * per * 0.1);   // 흘러간 몫(0.1)만큼 공급이 적다
                    if (P2 > Pk) { Pk = P2; sc = s2; dest = c.u; }
                }
                triedToday++;
                // 표류 장벽: |s| < 1/N 인 변이는 부모와 구별되지 않는다(Lynch). 따로 계통을 만들지 않는다(칸은 쓸모 있는 변이에 남긴다).
                // 대신 중립 표류: 그런 변이도 정착 확률 Pfix(s)(중립이면 1/N)로 부모 계통을 통째로 바꾼다(Kimura). 그래서 몸 꼴 유전자도 떠돌다 다른 꼴의 문턱을 넘는다
                // ponytail: 계통 전체를 한 번에 바꾼다(칸마다 따로 퍼지는 과정은 뺌) — 다른 칸에서만 해로운 변이도 같이 들어온다. 같은 틱에 이미 바뀐 부모는 건너뛴다
                if (sc < 1 / Cfg.NFOUND)
                {
                    if (drift && dest == v && rm > 0 && ReferenceEquals(par.g, c.pg) && rng.U() < Pfix(sc) * per)
                    { driftN++; if (!ReferenceEquals(body, par.b)) driftShape++; SetGenome(par, g, t, body); }
                    continue;
                }
                bool up = g[G.ADH] > par.g[G.ADH];
                if (gene == G.ADH) { adhDbg[0]++; adhDbg[up ? 1 : 3]++; if (up) adhDbg[5] += sc; }
                if (rng.U() >= Pk) continue;
                if (gene == G.ADH) adhDbg[up ? 2 : 4]++;
                evoDbg[0]++; if ((dest == v ? rm : c.rmu) <= 0) evoDbg[1]++;
                var ch = NewStrain(g, slot, gene, t, body); if (ch == null) continue;
                double f = B[si] * Cfg.FOUND; B[si] -= f; B[ch.slot * V + dest] += f * vol[v] / vol[dest];   // 농도(μM)를 옮기므로 부피 비로(몰 보존)
                ch.jump = ShapeDist(par, t, body); ch.sel = sc; ch.prev = f * vol[v] * 1e-6;
                if (jumps.Count >= 4096) jumps.RemoveAt(0); jumps.Add(ch.jump);
                keptToday++;
            }
        }

        // 후보 하나: 고른 (부모 계통, 칸), 돌연변이 유전체(pg = 그때 부모 유전체), 흘러갈 이웃 칸(없으면 −1), 부모·변이체의 순증가율(그 칸, 이웃 칸)
        struct Cand { public int si, u, gene; public float[] g, pg; public Traits t; public Body body; public double rp, rm, rpu, rmu; }
        [NonSerialized] readonly Cand[] cand = new Cand[Cfg.EVAL * 10];
        // 칸마다 이 틱에 사는 계통(alive 차례 그대로). 진화 단계의 섭취·먹힘은 이 목록만 훑는다: 없는 계통은 원래도 건너뛰므로 결과는 같고, 칸마다 사는 계통은 전체의 일부라 빠르다
        // 섭취·먹힘이 읽는 값은 칸마다 한 줄로 베껴 둔다(계통 → 몸·형질 객체를 따라가지 않게): 진화 단계 동안 B·eatScale·포식 값은 안 바뀌어 값·차례가 같다(결과 비트 같음).
        // IL2CPP 앱에서 이 두 훑기가 진화 단계 표본의 ⅓이었다(List 읽기·객체 따라가기)
        struct Pre { public int slot, sp; public double esd, feed, lsz, lfeed, lr, vul, att, mot, bm, eat; }
        [NonSerialized] Pre[][] pres; [NonSerialized] int[] nPres; [NonSerialized] bool presOn;
        void BuildPresent()
        {
            if (pres == null || pres.Length != V) { pres = new Pre[V][]; nPres = new int[V]; for (int v = 0; v < V; v++) pres[v] = new Pre[Cfg.SMAX]; }
            Array.Clear(nPres, 0, V);
            foreach (var s in alive)
            {
                int o = s.slot * V;
                for (int v = 0; v < V; v++)
                    if (B[o + v] > Cfg.BMIN)
                        pres[v][nPres[v]++] = new Pre { slot = s.slot, sp = s.species, esd = s.b.esd, feed = s.b.feed, lsz = s.lsz, lfeed = s.lfeed, lr = s.t.lr, vul = s.vul, att = s.att, mot = s.mot, bm = B[o + v], eat = eatScale[o + v] };
            }
        }
        [NonSerialized] double[] pR, pI, pE; [NonSerialized] int[] pStamp, pKeys; [NonSerialized] int stamp;   // 이 틱 부모 값: 순증가율·섭취·먹힘, 잰 틱 표시

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
            var nOf = new Dictionary<int, int>(); foreach (var s in alive) nOf[s.species] = nOf.GetValueOrDefault(s.species) + 1;
            foreach (var s in order)
            {
                bool tiny = s.total <= Cfg.RARE * all;   // 생물권의 이 몫보다 적으면 사라진 것으로 친다(셀 수 없는 꼬리)
                if (!tiny && (k >= need || nOf[s.species] < 2)) continue;   // 칸이 모자라 지울 때 종의 마지막 계통은 남긴다: 멸종은 생태로만
                nOf[s.species]--;
                int age = (int)Day - s.born; pruneDbg[age < 1 ? 0 : age < 10 ? 1 : 2]++; if (age < 10) { evoDbg[3]++; if (s.rate > 0) evoDbg[2]++; }
                Remove(s, tiny ? null : Kin(s)); k++;
            }
            RebuildAlive();
        }

        // 칸이 모자라 따로 세지 않게 된 계통은 죽은 게 아니다: 같은 종의 가장 가까운 산 친척(부모, 없으면 유전체가 가장 가까운 것)으로 합친다
        Strain Kin(Strain s)
        {
            Strain best = null; double bd = double.MaxValue;
            foreach (var o in alive)
            {
                if (o == s || o.slot < 0 || o.species != s.species) continue;   // 다른 종이 된 부모에 합치면 그 종이 사라진다
                if (o.id == s.parent) return o;
                double d = Genome.Dist(o.g, s.g); if (d < bd) { bd = d; best = o; }
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
