// 지구 시스템과 자연현상(사건). 생태(먹고 먹히기·진화)는 실제 시간으로, 느린 지질 과정(탄소 순환·빙상·산소·융기)은
// 시간을 압축해 푼다: 생태 1일 = 지질 1000년(GEO_X). 그래서 융기 1 m/일 = 실제 1 mm/년, 빙기 100일 = 10만 년.
// 화산재·충돌 먼지처럼 몇 달–몇 년 가는 것은 생태 시간 그대로.
//   기온(기준 수온) = 기후 바탕(환경 '수온' 목표) + 3 °C × log₂(CO₂/기준) − 화산재 − 먼지 어둠 + 사건 강제 − 얼음 반사
//   CO₂: 화산 방출 − 규산염 풍화(따뜻하고 CO₂ 많고 땅이 넓고 새로 솟은 산이 많을수록 빠름) − 유기물 매몰 → 저절로 기후가 안정
//   산소(생물이 정함): 묻힌 유기물만큼 남는다(매몰 속도에 비례하는 평형으로 천천히)
//   얼음: 고위도 바다가 얼면 빙상 → 해수면이 내려가 대륙붕이 드러난다(최대 −120 m), 녹으면 올라간다(최대 +70 m)
using System;
using System.Collections.Generic;
using System.Linq;

namespace Evo
{
    public enum Qk { Volcano, Uplift, Warm, IceAge, Impact }

    public sealed class Quake
    {
        public Qk kind; public int size, cell = -1; public float amp, radius, days; public long tick; public string text;
        public double Day0 => tick * 2 / 24.0;
    }

    public sealed partial class World
    {
        public const double CO2_0 = 1000;   // 기준 CO₂(ppm, 고생대 초 수준)
        public const float GEO_X = 1000;    // 지질 압축: 생태 1일 = 지질 1000년
        public static readonly string[] QName = { "화산 분출", "융기·침강", "온난화·냉각", "빙하기", "운석 충돌" };
        public static readonly string[] SizeName = { "작음", "중간", "큼", "거대", "초거대" };
        public static readonly string[] VolNote = { "VEI 3–4", "VEI 5", "VEI 6(피나투보급)", "VEI 7–8(탐보라·토바급)", "대규모 용암 대지(시베리아 트랩급)" };
        public static readonly string[] ImpNote = { "지름 100 m", "지름 500 m", "지름 2 km", "지름 5 km", "지름 10 km(칙술루브급)" };

        public double co2 = CO2_0;
        public bool o2Bio;                     // 대기 산소를 생물(유기물 매몰)이 정함. 끄면 '대기 산소' 목표값을 따른다
        public bool autoQ; public float activity = 0.5f;   // 자동 사건: 0 = 조용한 행성 … 1 = 격동하는 행성
        public float tBase, aer, dark, darkTau = 30, warmF, ice, ice0 = -1, wMul = 1, upl;
        public double burRate;                 // 유기물 매몰(에너지/일/km², 30일 평균)
        public readonly List<Quake> quakes = new();
        public readonly float[] iceR = new float[RMAX], rdT = new float[RMAX], rnM = new float[RMAX];   // 해역 바다얼음 0–1, 해역 보정(수온 °C, 영양염 배율)
        public readonly float[] cellDT = new float[Geo.G * Geo.G], cellNM = new float[Geo.G * Geo.G];  // 칸별 보정(고급 패널). 지형이 바뀌어도 칸에 남는다
        public int regridN, land0;
        bool geoDirty; double burPrev, areaTot, relaxAcc, crustAcc, crustDef; Rng qr;
        readonly int[] arc = { -1, -1, -1 }; readonly float[] arcLeft = new float[3]; int landPct;   // 원시 바다의 조산대(호상열도·대륙 가장자리) 세 곳
        readonly float[] h0 = new float[Geo.G * Geo.G];   // 처음 지형: 섬·융기는 침식·지각 냉각으로 천천히 이리로 돌아간다

