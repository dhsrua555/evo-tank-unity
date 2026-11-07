// 물질의 이동과 무생물 반응. 모든 계산은 '양'(μmol)을 옮기므로 질량이 보존된다.
// 들고 나는 길은 경계뿐: 열수공·화산·풍화로 들어오고, 매몰·풍화(탄산염)·수소 탈출로 나간다. 장부(Ledger)로 매일 확인한다.
using System;
using System.Collections.Generic;

namespace Life
{
    public sealed partial class Planet
    {
        (int a, int b, double q)[] links;    // 칸 사이 물 교환(L/틱)
        public int[][] nbr;                  // 칸 → 이웃 칸
        public double inC, outC, inN, outN, inP, outP, inFe, outFe, inS, outS;   // 경계 장부(mol)
        public double[] ledger0;
        public double buriedC;               // 누적 유기 탄소 매몰(mol) = 길게 보면 대기 산소의 원천

        void BuildLinks()
        {
            var L = new List<(int, int, double)>();
            for (int r = 0; r < NR; r++)
                for (int k = 0; k + 1 < Cfg.NY; k++)
                {
                    int a = r * Cfg.NY + k, b = a + 1; if (!live[a] || !live[b]) continue;
                    L.Add((a, b, Math.Min(vol[a], vol[b]) / Cfg.TAU_Z[k]));
                }
            foreach (var (ra, rb, e, dist) in geo.links)
                for (int k = 0; k < Cfg.NY; k++)
                {
                    int a = ra * Cfg.NY + k, b = rb * Cfg.NY + k;
                    if (!live[a] || !live[b]) { if (k == 0 && (kind[ra] != 0 || kind[rb] != 0)) { a = ra * Cfg.NY; b = rb * Cfg.NY; if (!live[a] || !live[b]) continue; } else continue; }
                    L.Add((a, b, Math.Min(vol[a], vol[b]) / Cfg.TAU_H));
                }
            for (int i = 0; i < Cfg.NVENT; i++) if (live[NV0 + i]) L.Add((NV0 + i, ventHost[i], vol[NV0 + i] / Cfg.TAU_VENT));
            links = new (int, int, double)[L.Count];
            var nb = new List<int>[V]; for (int v = 0; v < V; v++) nb[v] = new List<int>();
            foreach (var (a, b, _) in L) { nb[a].Add(b); nb[b].Add(a); }
            nbr = new int[V][]; for (int v = 0; v < V; v++) nbr[v] = nb[v].ToArray();
            for (int i = 0; i < L.Count; i++) { var (a, b, q) = L[i]; links[i] = (a, b, Math.Min(q * Cfg.DT, 0.2 * Math.Min(vol[a], vol[b]))); }   // 틱당 교환량(L), 안정 상한
        }

        // 섞기: 풀과 생물량을 같은 교환으로
        void Mix(double[] c, int stride = 0)
        {
            foreach (var (a, b, q) in links)
            {
                double d = (c[stride + a] - c[stride + b]) * q;
                c[stride + a] -= d / vol[a]; c[stride + b] += d / vol[b];
            }
        }

        void Transport()
        {
            if (links == null) BuildLinks();
            for (int p = 0; p < P.N; p++) Mix(pool[p]);
            foreach (var s in Live()) Mix(B, s.slot * V);
            // 가라앉기: 사체 입자, 철 산화물, 헤엄치지 않는 무거운 생물
            Sink(pool[P.POC0], 0, Cfg.POC_SINK[0]); Sink(pool[P.POC1], 0, Cfg.POC_SINK[1]); Sink(pool[P.POC2], 0, Cfg.POC_SINK[2]);
            Sink(pool[P.FE3], 0, Cfg.FE3_SINK);
            foreach (var s in Live())
            {
                double r = s.b.size / 2 * 1e-6, w = 2.0 / 9 * 50 * 9.81 * r * r / 1.0e-3 * 86400 * (1 - s.t.Sat(G.MOT, 0.01));   // Stokes(m/일)
                if (s.t.nA > 1) w *= 0.5;   // 덩이는 구멍이 많아 덜 가라앉는다
                if (w > 0.01) Sink(B, s.slot * V, w);
            }
        }

        void Sink(double[] c, int off, double w)
        {
            for (int r = 0; r < NR; r++)
                for (int k = Cfg.NY - 2; k >= 0; k--)
                {
                    int a = r * Cfg.NY + k, b = a + 1; if (!live[a] || !live[b]) continue;
                    double f = Math.Min(0.5, w * Cfg.DT / thick[a]), m = c[off + a] * f * vol[a];
                    c[off + a] -= m / vol[a]; c[off + b] += m / vol[b];
                }
        }

