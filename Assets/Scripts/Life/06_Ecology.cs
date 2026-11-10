// 칸 하나의 생태 한 틱: 먹고 먹히기(크기 커널) → 세포 생리 → 풀이 모자라면 나눠 쓰기 → 성장·죽음·배설을 풀과 생물량에 반영.
// 칸끼리는 이 단계에서 서로 건드리지 않으므로 칸마다 병렬로 돈다.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Life
{
    public sealed partial class Planet
    {
        // 계통·칸별 오늘 수입(mol): 0 빛 고정 · 1 화학합성 고정 · 2 산 먹이 · 3 사체 · 4 녹은 유기물 · 5 만든 O2 · 6 산소 호흡으로 태운 C · 7 태운 C
        public const int NINC = 11;
        public double[] inc;
        // [slot·V+v] 마지막 틱 값: 순성장률, 자원 나눔 몫, 포식 섭취 배율(Holling), 먹힘률 — 돌연변이 평가에 쓴다
        public const int LQ = 4 + P.N;
        // seen[풀·V + v]: 한 틱(8시간) 동안 세포가 겪는 평균 농도의 몫. 쓰는 속도가 풀보다 빠르면 틱 안에서 바닥나므로 낮춘다
        // (적게 쓰는 공여체 하나가 바닥났다고 세포 전체 성장을 막지 않게: 세포는 그 공여체를 덜 쓰고 다른 것으로 옮긴다)
        public double[] seen;
        public double[] grow, limA, eatScale, eatenRate, limBio, limLast;   // limBio[(q + LQ·광합성?)·V + v]: 성장을 막은 것별 생물량(mol). q 0–3 = 에너지·리보솜·질소·인, 4 + 풀 = 칸 자원 나눔
        public double[] binB;                 // [v·125 + 수용체 칸] 숙주 밀도(바이러스)
        public double[] nfixV;                // 칸별 질소 고정 누적(mol), 매일 장부 inN으로

        sealed class Scratch
        {
            public int[] slot = new int[Cfg.SMAX + 3];
            public double[] b = new double[Cfg.SMAX + 3],
                a = new double[Cfg.SMAX], scale = new double[Cfg.SMAX], loss = new double[Cfg.SMAX + 3], intake = new double[Cfg.SMAX],
                prey = new double[Cfg.SMAX], det = new double[Cfg.SMAX], lim = new double[Cfg.SMAX],
                eat = new double[Cfg.SMAX + 3], K = new double[Cfg.SMAX + 3], capF = new double[Cfg.SMAX + 3], dc = new double[P.N], lb = new double[2 * LQ];
            public int[] act = new int[Cfg.SMAX], limK = new int[Cfg.SMAX], col = new int[Cfg.SMAX + 3], capI = new int[Cfg.SMAX + 3];
            public Rates[] r = new Rates[Cfg.SMAX];
            public readonly double[] cons = new double[P.N], f = new double[P.N];
        }
        [NonSerialized] readonly ThreadLocal<Scratch> scratch = new(() => new Scratch());

        // 포식 계수 kK[먹는 계통 slot · KW + 먹이 열] = 공격률 × 크기 선호 × 먹이 약함(같은 종이면 0). 칸·틱과 상관없이 계통 짝으로 정해지므로
        // 틱 처음에 계통이나 종이 바뀐 slot의 행(먹는 쪽)·열(먹이)만 다시 센다. 먹이 열은 계통 SMAX개 + 사체 세 칸
        const int KW = Cfg.SMAX + 3;
        static readonly double[] PocLsz = { Math.Log10(Cfg.POC_SIZE[0]), Math.Log10(Cfg.POC_SIZE[1]), Math.Log10(Cfg.POC_SIZE[2]) };
        [NonSerialized] int[] nV;   // 칸마다 지난 틱 계통 수(병렬 순서용)
        [NonSerialized] Strain[] kOwn; [NonSerialized] int[] kSp; [NonSerialized] bool[] kNew; [NonSerialized] double[] kK;
        void PredCache()
        {
            if (kK == null) { kK = new double[Cfg.SMAX * KW]; kOwn = new Strain[Cfg.SMAX]; kSp = new int[Cfg.SMAX]; kNew = new bool[Cfg.SMAX]; }
            bool any = false;
            for (int s = 0; s < Cfg.SMAX; s++)
            {
                var st = strains[s]; kNew[s] = kOwn[s] != st || st != null && kSp[s] != st.species;
                if (kNew[s]) { kOwn[s] = st; kSp[s] = st?.species ?? 0; any = true; }
            }
            if (any) Parallel.For(0, Cfg.SMAX, po, PredRow);
        }
        void PredRow(int j)
        {
            var sj = strains[j]; if (sj == null) return; int r = j * KW;
            for (int i = 0; i < KW; i++)
            {
                if (!kNew[j] && (i >= Cfg.SMAX || !kNew[i])) continue;   // 먹는 쪽도 먹이도 그대로면 그대로
                double sz, lsz, vul, mi = 0;
                if (i < Cfg.SMAX)
                {
                    var si = strains[i];
                    if (si == null || i == j || si.species == sj.species && si.species >= 0) { kK[r + i] = 0; continue; }   // 자기·같은 종(친척)은 먹지 않는다
                    sz = si.b.esd; lsz = si.lsz; vul = si.vul; mi = si.mot;
                }
                else { sz = Cfg.POC_SIZE[i - Cfg.SMAX]; lsz = PocLsz[i - Cfg.SMAX]; vul = 1; }
                double k = 0;
                if (sz <= Cfg.PREY_MAX * sj.b.feed) { double x = (lsz - sj.lfeed + sj.t.lr) / Cfg.PREY_SD; if (x * x <= 9) k = sj.att * Math.Exp(-0.5 * x * x) * vul * Enc(sj.mot, mi); }   // 사체는 안 움직이는 먹이
                kK[r + i] = k;
            }
        }

        void AllocBio()
        {
            inc = new double[NINC * Cfg.SMAX * V];
            grow = new double[Cfg.SMAX * V]; limA = new double[Cfg.SMAX * V]; eatScale = new double[Cfg.SMAX * V]; eatenRate = new double[Cfg.SMAX * V]; limBio = new double[2 * LQ * V]; limLast = new double[2 * LQ];
            seen = new double[P.N * V]; Array.Fill(seen, 1.0);
            binB = new double[V * 125]; nfixV = new double[V]; nV = new int[V];
        }

        public Env EnvAt(int v)
        {
            var c = pool; var q = seen; int k = V;
            return new Env { T = temp[v], light = light[v], dry = dry[v], CO2 = c[P.CO2][v] * q[P.CO2 * k + v], O2 = c[P.O2][v] * q[P.O2 * k + v], H2 = c[P.H2][v] * q[P.H2 * k + v],
                CH4 = c[P.CH4][v] * q[P.CH4 * k + v], H2S = c[P.H2S][v] * q[P.H2S * k + v], FE2 = c[P.FE2][v] * q[P.FE2 * k + v], SO4 = c[P.SO4][v] * q[P.SO4 * k + v],
                NH4 = c[P.NH4][v] * q[P.NH4 * k + v], PO4 = c[P.PO4][v] * q[P.PO4 * k + v], DOC = c[P.DOC][v] * q[P.DOC * k + v] };
        }

        // 먹이 선호: 먹는 크기의 10^−lr 근처(로그 정규, lr은 유전자). 먹는 크기보다 조금 큰 것까지는 감싸 먹는다(와편모류처럼), 그보다 크면 못 먹는다
        public static double Pref(double prey, double feed, double lr)
        {
            if (prey > Cfg.PREY_MAX * feed) return 0;
            double x = (Math.Log10(prey / feed) + lr) / Cfg.PREY_SD;
            return x * x > 9 ? 0 : Math.Exp(-0.5 * x * x);
        }
        public static double Allo(Traits t, Body b) { double cl = t.dCell; return Math.Min(1, Math.Pow(cl / Cfg.FEED_REF, -Cfg.FEED_EXP)) * Math.Pow(b.feed / cl, -Cfg.MET_EXP * 3); }   // 큰 포식자일수록 몸 탄소당 덜 삼킨다(최대 섭취). 걸러 먹는 양(만남)은 CLR_EXP로 훨씬 덜 준다
        public static double Attack(Traits t, Body b) => Cfg.ATTACK * Math.Min(1, Math.Pow(b.feed / Cfg.FEED_REF, -Cfg.CLR_EXP)) * t.phi[G.PHAGO] * (1 - 0.7 * t.Sat(G.WALL, 0.02) * (1 - t.diff)) * 1.5 * (0.3 + 0.7 * b.exposed);   // 벽은 홀세포의 삼켜 먹기를 막는다(벽 있는 세포는 식세포 작용을 못 함). 분화한 몸은 벽 없는 먹는 세포(입·소화관)로 먹어 갑옷을 입고도 사냥한다(절지동물)
        public static double Vul(Traits t) => 1 - 0.8 * t.Sat(G.WALL, 0.02);
        public static double Mot(Traits t) => t.Sat(G.MOT, 0.01);
        public static double Enc(double mEat, double mPrey) => 1 - (1 - Cfg.ENC0) * (1 - mEat) * (1 - mPrey);   // 만남 배율(Attack의 1.5에 곱함): 둘 다 움직이면 1, 둘 다 안 움직이면 ENC0
        // 바이러스 죽음(kill-the-winner: 같은 수용체 칸 숙주 밀도에 비례). 덩이 속 세포는 겉 세포가 막아 덜 걸린다 —
        // 대장균 생물막 안쪽 세포는 T7 파지에 거의 안 걸림(Vidakovic 2018). 바탕 0.2는 막이 없는 덩이도 조금은 뚫린다는 가정
        public double VirusMort(Traits t, Body b, int v) { double hb = binB[v * 125 + t.vbin]; return Cfg.VIRUS * hb / (hb + Cfg.VIRUS_K) * (0.2 + 0.8 * b.exposed) * Cell.Clock(t, b); }   // 병도 몸 시계를 따른다(바이러스는 숙주 세포의 대사로 불어남; 바다 자연 사망 ∝ 몸무게^−0.25, Peterson & Wroblewski 1984). 홀세포는 시계 1이라 그대로
        static int PocClass(double size) => size < 3 ? P.POC0 : size < 30 ? P.POC1 : P.POC2;

        void Bio(int v)
        {
            if (!live[v]) return;
            var S = scratch.Value; var c = pool; double dt = Cfg.DT, M = vol[v] * 1e-6;   // M: μM → mol
            int n = 0;
            for (int i = 0; i < alive.Count; i++)
            {
                var s = alive[i]; double bb = B[s.slot * V + v];
                if (bb > Cfg.BMIN) { S.slot[n] = s.slot; S.col[n] = s.slot; S.b[n] = bb; n++; }
                else if (bb != 0) { ToDoc(c, v, bb); B[s.slot * V + v] = 0; }   // 사라진 몫은 녹은 유기물로
            }
            for (int k = 0; k < 125; k++) binB[v * 125 + k] = 0;
            nV[v] = n;
            if (n == 0) return;
            for (int i = 0; i < n; i++) binB[v * 125 + strains[S.slot[i]].t.vbin] += S.b[i];
            var e = EnvAt(v);

            // 1) 포식·청소: 먹이 = 다른 계통 + 사체 세 칸
            int np = n;
            for (int k = 0; k < 3; k++) { S.b[np] = c[P.POC0 + k][v]; S.slot[np] = -1; S.col[np] = Cfg.SMAX + k; np++; }
            double tot = 0; for (int i = 0; i < np; i++) tot += S.b[i];
            // 먹는 쪽 j마다 섭취 E_j = Σ_i K[j,i]·b_i(K는 계통 짝 표 kK에서), 처리 시간 배율, 한 틱 섭취 배율 sb_j를 낸다.
            // 먹이 i가 먹히는 양은 b_i·Σ_j K[j,i]·sb_j(= want_i)라서 같은 줄을 먹이 쪽으로 한 번 더 더해 둔다.
            // 한 틱에 90 %를 넘게 먹히는 먹이만 깎고(L_i), 깎은 몫을 그 먹이를 먹은 쪽들의 섭취에서 뺀다: 짝마다 흐름을 다시 셀 필요가 없다
            int na = 0; var row = S.K;
            for (int i = 0; i < np; i++) S.loss[i] = 0;
            for (int j = 0; j < n; j++)
            {
                var sj = strains[S.slot[j]]; S.scale[j] = 0; S.intake[j] = 0; S.prey[j] = 0; S.det[j] = 0;
                if (sj.att * tot < 1e-4) continue;
                int kr = S.slot[j] * KW; double Ep = 0, Ed = 0;
                for (int i = 0; i < n; i++) { double k = kK[kr + S.col[i]]; row[i] = k; Ep += k * S.b[i]; }
                for (int i = n; i < np; i++) { double k = kK[kr + S.col[i]]; row[i] = k; Ed += k * S.b[i]; }
                double E = Ep + Ed; if (E <= 0) continue;
                S.act[na++] = j;
                S.scale[j] = 1 / (1 + Cfg.HANDLE / sj.allo * E);
                double sb = S.scale[j] * S.b[j] * dt;
                S.prey[j] = Ep * sb; S.det[j] = Ed * sb;
                for (int i = 0; i < np; i++) S.loss[i] += row[i] * sb;
            }
            int ncap = 0;
            for (int i = 0; i < np; i++)
            {
                double want = S.loss[i] * S.b[i];
                if (want > 0.9 * S.b[i]) { S.eat[i] = 0.9 * S.b[i]; S.capI[ncap] = i; S.capF[ncap++] = S.b[i] * (1 - 0.9 * S.b[i] / want); }   // capF = b_i(1 − L_i)
                else S.eat[i] = want;
            }
            for (int q = 0; q < na; q++)
            {
                int j = S.act[q];
                if (ncap > 0)
                {
                    int kr = S.slot[j] * KW; double sb = S.scale[j] * S.b[j] * dt, dp = 0, dd = 0;
                    for (int c2 = 0; c2 < ncap; c2++) { int i = S.capI[c2]; double x = kK[kr + S.col[i]] * S.capF[c2]; if (i < n) dp += x; else dd += x; }
                    S.prey[j] = Math.Max(0, S.prey[j] - dp * sb); S.det[j] = Math.Max(0, S.det[j] - dd * sb);
                }
                S.intake[j] = S.prey[j] + S.det[j];
            }
            // 먹힌 양을 먹이에서 뺀다(사체는 풀에서)
            for (int i = 0; i < np; i++)
            {
                double eaten = S.eat[i];
                if (i < n) { eatenRate[S.slot[i] * V + v] = S.b[i] > 0 ? eaten / (S.b[i] * dt) : 0; S.b[i] -= eaten; }
                else c[P.POC0 + i - n][v] = Math.Max(0, c[P.POC0 + i - n][v] - eaten);
            }

            // 2) 생리
            for (int k = 0; k < P.N; k++) S.cons[k] = 0;
            for (int i = 0; i < n; i++)
            {
                var s = strains[S.slot[i]]; double bi = S.b[i];
                double phago = S.b[i] > 0 ? S.intake[i] * (1 - Cfg.EGEST) / (Math.Max(1e-30, bi) * dt) : 0;
                double vm = VirusMort(s.t, s.b, v);
                var r = Cell.Grow(s.t, s.b, e, phago, vm); S.r[i] = r;
                double w = bi * dt;
                S.cons[P.CO2] += Math.Max(0, -r.dCO2) * w; S.cons[P.O2] += Math.Max(0, -r.dO2) * w; S.cons[P.H2] += Math.Max(0, -r.dH2) * w;
                S.cons[P.CH4] += Math.Max(0, -r.dCH4) * w; S.cons[P.H2S] += Math.Max(0, -r.dH2S) * w; S.cons[P.FE2] += Math.Max(0, -r.dFE2) * w;
                S.cons[P.SO4] += Math.Max(0, -r.dSO4) * w; S.cons[P.NH4] += r.nh4 * w; S.cons[P.PO4] += r.po4 * w; S.cons[P.DOC] += r.docUp * w;
            }
            // 3) 풀이 모자라면 쓰는 쪽끼리 비례로 나눈다
            for (int k = 0; k < P.N; k++) S.f[k] = S.cons[k] > 0.9 * c[k][v] ? 0.9 * c[k][v] / S.cons[k] : 1;
            for (int k = 0; k < P.N; k++)
            {
                double ratio = S.cons[k] / Math.Max(1e-30, 0.9 * c[k][v]);
                seen[k * V + v] = Math.Clamp(seen[k * V + v] * Math.Min(1.2, 0.95 / Math.Max(1e-9, ratio)), 1e-9, 1);
            }
            for (int i = 0; i < n; i++)
            {
                ref var r = ref S.r[i]; double l = 1; int k0 = -1;
                void Lim(bool uses, int k) { if (uses && S.f[k] < l) { l = S.f[k]; k0 = k; } }
                Lim(r.dCO2 < 0, P.CO2); Lim(r.dO2 < 0, P.O2); Lim(r.dH2 < 0, P.H2); Lim(r.dCH4 < 0, P.CH4); Lim(r.dH2S < 0, P.H2S); Lim(r.dFE2 < 0, P.FE2);
                Lim(r.dSO4 < 0, P.SO4); Lim(r.nh4 > 0, P.NH4); Lim(r.po4 > 0, P.PO4); Lim(r.docUp > 0, P.DOC);
                S.lim[i] = l; S.limK[i] = k0;
            }

            // 4) 반영. 칸 풀·질소 고정·한계 생물량은 칸 안 합(dc·nfx·lb)으로 모았다가 끝에 한 번 쓴다(이웃 칸을 도는 스레드와 같은 캐시 줄을 덜 다투게)
            var dc = S.dc; var lb = S.lb; Array.Clear(dc, 0, dc.Length); Array.Clear(lb, 0, lb.Length); double nfx = 0;
            void Doc(double x) { dc[P.DOC] += x; dc[P.NH4] += x * 16 / 106; dc[P.PO4] += x / 106; }
            for (int i = 0; i < n; i++)
            {
                var s = strains[S.slot[i]]; ref var r = ref S.r[i]; double l = S.lim[i], bi = Math.Max(0, S.b[i]), w = bi * dt * l;
                int si = s.slot * V + v;
                double gpos = Math.Max(0, r.g) * l, gneg = Math.Min(0, r.g);
                double fix = r.fix * l, assim = r.assim * l, catab = r.catab * l;
                dc[P.CO2] += r.dCO2 * w; dc[P.O2] += r.dO2 * w; dc[P.H2] += r.dH2 * w; dc[P.CH4] += r.dCH4 * w;
                dc[P.H2S] += r.dH2S * w; dc[P.FE2] += r.dFE2 * w; dc[P.FE3] -= r.dFE2 * w; dc[P.SO4] += r.dSO4 * w;
                dc[P.DOC] -= r.docUp * w; dc[P.NH4] -= r.nh4 * w; dc[P.PO4] -= r.po4 * w;
                nfx += r.nfix * w * M;   // 질소 고정(대기 N2 → 몸): 칸별로 모아 매일 장부에
                dc[P.NH4] += r.catabNP * l * 16 / 106 * bi * dt; dc[P.PO4] += r.catabNP * l / 106 * bi * dt;   // 태운 먹이의 N·P는 다시 풀로
                // 탄소 수지: 새 몸 = 고정 + 동화. 남는 몫은 먼저 고정을 덜 한 것으로(CO2로 되돌림), 그래도 남으면 동화 탄소를 녹은 유기물로 내보낸다
                double cin = fix + assim, built = Math.Min(gpos, cin), exc = cin - built;
                double excFix = Math.Min(exc, fix), excOrg = exc - excFix;
                dc[P.DOC] += excOrg * bi * dt; dc[P.CO2] += excFix * bi * dt;
                // 질소·인 수지: 들어온 것(무기염 + 고정 + 먹이 몫) − 새 몸에 든 것 = 남는 몫은 무기염으로
                double nLeft = (r.nh4 + r.nfix) * l + r.assimNP * l * 16 / 106 - built * 16 / 106, pLeft = r.po4 * l + r.assimNP * l / 106 - built / 106;
                dc[P.NH4] += nLeft * bi * dt; dc[P.PO4] += pLeft * bi * dt;
                // 먹고 남긴 것: 똥(30 %) → 사체, 쓰지 못한 섭취 → 녹은 유기물
                double eatTot = S.intake[i];
                dc[P.POC1] += eatTot * Cfg.EGEST;
                double phUsed = r.phagoUsed * l * bi * dt, phAvail = eatTot * (1 - Cfg.EGEST);
                Doc(Math.Max(0, phAvail - phUsed));
                // 죽음: 바이러스(→ 녹은 유기물), 굶음(→ 녹은 유기물), 나머지(→ 몸 크기에 맞는 사체)
                double vm = VirusMort(s.t, s.b, v);
                double other = Math.Max(0, r.mort - vm), rate = other + vm - gneg, lost = bi * (1 - Math.Exp(-rate * dt));
                if (rate > 0) { dc[PocClass(s.b.size)] += lost * other / rate; Doc(lost * (vm - gneg) / rate); }
                B[si] = bi + built * bi * dt - lost;
                var ph = s.t.phi; bool isPh = ph[G.PHOTO] >= Math.Max(Math.Max(ph[G.CHEMO], ph[G.OSMO]), ph[G.PHAGO]);
                lb[(l < 0.9 ? 4 + S.limK[i] : r.lim) + (isPh ? LQ : 0)] += bi * M;
                grow[si] = Math.Max(0, r.g) * l; limA[si] = l; eatScale[si] = S.scale[i];   // grow = 분열 속도(돌연변이 공급)
                // 오늘 수입(mol)
                int o = s.slot * V + v, st = Cfg.SMAX * V;
                double phF = r.fix > 0 ? r.fixPh / r.fix : 0;
                inc[0 * st + o] += fix * phF * bi * dt * M; inc[1 * st + o] += fix * (1 - phF) * bi * dt * M;
                double usedF = eatTot > 0 ? Math.Min(1, phUsed / Math.Max(1e-30, phAvail)) : 0;
                inc[2 * st + o] += S.prey[i] * (1 - Cfg.EGEST) * usedF * M; inc[3 * st + o] += S.det[i] * (1 - Cfg.EGEST) * usedF * M;
                inc[4 * st + o] += r.docUp * w * M; inc[5 * st + o] += r.o2Made * w * M;
                inc[6 * st + o] += catab * r.aerFrac * bi * dt * M; inc[7 * st + o] += catab * bi * dt * M;
                inc[8 * st + o] += r.photoATP * w * M; inc[9 * st + o] += r.chemoATP * w * M; inc[10 * st + o] += r.orgATP * w * M;
            }
            for (int k = 0; k < P.N; k++) { c[k][v] += dc[k]; if (c[k][v] < 0) c[k][v] = 0; }
            nfixV[v] += nfx; for (int k = 0; k < 2 * LQ; k++) limBio[k * V + v] += lb[k];
        }
    }
}