        void EarthReset()
        {
            co2 = CO2_0; tBase = env[Ev.Temp]; aer = dark = warmF = upl = 0; wMul = 1; ice0 = -1; ice = 0; darkTau = 30;
            burRate = 0; burPrev = 0; quakes.Clear(); geo.sea = 0; geoDirty = false; qr = new Rng((ulong)seed * 31 + 7);
            Array.Fill(cellNM, 1f); Array.Clear(cellDT, 0, cellDT.Length);
            land0 = 0; foreach (float x in geo.hM) if (x >= 0) land0++;   // 풍화 기준 = 다 자란 대륙
            Array.Copy(geo.h, h0, h0.Length); relaxAcc = 0; crustAcc = 0; Array.Fill(arc, -1); landPct = geo.LandCells() * 100 / (Geo.G * Geo.G);
            crustDef = 0; for (int i = 0; i < h0.Length; i++) crustDef += Def(i);
        }

        // 틱마다: 사건 진행·자동 사건 → 탄소·산소·얼음 → 기온·해수면. 지형이 바뀌었으면 하루 한 번 해역을 다시 나눈다
        void EarthStep()
        {
            float dd = P.DT / 24f; double day = tick * dd;
            double outg = 0; warmF = 0;
            foreach (var q in quakes)
            {
                double t = day - q.Day0; if (t < 0 || t > q.days) continue;
                float ramp = (float)Math.Min(1, Math.Min(t, q.days - t) / Math.Max(1, 0.2 * q.days));   // 앞뒤 20%는 서서히
                switch (q.kind)
                {
                    case Qk.Volcano: if (q.size == 4) outg += 3 * CO2_0 / q.days; break;          // 용암 대지: 기간 내내 CO₂
                    case Qk.Warm: warmF += q.amp * ramp; break;
                    case Qk.IceAge: warmF -= q.amp * ramp; break;
                    case Qk.Uplift: Bump(q.cell, q.amp * dd, q.radius, true); break;
                }
            }
            if (autoQ) AutoQuakes(dd);
            // 탄소: 기준 상태에서 방출 = 풍화(80%) + 매몰(20%)
            double V0 = CO2_0 / P.TAU_C, dT = env[Ev.Temp] - tBase;
            if (tick % 12 == 0) { double b = buried - burPrev; burPrev = buried; areaTot = 0; for (int c = 0; c < geo.NO; c++) areaTot += geo.rArea[c]; double r = b / Math.Max(1, areaTot * 1e-6); burRate = burRate == 0 ? r : burRate + (r - burRate) / 30; }
            double bN = Math.Min(3, burRate / P.BUR21);
            wMul = (float)(Math.Sqrt(co2 / CO2_0) * Math.Exp(0.07 * dT) * Math.Max(geo.LandCells(), 0.3 * land0) / Math.Max(1.0, land0) * (1 + upl));   // 땅이 적어도 바다 밑 현무암 풍화가 바닥을 받친다(Krissansen-Totton 2018)
            co2 = Math.Max(20, co2 + (V0 + outg - 0.8 * V0 * wMul - 0.2 * V0 * bN) * dd);
            upl *= MathF.Exp(-dd / 100);
            if (o2Bio) env[Ev.O2] = (float)Math.Clamp(env[Ev.O2] + (21 * bN - env[Ev.O2]) * dd / P.TAU_O2, 0, 30);
            // 얼음: 북위 60° 연평균 표층 수온이 영하로 갈수록 빙상이 커진다
            float m60 = env[Ev.Temp] + 3 - 30 * 0.75f, iceEq = Math.Clamp((-0.5f - m60) / 12, 0, 1);
            if (ice0 < 0) ice0 = ice = iceEq;
            ice += (iceEq - ice) * Math.Min(1, dd / P.TAU_ICE);
            float sea = Math.Clamp(150 * (ice0 - ice), -120, 70);
            if (MathF.Abs(sea - geo.sea) > 2) { geo.sea = sea; geoDirty = true; }
            aer *= MathF.Exp(-dd / P.TAU_AER); dark *= MathF.Exp(-dd / darkTau);
            env[Ev.Temp] = Math.Clamp(tBase + 3 * (float)Math.Log(co2 / CO2_0, 2) - aer - 18 * dark + warmF - P.ICE_ALB * (ice - ice0), Ev.Def[Ev.Temp].lo, Ev.Def[Ev.Temp].hi);
            if (tick % 12 == 0)   // 하루: 침식·침강으로 지형이 처음 모습 쪽으로(화산 섬이 깎이고 가라앉아 환초·평정해산이 된다, 지질 수백만 년)
            {
                if (P.PRIMO > 0) Crust();
                float mx = 0; for (int i = 0; i < h0.Length; i++) { float d = (h0[i] - geo.h[i]) / P.TAU_GEO; geo.h[i] += d; mx = Math.Max(mx, Math.Abs(d)); }
                relaxAcc += mx; if (relaxAcc > 2) { relaxAcc = 0; geoDirty = true; }
                if (geoDirty) Regrid();
            }
        }

