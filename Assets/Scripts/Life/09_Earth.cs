// 기후와 자연 사건. 기온은 온실 기체(CO2·CH4)로 정해지고, 사건이 크기가 거듭제곱 분포(작은 것은 잦고 큰 것은 드묾)로 무작위로 일어난다.
// 사건이 생물을 직접 죽이지 않는다: 빛·기온·화학이 바뀔 뿐이다. 사건마다 무엇이 바뀌고 그 결과 무엇이 따라오는지는 아래 KINDS의 설명(화면 '사건' 메뉴에도 그대로)에 있다.
using System;
using System.Collections.Generic;

namespace Life
{
    public sealed class Event { public int day; public string kind, text; public double size; }

    public sealed partial class Planet
    {
        // 이름, 무엇이 바뀌고 무엇이 따라오나(화면 메뉴 설명), 자연에서 일어나는 몫(가중치)
        // 설명의 숫자는 잰 값: LifeTest RESP=4500:10, 시드 1–4 평균, 크기 10(메뉴 단추와 같음), 같은 상태의 대조와 견줌. 잡음(난수만 흔듦)은 광합성 ±6 %·산소 ±2 % 안
        // 가중치: 빙하기 방아쇠·소행성은 드물게(실제 지구 30억 년에 큰 빙하기 몇 번, 큰 충돌도 드묾), 바다 뒤집힘·열수공 이동·화산·산맥은 잦게
        public static readonly (string name, string fx, double w)[] KINDS =
        {
            ("화산 대분출", "재로 몇 주 식고 CO₂ 온실로 몇 년 더워짐(100일 뒤 +3 °C) · 화산 가스(H₂·황)↑ → 화학합성 먹이↑ · 산소는 그대로", 2),
            ("소행성 충돌", "먼지가 햇빛을 가림: 10일 뒤 −12 °C, 100일 동안 광합성 −43 %·산소 −28 % · 몇 달–1년에 걸쳐 걷히고 회복", 1),
            ("빙하기 방아쇠", "얼음이 햇빛을 되비침: −6~−9 °C가 몇백 일 · 얼음 밑 바다는 빛·기체 교환↓ → 100일 동안 산소 −12 %", 1),
            ("열수공 이동", "열수공 몇 곳이 약해지고 다른 곳이 세짐 → 화학합성 생물의 터전이 옮겨감 · 지구 전체 기온·산소는 그대로", 2),
            ("메탄 분출", "메탄 온실: 10일 뒤 +7.6 °C, 몇 달에 걸쳐 식음 · 세포 맞춤 온도보다 더워지면 광합성↓(세계마다 −39~+15 %)", 1),
            ("산맥 솟음", "풍화가 몇 년 세져 바다로 인↑ · 인이 광합성을 막을 때만 효과(잰 세계에선 +3 %: 빛·질소가 먼저 모자랐음)", 2),
            ("바다 뒤집힘", "깊은 물이 올라와 인·질소·철 공급 → 100일 동안 광합성 +22 % · 같이 올라온 H₂S·철이 산소를 먹어 산소 −22 %", 3),
        };

        public double Tglobal;                    // 평균 표층 기온(°C)
        public double volcMul = 1, sulfurPulse;   // 화산 활동 배율, 오늘 황 비
        public double dT_aer, dT_imp, dT_ice, dT_ghg, dT_ch4, dark = 0;   // 사건 기온 변화(°C): 화산재, 충돌, 빙하, 화산 온실, 메탄 분출. 어둠(0 = 평소, 1 = 캄캄)
        public double darkTau = 25;               // 충돌 어둠·식음이 줄어드는 시간(일, e-fold). 충돌 크기에 따라 정한다
        public double weatherMul = 1;             // 산맥 풍화 배율(인 공급에 곱함)
        public readonly List<Event> events = new();
        public double[] ice;                      // 해역 얼음 덮인 몫
        public double TghCH4 => 2 * Math.Log(1 + Math.Max(0, CH4ppm) / 2, 2);   // 메탄 온실(°C)

        void Climate()
        {
            double co2 = Math.Max(1e-3, CO2PAL);
            Tglobal = 20 + 3 * Math.Log(co2, 2) + TghCH4 - 20 * (1 - Cfg.SUN) / 0.22 + dT_aer + dT_imp + dT_ice + dT_ghg + dT_ch4;
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
            dT_ghg *= Math.Exp(-1.0 / 600); dT_ch4 *= Math.Exp(-1.0 / 200);
            volcMul = 1 + (volcMul - 1) * Math.Exp(-1.0 / 60);
            weatherMul = 1 + (weatherMul - 1) * Math.Exp(-1.0 / 1000);
            for (int v = NV0; v < V; v++) if (vent[v]) ventMul[v] = 1 + (ventMul[v] - 1) * Math.Exp(-1.0 / 3000);   // 열수공 세기는 몇천 일에 걸쳐 제자리로
            if (knobs.eventRate <= 0 || rng.U() >= knobs.eventRate / 40) return;
            double m = Math.Min(30, Math.Pow(1 - rng.U(), -1 / 1.2));   // 크기 1–30, 큰 것일수록 드묾
            double wt = 0; foreach (var k in KINDS) wt += k.w;
            double x = rng.U() * wt; int kind = 0;
            while (kind < KINDS.Length - 1 && (x -= KINDS[kind].w) >= 0) kind++;
            Trigger(kind, m);
        }

