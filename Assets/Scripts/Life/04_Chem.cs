// 물질의 이동과 무생물 반응. 모든 계산은 '양'(μmol)을 옮기므로 질량이 보존된다.
// 들고 나는 길은 경계뿐: 열수공·화산·풍화로 들어오고, 매몰·풍화(탄산염)·수소 탈출로 나간다. 장부(Ledger)로 매일 확인한다.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Life
{
    public sealed partial class Planet
    {
        (int a, int b, double q)[] links, linksP;   // 칸 사이 물 교환(L/틱): 녹은 것 / 입자·생물(퇴적면 ↔ 위 물 칸만 다름: 재부유)
        public int[][] nbr;                  // 칸 → 이웃 칸
        public double inC, outC, inN, outN, inP, outP, inFe, outFe, inS, outS;   // 경계 장부(mol)
        public double[] ledger0;
        public double[] alk, caco3;          // 칸별 알칼리도(μM), 해역 바닥에 새로 쌓인 탄산칼슘(mol, 녹아 줄면 음수: 처음부터 있던 것 CACO3_0 × 넓이까지)
        public bool dic;                     // 풀 CO2가 녹은 무기 탄소 전체(DIC)인가. 이 필드 전 저장은 CO2(aq)였다 → 불러올 때 ToDic
        public double buriedC;               // 누적 유기 탄소 매몰(mol) = 길게 보면 대기 산소의 원천
        public double weatherO2;             // 누적 산화 풍화가 쓴 산소(mol)
        public double redB;                  // 경계로 들어온 환원력 누적(전자 mol, CO2·SO4·Fe3+·H2O 기준): 열수공·화산·풍화 +, 매몰·수소 탈출 −. 산화환원 장부(Red)와 맞춰 본다

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
            for (int r = 0; r < NR; r++) if (HasFloor(r)) L.Add((NF0 + r, Bottom(r), vol[NF0 + r] / Cfg.TAU_FLOOR));
            links = new (int, int, double)[L.Count]; linksP = new (int, int, double)[L.Count];
            var nb = new List<int>[V]; for (int v = 0; v < V; v++) nb[v] = new List<int>();
            foreach (var (a, b, _) in L) { nb[a].Add(b); nb[b].Add(a); }
            nbr = new int[V][]; for (int v = 0; v < V; v++) nbr[v] = nb[v].ToArray();
            for (int i = 0; i < L.Count; i++)
            {
                var (a, b, q) = L[i]; double k = 1 / vol[a] + 1 / vol[b];   // 퇴적면 ↔ 위 물: 두 칸 정확 해(1 − e^(−q·Δt·k))/k — 틱 상한 없이 빨리 섞여도 넘치지 않음
                links[i] = (a, b, a >= NF0 ? (1 - Math.Exp(-q * Cfg.DT * k)) / k : Math.Min(q * Cfg.DT, 0.2 * Math.Min(vol[a], vol[b])));
                linksP[i] = a >= NF0 ? (a, b, Math.Min(vol[a] / Cfg.TAU_RESUSP * Cfg.DT, 0.2 * Math.Min(vol[a], vol[b]))) : links[i];
            }   // 틱당 교환량(L), 안정 상한
        }

        // 섞기: 풀과 생물량을 같은 교환으로
        void Mix(double[] c, (int a, int b, double q)[] lk, int stride = 0)
        {
            foreach (var (a, b, q) in lk)
            {
                double d = (c[stride + a] - c[stride + b]) * q;
                c[stride + a] -= d / vol[a]; c[stride + b] += d / vol[b];
            }
        }

        void Transport()
        {
            if (links == null || linksP == null) BuildLinks();
            // 풀·알칼리도·계통 줄마다 따로(병렬): 섞기·가라앉기는 배열(계통 줄) 하나 안에서만 차례가 있고 서로 다른 칸을 안 건드려 결과가 같다
            int np = P.N + 1;
            Parallel.For(0, np + alive.Count, po, j =>
            {
                if (j < P.N) Mix(pool[j], j >= P.POC0 && j <= P.POC2 || j == P.FE3 ? linksP : links);
                else if (j == P.N) Mix(alk, links);
                else
                {
                    var s = alive[j - np]; Mix(B, linksP, s.slot * V);
                    // 가라앉기: 헤엄치지 않는 무거운 생물
                    // Stokes: 무게(세포 부피 합) ÷ 끌림(몸의 끌림 반지름) → 세포 하나 값 ÷ (길쭉함 × 나눠 쓰는 몫). 곧은 실 65세포는 홀로 있는 세포의 약 5배, 같은 수의 덩어리는 약 14배
                    // (예전엔 몸 길이를 공 지름으로 써서 실이 수천 배 빨리 가라앉았다)
                    double r = s.t.dCell / 2 * 1e-6, w = 2.0 / 9 * 50 * 9.81 * r * r / 1.0e-3 * 86400 / (s.t.elong * s.b.share) * (1 - s.t.Sat(G.MOT, 0.01));   // m/일
                    if (w > 0.01) Sink(B, s.slot * V, w);
                }
            });
            // 가라앉기: 사체 입자, 철 산화물
            Sink(pool[P.POC0], 0, Cfg.POC_SINK[0]); Sink(pool[P.POC1], 0, Cfg.POC_SINK[1]); Sink(pool[P.POC2], 0, Cfg.POC_SINK[2]);
            Sink(pool[P.FE3], 0, Cfg.FE3_SINK);
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
            for (int r = 0; r < NR; r++)   // 맨 아래 물 칸 → 퇴적면
            {
                if (!HasFloor(r)) continue; int a = Bottom(r), b = NF0 + r;
                double f = Math.Min(0.5, w * Cfg.DT / thick[a]), m = c[off + a] * f * vol[a];
                c[off + a] -= m / vol[a]; c[off + b] += m / vol[b];
            }
        }

        double[] buryC, buryFe, buryS, buryP;
        [NonSerialized] double pTick;   // 이 틱 인 순 유입(풍화 − 매몰·철 흡착, mol)
        void SumBurial()
        {
            pTick = 0; for (int v = 0; v < V; v++) pTick -= buryP[v];
            for (int v = 0; v < V; v++)
            {
                double m = buryC[v]; redB -= 4 * m + 9 * buryS[v]; outC += m; outN += m * 16 / 106; outP += buryP[v]; buriedC += m; outFe += buryFe[v]; outS += buryS[v];
                buryC[v] = 0; buryFe[v] = 0; buryS[v] = 0; buryP[v] = 0;
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
            // 황화철: Fe2+ × 황화물이 용해도곱을 넘으면 넘는 만큼 FeS로 가라앉아 묻힌다(황철석 매몰 = 황의 주 출구, Berner 1984)
            // ponytail: 바로 매몰(가라앉는 동안 다시 산화되는 몫은 뺌). 바닷물 철·황은 이것 말고 나갈 곳이 없어 깊은 물에 황화수소가 끝없이 쌓였다
            double fe = c[P.FE2][v], hs = c[P.H2S][v];
            if (fe * hs > Cfg.KSP_FES)
            {
                d = 0.5 * (fe + hs - Math.Sqrt((fe - hs) * (fe - hs) + 4 * Cfg.KSP_FES));   // (fe−d)(hs−d) = Ksp
                c[P.FE2][v] -= d; c[P.H2S][v] -= d; d *= vol[v] * 1e-6; buryFe[v] += d; buryS[v] += d;
            }
            for (int p = P.POC0; p <= P.POC2; p++) { d = c[p][v] * Cfg.HYDROLYSIS * dt; c[p][v] -= d; ToDoc(c, v, d); }
            // 자외선: 오존이 없으면 표층의 녹은 유기물을 CO2로 깬다(산소가 1e-2 PAL쯤 되면 오존층이 막는다)
            // 전자는 지킨다: 산소가 있으면 산소로 태우고(광산화), 없으면 물을 깨 H2로 낸다(CH2O + H2O → CO2 + 2 H2)
            d = c[P.DOC][v] * Cfg.K_UV * light[v] / (1 + O2PAL / 0.01) * dt; c[P.DOC][v] -= d; c[P.CO2][v] += d;
            double dO = Math.Min(d, 0.9 * c[P.O2][v]); c[P.O2][v] -= dO; c[P.H2][v] += 2 * (d - dO);
            // 철 많은 물: 녹은 철이 산화물·인산철로 빠지며 인을 붙잡아 가라앉아 묻힌다(ponytail: 가라앉는 동안 다시 풀리는 몫은 뺌)
            d = Math.Min(Cfg.K_FEP * c[P.FE2][v] * dt, 0.5) * c[P.PO4][v]; c[P.PO4][v] -= d; buryP[v] += d * vol[v] * 1e-6;
            if (IsBottom(v))
            {
                double m = 0; for (int p = P.POC0; p <= P.POC2; p++) { d = c[p][v] * Cfg.BURIAL * dt; c[p][v] -= d; m += d; }
                // 사체 속 인(레드필드 C:P 106) 중 묻히지 않는 몫은 물로 돌아가고(+, 산소 없는 바닥), 산소 있는 바닥은 물의 인을 더 붙잡아 같이 묻는다(−)
                double cp = Cfg.CP_OX + (Cfg.CP_ANOX - Cfg.CP_OX) * Cfg.CP_K / (Cfg.CP_K + Math.Max(0, c[P.O2][v]));
                double dp = m * (1.0 / 106 - 1 / cp); if (dp < 0) dp = Math.Max(dp, -0.5 * c[P.PO4][v]);
                c[P.PO4][v] += dp;
                buryC[v] += m * vol[v] * 1e-6; buryP[v] += (m / 106 - dp) * vol[v] * 1e-6;   // 칸별로 모은 뒤 한 스레드에서 더한다(병렬 칸끼리 같은 수를 고치면 몫이 사라진다)
                d = c[P.FE3][v] * 0.01 * dt; c[P.FE3][v] -= d; buryFe[v] += d * vol[v] * 1e-6;
            }
        }

        // 수면 기체 교환(대기는 유한)
        void Gas()
        {
            // 대기가 바다보다 훨씬 작아서 명시적으로 풀면 출렁인다 → 대기 새 값을 암시적으로 푼다(질량 보존, 늘 안정)
            atmO2 = Ex(P.O2, atmO2, Cfg.O2_SAT / Cfg.O2_PAL_MOL, 0.02); atmCO2 = Ex(P.CO2, atmCO2, Cfg.CO2_SAT / Cfg.CO2_PAL_MOL, 0.03, true);
            atmCH4 = Ex(P.CH4, atmCH4, Cfg.CH4_SAT / Cfg.PPM_MOL, 0.02); atmH2 = Ex(P.H2, atmH2, Cfg.H2_SAT / Cfg.PPM_MOL, 0.02);
            double Ex(int p, double atm, double hPerMol, double tc, bool aq = false)   // aq: 풀이 DIC라 물 쪽 기체 농도는 CO2(aq)
            {
                double A = 0, C = 0;
                for (int r = 0; r < NR; r++)
                {
                    int v = r * Cfg.NY; if (!live[v]) continue;
                    double f = Math.Min(0.5, Cfg.PISTON / thick[v] * Cfg.DT) * (1 - ice[r]), L = vol[v] * 1e-6, h = hPerMol * Math.Exp(-tc * (temp[v] - 15));
                    A += f * h * L; C += f * (aq ? Aq(pool[p][v], alk[v], temp[v]) : pool[p][v]) * L;
                }
                double na = (atm + C) / (1 + A);
                for (int r = 0; r < NR; r++)
                {
                    int v = r * Cfg.NY; if (!live[v]) continue;
                    double f = Math.Min(0.5, Cfg.PISTON / thick[v] * Cfg.DT) * (1 - ice[r]), h = hPerMol * Math.Exp(-tc * (temp[v] - 15));
                    pool[p][v] += f * (h * na - (aq ? Aq(pool[p][v], alk[v], temp[v]) : pool[p][v]));
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
                inFe += Cfg.VENT_FE * m; inS += Cfg.VENT_H2S * m; inC += Cfg.VENT_CO2 * m; redB += (2 * Cfg.VENT_H2 + Cfg.VENT_FE + 8 * Cfg.VENT_H2S) * m;
            }
            double volc = volcMul * dt;
            atmCO2 += Cfg.VOLC_CO2 * volc; inC += Cfg.VOLC_CO2 * volc; atmH2 += Cfg.VOLC_H2 * volc; redB += 2 * Cfg.VOLC_H2 * volc;
            // 황산(화산 SO2가 비로) · 풍화 인: 바다 표층에 넓이 비례로
            double seaA = 0; for (int r = 0; r < NR; r++) if (kind[r] == 0) seaA += area[r];
            double land = 0; for (int r = 0; r < NR; r++) if (kind[r] == 2) land += area[r];
            double w = land / Math.Max(1, seaA + land) / 0.05 * Math.Exp((Tglobal - 15) / 13) * Math.Sqrt(Math.Max(0.01, CO2PAL)) * knobs.phosphate * weatherMul;   // weatherMul: 산맥 솟음 사건(09)
            double pIn = Cfg.WEATHER_P * w * dt, so4 = Cfg.VOLC_SO4 * volc + sulfurPulse; sulfurPulse = 0;
            // 산화 풍화: 속도 ∝ O2^0.5(황철석 Williamson & Rimstidt 1994 [DO]^0.5). 1 PAL이면 드러난 만큼 다 녹는다(침식 한계, 유기 탄소는 O2가 낮을 때만 O2 율속 — Bolton 2006).
            // 탄소는 CO2로 대기에, 황은 황산으로 바다에, 철은 산화철로 땅에 남는다. 산소: C 하나에 O2 1, 황철석 S 하나에 O2 15/8
            double fo = Math.Min(1, Math.Sqrt(Math.Max(0, O2PAL))) * land / Math.Max(1, seaA + land) / 0.29 * dt;
            double wOrg = Cfg.WEATHER_ORG * fo, wPyr = Cfg.WEATHER_PYR * fo, need = wOrg + wPyr * 15 / 8;
            if (need > atmO2 * 0.01) { double k = atmO2 * 0.01 / need; wOrg *= k; wPyr *= k; need *= k; }
            atmO2 -= need; atmCO2 += wOrg; inC += wOrg; so4 += wPyr; weatherO2 += need; redB += 4 * need;
            for (int r = 0; r < NR; r++)
            {
                int v = r * Cfg.NY; if (!live[v] || kind[r] == 2) continue;
                double share = area[r] / seaA, L = vol[v] * 1e-6;
                if (kind[r] == 0) pool[P.SO4][v] += so4 * share / L;
                pool[P.PO4][v] += pIn * (kind[r] == 1 ? 3 : 1) * share / L;
            }
            inS += so4; inP += pIn * SumShore(); pTick += pIn * SumShore();
            double wc = Math.Min(atmCO2 * 0.01, Cfg.WEATHER_CO2 * w * dt / knobs.phosphate); atmCO2 -= wc; outC += wc;   // 탄산염 풍화로 CO2 흡수
            // 대기: 수소 탈출, CH4 광분해·산화
            double esc = atmH2 * Cfg.H_ESCAPE * dt; atmH2 -= esc; redB -= 2 * esc;
            double o0 = atmO2, ox = atmCH4 * Cfg.CH4_OX * (0.05 + Math.Sqrt(Math.Max(0, O2PAL))) * dt; atmCH4 -= ox; atmCO2 += ox; atmO2 = Math.Max(0, atmO2 - 2 * ox * Math.Min(1, Math.Sqrt(Math.Max(0, O2PAL))));
            redB += -8 * ox + 4 * (o0 - atmO2);   // 산소 없이 깨진 CH4는 수소 탈출로 셈
        }

        // 인 저장고 시간 압축: 바다 인은 들고 남으로 ~2e5일에 한 번 바뀌어 한 판 안에서 안 움직인다(인 공급 손잡이도 안 듣는다) →
        // 이 틱의 순 들고 남을 (P_FAST − 1)배 더 모든 칸 인에 같은 비율로 얹는다. 대기 ATM처럼 느린 저장고만 빠르게: 칸의 생물이 보는 유입·매몰은 실제 속도 그대로
        void SpeedP()
        {
            double tot = 0; for (int v = 0; v < V; v++) if (live[v]) tot += pool[P.PO4][v] * vol[v] * 1e-6;
            double x = Math.Clamp((Cfg.P_FAST - 1) * pTick / Math.Max(1e-30, tot), -0.5, 1);
            for (int v = 0; v < V; v++) if (live[v]) pool[P.PO4][v] *= 1 + x;
            x *= tot; if (x > 0) inP += x; else outP -= x;
        }

        double SumShore() { double seaA = 0, s = 0; for (int r = 0; r < NR; r++) if (kind[r] == 0) seaA += area[r]; for (int r = 0; r < NR; r++) if (live[r * Cfg.NY] && kind[r] != 2) s += area[r] / seaA * (kind[r] == 1 ? 3 : 1); return s; }

        // 장부: C, N, P, Fe, S의 행성 전체 양(mol) − 경계 순유입
        // 유기물이 녹으면(세포 터짐·사체 분해) 탄소는 녹은 유기물로, 질소·인은 바로 무기염으로 간다.
        // 녹은 유기 질소·인은 세포 밖 효소(탈아미노·인산 가수분해)로 빨리 풀리므로 녹은 유기물은 탄소만 센다(실제 바다 DOM도 C가 훨씬 많다)
        static void ToDoc(double[][] c, int v, double x) { c[P.DOC][v] += x; c[P.NH4][v] += x * 16 / 106; c[P.PO4][v] += x / 106; }

        // 산화환원 장부: 행성 전체 환원력(전자 mol). 유기물 CH2O 4, CH4 8, H2S 8, H2 2, Fe2+ 1, O2 −4. 생물 반응이 전자를 지키면 Red − Red(0) = redB
        public double Red()
        {
            double R = 8 * atmCH4 + 2 * atmH2 - 4 * atmO2;
            for (int v = 0; v < V; v++)
            {
                if (!live[v]) continue;
                double org = pool[P.POC0][v] + pool[P.POC1][v] + pool[P.POC2][v] + pool[P.DOC][v];
                foreach (var s in Live()) org += B[s.slot * V + v];
                R += (4 * org + 8 * (pool[P.CH4][v] + pool[P.H2S][v]) + 2 * pool[P.H2][v] + pool[P.FE2][v] - 4 * pool[P.O2][v]) * vol[v] * 1e-6;
            }
            return R;
        }

        public double[] Ledger()
        {
            double C = atmCO2 + atmCH4, N = 0, Pp = 0, Fe = 0, S = 0;
            for (int r = 0; r < NR; r++) C += caco3[r];
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

        // 탄산 완충: 풀 CO2 = 녹은 무기 탄소 전체(DIC = CO2(aq) + HCO3⁻ + CO3²⁻). 생물과 기체 교환이 겪는 CO2(aq)는 알칼리도(00 ALK)와 수온으로 정해진다.
        // 바다 탄소 대부분이 HCO3⁻로 잡혀 있어 표층 DIC를 10 % 빼도 CO2(aq)는 ~2.5배만 준다(레벨 인자 ~10). 이게 없을 땐 생물 펌프가 표층 CO2를 1/5–1/18로 빨았고
        // 심해는 처음 300 μM 그대로여서, 바다 뒤집힘 사건이 대기 CO2를 며칠 만에 2 → 90 PAL(기온 +25 °C)로 올렸다(진단 17)
        static (double K1, double K2) Kc(double T)   // 바닷물(염분 35) pK1·pK2: 0 °C 6.10·9.38, 25 °C 5.86·8.92(Lueker 2000)에 직선
        {
            double t = Math.Clamp(T, -2, 40); return (Math.Pow(10, -(6.10 - 0.0096 * t)), Math.Pow(10, -(9.38 - 0.0184 * t)));
        }
        static double H(double dic, double alk, double K1, double K2)   // [H+]: a·h² + b·h + c = 0의 양의 근(a = ALK/DIC, 빼기에서 자릿수를 잃지 않는 쪽)
        {
            double a = Math.Clamp(alk / dic, 1e-6, 1.999);   // a ≥ 2면 탄산이 모두 CO3²⁻(CO2(aq) → 0)
            double b = (a - 1) * K1, c = (a - 2) * K1 * K2, D = Math.Sqrt(b * b - 4 * a * c);
            return b >= 0 ? -2 * c / (b + D) : (-b + D) / (2 * a);
        }
        public static double Aq(double dic, double alk, double T)   // DIC → CO2(aq)(μM)
        {
            if (dic <= 0) return 0;
            var (K1, K2) = Kc(T); double h = H(dic, alk, K1, K2);
            return dic / (1 + K1 / h + K1 * K2 / (h * h));
        }
        public static double Omega(double dic, double alk, double T)   // 칼사이트 포화도 = Ca²⁺·CO3²⁻ / Ksp
        {
            if (dic <= 0) return 0;
            var (K1, K2) = Kc(T); double h = H(dic, alk, K1, K2);
            return Cfg.CA * dic / (h * h / (K1 * K2) + h / K2 + 1) / Cfg.KSP;
        }
        public static double Dic(double aq, double alk, double T)   // CO2(aq) → DIC(μM)
        {
            var (K1, K2) = Kc(T); aq = Math.Max(aq, 1e-9);
            double h = (aq * K1 + Math.Sqrt(aq * aq * K1 * K1 + 8 * alk * aq * K1 * K2)) / (2 * alk);
            return aq * (1 + K1 / h + K1 * K2 / (h * h));
        }
        // 탄산칼슘을 x μM 빼면(2 ALK : 1 DIC, x < 0이면 녹여 더함) Ω가 target이 되는 x(Ω는 x에 대해 줄기만 한다)
        static double ToOmega(double dic, double alk, double T, double target)
        {
            double lo = -4000, hi = Math.Min(dic, alk / 2) * 0.999;
            if (Omega(dic - hi, alk - 2 * hi, T) >= target) return hi;
            if (Omega(dic - lo, alk - 2 * lo, T) <= target) return lo;
            for (int i = 0; i < 50; i++) { double m = (lo + hi) / 2; if (Omega(dic - m, alk - 2 * m, T) > target) lo = m; else hi = m; }
            return (lo + hi) / 2;
        }
        // 탄산칼슘: 표층 물이 Ω_P를 넘으면 무기 침전(2 ALK : 1 DIC를 빼 CO2(aq)가 오른다 — 생물이 빤 CO2를 일부 되돌림). 그 CaCO3는 해역 바닥에 쌓이고(가라앉는 시간 무시),
        // 바닥 물이 Ω < 1(호흡 CO2로 산성)이면 쌓인 만큼까지 녹아 2 ALK : 1 DIC를 더한다(탄산염 보상: 심해 CO2(aq)를 Ω 1 수준으로 묶음).
        // 이게 없을 땐 알칼리도가 고정이라 심해 DIC가 알칼리도를 넘어 CO2(aq) 300 μM(대기 30 PAL 평형)을 들고 있었고, 바다 뒤집힘이 그걸 대기로 뿜었다(시험 21).
        // 알칼리도 + 2 × 쌓인 CaCO3는 그대로(풍화·매몰 없음). ponytail: Ksp 수온·수압 의존, 풍화 알칼리도, 탄산염 매몰은 뺌
        void Carbonate()
        {
            Parallel.For(0, NR, po, r =>   // 해역마다 따로(표층 칸·바닥 칸·쌓인 CaCO3가 해역마다 달라 결과가 같다)
            {
                int s = r * Cfg.NY; if (!live[s]) return;
                double x = ToOmega(pool[P.CO2][s], alk[s], temp[s], Cfg.OMEGA_P);
                if (x > 0) { x *= Cfg.DT / Cfg.TAU_PREC; pool[P.CO2][s] -= x; alk[s] -= 2 * x; caco3[r] += x * vol[s] * 1e-6; }
                int f = Bottom(r); double have = caco3[r] + Cfg.CACO3_0 * area[r]; if (f < 0 || have <= 0) return;
                double y = -ToOmega(pool[P.CO2][f], alk[f], temp[f], 1);
                if (y > 0) { y = Math.Min(y * Cfg.DT / Cfg.TAU_DISS, have / (vol[f] * 1e-6)); pool[P.CO2][f] += y; alk[f] += 2 * y; caco3[r] -= y * vol[f] * 1e-6; }
            });
        }
        // 풀 CO2를 CO2(aq)에서 DIC로: 새 지구(03 Init)와 이 필드 전 저장(11 Read). 더해진 탄소는 처음 장부에도 더한다(장부 어긋남이 튀지 않게)
        public void ToDic()
        {
            if (dic) return;
            double add = 0;
            for (int v = 0; v < V; v++) { if (!live[v]) continue; double d = Dic(pool[P.CO2][v], alk[v], temp[v]); add += (d - pool[P.CO2][v]) * vol[v] * 1e-6; pool[P.CO2][v] = d; }
            if (ledger0 != null) ledger0[0] += add;
            dic = true;
        }
    }
}