        float Def(int i) => geo.hM[i] > -250 ? Math.Max(0, geo.hM[i] - h0[i]) : 0;   // 아직 덜 자란 대륙 지각(대륙붕보다 깊은 대양 바닥은 그대로)

        // 하루: 조산대마다 둘레 땅을 올린다(다 자란 고도까지). 조산대는 100–300일 뒤, 또는 둘레가 다 자라면 옮긴다.
        // 새 자리는 덜 자란 만큼, 그리고 이미 드러난 땅 옆일수록 잘 고른다(대륙은 가장자리에 호상열도가 붙으며 자란다, Condie 2007)
        void Crust()
        {
            const float SIG = 1.5f;
            double peak = crustDef / Math.Max(1, P.CRUST_T) * (0.4 + 1.2 * activity) / (3 * 2 * Math.PI * SIG * SIG);   // 조산대 한 곳 가운데 융기(m/일 = mm/년)
            float mx = 0;
            for (int k = 0; k < 3; k++)
            {
                arcLeft[k] -= 1;
                if (arc[k] < 0 || arcLeft[k] <= 0 || Def(arc[k]) < 20)
                {
                    double tot = 0; var wt = new double[h0.Length];
                    for (int i = 0; i < h0.Length; i++)
                    {
                        float d = Def(i); if (d < 20) continue;
                        int x = i % Geo.G, z = i / Geo.G; bool near = false;
                        for (int dz = -1; dz <= 1 && !near; dz++) for (int dx = -1; dx <= 1; dx++) { int xx = x + dx, zz = z + dz; if (xx >= 0 && zz >= 0 && xx < Geo.G && zz < Geo.G && geo.h[zz * Geo.G + xx] >= geo.sea) { near = true; break; } }
                        tot += wt[i] = d * (near ? 5 : 1);
                    }
                    if (tot <= 0) { arc[k] = -1; continue; }
                    double u = qr.U() * tot; int c = 0; while (c < wt.Length - 1 && (u -= wt[c]) > 0) c++;
                    arc[k] = c; arcLeft[k] = 100 + 200 * (float)qr.U();
                    Log("earth", $"조산 운동: {Where(c)}에서 지각이 솟기 시작했습니다(가운데 약 {peak:0.#} mm/년).");
                }
                for (int i = 0; i < h0.Length; i++)
                {
                    float dd = CellDist(i, arc[k]); if (dd > 3 * SIG) continue;
                    float dh = Math.Min((float)peak * MathF.Exp(-dd * dd / (2 * SIG * SIG)), Def(i)); if (dh <= 0) continue;
                    h0[i] += dh; geo.h[i] += dh; mx = Math.Max(mx, dh);
                }
            }
            crustAcc += mx; if (crustAcc > 20) { crustAcc = 0; geoDirty = true; }
            int pct = geo.LandCells() * 100 / (Geo.G * Geo.G);
            if (pct >= landPct + 5) { landPct = pct / 5 * 5; Log("earth", $"땅이 지표의 {pct}%로 늘었습니다."); }
        }