        public void Trigger(int kind, double m)
        {
            var e = new Event { day = (int)Day, size = m, kind = KINDS[kind].name };
            switch (kind)
            {
                case 0:
                    // 깊은 바다에 황화수소를 바로 넣던 것은 뺐다(m 10이면 대기 산소 1 PAL의 17배를 한 번에 없애는 양 — 근거 없음). 무산소·황화 바다는 더워짐·생물에서 저절로 생긴다
                    // 온실: 여기선 대기만 1e-5로 줄여 바다 탄소가 대기의 수만 배라, 넣은 CO₂는 바다가 거의 다 먹고 pCO₂가 안 움직인다(실제 지구는 바다:대기 ≈ 50:1).
                    // 그래서 큰 화산 지대(LIP)가 CO₂로 올린 기온을 기온 항으로 직접 넣는다: 가장 큰 것(m 30) +9 °C — 페름기 말 시베리아 트랩 무렵 +8–10 °C(Sun 2012, Joachimski 2012).
                    // 줄어드는 시간(600일 e-fold)은 풍화가 CO₂를 거두는 수만–수십만 년을 게임 시간으로 줄인 것(가정)
                    volcMul += 2 * m; sulfurPulse += 2e6 * m; dT_aer -= 0.4 * m; dT_ghg += 0.3 * m;
                    e.text = $"화산 활동 {1 + 2 * m:0}배 · 화산재로 {0.4 * m:0.0} °C 식었다가(몇 주) CO₂ 온실로 +{0.3 * m:0.0} °C(몇 년에 걸쳐 줄어듦)"; break;
                case 1:
                    // 어둠 지속: 가장 큰 충돌(m 30, 칙술루브급)에서 약 1년 — 그을음이 광합성을 1–2년 막음(Bardeen 2017), 황산염 냉각 수 년(Brugger 2017).
                    // 작은 충돌의 지속은 문헌 값을 못 찾아 크기에 비례한다고 두고 25일을 바닥으로 한다(가정). 겹치면 긴 쪽을 따른다
                    darkTau = Math.Max(Math.Max(25, 12 * m), dark > 0.05 ? darkTau : 0);
                    dark = Math.Min(0.999, dark + 1 - Math.Exp(-0.25 * m)); dT_imp -= 1.2 * m;
                    e.text = $"햇빛 {100 * dark:0}% 가림, {1.2 * m:0.0} °C 식음(약 {darkTau:0}일마다 1/e로 걷힘)"; break;
                case 2:
                    dT_ice -= 0.8 * m; e.text = $"{0.8 * m:0.0} °C 식음(몇백 일에 걸쳐 회복)"; break;
                case 3:
                    {
                        // 판 움직임: 열수공 몇 곳이 약해지고(최대 절반) 다른 곳이 그만큼 세진다
                        var vs = new List<int>(); for (int v = NV0; v < V; v++) if (live[v] && vent[v]) vs.Add(v);
                        int want = Math.Max(1, Math.Min(vs.Count / 2, (int)Math.Round(0.15 * m * vs.Count))), nOff = 0, nOn = 0;
                        for (int k = vs.Count - 1; k > 0; k--) { int j = (int)(rng.U() * (k + 1)); (vs[k], vs[j]) = (vs[j], vs[k]); }
                        for (int k = 0; k < vs.Count; k++)
                            if (nOff < want && ventMul[vs[k]] > 0.2) { ventMul[vs[k]] *= 0.3; nOff++; }
                            else if (nOn < want) { ventMul[vs[k]] = Math.Min(3, ventMul[vs[k]] * 2 + 0.3); nOn++; }
                        e.text = $"열수공 {nOff}곳 약해지고 {nOn}곳 세짐"; break;
                    }
                case 4:
                    {
                        // 메탄 얼음(클라스레이트) 분출. 메탄을 대기에 넣으면 대기만 압축돼 있어 바다가 며칠 만에 거의 다 녹여 산화시킨다(재 보니 m 10에서 +6 °C가 열흘 안에 0).
                        // 실제 대기 메탄 수명은 약 10년(지금)–수백 년(산소 적은 원생대)이라 기온 항으로 넣고 200일 e-fold로 줄인다(시간 압축, 가정).
                        // 크기: m 10 → +6.9 °C, m 30 → +9.9 °C — 팔레오세-에오세 극열기(PETM) +5–8 °C(McInerney & Wing 2011)
                        double dT = 2 * Math.Log(1 + m, 2); dT_ch4 += dT;
                        e.text = $"메탄 온실 +{dT:0.0} °C(몇 달에 걸쳐 산화되며 줄어듦)"; break;
                    }
                case 5:
                    // 산맥 솟음: 새로 드러난 암석이 깎이며 풍화(인 공급)가 세진다. 1000일 e-fold로 제자리로(조산의 수백만 년을 줄인 것, 가정)
                    weatherMul += 0.3 * m; e.text = $"풍화 ×{weatherMul:0.0} → 바다로 인 공급↑(몇 년에 걸쳐 줄어듦)"; break;
                default:
                    {
                        // 바다 뒤집힘(용승·전복): 고른 해역의 물기둥을 위아래로 다 섞는다(부피 가중 평균이라 몰은 그대로). 생물은 섞지 않는다
                        var rs = new List<int>(); for (int r = 0; r < NR; r++) if (this.kind[r] != 2 && live[r * Cfg.NY]) rs.Add(r);   // kind(인자)가 해역 종류 필드를 가린다
                        int n = Math.Max(1, Math.Min(rs.Count, (int)Math.Round(0.05 * m * rs.Count)));
                        for (int k = rs.Count - 1; k > 0; k--) { int j = (int)(rng.U() * (k + 1)); (rs[k], rs[j]) = (rs[j], rs[k]); }
                        double pUp = 0;
                        for (int q = 0; q < n; q++)
                        {
                            int r = rs[q], v0 = r * Cfg.NY; double vt = 0; for (int k = 0; k < Cfg.NY; k++) if (live[v0 + k]) vt += vol[v0 + k];
                            double p0 = pool[P.PO4][v0];
                            for (int p = 0; p < P.N; p++)
                            {
                                double sum = 0; for (int k = 0; k < Cfg.NY; k++) if (live[v0 + k]) sum += pool[p][v0 + k] * vol[v0 + k];
                                for (int k = 0; k < Cfg.NY; k++) if (live[v0 + k]) pool[p][v0 + k] = sum / vt;
                            }
                            pUp += p0 > 0 ? pool[P.PO4][v0] / p0 : 1;
                        }
                        e.text = $"해역 {n}곳 물기둥이 뒤섞임 · 해수면 인 평균 {pUp / n:0.0}배"; break;
                    }
            }
            events.Add(e);
        }

