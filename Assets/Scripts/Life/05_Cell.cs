// 세포 생리: 단백질 배분 몫과 칸의 화학으로 에너지(ATP)·탄소·질소·인 수지를 풀어 성장률을 낸다.
// 적합도 함수를 따로 두지 않는다. 돌연변이의 선택 계수도 같은 식으로 잰다.
// 모든 값은 세포 탄소 1 mol당, 하루당.
using System;

namespace Life
{
    public struct Env
    {
        public double T, light, dry, CO2, O2, H2, CH4, H2S, FE2, SO4, NH4, PO4, DOC;
    }

    // 성장 결과와 물질 흐름(세포 탄소 1 mol당 하루)
    public struct Rates
    {
        public double g;                 // 성장률(음수 = 굶어 줄어듦)
        public double mort;              // 먹히지 않고 죽는 비율(온도·산소 독성·황화수소·마름·바탕)
        public double fix, assim, catab, docUp, phagoUsed;  // 고정한 C, 몸으로 쓴 유기 C, 태운 유기 C, 흡수한 녹은 유기물, 쓴 먹은 것
        public double assimNP, catabNP;  // 그중 N·P를 같이 가진 몫(먹은 것). 녹은 유기물은 탄소만
        public double aerFrac;           // 태운 것 중 산소 호흡 몫
        public double nh4, po4, nfix;    // 흡수
        public double dCO2, dO2, dH2, dCH4, dH2S, dFE2, dSO4;   // 칸 풀 변화(+ = 내놓음)
        public double chemoATP, o2Made, fixPh;   // fixPh: 빛 에너지로 고정한 몫
        public double photoATP, orgATP;  // 쓴 ATP의 출처: 빛, (chemoATP) 화학, 유기물 태우기
        public int lim;                  // 성장을 막은 것: 0 에너지·탄소, 1 리보솜, 2 질소, 3 인
    }

    public static class Cell
    {
        static double Sig(double x) => 1 / (1 + Math.Exp(-x));
        // 공여체 산화 환원 전위(V, pH 7): H2 −0.41, H2S −0.24, Fe2+ +0.20, H2O +0.82
        static readonly double[] E = { -0.41, -0.24, 0.20, 0.82 };
        // 화학합성 공여체(H2, CH4, H2S, Fe2+): 선호 축 위치, 반포화(μM), 전자 수, ATP(산소 호흡), 산소 소비
        static readonly double[] DPOS = { 0.1, 0.35, 0.6, 0.9 }, DK = { 0.1, 1, 1, 5 }, DE = { 2, 8, 8, 1 }, YAER = { 1.0, 4, 3, 0.1 }, DO2 = { 0.5, 2, 2, 0.25 };

        // 형질만으로 정해지는 값: 계통을 만들 때 한 번(Grow는 틱마다 계통·칸마다 불린다)
        public static void Prep(Traits t)
        {
            double rho = Cfg.A_CM * t.dCell;
            t.pack = rho < 1e-3 ? 1 : 1.5 / rho * (1 + 2 * Math.Exp(-rho) / rho + 2 * (Math.Exp(-rho) - 1) / (rho * rho));   // 묶음 효과 3Q*/(2ρ′): 10 μm 0.83, 100 μm 0.28
            t.uH2 = Sig((t.eox - E[0] - 0.1) / 0.15); t.uHS = Sig((t.eox - E[1] - 0.1) / 0.15); t.uFe = Sig((t.eox - E[2] - 0.1) / 0.15); t.uW = Sig((t.eox - E[3] - 0.1) / 0.15);
            t.sav = Math.Min(1, Math.Sqrt(1.0 / t.dCell));   // 큰 세포는 겉넓이가 모자란다
            t.sqS = Math.Sqrt(20 / t.S);
            for (int d = 0; d < 4; d++) { double x = (DPOS[d] - t.dnr) / 0.25; t.wD[d] = Math.Exp(-x * x); }
            // 길쭉한 세포(장단축비 a, 같은 부피의 긴 회전타원체)의 확산·끌림 반지름 ÷ 같은 부피 공: a^(−1/3)·√(a²−1)/arcosh a — a 8이면 1.43
            double a = t.aspect; t.elong = a > 1.0001 ? Math.Pow(a, -1.0 / 3) * Math.Sqrt(a * a - 1) / Math.Log(a + Math.Sqrt(a * a - 1)) : 1;
            // 확산이 세포 하나에 대 주는 흡수 4πDrC(mol/일) ÷ 세포 탄소: 1 μm 세포 약 55, 10 μm 약 0.85 /μM/일 — 큰 세포는 운반체보다 확산이 먼저 막는다
            t.kd = 4 * Math.PI * Cfg.D_NUT * (t.dCell / 2 * 1e-6) * t.elong * 1e-3 / t.cellC;
        }
        static double Ser(double up, double dif) => up > 0 && dif > 0 ? up * dif / (up + dif) : 0;   // 운반체와 확산이 이어진 두 저항(느린 쪽이 막는다)