        // 사건 일으키기(화면·자동·시험). cell = 지도 칸(z*G+x), 위치 없는 사건은 −1
        public Quake Fire(Qk kind, int size, int cell = -1, float amp = 0, float radius = 2, float days = 100)
        {
            size = Math.Clamp(size, 0, 4);
            var q = new Quake { kind = kind, size = size, cell = cell, amp = amp, radius = radius, days = days, tick = tick };
            string where = cell >= 0 ? Where(cell) : "";
            switch (kind)
            {
                case Qk.Volcano:
                    aer += new[] { 0.005f, 0.03f, 0.4f, 1.5f, 1f }[size];   // 피나투보(VEI 6) −0.5 °C, VEI 4–5는 거의 0
                    co2 += new[] { 2, 10, 40, 150, 0 }[size];
                    Ash(cell, new[] { 1f, 1.5f, 2.5f, 4, 6 }[size], new[] { 0.3f, 1, 2, 4, 6 }[size]);
                    if (geo.Depth(cell) > 0) Bump(cell, new[] { 40f, 120, 350, 800, 0 }[size], 0.8f, false);   // 바다 화산: 쌓이면 섬
                    Kill(cell, new[] { 0f, 1, 1.5f, 2.5f, 6 }[size], new[] { 0f, 0.02f, 0.1f, 0.3f, 0.3f }[size], 200);
                    if (size == 4) q.days = Math.Max(30, days);
                    q.text = $"{QName[0]}({SizeName[size]}, {VolNote[size]}) — {where}"; break;
                case Qk.Impact:
                    dark = Math.Max(dark, new[] { 0.05f, 0.15f, 0.4f, 0.7f, 0.97f }[size]); darkTau = new[] { 5f, 15, 30, 60, 150 }[size];   // 먼지·그을음: 칙술루브는 1–2년 광합성이 거의 멈춤
                    aer += new[] { 0f, 0.1f, 0.4f, 1.5f, 4 }[size];
                    Bump(cell, -new[] { 0f, 30, 150, 500, 1500 }[size], new[] { 0.5f, 0.7f, 1, 1.5f, 2.5f }[size], false);
                    Kill(cell, new[] { 1f, 2, 4, 8, 40 }[size], new[] { 0.05f, 0.2f, 0.5f, 0.8f, 0.95f }[size], new[] { 50f, 100, 200, 1000, 4500 }[size]);
                    q.text = $"{QName[4]}({SizeName[size]}, {ImpNote[size]}) — {where}"; break;
                case Qk.Uplift:
                    q.text = $"{(amp >= 0 ? "융기" : "침강")} {MathF.Abs(amp):0.#} m/일 × {days:0}일(실제 {MathF.Abs(amp):0.#} mm/년 × {days * GEO_X / 1e4:0.#}만 년), 반경 {radius * 200:0} km — {where}"; break;
                case Qk.Warm:
                    q.text = $"{(amp >= 0 ? "온난화" : "냉각")} {MathF.Abs(amp):0.#} °C × {days:0}일(지질 {days * GEO_X / 1e4:0.#}만 년)"; break;
                case Qk.IceAge:
                    q.text = $"빙하기(세기 {SizeName[size]}, −{amp:0.#} °C) × {days:0}일(지질 {days * GEO_X / 1e4:0.#}만 년)"; break;
            }
            quakes.Add(q); if (quakes.Count > 300) quakes.RemoveAt(0);
            Log("earth", q.text); return q;
        }

        public string Where(int cell)
        {
            float lat = Geo.Lat(cell / Geo.G), d = geo.Depth(cell);
            return $"북위 {lat:0}° {(d > 0 ? $"{Geo.LName(lat)} {Geo.DName(d)}(수심 {d:0} m)" : $"육지(고도 {-d:0} m)")}";
        }

        // 실제 지구 빈도 비율(크기가 한 단계 클수록 5–10배 드묾), 전체 빈도는 격동 슬라이더로 0.03–3배. 화산재 평균 냉각 ≈ 0.7 °C × 배율(압축된 시간이라 실제 빈도를 그대로 쓰면 재가 쌓여 얼어붙는다)
        void AutoQuakes(float dd)
        {
            double mult = Math.Pow(10, 2 * activity - 1.5);
            double[] vol = { 0.1, 0.02, 0.002, 0.0003, 0.00005 }, imp = { 0.002, 0.0005, 0.0001, 0.00002, 0.000004 };
            int Cell() => (int)(qr.U() * Geo.G * Geo.G);
            int Spot()   // 화산은 대부분 열점에서 되풀이된다(하와이처럼 섬이 자란다). 나머지는 아무 데나
            {
                if (geo.spots.Count == 0 || qr.U() < 0.2) return Cell();
                int c = geo.spots[(int)(qr.U() * geo.spots.Count)], x = Math.Clamp(c % Geo.G + (int)(qr.U() * 3) - 1, 0, Geo.G - 1), z = Math.Clamp(c / Geo.G + (int)(qr.U() * 3) - 1, 0, Geo.G - 1);
                return z * Geo.G + x;
            }
            for (int s = 0; s < 5; s++)
            {
                if (qr.U() < vol[s] * mult * dd) Fire(Qk.Volcano, s, Spot(), days: 60);
                if (qr.U() < imp[s] * mult * dd) Fire(Qk.Impact, s, Cell());
            }
            if (qr.U() < 0.003 * mult * dd) Fire(Qk.Uplift, 0, Cell(), (float)((qr.U() < 0.5 ? 1 : -1) * (0.5 + 2.5 * qr.U())), 1 + (float)(3 * qr.U()), 30 + (float)(120 * qr.U()));
            if (qr.U() < 0.001 * mult * dd) Fire(Qk.Warm, 0, -1, (float)((qr.U() < 0.5 ? 1 : -1) * (1 + 3 * qr.U())), 0, 50 + (float)(250 * qr.U()));
            if (qr.U() < 0.001 * mult * dd) { int s = (int)(qr.U() * 4); Fire(Qk.IceAge, s, -1, IceAmp(s), 0, 100 + (float)(200 * qr.U())); }
        }
        public static float IceAmp(int size) => new[] { 3f, 5, 7, 10, 14 }[Math.Clamp(size, 0, 4)];

