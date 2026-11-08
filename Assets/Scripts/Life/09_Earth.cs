// 기후와 자연 사건. 기온은 온실 기체(CO2·CH4)로 정해지고, 사건(화산 대분출·소행성 충돌·빙하기 방아쇠·열수공 이동)이
// 크기가 거듭제곱 분포(작은 것은 잦고 큰 것은 드묾)로 무작위로 일어난다. 사건이 생물을 직접 죽이지 않는다: 빛·기온·화학이 바뀔 뿐이다.
using System;
using System.Collections.Generic;

namespace Life
{
    public sealed class Event { public int day; public string kind, text; public double size; }

    public sealed partial class Planet
    {
        public double Tglobal;                    // 평균 표층 기온(°C)
        public double volcMul = 1, sulfurPulse;   // 화산 활동 배율, 오늘 황 비
        public double dT_aer, dT_imp, dT_ice, dark = 0;   // 사건 기온 변화(°C), 어둠(0 = 평소, 1 = 캄캄)
        public double darkTau = 25;               // 충돌 어둠·식음이 줄어드는 시간(일, e-fold). 충돌 크기에 따라 정한다
        public readonly List<Event> events = new();
        public double[] ice;                      // 해역 얼음 덮인 몫

        void Climate()
        {
            double co2 = Math.Max(1e-3, CO2PAL), ch4 = Math.Max(0, CH4ppm);
            Tglobal = 20 + 3 * Math.Log(co2, 2) + 2 * Math.Log(1 + ch4 / 2, 2) - 20 * (1 - Cfg.SUN) / 0.22 + dT_aer + dT_imp + dT_ice;
            double tDeep = Math.Max(2, Tglobal - 8);
            // 그늘: 칸의 생물량이 빛을 가린다
            var shade = new double[V];
            foreach (var s in alive) for (int v = 0; v < V; v++) shade[v] += B[s.slot * V + v];
            ice ??= new double[NR];
            for (int r = 0; r < NR; r++)
            {
                double lat = geo.rLat[r] * Math.PI / 180, sn = Math.Sin(lat);
                double tS = Math.Max(-1.8, Tglobal + 9 - 30 * sn * sn); ice[r] = Math.Clamp(-(Tglobal + 9 - 30 * sn * sn) / 10, 0, 0.95);
                double I0 = Cfg.SUN * Math.Cos(lat) * (1 - dark) * (1 - 0.9 * ice[r]);
                for (int k = 0; k < Cfg.NY; k++)
                {
                    int v = r * Cfg.NY + k; if (!live[v]) continue;
                    double zm = zTop[v] + thick[v] / 2;
                    temp[v] = kind[r] == 2 ? Tglobal + 9 - 30 * sn * sn : tDeep + (tS - tDeep) * Math.Exp(-zm / 150);
                    double kk = 0.04 + 0.002 * shade[v], top = zTop[v], bot = top + thick[v];
                    light[v] = I0 * (Math.Exp(-kk * top) - Math.Exp(-kk * bot)) / (kk * thick[v]);
                }
            }
            for (int i = 0; i < Cfg.NVENT; i++) { int v = NV0 + i; if (!live[v]) continue; temp[v] = tDeep + 15 * Math.Min(1, ventMul[v]); light[v] = 0; }
        }

        // 하루 한 번: 사건이 일어나는지, 지난 사건의 효과가 얼마나 남았는지
        void Events()
        {
            dT_aer *= Math.Exp(-1.0 / 10); dT_imp *= Math.Exp(-1.0 / Math.Max(40, darkTau)); dT_ice *= Math.Exp(-1.0 / 250); dark *= Math.Exp(-1.0 / darkTau);
            volcMul = 1 + (volcMul - 1) * Math.Exp(-1.0 / 60);
            for (int v = NV0; v < V; v++) if (vent[v]) ventMul[v] = 1 + (ventMul[v] - 1) * Math.Exp(-1.0 / 3000);   // 열수공 세기는 몇천 일에 걸쳐 제자리로
            if (knobs.eventRate <= 0 || rng.U() >= knobs.eventRate / 40) return;
            double m = Math.Min(30, Math.Pow(1 - rng.U(), -1 / 1.2));   // 크기 1–30, 큰 것일수록 드묾
            int kind = (int)(rng.U() * 4);
            Trigger(kind, m);
        }

        public void Trigger(int kind, double m)
        {
            var e = new Event { day = (int)Day, size = m };
            switch (kind)
            {
                case 0:
                    volcMul += 2 * m; sulfurPulse += 2e6 * m; dT_aer -= 0.4 * m;
                    for (int v = 0; v < V; v++) if (live[v] && zTop[v] >= 100) pool[P.H2S][v] += 0.5 * m;   // 깊은 바다로 황화수소
                    e.kind = "화산 대분출"; e.text = $"화산 활동 {1 + 2 * m:0}배, 잠깐 식은 뒤 CO₂로 더워짐"; break;
                case 1:
                    // 어둠 지속: 가장 큰 충돌(m 30, 칙술루브급)에서 약 1년 — 그을음이 광합성을 1–2년 막음(Bardeen 2017), 황산염 냉각 수 년(Brugger 2017).
                    // 작은 충돌의 지속은 문헌 값을 못 찾아 크기에 비례한다고 두고 25일을 바닥으로 한다(가정). 겹치면 긴 쪽을 따른다
                    darkTau = Math.Max(Math.Max(25, 12 * m), dark > 0.05 ? darkTau : 0);
                    dark = Math.Min(0.999, dark + 1 - Math.Exp(-0.25 * m)); dT_imp -= 1.2 * m;
                    e.kind = "소행성 충돌"; e.text = $"햇빛 {100 * dark:0}% 가림, {1.2 * m:0.0} °C 식음(약 {darkTau:0}일마다 1/e로 걷힘)"; break;
                case 2:
                    dT_ice -= 0.8 * m; e.kind = "빙하기 방아쇠"; e.text = $"{0.8 * m:0.0} °C 식음(몇백 일에 걸쳐 회복)"; break;
                default:
                    // 판 움직임: 열수공 몇 곳이 약해지고(최대 절반) 다른 곳이 그만큼 세진다
                    var vs = new List<int>(); for (int v = NV0; v < V; v++) if (live[v] && vent[v]) vs.Add(v);
                    int want = Math.Max(1, Math.Min(vs.Count / 2, (int)Math.Round(0.15 * m * vs.Count))), nOff = 0, nOn = 0;
                    for (int k = vs.Count - 1; k > 0; k--) { int j = (int)(rng.U() * (k + 1)); (vs[k], vs[j]) = (vs[j], vs[k]); }
                    for (int k = 0; k < vs.Count; k++)
                        if (nOff < want && ventMul[vs[k]] > 0.2) { ventMul[vs[k]] *= 0.3; nOff++; }
                        else if (nOn < want) { ventMul[vs[k]] = Math.Min(3, ventMul[vs[k]] * 2 + 0.3); nOn++; }
                    e.kind = "열수공 이동"; e.text = $"열수공 {nOff}곳 약해지고 {nOn}곳 세짐"; break;
            }
            // 황화수소를 넣었으면 장부에
            if (kind == 0) { double add = 0; for (int v = 0; v < V; v++) if (live[v] && zTop[v] >= 100) add += 0.5 * m * vol[v] * 1e-6; inS += add; }
            events.Add(e);
        }
    }
}