        double[] buryC, buryFe;
        void SumBurial()
        {
            for (int v = 0; v < V; v++)
            {
                double m = buryC[v]; outC += m; outN += m * 16 / 106; outP += m / 106; buriedC += m; outFe += buryFe[v];
                buryC[v] = 0; buryFe[v] = 0;
            }
        }

        // 무생물 반응·매몰
        void Abiotic(int v)
        {
            var c = pool; double dt = Cfg.DT;
            double d = Math.Min(Cfg.K_FE_OX * c[P.FE2][v] * c[P.O2][v] * dt, Math.Min(c[P.FE2][v], 4 * c[P.O2][v]) * 0.9);
            c[P.FE2][v] -= d; c[P.FE3][v] += d; c[P.O2][v] -= d / 4;
            d = Math.Min(Cfg.K_HS_OX * c[P.H2S][v] * c[P.O2][v] * dt, Math.Min(c[P.H2S][v], c[P.O2][v] / 2) * 0.9);
            c[P.H2S][v] -= d; c[P.SO4][v] += d; c[P.O2][v] -= 2 * d;
            d = Math.Min(Cfg.K_CH4_OX * c[P.CH4][v] * c[P.O2][v] * dt, Math.Min(c[P.CH4][v], c[P.O2][v] / 2) * 0.9);
            c[P.CH4][v] -= d; c[P.CO2][v] += d; c[P.O2][v] -= 2 * d;
            d = Math.Min(Cfg.K_H2_OX * c[P.H2][v] * c[P.O2][v] * dt, Math.Min(c[P.H2][v], 2 * c[P.O2][v]) * 0.9);
            c[P.H2][v] -= d; c[P.O2][v] -= d / 2;
            for (int p = P.POC0; p <= P.POC2; p++) { d = c[p][v] * Cfg.HYDROLYSIS * dt; c[p][v] -= d; ToDoc(c, v, d); }
            // 자외선: 오존이 없으면 표층의 녹은 유기물을 CO2로 깬다(산소가 1e-2 PAL쯤 되면 오존층이 막는다)
            d = c[P.DOC][v] * Cfg.K_UV * light[v] / (1 + O2PAL / 0.01) * dt; c[P.DOC][v] -= d; c[P.CO2][v] += d;
            if (IsBottom(v))
            {
                double m = 0; for (int p = P.POC0; p <= P.POC2; p++) { d = c[p][v] * Cfg.BURIAL * dt; c[p][v] -= d; m += d; }
                buryC[v] += m * vol[v] * 1e-6;   // 칸별로 모은 뒤 한 스레드에서 더한다(병렬 칸끼리 같은 수를 고치면 몫이 사라진다)
                d = c[P.FE3][v] * 0.01 * dt; c[P.FE3][v] -= d; buryFe[v] += d * vol[v] * 1e-6;
            }
        }

        // 수면 기체 교환(대기는 유한)
        void Gas()
        {
            // 대기가 바다보다 훨씬 작아서 명시적으로 풀면 출렁인다 → 대기 새 값을 암시적으로 푼다(질량 보존, 늘 안정)
            atmO2 = Ex(P.O2, atmO2, Cfg.O2_SAT / Cfg.O2_PAL_MOL, 0.02); atmCO2 = Ex(P.CO2, atmCO2, Cfg.CO2_SAT / Cfg.CO2_PAL_MOL, 0.03);
            atmCH4 = Ex(P.CH4, atmCH4, Cfg.CH4_SAT / Cfg.PPM_MOL, 0.02); atmH2 = Ex(P.H2, atmH2, Cfg.H2_SAT / Cfg.PPM_MOL, 0.02);
            double Ex(int p, double atm, double hPerMol, double tc)
            {
                double A = 0, C = 0;
                for (int r = 0; r < NR; r++)
                {
                    int v = r * Cfg.NY; if (!live[v]) continue;
                    double f = Math.Min(0.5, Cfg.PISTON / thick[v] * Cfg.DT) * (1 - ice[r]), L = vol[v] * 1e-6, h = hPerMol * Math.Exp(-tc * (temp[v] - 15));
                    A += f * h * L; C += f * pool[p][v] * L;
                }
                double na = (atm + C) / (1 + A);
                for (int r = 0; r < NR; r++)
                {
                    int v = r * Cfg.NY; if (!live[v]) continue;
                    double f = Math.Min(0.5, Cfg.PISTON / thick[v] * Cfg.DT) * (1 - ice[r]), h = hPerMol * Math.Exp(-tc * (temp[v] - 15));
                    pool[p][v] += f * (h * na - pool[p][v]);
                }
                return na;
            }
        }