        float CellDist(int a, int b) { int dx = a % Geo.G - b % Geo.G, dz = a / Geo.G - b / Geo.G; return MathF.Sqrt(dx * dx + dz * dz); }

        // 칸 cell 둘레 가우스 모양으로 고도를 바꾼다(peak m, sigma 칸). spread면 반경 안을 고르게(융기·침강)
        void Bump(int cell, float peak, float sigma, bool spread)
        {
            if (cell < 0 || peak == 0) return;
            for (int i = 0; i < Geo.G * Geo.G; i++)
            {
                float d = CellDist(i, cell), f = spread ? Math.Clamp(1.5f - d / sigma, 0, 1) : MathF.Exp(-d * d / (2 * sigma * sigma));
                if (f < 1e-3f) continue;
                float dh = peak * f; geo.h[i] += dh;
                if (spread && dh > 0 && geo.Depth(i) < 0) upl += dh / 50f / Math.Max(1, land0);   // 새로 솟은 땅: 풍화가 빨라진다(새 산 50 m당 땅 한 칸만큼)
            }
            geoDirty = true;
        }

        // 해역마다 반경 안 칸 비율
        double[] Near(int cell, float radius)
        {
            var wr = new double[NR]; if (cell < 0) return wr;
            for (int i = 0; i < Geo.G * Geo.G; i++) if (CellDist(i, cell) <= radius) foreach (int a in Owners(i)) wr[a] += 1.0 / geo.rCells[a];
            return wr;
        }

        // 화산재의 철·인 → 표층 영양염
        void Ash(int cell, float radius, float uM)
        {
            var wr = Near(cell, radius);
            for (int c = 0; c < NR; c++) if (wr[c] > 0) for (int y = 0; y < nyR[c] && ZE[y] < 30; y++) nut[y * LS + c] += uM * wr[c];
        }

        // 반경 안 해역의 얕은 층(zmax m 위)에서 비율 frac이 죽는다(쓰나미·화쇄류·충격). 사체는 유기물 입자로
        void Kill(int cell, float radius, float frac, float zmax)
        {
            if (frac <= 0) return; var wr = Near(cell, radius);
            for (int c = 0; c < NR; c++)
            {
                double f = Math.Min(1, frac * wr[c]); if (f <= 0) continue;
                for (int y = 0; y < nyR[c] && ZE[y] < zmax; y++)
                {
                    int v = y * LS + c;
                    foreach (int s in live)
                    {
                        var st = strains[s]; double nN = P.QN * Math.Pow(st.esd0 / 10, 3) / vol[v];
                        for (int k = 0; k < st.K; k++) { int q = Ix(s, v, k); double d = Math.Floor(cnt[q] * f); cnt[q] -= d; pom[v] += d * st.C[k] * nN; }
                    }
                }
            }
        }

        // 지도 칸이 속한 해역들(바다 칸 = 해역 하나, 땅 칸 = 육지 + 해안이면 조간대)
        IEnumerable<int> Owners(int i) { if (geo.reg[i] >= 0) yield return geo.reg[i]; if (geo.sreg[i] >= 0) yield return geo.sreg[i]; if (geo.lreg[i] >= 0) yield return geo.lreg[i]; }

