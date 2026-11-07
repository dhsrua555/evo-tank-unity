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
        public double[] grow, limA, eatScale, eatenRate;
        public double[] binB;                 // [v·125 + 수용체 칸] 숙주 밀도(바이러스)
        public double[] nfixV;                // 칸별 질소 고정 누적(mol), 매일 장부 inN으로

        sealed class Scratch
        {
            public int[] slot = new int[Cfg.SMAX + 3];
            public double[] b = new double[Cfg.SMAX + 3], size = new double[Cfg.SMAX + 3], vul = new double[Cfg.SMAX + 3],
                a = new double[Cfg.SMAX], scale = new double[Cfg.SMAX], loss = new double[Cfg.SMAX + 3], intake = new double[Cfg.SMAX],
                prey = new double[Cfg.SMAX], det = new double[Cfg.SMAX], lim = new double[Cfg.SMAX];
            public Rates[] r = new Rates[Cfg.SMAX];
            public readonly double[] cons = new double[P.N], f = new double[P.N];
        }
        readonly ThreadLocal<Scratch> scratch = new(() => new Scratch());

        void AllocBio()
        {
            inc = new double[NINC * Cfg.SMAX * V];
            grow = new double[Cfg.SMAX * V]; limA = new double[Cfg.SMAX * V]; eatScale = new double[Cfg.SMAX * V]; eatenRate = new double[Cfg.SMAX * V];
            binB = new double[V * 125]; nfixV = new double[V];
        }

        public Env EnvAt(int v)
        {
            var c = pool;
            return new Env { T = temp[v], light = light[v], dry = dry[v], CO2 = c[P.CO2][v], O2 = c[P.O2][v], H2 = c[P.H2][v], CH4 = c[P.CH4][v],
                H2S = c[P.H2S][v], FE2 = c[P.FE2][v], SO4 = c[P.SO4][v], NH4 = c[P.NH4][v], PO4 = c[P.PO4][v], DOC = c[P.DOC][v] };
        }

        // 먹이 선호: 몸 크기의 1/10 근처(로그 정규), 자기보다 큰 것은 못 먹는다
        static double Pref(double prey, double pred)
        {
            if (prey >= pred) return 0;
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
                if (bb > Cfg.BMIN) { S.slot[n] = s.slot; S.b[n] = bb; S.size[n] = s.b.size; S.vul[n] = Vul(s.t); n++; }
                else if (bb != 0) { c[P.DOC][v] += bb; B[s.slot * V + v] = 0; }   // 사라진 몫은 녹은 유기물로
            }
            for (int k = 0; k < 125; k++) binB[v * 125 + k] = 0;
            if (n == 0) return;
            for (int i = 0; i < n; i++) binB[v * 125 + strains[S.slot[i]].t.vbin] += S.b[i];
            var e = EnvAt(v);

            // 1) 포식·청소: 먹이 = 다른 계통 + 사체 세 칸
            int np = n;
            for (int k = 0; k < 3; k++) { S.size[np] = Cfg.POC_SIZE[k]; S.vul[np] = 1; S.b[np] = c[P.POC0 + k][v]; S.slot[np] = -1; np++; }
            double tot = 0; for (int i = 0; i < np; i++) { tot += S.b[i]; S.loss[i] = 0; }
            for (int j = 0; j < n; j++)
            {
                var sj = strains[S.slot[j]]; S.a[j] = Attack(sj.t, sj.b); S.scale[j] = 0; S.intake[j] = 0; S.prey[j] = 0; S.det[j] = 0;
                if (S.a[j] * tot < 1e-4) continue;
                double E = 0;
                for (int i = 0; i < np; i++) if (i != j) E += S.a[j] * Pref(S.size[i], S.size[j]) * S.vul[i] * S.b[i];
                if (E <= 0) continue;
                S.scale[j] = 1 / (1 + Cfg.HANDLE * E);
                for (int i = 0; i < np; i++) if (i != j) S.loss[i] += S.a[j] * Pref(S.size[i], S.size[j]) * S.vul[i] * S.b[i] * S.scale[j] * S.b[j] * dt;
            }
            for (int i = 0; i < np; i++) S.loss[i] = S.loss[i] > 0.9 * S.b[i] ? 0.9 * S.b[i] / S.loss[i] : 1;   // 이제 loss = 먹힘 상한 배율
            for (int j = 0; j < n; j++)
            {
                if (S.scale[j] <= 0) continue;
                for (int i = 0; i < np; i++)
                {
                    if (i == j) continue;
                    double fl = S.a[j] * Pref(S.size[i], S.size[j]) * S.vul[i] * S.b[i] * S.scale[j] * S.b[j] * dt * S.loss[i];
                    if (i < n) S.prey[j] += fl; else S.det[j] += fl;
                }
                S.intake[j] = S.prey[j] + S.det[j];
            }
            // 먹힌 양을 먹이에서 뺀다(사체는 풀에서)
            for (int i = 0; i < np; i++)
            {
                double eaten = 0;
                for (int j = 0; j < n; j++) if (j != i && S.scale[j] > 0) eaten += S.a[j] * Pref(S.size[i], S.size[j]) * S.vul[i] * S.b[i] * S.scale[j] * S.b[j] * dt * S.loss[i];
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
            for (int i = 0; i < n; i++)
            {
                ref var r = ref S.r[i]; double l = 1;
                if (r.dCO2 < 0) l = Math.Min(l, S.f[P.CO2]); if (r.dO2 < 0) l = Math.Min(l, S.f[P.O2]); if (r.dH2 < 0) l = Math.Min(l, S.f[P.H2]);
                if (r.dCH4 < 0) l = Math.Min(l, S.f[P.CH4]); if (r.dH2S < 0) l = Math.Min(l, S.f[P.H2S]); if (r.dFE2 < 0) l = Math.Min(l, S.f[P.FE2]);
                if (r.dSO4 < 0) l = Math.Min(l, S.f[P.SO4]); if (r.nh4 > 0) l = Math.Min(l, S.f[P.NH4]); if (r.po4 > 0) l = Math.Min(l, S.f[P.PO4]);
                if (r.docUp > 0) l = Math.Min(l, S.f[P.DOC]);
                S.lim[i] = l;
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