        public static Rates Grow(Traits t, Body b, in Env e, double phago, double virusMort)
        {
            Rates r = default; var phi = t.phi;
            double dT = (e.T - t.topt) / t.tw, fT = Math.Exp(-0.5 * dT * dT), q10 = Math.Exp(0.06931471805599453 * (e.T - 20));   // q10 = 2^((T−20)/10)
            double ex = 0.3 + 0.7 * b.exposed, sav = t.sav;   // 덩이 속 세포는 빛을 덜 받는다(가림). 큰 세포는 겉넓이가 모자란다(Prep)

            // 빛 에너지와 광합성 전자 공여체
            double P = phi[G.PHOTO] * Cfg.K_PHOTO * e.light / (e.light + Cfg.K_LIGHT) * ex * fT * t.pack;
            double uH2 = t.uH2, uHS = t.uHS, uFe = t.uFe, uW = t.uW;
            double aH2 = uH2 * e.H2 / (e.H2 + 0.1), aHS = uHS * e.H2S / (e.H2S + 1), aFe = uFe * e.FE2 / (e.FE2 + 5), aW = uW;
            double D = Math.Min(1, aH2 + aHS + aFe + aW), dsum = Math.Max(1e-12, aH2 + aHS + aFe + aW);
            double atpPh = P * (0.3 + 0.7 * D), ePh = 0.5 * P * D;

            // 화학합성: 공여체를 산화해 ATP(산소가 있으면 산소로, 없으면 황산·CO2로)
            double resp = t.Sat(G.RESP, 0.02), oAer = e.O2 / (e.O2 + Cfg.K_AER), aer = resp * oAer, sSO4 = e.SO4 / (e.SO4 + 20);
            Span<double> conc = stackalloc double[] { e.H2, e.CH4, e.H2S, e.FE2 }, R = stackalloc double[4]; double atpCh = 0, eCh = 0;   // 스택에: 한 틱에 수만 번 불려서 힙에 만들면 GC가 잦다
            for (int d = 0; d < 4; d++)
            {
                double yan = d == 0 ? 0.25 * sSO4 + 0.125 * (1 - sSO4) * e.CO2 / (e.CO2 + 5) : d == 1 ? 0.1 * sSO4 : 0;
                double y = aer * YAER[d] + (1 - aer) * yan; if (y <= 0) continue;
                double w = t.wD[d];
                R[d] = phi[G.CHEMO] * Cfg.K_CHEMO * fT * w * conc[d] / (conc[d] + DK[d]);
                if (d >= 2) { R[d] *= aer; y = YAER[d]; }   // 황화수소·철 산화는 받개가 산소뿐(이 모형엔 질산이 없다): 산소 몫만큼만 돈다
                atpCh += R[d] * y; eCh += R[d] * DE[d] * 0.2;
            }

            // 탄소 고정: 특이성 S가 높을수록 느리지만 O2에 덜 속는다(광호흡). 탄소 농축은 세포 안 CO2를 올린다
            double ccm = t.Sat(G.CCM, 0.03), ci = e.CO2 * (1 + 20 * ccm);
            double gross = phi[G.FIX] * Cfg.K_FIX * t.sqS * ci / (ci + Cfg.K_CO2) * fT;
            double pr = Math.Max(0, 1 - 0.5 * e.O2 / (t.S * Math.Max(ci, 1e-6)));
            double fixMax = Math.Min(gross * pr, (ePh + eCh) / 4), aFix = Cfg.A_FIX + 2 * ccm;

            // 유기물: 녹은 유기물 흡수 + 먹은 것(포식·청소)
            // 녹은 것의 흡수 = 운반체 × 확산(덩이는 이웃과 같은 물을 나눠 쓴다: share). 덩이 속 세포가 덜 받는 몫은 이 확산 몫이 맡는다(빛 가림은 ex)
            double kd = t.kd * b.share;
            double docUp = Ser(phi[G.OSMO] * Cfg.K_OSMO * fT * e.DOC / (e.DOC + Cfg.K_DOC) * sav, kd * e.DOC);
            double U = docUp + phago;
            // 태우기: 산소 호흡은 호흡 단백질 몫에 비례하는 속도까지만, 넘치는 몫은 발효(넘침 대사, Basan 2015). 해당 과정은 살림 단백질에 든다.
            // 발효는 H2가 쌓이면 잘 안 된다(열역학)
            double capAer = Cfg.K_RESP * phi[G.RESP] * oAer * fT, yF = Cfg.Y_FERM / (1 + e.H2);
            double OrgATP(double c) { double a = Math.Min(c, capAer); return a * Cfg.Y_AER + (c - a) * yF; }

            // 질소·인
            double tr = (0.03 + phi[G.NUT]) * sav * fT;   // 암모늄·인산 운반체는 유기물 운반체와 다른 단백질(AMT·Pst)
            double nIn = Ser(3 * tr * e.NH4 / (e.NH4 + 0.5), kd * e.NH4), pIn = Ser(0.2 * tr * e.PO4 / (e.PO4 + 0.05), kd * e.PO4);
            double o2Own = uW / dsum * Math.Min(fixMax, ePh / 4);             // 산소 광합성으로 세포 안에서 생기는 O2
            double seg = t.diff * (1 - b.exposed);                            // 분화: 속 세포가 산소에서 떨어져 질소를 고정한다(이형세포처럼)
            double nfix = phi[G.NFIX] * Cfg.K_NFIX * fT / (1 + (e.O2 + Cfg.OWN_O2 * o2Own * (1 - seg)) / 2);

            // 유지비: 온도 폭이 넓을수록, 편모·분화·물 산화 광계 수리 비용
            double M = Cfg.M0 * q10 * (1 + 0.15 * t.tw / 5) + 3 * phi[G.MOT] + 0.1 * P * uW + 0.05 * t.diff;

            // 탄소·에너지로 낼 수 있는 성장률: 유기물 중 태울 몫 f와 고정량을 고른다.
            // Gce(f)는 조각마다 직선이고 오목하다(고정이 0과 최대 사이면 두 한계가 같아진다). 그래서 최댓값은 꺾이는 곳 —
            // 양 끝, 산소 호흡 상한(f·U = capAer), 고정이 0·최대가 되는 곳 — 중 하나에 있다: 그 몇 곳만 잰다(옛 삼분 탐색 49번과 같은 답)
            double atpIn = atpPh + atpCh;
            double bestF = 0, bestC = 0;
            double Inner(double f) => atpIn + OrgATP(f * U) - M - Cfg.A_BIO * (1 - f) * U;   // 고정 식의 분자: 0이면 고정 0, (A_BIO + aFix)·fixMax면 최대
            double Gce(double f, out double cfix)
            {
                double a = (1 - f) * U, bb = atpIn + OrgATP(f * U) - M;
                cfix = Math.Clamp((bb - Cfg.A_BIO * a) / (Cfg.A_BIO + aFix), 0, fixMax);
                return Math.Min(a + cfix, (bb - aFix * cfix) / Cfg.A_BIO);
            }
            double gce;
            if (U <= 0) gce = Gce(0, out bestC);
            else
            {
                double fa = Math.Min(1, capAer / U);
                Span<double> fs = stackalloc double[7]; int nf = 0; fs[nf++] = 0; fs[nf++] = 1; fs[nf++] = fa;
                for (int s = 0; s < 2; s++)
                {
                    double s0 = s == 0 ? 0 : fa, s1 = s == 0 ? fa : 1, i0 = Inner(s0), i1 = Inner(s1); if (i1 <= i0) continue;
                    for (int q = 0; q < 2; q++) { double tg = q == 0 ? 0 : (Cfg.A_BIO + aFix) * fixMax; if (tg >= i0 && tg <= i1) fs[nf++] = s0 + (s1 - s0) * (tg - i0) / (i1 - i0); }
                }
                gce = double.NegativeInfinity;
                for (int q = 0; q < nf; q++) { double v = Gce(fs[q], out double c); if (v > gce) { gce = v; bestF = fs[q]; bestC = c; } }
            }
            double gRib = Cfg.K_RIB * phi[G.RIB] * fT;
            double gN = (nIn + nfix + phago * 16 / 106) * 106 / 16, gP = (pIn + phago / 106) * 106;   // 녹은 유기물은 N·P가 없다
            double g = Math.Min(Math.Min(gce, gRib), Math.Min(gN, gP));
            // 질소 고정 비용을 쓴 만큼 다시 셈
            if (g > 0)
            {
                double nUse = Math.Max(0, Math.Min(nfix, g * 16 / 106 - nIn - (1 - bestF) * phago * 16 / 106));
                if (nUse > 0) { double g2 = (gce * Cfg.A_BIO - Cfg.A_NFIX * nUse) / Cfg.A_BIO; g = Math.Min(g, g2); }
            }
            r.g = g; r.lim = g == gce ? 0 : g == gRib ? 1 : g == gN ? 2 : g == gP ? 3 : 0;

            // 실제 흐름: 성장이 다른 한계로 막히면 그만큼 덜 먹고 덜 고정한다
            double lam = gce > 0 ? Math.Clamp(g / gce, 0, 1) : (g < 0 ? 1 : 0);
            if (g <= 0) lam = U > 0 || fixMax > 0 ? 1 : 0;
            r.fix = g > 0 ? bestC * lam : 0;
            r.docUp = docUp * lam; r.phagoUsed = phago * lam;
            double Uu = r.docUp + r.phagoUsed;
            r.catab = bestF * Uu; r.assim = (1 - bestF) * Uu; r.aerFrac = r.catab > 0 ? Math.Min(1, capAer / r.catab) : 0;
            r.catabNP = bestF * r.phagoUsed; r.assimNP = (1 - bestF) * r.phagoUsed;
            double needN = Math.Max(0, g) * 16 / 106 - r.assimNP * 16 / 106; needN = Math.Max(0, needN);
            r.nh4 = Math.Min(nIn, needN); r.nfix = Math.Max(0, needN - r.nh4);
            r.po4 = Math.Max(0, Math.Max(0, g) / 106 - r.assimNP / 106);
            // 고정에 쓴 전자: 광합성 몫은 공여체 몫대로, 화학합성 몫은 이미 산화한 공여체에서
            double eUse = 4 * r.fix, ePhUse = ePh + eCh > 0 ? eUse * ePh / (ePh + eCh) : 0;
            r.dH2 -= ePhUse * aH2 / dsum / 2; r.dH2S -= ePhUse * aHS / dsum / 8; r.dSO4 += ePhUse * aHS / dsum / 8;
            r.dFE2 -= ePhUse * aFe / dsum; r.o2Made = ePhUse * aW / dsum / 4; r.dO2 += r.o2Made;
            r.dCO2 -= r.fix; r.fixPh = ePh + eCh > 0 ? r.fix * ePh / (ePh + eCh) : 0;
            // 화학합성: 필요한 만큼만 돌린다(lam)
            double chemoLam = atpCh > 0 ? Math.Clamp(lam, 0, 1) : 0;
            r.chemoATP = atpCh * chemoLam; r.photoATP = atpPh * lam; r.orgATP = OrgATP(r.catab);
            // 고정에 쓴 공여체 전자(역전자 흐름)는 받개로 가지 않는다: 받개 쓰는 양을 그만큼 줄인다(전자 보존)
            double eDon = 0; for (int d = 0; d < 4; d++) eDon += R[d] * chemoLam * DE[d];
            double kA = eDon > 0 ? Math.Max(0, 1 - (eUse - ePhUse) / eDon) : 1;
            for (int d = 0; d < 4; d++)
            {
                double x = R[d] * chemoLam; if (x <= 0) continue;
                double xa = d >= 2 ? x : x * aer, xn = (x - xa) * kA;
                r.dO2 -= xa * DO2[d] * kA;
                if (d == 0) { r.dH2 -= x; r.dSO4 -= xn * 0.25 * sSO4; r.dH2S += xn * 0.25 * sSO4; r.dCO2 -= xn * 0.25 * (1 - sSO4); r.dCH4 += xn * 0.25 * (1 - sSO4); }
                else if (d == 1) { r.dCH4 -= x; r.dCO2 += x; r.dSO4 -= xn; r.dH2S += xn; }
                else if (d == 2) { r.dH2S -= x; r.dSO4 += x; }
                else { r.dFE2 -= x; }
            }
            // 태운 유기물: 산소 호흡 → CO2(O2 소비), 발효 → CO2 + 2 H2
            r.dCO2 += r.catab; r.dO2 -= r.catab * r.aerFrac; r.dH2 += r.catab * (1 - r.aerFrac) * 2;

            // 죽음: 온도, 산소 독성(세포 안 O2 포함, 항산화가 막음), 황화수소(산소 호흡 세포), 마름, 바이러스, 바탕
            double o2eff = e.O2 + Cfg.OWN_O2 * o2Own;
            r.mort = Cfg.MORT0 + 3 * (1 - fT) * (1 - fT)
                   + Cfg.ROS * o2eff / (o2eff + Cfg.ROS_K) * (1 - t.Sat(G.SOD, 0.005))
                   + Cfg.HS_TOX * e.H2S / (e.H2S + Cfg.HS_K) * resp
                   + Cfg.DRY_MORT * e.dry * (1 - t.Sat(G.DRY, 0.02))
                   + virusMort;
            return r;
        }
    }
}