        // 지형·해수면이 바뀐 뒤: 해역을 다시 나누고, 개체·물질을 칸 단위로 옛 해역 → 새 해역에 옮긴다(양 보존, 땅이 된 칸의 것은 사라짐)
        void Regrid()
        {
            geoDirty = false; int G2 = Geo.G * Geo.G;
            var oReg = (int[])geo.reg.Clone(); var oS = (int[])geo.sreg.Clone(); var oL = (int[])geo.lreg.Clone(); var oCells = (int[])geo.rCells.Clone(); var oNy = (int[])nyR.Clone(); var oVol = (double[])vol.Clone();
            var chem = new[] { nut, bac, dox, pom, dom, cya }; var oChem = new double[chem.Length][]; for (int j = 0; j < chem.Length; j++) oChem[j] = (double[])chem[j].Clone();
            var oDep = (double[])dep.Clone(); var oCnt = new double[live.Count][];
            for (int j = 0; j < live.Count; j++) { oCnt[j] = new double[V * KM]; Array.Copy(cnt, Ix(live[j], 0, 0), oCnt[j], 0, V * KM); }
            double before = 0; foreach (var a in oCnt) foreach (var x in a) before += x;
            geo.Regions(RMAX - Geo.NLAND); Geometry();
            var pair = new Dictionary<(int, int), int>();
            void Pair(int a, int b) { if (a >= 0 && b >= 0) { var k = (a, b); pair[k] = pair.GetValueOrDefault(k) + 1; } }
            for (int i = 0; i < G2; i++) { Pair(oReg[i], geo.reg[i]); Pair(oS[i], geo.sreg[i]); Pair(oL[i], geo.lreg[i]); }   // 바다는 바다로, 조간대는 조간대로, 육지는 육지로
            foreach (int s in live) Array.Clear(cnt, Ix(s, 0, 0), V * KM);
            var amt = new double[chem.Length][]; for (int j = 0; j < chem.Length; j++) amt[j] = new double[V];
            Array.Clear(dep, 0, V);
            foreach (var kv in pair)
            {
                var (a, b) = kv.Key; double sh = (double)kv.Value / oCells[a];
                for (int y = 0; y < oNy[a]; y++)
                {
                    int vo = y * LS + a, vn = Math.Min(y, nyR[b] - 1) * LS + b;
                    for (int j = 0; j < live.Count; j++) { int s = live[j]; for (int k = 0; k < KM; k++) { double x = oCnt[j][vo * KM + k]; if (x > 0) cnt[Ix(s, vn, k)] += x * sh; } }
                    for (int j = 0; j < chem.Length; j++) amt[j][vn] += oChem[j][vo] * oVol[vo] * sh;
                    if (y == oNy[a] - 1) dep[Bot(b)] += oDep[vo] * sh;
                }
            }
            double[] init = { P.NSED * env[Ev.Nut] / 100 * 0.6, 1e6, env[Ev.O2], 0.5, 2, 1e5 };
            for (int v = 0; v < V; v++)
                for (int j = 0; j < chem.Length; j++) chem[j][v] = vol[v] <= 0 ? 0 : amt[j][v] > 0 ? amt[j][v] / vol[v] : init[j];   // 새로 물에 잠긴 해역은 기본값
            foreach (var a in new Array[] { mu, cue, gP, gB, kR, satA, genAcc, killAcc, depS }) Array.Clear(a, 0, a.Length);
            Tweaks(); regridN++;
            double after = 0; foreach (int s in live) for (int q = Ix(s, 0, 0), e = q + V * KM; q < e; q++) after += cnt[q];
            if (before > 0 && after < before * 0.99) Log("earth", $"바다였던 곳이 땅이 되어 개체의 {(1 - after / before) * 100:0.#}%가 사라졌습니다 (해역 {NR}곳, 해수면 {geo.sea:+0;-0} m).");
            Totals();
        }

        void Tweaks()   // 칸별 보정 → 해역 평균
        {
            Array.Clear(rdT, 0, RMAX); Array.Clear(rnM, 0, RMAX);
            for (int i = 0; i < Geo.G * Geo.G; i++) foreach (int c in Owners(i)) { rdT[c] += cellDT[i] / geo.rCells[c]; rnM[c] += cellNM[i] / geo.rCells[c]; }
        }

        // 고급 패널: 해역 c의 칸들에 수온 보정·영양염 배율
        public void SetRegionTweak(int c, float dT, float nM)
        {
            for (int i = 0; i < Geo.G * Geo.G; i++) if (Owners(i).Contains(c)) { cellDT[i] = dT; cellNM[i] = nM; }
            rdT[c] = dT; rnM[c] = nM;
        }
    }
}
