// 칸 하나의 생태 한 틱: 먹고 먹히기(크기 커널) → 세포 생리 → 풀이 모자라면 나눠 쓰기 → 성장·죽음·배설을 풀과 생물량에 반영.
// 칸끼리는 이 단계에서 서로 건드리지 않으므로 칸마다 병렬로 돈다.
using System;
using System.Threading;

namespace Life
{
    public sealed partial class Planet
    {
        // 계통·칸별 오늘 수입(mol): 0 빛 고정 · 1 화학합성 고정 · 2 산 먹이 · 3 사체 · 4 녹은 유기물 · 5 만든 O2 · 6 산소 호흡으로 태운 C · 7 태운 C
        public const int NINC = 8;
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
            public double[] b = new double[Cfg.SMAX + 3], size = new double[Cfg.SMAX + 3], vul = new double[Cfg.SMAX + 3],
                a = new double[Cfg.SMAX], scale = new double[Cfg.SMAX], loss = new double[Cfg.SMAX + 3], intake = new double[Cfg.SMAX],
                prey = new double[Cfg.SMAX], det = new double[Cfg.SMAX], lim = new double[Cfg.SMAX],
                lsz = new double[Cfg.SMAX + 3], eat = new double[Cfg.SMAX + 3], K = new double[Cfg.SMAX * (Cfg.SMAX + 3)];
            public int[] act = new int[Cfg.SMAX], limK = new int[Cfg.SMAX], spc = new int[Cfg.SMAX + 3];
            public Rates[] r = new Rates[Cfg.SMAX];
            public readonly double[] cons = new double[P.N], f = new double[P.N];
        }
        readonly ThreadLocal<Scratch> scratch = new(() => new Scratch());

        void AllocBio()
        {
            inc = new double[NINC * Cfg.SMAX * V];
            grow = new double[Cfg.SMAX * V]; limA = new double[Cfg.SMAX * V]; eatScale = new double[Cfg.SMAX * V]; eatenRate = new double[Cfg.SMAX * V]; limBio = new double[2 * LQ * V]; limLast = new double[2 * LQ];
            seen = new double[P.N * V]; Array.Fill(seen, 1.0);
            binB = new double[V * 125]; nfixV = new double[V];
        }

        public Env EnvAt(int v)
        {
            var c = pool; var q = seen; int k = V;
            return new Env { T = temp[v], light = light[v], dry = dry[v], CO2 = c[P.CO2][v] * q[P.CO2 * k + v], O2 = c[P.O2][v] * q[P.O2 * k + v], H2 = c[P.H2][v] * q[P.H2 * k + v],
                CH4 = c[P.CH4][v] * q[P.CH4 * k + v], H2S = c[P.H2S][v] * q[P.H2S * k + v], FE2 = c[P.FE2][v] * q[P.FE2 * k + v], SO4 = c[P.SO4][v] * q[P.SO4 * k + v],
                NH4 = c[P.NH4][v] * q[P.NH4 * k + v], PO4 = c[P.PO4][v] * q[P.PO4 * k + v], DOC = c[P.DOC][v] * q[P.DOC * k + v] };
        }

        // 먹이 선호: 몸 크기의 1/10 근처(로그 정규). 몸보다 조금 큰 것까지는 감싸 먹는다(와편모류처럼), 그보다 크면 못 먹는다
        static double Pref(double prey, double pred)
        {
            if (prey > Cfg.PREY_MAX * pred) return 0;
            double x = (Math.Log10(prey / pred) + 1) / Cfg.PREY_SD;
            return x * x > 9 ? 0 : Math.Exp(-0.5 * x * x);
        }
        public static double Attack(Traits t, Body b) => Cfg.ATTACK * t.phi[G.PHAGO] * (1 - 0.7 * t.Sat(G.WALL, 0.02)) * (0.5 + t.Sat(G.MOT, 0.01)) * (0.3 + 0.7 * b.exposed);
        public static double Vul(Traits t) => 1 - 0.8 * t.Sat(G.WALL, 0.02);
        static int PocClass(double size) => size < 10 ? P.POC0 : size < 100 ? P.POC1 : P.POC2;