        // 경계 유입·유출과 대기 광화학
        void Boundary()
        {
            double dt = Cfg.DT;
            foreach (int v in Vents())
            {
                double L = vol[v] * 1e-6, m = ventMul[v] / nVent0 * dt;
                pool[P.H2][v] += Cfg.VENT_H2 * m / L; pool[P.FE2][v] += Cfg.VENT_FE * m / L; pool[P.H2S][v] += Cfg.VENT_H2S * m / L; pool[P.CO2][v] += Cfg.VENT_CO2 * m / L;
                inFe += Cfg.VENT_FE * m; inS += Cfg.VENT_H2S * m; inC += Cfg.VENT_CO2 * m;
            }
            double volc = volcMul * dt;
            atmCO2 += Cfg.VOLC_CO2 * volc; inC += Cfg.VOLC_CO2 * volc; atmH2 += Cfg.VOLC_H2 * volc;
            // 황산(화산 SO2가 비로) · 풍화 인: 바다 표층에 넓이 비례로
            double seaA = 0; for (int r = 0; r < NR; r++) if (kind[r] == 0) seaA += area[r];
            double land = 0; for (int r = 0; r < NR; r++) if (kind[r] == 2) land += area[r];
            double w = land / Math.Max(1, seaA + land) / 0.05 * Math.Exp((Tglobal - 15) / 13) * Math.Sqrt(Math.Max(0.01, CO2PAL)) * knobs.phosphate;
            double pIn = Cfg.WEATHER_P * w * dt, so4 = Cfg.VOLC_SO4 * volc + sulfurPulse; sulfurPulse = 0;
            for (int r = 0; r < NR; r++)
            {
                int v = r * Cfg.NY; if (!live[v] || kind[r] == 2) continue;
                double share = area[r] / seaA, L = vol[v] * 1e-6;
                if (kind[r] == 0) pool[P.SO4][v] += so4 * share / L;
                pool[P.PO4][v] += pIn * (kind[r] == 1 ? 3 : 1) * share / L;
            }
            inS += so4; inP += pIn * SumShore();
            double wc = Math.Min(atmCO2 * 0.01, Cfg.WEATHER_CO2 * w * dt / knobs.phosphate); atmCO2 -= wc; outC += wc;   // 탄산염 풍화로 CO2 흡수
            // 대기: 수소 탈출, CH4 광분해·산화
            double esc = atmH2 * Cfg.H_ESCAPE * dt; atmH2 -= esc;
            double ox = atmCH4 * Cfg.CH4_OX * (0.05 + Math.Sqrt(Math.Max(0, O2PAL))) * dt; atmCH4 -= ox; atmCO2 += ox; atmO2 = Math.Max(0, atmO2 - 2 * ox * Math.Min(1, Math.Sqrt(Math.Max(0, O2PAL))));
        }

        double SumShore() { double seaA = 0, s = 0; for (int r = 0; r < NR; r++) if (kind[r] == 0) seaA += area[r]; for (int r = 0; r < NR; r++) if (live[r * Cfg.NY] && kind[r] != 2) s += area[r] / seaA * (kind[r] == 1 ? 3 : 1); return s; }

        // 장부: C, N, P, Fe, S의 행성 전체 양(mol) − 경계 순유입
        // 유기물이 녹으면(세포 터짐·사체 분해) 탄소는 녹은 유기물로, 질소·인은 바로 무기염으로 간다.
        // 녹은 유기 질소·인은 세포 밖 효소(탈아미노·인산 가수분해)로 빨리 풀리므로 녹은 유기물은 탄소만 센다(실제 바다 DOM도 C가 훨씬 많다)
        static void ToDoc(double[][] c, int v, double x) { c[P.DOC][v] += x; c[P.NH4][v] += x * 16 / 106; c[P.PO4][v] += x / 106; }

        public double[] Ledger()
        {
            double C = atmCO2 + atmCH4, N = 0, Pp = 0, Fe = 0, S = 0;
            for (int v = 0; v < V; v++)
            {
                if (!live[v]) continue;
                double L = vol[v] * 1e-6, org = pool[P.POC0][v] + pool[P.POC1][v] + pool[P.POC2][v];   // 레드필드 몫(사체·몸). 녹은 유기물은 탄소만
                foreach (var s in Live()) org += B[s.slot * V + v];
                C += (pool[P.CO2][v] + pool[P.CH4][v] + pool[P.DOC][v] + org) * L;
                N += (pool[P.NH4][v] + org * 16 / 106) * L; Pp += (pool[P.PO4][v] + org / 106) * L;
                Fe += (pool[P.FE2][v] + pool[P.FE3][v]) * L; S += (pool[P.H2S][v] + pool[P.SO4][v]) * L;
            }
            return new[] { C - inC + outC, N - inN + outN, Pp - inP + outP, Fe - inFe + outFe, S - inS + outS };
        }
    }
}