        // 지금 남아 있는 사건 효과(화면 표시용 한 줄씩). 작아진 것은 뺀다
        // 어제 광합성 세포의 성장을 막은 것(생물량 몫): 무엇을 늘려야 광합성이 느는지. 빛 = 에너지, 나눔 = 한 칸의 같은 자원을 여럿이 나눔(붐빔)
        public string PhotoLimits()
        {
            double t = 0; for (int q = 0; q < LQ; q++) t += limLast[LQ + q]; if (t <= 0) return "아직 광합성 세포 없음";
            double share = 0; for (int q = 4; q < LQ; q++) share += limLast[LQ + q];
            return $"빛 {limLast[LQ] / t:P0} · 질소 {limLast[LQ + 2] / t:P0} · 인 {limLast[LQ + 3] / t:P0} · 리보솜 {limLast[LQ + 1] / t:P0} · 붐빔 {share / t:P0}";
        }

        public List<string> Effects()
        {
            var l = new List<string>();
            if (dT_aer < -0.05) l.Add($"화산재 {dT_aer:+0.0;-0.0} °C");
            if (dT_ghg > 0.05) l.Add($"화산 온실 {dT_ghg:+0.0;-0.0} °C");
            if (dark > 0.02) l.Add($"충돌 먼지: 햇빛 {dark:P0} 가림");
            if (dT_imp < -0.05) l.Add($"충돌 식음 {dT_imp:+0.0;-0.0} °C");
            if (dT_ice < -0.05) l.Add($"빙하 {dT_ice:+0.0;-0.0} °C");
            if (dT_ch4 > 0.05) l.Add($"메탄 분출 온실 {dT_ch4:+0.0} °C");
            if (TghCH4 > 0.1) l.Add($"대기 메탄 온실 {TghCH4:+0.0} °C");
            if (volcMul > 1.05) l.Add($"화산 가스 ×{volcMul:0.0}");
            if (weatherMul > 1.02) l.Add($"풍화(인 공급) ×{weatherMul:0.00}");
            return l;
        }
    }
}