        void Bio(int v)
        {
            if (!live[v]) return;
            var S = scratch.Value; var c = pool; double dt = Cfg.DT, M = vol[v] * 1e-6;   // M: μM → mol
            int n = 0;
            for (int i = 0; i < alive.Count; i++)
            {
                var s = alive[i]; double bb = B[s.slot * V + v];
                if (bb > Cfg.BMIN) { S.slot[n] = s.slot; S.b[n] = bb; S.size[n] = s.b.size; S.vul[n] = Vul(s.t); S.spc[n] = s.species; n++; }
                else if (bb != 0) { c[P.DOC][v] += bb; B[s.slot * V + v] = 0; }   // 사라진 몫은 녹은 유기물로
            }
            for (int k = 0; k < 125; k++) binB[v * 125 + k] = 0;
            if (n == 0) return;
            for (int i = 0; i < n; i++) binB[v * 125 + strains[S.slot[i]].t.vbin] += S.b[i];
            var e = EnvAt(v);

            // 1) 포식·청소: 먹이 = 다른 계통 + 사체 세 칸
            int np = n;
            for (int k = 0; k < 3; k++) { S.size[np] = Cfg.POC_SIZE[k]; S.vul[np] = 1; S.b[np] = c[P.POC0 + k][v]; S.slot[np] = -1; S.spc[np] = -2; np++; }
            double tot = 0; for (int i = 0; i < np; i++) { tot += S.b[i]; S.loss[i] = 0; S.lsz[i] = Math.Log10(S.size[i]); S.eat[i] = 0; }
            // 먹는 쪽 j마다 계수 K[j,i] = 공격률 × 크기 선호 × 먹이 약함 을 한 번만 셈(아래 세 번 쓴다)
            int na = 0;
            for (int j = 0; j < n; j++)
            {
                var sj = strains[S.slot[j]]; S.a[j] = Attack(sj.t, sj.b); S.scale[j] = 0; S.intake[j] = 0; S.prey[j] = 0; S.det[j] = 0;
                if (S.a[j] * tot < 1e-4) continue;
                int row = na * np; double E = 0;
                for (int i = 0; i < np; i++)
                {
                    double k = 0;
                    if (i != j && (S.spc[i] != S.spc[j] || S.spc[i] < 0) && S.size[i] <= Cfg.PREY_MAX * S.size[j])   // 같은 종(친척)은 먹지 않는다
                    {
                        double x = (S.lsz[i] - S.lsz[j] + 1) / Cfg.PREY_SD;
                        if (x * x <= 9) k = S.a[j] * Math.Exp(-0.5 * x * x) * S.vul[i];
                    }
                    S.K[row + i] = k; E += k * S.b[i];
                }
                if (E <= 0) continue;
                S.act[na++] = j;
                S.scale[j] = 1 / (1 + Cfg.HANDLE * E);
                double sb = S.scale[j] * S.b[j] * dt;
                for (int i = 0; i < np; i++) S.loss[i] += S.K[row + i] * S.b[i] * sb;
            }
            for (int i = 0; i < np; i++) S.loss[i] = S.loss[i] > 0.9 * S.b[i] ? 0.9 * S.b[i] / S.loss[i] : 1;   // 이제 loss = 먹힘 상한 배율
            for (int q = 0; q < na; q++)
            {
                int j = S.act[q], row = q * np; double sb = S.scale[j] * S.b[j] * dt;
                for (int i = 0; i < np; i++)
                {
                    double fl = S.K[row + i] * S.b[i] * sb * S.loss[i]; if (fl <= 0) continue;
                    if (i < n) S.prey[j] += fl; else S.det[j] += fl;
                    S.eat[i] += fl;
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
                double hb = binB[v * 125 + s.t.vbin], vm = Cfg.VIRUS * hb / (hb + Cfg.VIRUS_K);
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

            // 4) 반영
            for (int i = 0; i < n; i++)
            {
                var s = strains[S.slot[i]]; ref var r = ref S.r[i]; double l = S.lim[i], bi = Math.Max(0, S.b[i]), w = bi * dt * l;
                int si = s.slot * V + v;
                double gpos = Math.Max(0, r.g) * l, gneg = Math.Min(0, r.g);
                double fix = r.fix * l, assim = r.assim * l, catab = r.catab * l;
                c[P.CO2][v] += r.dCO2 * w; c[P.O2][v] += r.dO2 * w; c[P.H2][v] += r.dH2 * w; c[P.CH4][v] += r.dCH4 * w;
                c[P.H2S][v] += r.dH2S * w; c[P.FE2][v] += r.dFE2 * w; c[P.FE3][v] -= r.dFE2 * w; c[P.SO4][v] += r.dSO4 * w;
                c[P.DOC][v] -= r.docUp * w; c[P.NH4][v] -= r.nh4 * w; c[P.PO4][v] -= r.po4 * w;
                nfixV[v] += r.nfix * w * M;   // 질소 고정(대기 N2 → 몸): 칸별로 모아 매일 장부에
                c[P.NH4][v] += catab * 16 / 106 * bi * dt; c[P.PO4][v] += catab / 106 * bi * dt;   // 태운 유기물의 N·P는 다시 풀로
                // 탄소 수지: 새 몸 = 고정 + 동화. 남는 몫은 먼저 고정을 덜 한 것으로(CO2로 되돌림), 그래도 남으면 동화 탄소를 녹은 유기물로 내보낸다
                // (이 순서여야 생리에서 셈한 무기 질소·인 필요량 = (성장 − 동화)와 맞는다)
                double cin = fix + assim, built = Math.Min(gpos, cin), exc = cin - built;
                double excFix = Math.Min(exc, fix), excOrg = exc - excFix;
                c[P.DOC][v] += excOrg * bi * dt; c[P.CO2][v] += excFix * bi * dt;
                // 먹고 남긴 것: 똥(30 %) → 사체, 쓰지 못한 섭취 → 녹은 유기물
                double eatTot = S.intake[i];
                c[P.POC1][v] += eatTot * Cfg.EGEST;
                double phUsed = r.phagoUsed * l * bi * dt, phAvail = eatTot * (1 - Cfg.EGEST);
                c[P.DOC][v] += Math.Max(0, phAvail - phUsed);
                // 죽음: 바이러스(→ 녹은 유기물), 굶음(→ 녹은 유기물), 나머지(→ 몸 크기에 맞는 사체)
                double hb = binB[v * 125 + s.t.vbin], vm = Cfg.VIRUS * hb / (hb + Cfg.VIRUS_K);
                double other = Math.Max(0, r.mort - vm), rate = other + vm - gneg, lost = bi * (1 - Math.Exp(-rate * dt));
                if (rate > 0) { c[PocClass(s.b.size)][v] += lost * other / rate; c[P.DOC][v] += lost * (vm - gneg) / rate; }
                B[si] = bi + built * bi * dt - lost;
                var ph = s.t.phi; bool isPh = ph[G.PHOTO] >= Math.Max(Math.Max(ph[G.CHEMO], ph[G.OSMO]), ph[G.PHAGO]);
                limBio[((l < 0.9 ? 4 + S.limK[i] : r.lim) + (isPh ? LQ : 0)) * V + v] += bi * M;
                grow[si] = Math.Max(0, r.g) * l; limA[si] = l; eatScale[si] = S.scale[i];   // grow = 분열 속도(돌연변이 공급)
                // 오늘 수입(mol)
                int o = s.slot * V + v, st = Cfg.SMAX * V;
                double phF = r.fix > 0 ? r.fixPh / r.fix : 0;
                inc[0 * st + o] += fix * phF * bi * dt * M; inc[1 * st + o] += fix * (1 - phF) * bi * dt * M;
                double usedF = eatTot > 0 ? Math.Min(1, phUsed / Math.Max(1e-30, phAvail)) : 0;
                inc[2 * st + o] += S.prey[i] * (1 - Cfg.EGEST) * usedF * M; inc[3 * st + o] += S.det[i] * (1 - Cfg.EGEST) * usedF * M;
                inc[4 * st + o] += r.docUp * w * M; inc[5 * st + o] += r.o2Made * w * M;
                inc[6 * st + o] += catab * r.aerFrac * bi * dt * M; inc[7 * st + o] += catab * bi * dt * M;
            }
            for (int k = 0; k < P.N; k++) if (c[k][v] < 0) c[k][v] = 0;
        }
    }
}
