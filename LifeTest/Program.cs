// 머리 없는 시험: dotnet LifeTest.dll <일수> <시드> [보고 간격] [돌연변이율] [csv]. 환경 변수: THREADS EVENTS PHOS VENT NODRIFT LOAD SAVE RESP RCASES PROF PROBE CELL SHAPE BENCH OXCAL SIZEPROBE ENDSAVE TOPPROBE ATKPROBE O2HOLD PO4X NANO
// 매 간격 상태 한 줄, 사건은 일어날 때, 끝에 '됐다' 기준 판정(PASS/FAIL).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Life;

static class Program
{
    static int Main(string[] a)
    {
        int days = a.Length > 0 ? int.Parse(a[0]) : 100, seed = a.Length > 1 ? int.Parse(a[1]) : 1, every = a.Length > 2 ? int.Parse(a[2]) : 10;
        var pl = Environment.GetEnvironmentVariable("LOAD") is string ld ? Save.Read(ld, out _) : new Planet(seed);   // LOAD=파일: 저장한 지구에서 이어서(속도 재기 등)
        if (a.Length > 3) pl.knobs.mu = double.Parse(a[3]);
        if (Environment.GetEnvironmentVariable("THREADS") is string th) pl.po.MaxDegreeOfParallelism = int.Parse(th);   // 여러 시드를 한꺼번에 돌릴 때 실행마다 스레드 수를 묶는다
        if (Environment.GetEnvironmentVariable("EVENTS") is string er) pl.knobs.eventRate = double.Parse(er);
        if (Environment.GetEnvironmentVariable("PO4X") is string px) { for (int v = 0; v < pl.V; v++) pl.pool[P.PO4][v] *= double.Parse(px); pl.ledger0 = pl.Ledger(); }   // PO4X: 처음 바다 인을 이 배수로(원생누대의 낮은 인 → 신원생대 인 증가 가설 시험)   // 사건 빈도 배율(0 = 사건 없음)
        if (Environment.GetEnvironmentVariable("PHOS") is string phx) pl.knobs.phosphate = double.Parse(phx);   // 인 공급 배율
        if (Environment.GetEnvironmentVariable("VENT") is string vt) for (int v = pl.NV0; v < pl.V; v++) pl.ventMul[v] *= double.Parse(vt);   // 열수공 세기 배율
        if (Environment.GetEnvironmentVariable("NODRIFT") != null) pl.drift = false;   // 중립 표류 끔(대조)
        string csv = a.Length > 4 ? a[4] : null;
        int nv = 0, nvent = 0; for (int v = 0; v < pl.V; v++) { if (pl.live[v]) nv++; if (pl.vent[v]) nvent++; }
        Console.WriteLine($"시드 {seed}: 해역 {pl.NR}, 칸 {nv}, 열수공 {nvent}, μ {pl.knobs.mu}");
        if (Environment.GetEnvironmentVariable("PROBE") is string pr)
        {
            int until = int.Parse(pr); double[] last = pl.Ledger();
            pl.probe = name =>
            {
                if (pl.tick >= until) return; var L = pl.Ledger();
                Console.WriteLine($"t{pl.tick} {name,-9} dC {L[0] - last[0]:+0.000e0;-0.000e0} dN {L[1] - last[1]:+0.0e0;-0.0e0} dP {L[2] - last[2]:+0.0e0;-0.0e0} dFe {L[3] - last[3]:+0.0e0;-0.0e0} dS {L[4] - last[4]:+0.0e0;-0.0e0} | atmCO2 {pl.atmCO2:0.000e0} 생물 {pl.alive.Sum(s => Enumerable.Range(0, pl.V).Sum(v => pl.B[s.slot * pl.V + v] * pl.vol[v] * 1e-6)):0.00e0}");
                last = L;
            };
        }
        if (Environment.GetEnvironmentVariable("BENCH") != null) { Bench(pl); return 0; }
        if (Environment.GetEnvironmentVariable("OXCAL") != null) return OxCal(pl);   // 몸 속 산소 맞춤 확인(Payne 2009)   // 진화 후보 하나에 드는 시간(한 스레드): 형질·몸·섭취·먹힘
        if (Environment.GetEnvironmentVariable("CELL") != null)
            foreach (var st in pl.alive) for (int v = 0; v < pl.V; v++)
            {
                if (pl.B[st.slot * pl.V + v] <= 0) continue; var e = pl.EnvAt(v); var r = Cell.Grow(st.t, st.b, e, 0, 0);
                Console.WriteLine($"칸 {v} T {e.T:0.0} 빛 {e.light:0.000} H2 {e.H2:0.00} CO2 {e.CO2:0} DOC {e.DOC:0.0} NH4 {e.NH4:0.0} PO4 {e.PO4:0.00} | g {r.g:0.000} mort {r.mort:0.000} fix {r.fix:0.000} assim {r.assim:0.000} catab {r.catab:0.000} chemoATP {r.chemoATP:0.00} nh4 {r.nh4:0.0000} po4 {r.po4:0.00000} | φ " + string.Join(" ", st.t.phi.Select(x => x.ToString("0.000"))));
            }
        if (Environment.GetEnvironmentVariable("PROF") is string pf)   // PROF=틱 수: 단계별 시간(THREADS 없으면 한 스레드)
        {
            if (Environment.GetEnvironmentVariable("THREADS") == null) pl.po.MaxDegreeOfParallelism = 1;
            int nt = int.Parse(pf); for (int i = 0; i < 6; i++) pl.Step();
            Array.Clear(pl.tStage, 0, 5); var w0 = Stopwatch.StartNew();
            for (int i = 0; i < nt; i++) pl.Step();
            double tot = w0.Elapsed.TotalMilliseconds / nt;
            int pairs = 0; for (int v = 0; v < pl.V; v++) foreach (var st in pl.alive) if (pl.B[st.slot * pl.V + v] > Cfg.BMIN) pairs++;
            Console.WriteLine($"{pl.Day:0}일 계통 {pl.alive.Count} (계통·칸) {pairs}: 틱 {tot:0.00} ms = 하루 {pl.tStage[0] * 1000 / nt:0.00} 기후 {pl.tStage[1] * 1000 / nt:0.00} 생태 {pl.tStage[2] * 1000 / nt:0.00} 물질 {pl.tStage[3] * 1000 / nt:0.00} 진화 {pl.tStage[4] * 1000 / nt:0.00} · {1000 / (3 * tot):0} 일/초");
            return 0;
        }
        var sw = Stopwatch.StartNew(); int ev = 0;
        double landF = 0, allA = 0; for (int r = 0; r < pl.NR; r++) { allA += pl.area[r]; if (pl.kind[r] == 2) landF += pl.area[r]; }
        double pIn0 = pl.inP, pOut0 = pl.outP; double pBur = 0, pS = 0, pW = 0, pC = pl.inC, pO = pl.outC, pBc = pl.buriedC;   // 산소 장부: 지난 보고 때 누적값
        double pRed = pl.Red(), pRb = pl.redB, red0 = pRed, rb0 = pRb;   // 산화환원 장부: 지난 보고 때 환원력·경계 누적, 처음 값
        double o2Hold = Environment.GetEnvironmentVariable("O2HOLD") is string oh ? double.Parse(oh) : 0;
        for (int d = 1; d <= days; d++)
        {
            for (int k = 0; k < Cfg.TPD; k++) pl.Step();
            if (o2Hold > 0 && pl.O2PAL > 0.01 && pl.O2PAL < o2Hold) { double add = (o2Hold - pl.O2PAL) * Cfg.O2_PAL_MOL; pl.atmO2 += add; pl.redB -= 4 * add; }   // O2HOLD: 산소 대사건 뒤 대기 산소를 이 PAL로 받친다(밖에서 넣은 O2는 경계 산화력으로 장부에 적음)
            if (Environment.GetEnvironmentVariable("SAVE") is string svD && d == int.Parse(svD)) return SaveCheck(pl, (csv ?? "save") + ".sav");
            if (Environment.GetEnvironmentVariable("RESP") is string rsD && d == int.Parse(rsD.Split(':')[0])) return Response(pl, (csv ?? "resp") + ".sav", double.Parse(rsD.Split(':')[1]));
            if (d % every == 0)
            {
                double so = 0, sv = 0; for (int r = 0; r < pl.NR; r++) { int v = r * Cfg.NY; if (pl.live[v] && pl.kind[r] == 0) { so += pl.pool[P.SO4][v] * pl.vol[v]; sv += pl.vol[v]; } }
                Console.WriteLine($"        산소 장부(mol/일): 유기 매몰 {(pl.buriedC - pBur) / every:0.0e0} · 황 매몰 {(pl.outS - pS) / every:0.0e0} · 산화 풍화 O2 {(pl.weatherO2 - pW) / every:0.0e0} | 땅 {landF / allA:P0} · 표층 SO4 {so / Math.Max(1, sv):0.0} μM");
                double tP = 0, tPv = 0; for (int v = 0; v < pl.V; v++) if (pl.live[v]) { tP += pl.pool[P.PO4][v] * pl.vol[v] * 1e-6; tPv += pl.vol[v] * 1e-6; }
                double pBurD = (pl.outP - pOut0) / every;
                Console.WriteLine($"        인 장부: 풍화 유입 {(pl.inP - pIn0) / every:0.00e0} · 매몰 {pBurD:0.00e0} mol/일 | 바다 인 {tP:0.00e0} mol(평균 {tP / tPv:0.000} μM) · 매몰로 비는 시간 {tP / Math.Max(1e-30, pBurD):0.0e0}일");
                pIn0 = pl.inP; pOut0 = pl.outP;
                pBur = pl.buriedC; pS = pl.outS; pW = pl.weatherO2;
                double red = pl.Red(), tO2 = 0, tSO4 = 0, tHS = 0;
                for (int v = 0; v < pl.V; v++) if (pl.live[v]) { double L = pl.vol[v] * 1e-6; tO2 += pl.pool[P.O2][v] * L; tSO4 += pl.pool[P.SO4][v] * L; tHS += pl.pool[P.H2S][v] * L; }
                Console.WriteLine($"        산화환원(O2 당량 mol/일, + = 환원): 경계 {(pl.redB - pRb) / 4 / every:0.0e0} · 새는 몫 {((red - pRed) - (pl.redB - pRb)) / 4 / every:0.0e0} | 대기 O2 {pl.atmO2:0.0e0} · 바다 O2 {tO2:0.0e0} · SO4 {tSO4:0.0e0} · H2S {tHS:0.0e0} mol");
                pRed = red; pRb = pl.redB;
                double oC = 0; for (int v = 0; v < pl.V; v++) if (pl.live[v]) oC += pl.pool[P.CO2][v] * pl.vol[v] * 1e-6;
                double gh = 20 + 3 * Math.Log(Math.Max(1e-3, pl.CO2PAL), 2) + 2 * Math.Log(1 + Math.Max(0, pl.CH4ppm) / 2, 2) - 20 * (1 - Cfg.SUN) / 0.22;
                Console.WriteLine($"        기후: T {pl.Tglobal:0.0} = 온실 {gh:0.0} + 사건 {pl.dT_ice + pl.dT_imp + pl.dT_aer + pl.dT_ghg + pl.dT_ch4:0.0}(빙하 {pl.dT_ice:0.0} 충돌 {pl.dT_imp:0.0} 화산재 {pl.dT_aer:0.0} 화산온실 {pl.dT_ghg:0.0} 메탄분출 {pl.dT_ch4:0.0}) 대기메탄 {pl.TghCH4:0.0} · 탄소(mol/일) 들어옴 {(pl.inC - pC) / every:0.0e0} 나감 {(pl.outC - pO) / every:0.0e0}(유기 매몰 {(pl.buriedC - pBc) / every:0.0e0}) | CO2 대기 {pl.atmCO2:0.0e0} · 바다 {oC:0.0e0} mol");
                pC = pl.inC; pO = pl.outC; pBc = pl.buriedC;
            }
            while (ev < pl.events.Count) { var e = pl.events[ev++]; Console.WriteLine($"  ! {e.day}일 {e.kind}({e.size:0.0}): {e.text}"); }
            if (Environment.GetEnvironmentVariable("VENTS") != null && d % every == 0)
                for (int i = 0; i < Cfg.NVENT; i++)
                {
                    int v = pl.NV0 + i; if (!pl.live[v]) continue; double b = 0; Strain top = null; double tb = 0;
                    foreach (var st in pl.alive) { double x = pl.B[st.slot * pl.V + v]; b += x; if (x > tb) { tb = x; top = st; } }
                    var e = pl.EnvAt(v); string gr = "";
                    if (top != null) { var r = Cell.Grow(top.t, top.b, e, 0, 0); gr = $"g {r.g:0.000} mort {r.mort:0.000} topt {top.t.topt:0.0} tw {top.t.tw:0.0}"; }
                    Console.WriteLine($"    열수공 {i} 칸 {v}: T {e.T:0.0} H2 {e.H2:0.0} H2S {e.H2S:0.0} Fe2 {e.FE2:0} CO2 {e.CO2:0} NH4 {e.NH4:0.00} PO4 {e.PO4:0.000} DOC {e.DOC:0.0} 생물 {b:0.0e0} μM mul {pl.ventMul[v]:0.00} {gr}");
                }
            if (Environment.GetEnvironmentVariable("SEL") != null && d % every == 0)
            {
                // 빛이 가장 센 칸에서 가장 많은 계통의 유전자 한 걸음씩 바꿨을 때 성장률 변화(먹힘·바이러스 뺌)
                int vb = -1; double lb = -1; for (int v = 0; v < pl.NV0; v++) if (pl.live[v] && pl.light[v] > lb) { lb = pl.light[v]; vb = v; }
                foreach (int vv in new[] { vb, vb + 1 })
                {
                    Strain top = null; double tb = 0; foreach (var st in pl.alive) { double x = pl.B[st.slot * pl.V + vv]; if (x > tb) { tb = x; top = st; } }
                    if (top == null) continue; var e = pl.EnvAt(vv); var r0 = Cell.Grow(top.t, top.b, e, 0, 0); double g0 = r0.g - r0.mort;   // 순증가(먹힘·바이러스 뺌)
                    var sb = new System.Text.StringBuilder($"    선택 칸 {vv} 빛 {e.light:0.00} T {e.T:0} H2 {e.H2:0.00} H2S {e.H2S:0.0} Fe2 {e.FE2:0.0} DOC {e.DOC:0.00} | g {g0:0.000} |");
                    foreach (int gi in new[] { G.PHOTO, G.OSMO, G.FIX, G.RIB, G.EOX, G.SOD, G.RESP, G.PHAGO, G.ADH, G.SIZE })
                        foreach (double dd in new[] { Cfg.STEP, -Cfg.STEP })
                        {
                            var g2 = (float[])top.g.Clone(); g2[gi] = (float)Math.Clamp(g2[gi] + dd, 0, 1); var t2 = Traits.Of(g2);
                            var r1 = Cell.Grow(t2, Body.Of(g2, t2), e, 0, 0); double g1 = r1.g - r1.mort; sb.Append($" {G.Name[gi]}{(dd > 0 ? "+" : "−")} {g1 - g0:+0.000;-0.000}");
                        }
                    Console.WriteLine(sb);
                }
            }
            if (Environment.GetEnvironmentVariable("LAND") != null && d % every == 0) Land(pl);
            if (Environment.GetEnvironmentVariable("AER") != null && d % every == 0) Aer(pl);
            if (Environment.GetEnvironmentVariable("PHOTO") != null && d % every == 0) Photo(pl);
            if (Environment.GetEnvironmentVariable("DOM") != null && d % every == 0) Dom(pl);
            if (d % every == 0 && pl.history.Count > 0)
            {
                var h = pl.history[^1];
                var lay = new double[Cfg.NY + 1];
                foreach (var st in pl.alive) for (int v = 0; v < pl.V; v++) { if (!pl.live[v]) continue; lay[v >= pl.NV0 ? Cfg.NY : v % Cfg.NY] += pl.B[st.slot * pl.V + v] * pl.vol[v] * 1e-6; }
                double W = 0, eox = 0, eoxP = 0, wP = 0, ph = 0, ch = 0, os = 0, pg = 0, sz = 0, tp = 0, rs = 0;
                foreach (var st in pl.alive) { double w = st.total; W += w; eox += w * st.t.eox; eoxP += w * st.t.phi[G.PHOTO] * st.t.eox; wP += w * st.t.phi[G.PHOTO]; ph += w * st.t.phi[G.PHOTO]; ch += w * st.t.phi[G.CHEMO]; os += w * st.t.phi[G.OSMO]; pg += w * st.t.phi[G.PHAGO]; sz += w * Math.Log10(st.b.size); tp += w * st.t.topt; rs += w * st.t.phi[G.RESP]; }
                if (W > 0) Console.WriteLine($"        평균 형질: 광계 산화력 {eox / W:0.00} V(광합성 가중 {eoxP / Math.Max(1e-30, wP):0.00}) · 광계 {ph / W:0.000} · 화학합성 {ch / W:0.000} · 흡수 {os / W:0.000} · 포식 {pg / W:0.000} · 호흡 {rs / W:0.000} · 크기 {Math.Pow(10, sz / W):0.0} μm · 최적 {tp / W:0} °C");
                var ew = new double[8]; double ewt = 0;
                for (int v = 0; v < pl.V; v++)
                {
                    if (!pl.live[v]) continue; double bv = 0; foreach (var st in pl.alive) bv += pl.B[st.slot * pl.V + v]; if (bv <= 0) continue;
                    var e = pl.EnvAt(v); double w = bv * pl.vol[v] * 1e-6; ewt += w;
                    ew[0] += w * e.NH4; ew[1] += w * e.PO4; ew[2] += w * e.H2; ew[3] += w * e.H2S; ew[4] += w * e.FE2; ew[5] += w * e.DOC; ew[6] += w * e.light; ew[7] += w * bv;
                }
                if (ewt > 0) Console.WriteLine($"        생물 가중 환경: NH4 {ew[0] / ewt:0.000} PO4 {ew[1] / ewt:0.0000} H2 {ew[2] / ewt:0.000} H2S {ew[3] / ewt:0.00} Fe2 {ew[4] / ewt:0.0} DOC {ew[5] / ewt:0.00} 빛 {ew[6] / ewt:0.000} 생물 {ew[7] / ewt:0.0e0} μM | 막힘 에너지 {h.limE:P0} 리보솜 {h.limR:P0} 질소 {h.limN:P0} 인 {h.limP:P0} 나눔 {h.limS:P0}");
                {
                    string[] qn = { "에너지", "리보솜", "질소", "인" }; string[] pn = { "CO2", "O2", "H2", "CH4", "Fe2", "Fe3", "H2S", "SO4", "NH4", "PO4", "DOC", "POC0", "POC1", "POC2" };
                    for (int half = 0; half < 2; half++)
                    {
                        double t2 = 0; for (int q = 0; q < Planet.LQ; q++) t2 += pl.limLast[half * Planet.LQ + q]; if (t2 <= 0) continue;
                        var sb2 = new System.Text.StringBuilder(half == 1 ? "        광합성 막힘:" : "        나머지 막힘:");
                        for (int q = 0; q < Planet.LQ; q++) { double f = pl.limLast[half * Planet.LQ + q] / t2; if (f >= 0.02) sb2.Append($" {(q < 4 ? qn[q] : pn[q - 4] + " 나눔")} {f:P0}"); }
                        Console.WriteLine(sb2);
                    }
                }
                Console.WriteLine($"        층별 생물(mol) 표층 {lay[0]:0.0e0} · 20–100 m {lay[1]:0.0e0} · 100–500 m {lay[2]:0.0e0} · 심해 {lay[3]:0.0e0} · 열수공 {lay[4]:0.0e0}");
                {   // 층별 물(부피 평균 μM): 산소가 어느 층에 쌓이고 어디서 황화수소와 만나는지
                    var cw = new double[Cfg.NY + 1, 5]; var vw = new double[Cfg.NY + 1]; int[] ps = { P.O2, P.H2S, P.FE2, P.PO4, P.NH4 };
                    for (int v = 0; v < pl.V; v++) { if (!pl.live[v]) continue; int L = v >= pl.NV0 ? Cfg.NY : v % Cfg.NY; vw[L] += pl.vol[v]; for (int k = 0; k < 5; k++) cw[L, k] += pl.pool[ps[k]][v] * pl.vol[v]; }
                    var sw2 = new System.Text.StringBuilder("        층별 물(μM O2·H2S·Fe2·PO4·NH4):");
                    string[] ln = { "표층", "20–100 m", "100–500 m", "심해" };
                    for (int L = 0; L < Cfg.NY; L++) sw2.Append($" {ln[L]} {cw[L, 0] / vw[L]:0.0}·{cw[L, 1] / vw[L]:0.0}·{cw[L, 2] / vw[L]:0.0}·{cw[L, 3] / vw[L]:0.00}·{cw[L, 4] / vw[L]:0.0}");
                    Console.WriteLine(sw2);
                }
                Console.WriteLine($"{h.day,6}일 O2 {h.O2:0.0e0} CO2 {h.CO2:0.0} CH4 {h.CH4:0} T {h.T:0.0} | 생물 {h.bio:0.0e0} 종 {h.nSp} 계통 {h.nStrain} 갈래 {h.nClade} | 산소광합 {h.oxyShare:P0} 덩이 {h.multi:P1} 최대세포 {h.maxCells:0} 크기 {h.maxSize:0} | 호흡 {h.aer:P0} 항산 {h.sod:P0} | 역할 광{h.rPhoto} 화{h.rChemo} 포{h.rPrey} 청{h.rDet} 분{h.rDoc} 수입 광{h.photoSh:P0} 화{h.chemoSh:P0} 포{h.preySh:P0} 청{h.detSh:P0} 분{h.docSh:P0} | 정착 {h.kept} 장부 {h.drift:0e0} | {sw.Elapsed.TotalSeconds / d * 1000:0}ms/일");
            }
        }
        var H = pl.history;
        if (csv != null)
            File.WriteAllLines(csv, new[] { "day,O2,CO2,CH4,T,bio,nSp,nStrain,nClade,oxy,multi,aer,sod,rPhoto,rChemo,rPrey,rDet,rDoc,photoSh,chemoSh,preySh,detSh,docSh,maxCells,maxSize,drift,kept,resp,aerOx" }
                .Concat(H.Select(h => $"{h.day},{h.O2:g4},{h.CO2:g4},{h.CH4:g4},{h.T:0.00},{h.bio:g4},{h.nSp},{h.nStrain},{h.nClade},{h.oxyShare:0.0000},{h.multi:0.0000},{h.aer:0.0000},{h.sod:0.0000},{h.rPhoto},{h.rChemo},{h.rPrey},{h.rDet},{h.rDoc},{h.photoSh:0.000},{h.chemoSh:0.000},{h.preySh:0.000},{h.detSh:0.000},{h.docSh:0.000},{h.maxCells:0.0},{h.maxSize:0.0},{h.drift:g3},{h.kept},{h.resp:0.0000},{h.aerOx:0.0000}")));

        // 판정
        Console.WriteLine("── 판정 ──");
        int run = 0, best = 0; foreach (var h in H) { run = h.multi >= 0.01 ? run + 1 : 0; best = Math.Max(best, run); }
        Say("다세포 자발 출현(덩이 ≥ 1 % 생물량, 30일 연속)", best >= 30, $"최장 {best}일, 최대 세포 수 {H.Max(h => h.maxCells):0}");
        var tro = H.FirstOrDefault(h => h.rPhoto > 0 && h.rPrey > 0 && h.rDet > 0 && h.rDoc > 0);
        Say("먹이 관계 분화(광합성·포식·청소·분해 종이 한날 다 있음)", tro.day > 0, tro.day > 0 ? $"{tro.day}일" : $"최대 광{H.Max(h => h.rPhoto)} 포{H.Max(h => h.rPrey)} 청{H.Max(h => h.rDet)} 분{H.Max(h => h.rDoc)}");
        int low = H.FindIndex(h => h.O2 < 1e-5), hi = low >= 0 ? H.FindIndex(low, h => h.O2 >= 1e-2) : -1;
        Say("산소 대사건(O2 1e-5 미만 → 1e-2 PAL 이상)", hi >= 0, hi >= 0 ? $"{H[hi].day}일, 산소광합 {H[hi].oxyShare:P0}" : $"최대 O2 {H.Max(h => h.O2):0.0e0} PAL");
        if (hi >= 0)
        {
            double aer0 = H.Take(hi).Average(h => h.aer), sod0 = H.Take(hi).Average(h => h.sod);
            var after = H.Skip(hi).ToList(); double aer1 = after.Average(h => h.aer), sod1 = after.Average(h => h.sod);
            Say("산소 적응(산소 호흡·항산화 몫 증가)", aer1 > aer0 + 0.1 && sod1 > sod0, $"호흡 {aer0:P0}→{aer1:P0}, 항산화 {sod0:P0}→{sod1:P0}");
            double rs0 = H.Take(hi).Average(h => h.resp), rs1 = after.Average(h => h.resp), ox1 = after.Average(h => h.aerOx);
            Say("유전 적응(호흡·항산화 단백질 몫 증가)", rs1 > rs0 + 0.1 && sod1 > sod0, $"호흡 단백질 {rs0:P0}→{rs1:P0}, 항산화 {sod0:P0}→{sod1:P0}, 산소 있는 물의 호흡 몫 {ox1:P0}");
        }
        int me = -1, rec = -1;
        for (int i = 30; i < H.Count && me < 0; i++) { int mx = H.Skip(i - 30).Take(30).Max(h => h.nSp); if (mx >= 6 && H[i].nSp <= mx / 2) { me = i; int target = (int)Math.Ceiling(0.9 * mx); rec = H.FindIndex(i, h => h.nSp >= target); } }
        Say("대멸종(30일 안에 종 절반 이상 사라짐)과 회복(이전의 90 %)", me >= 0 && rec >= 0, me >= 0 ? $"{H[me].day}일 멸종, 회복 {(rec >= 0 ? H[rec].day + "일" : "아직")}" : "없음");
        var j = pl.jumps.OrderBy(x => x).ToList();
        if (j.Count > 0) Say("점진성(정착 돌연변이 몸 모양 변화 95 % ≤ 0.4)", j[(int)(0.95 * (j.Count - 1))] <= 0.4, $"중앙 {j[j.Count / 2]:0.000}, 95 % {j[(int)(0.95 * (j.Count - 1))]:0.000}, 최대 {j[^1]:0.00}");
        Say("질량 보존(장부 어긋남 < 1e-6)", H.Max(h => h.drift) < 1e-6, $"최대 {H.Max(h => h.drift):0e0}");
        { double lk = ((pl.Red() - red0) - (pl.redB - rb0)) / 4; Say("전자 보존(새는 O2 당량 < 유기 매몰의 1 %)", Math.Abs(lk) < 0.01 * Math.Max(1, pl.buriedC), $"새는 몫 {lk:0.0e0} mol O2 · 유기 매몰 {pl.buriedC:0.0e0} mol C"); }
        { var L = pl.Ledger(); L[1] -= pl.nfixV.Sum(); string[] en = { "C", "N", "P", "Fe", "S" }; Console.WriteLine("장부 원소별 어긋남: " + string.Join(" ", Enumerable.Range(0, 5).Select(k => $"{en[k]} {(L[k] - pl.ledger0[k]) / Math.Abs(pl.ledger0[k]):+0.0e0;-0.0e0}"))); }
        { var hg = new int[G.N]; foreach (var st in pl.alive) if (st.gene >= 0) hg[st.gene]++; Console.WriteLine("살아 있는 계통의 창시 돌연변이 유전자: " + string.Join(" ", Enumerable.Range(0, G.N).Select(i => $"{G.Name[i]} {hg[i]}")));
          Console.WriteLine("g[EOX] 분포: " + string.Join(" ", pl.alive.Select(st => st.g[G.EOX]).Distinct().Take(12).Select(x => x.ToString("0.000")))); }
        Forms(pl); if (Environment.GetEnvironmentVariable("SHAPE") != null) Shapes(pl);
        if (Environment.GetEnvironmentVariable("SIZEPROBE") != null) SizeProbe(pl);
        if (Environment.GetEnvironmentVariable("NANO") != null) NanoProbe(pl);
        Diversity(pl); var (tA, tTL) = Trophic(pl); if (Environment.GetEnvironmentVariable("TOPPROBE") != null && tA != null) TopProbe(pl, tA, tTL);
        if (Environment.GetEnvironmentVariable("ATKPROBE") != null)   // 청소율 높이: 포식 계통마다 '청소 몸탄소(μg C/개체) 최대청소율(ml/mg C/h) 최대섭취(/일) 생물량 몸(μm)' — Kiørboe & Hirst 2014와 견줌
        {
            double all = pl.alive.Sum(o => o.total);
            foreach (var s in pl.alive.Where(s => s.t.phi[G.PHAGO] > 0.02 && s.total > 1e-6 * all))
                Console.WriteLine($"청소 {s.t.cellC * s.t.MeanCells * 12e6:0.000e0} {Planet.Attack(s.t, s.b) * 1000 / 12e-3 / 24:0.0} {Planet.Allo(s.t, s.b) / Cfg.HANDLE:0.00} {s.total / all:0.0e0} {s.b.size:0} | 막 {s.t.phi[G.PHAGO]:0.00} 운동 {s.t.Sat(G.MOT, 0.01):0.00} 노출 {s.b.exposed:0.00} 벽 {s.t.Sat(G.WALL, 0.02):0.00} 먹는 {s.b.feed:0} 세포 {s.t.MeanCells:0}");
        }
        if (Environment.GetEnvironmentVariable("EATCHK") != null)   // 진화가 쓰는 먹힘 예측(Eaten)과 틱에서 실제 먹힌 비율(eatenRate)이 같은지
        {
            var q = new List<double>();
            foreach (var s in pl.alive) for (int v = 0; v < pl.NV0; v++) { int o = s.slot * pl.V + v; if (pl.B[o] > 1e-6 && pl.eatenRate[o] > 1e-3) q.Add(pl.Eaten(s.t, s.b, v, s.slot) / pl.eatenRate[o]); }
            q.Sort(); if (q.Count > 0) Console.WriteLine($"먹힘 검사: {q.Count}칸 · 예측/실제 중앙 {q[q.Count / 2]:0.000} · 5–95 % {q[q.Count / 20]:0.000}–{q[q.Count * 19 / 20]:0.000}");
        }
        if (Environment.GetEnvironmentVariable("ENDSAVE") != null && csv != null) Save.Write(pl, csv + ".end.sav", 0);   // 끝 상태를 남겨 탐침을 다시 돌릴 때 처음부터 안 돌린다
        Console.WriteLine($"단계 시간(초): 하루 {pl.tStage[0]:0.0} · 기후 {pl.tStage[1]:0.0} · 생태 {pl.tStage[2]:0.0} · 물질 {pl.tStage[3]:0.0} · 진화 {pl.tStage[4]:0.0}");
        Console.WriteLine($"속도 {days / sw.Elapsed.TotalSeconds:0.0} 일/초, 종 {pl.species.Count}개 생김(살아 있음 {pl.nAliveSp}), 갈래 {pl.clades.Count}");
        return 0;
    }
    // 다세포 지형: 역할마다 가장 많은 종의 대표 계통을 그 계통이 가장 진한 칸에 두고, 붙음 유전자만 바꿔 덩이 세포 수를 1·2·4·…로 했을 때
    // 순증가율(먹이 섭취·먹힘·바이러스 포함, Fit 그대로)이 얼마나 바뀌는지. 가라앉는 속도(Fit 밖)도 같이 적는다.
    static void Land(Planet pl)
    {
        foreach (Role role in new[] { Role.광합성, Role.분해, Role.포식, Role.청소, Role.화학합성 })
        {
            Species sp = null; foreach (var s in pl.species) if (s.Alive && s.role == role && s.rep >= 0 && (sp == null || s.biomass > sp.biomass)) sp = s;
            if (sp == null) continue; var st = pl.strains[sp.rep]; if (st == null) continue;
            int v = 0; for (int u = 0; u < pl.V; u++) if (pl.B[st.slot * pl.V + u] > pl.B[st.slot * pl.V + v]) v = u;
            int si = st.slot * pl.V + v; double lim = pl.limA[si] > 0 ? pl.limA[si] : 1;
            var sb = new System.Text.StringBuilder($"    지형 {role} {sp.name} 칸 {v}{(v >= pl.NV0 ? "(열수공)" : $"(층 {v % Cfg.NY})")} 크기 {st.b.size:0.0} μm 세포 {st.t.MeanCells:0.0} 먹힘 {pl.Eaten(st.t, st.b, v, st.slot):0.000} |");
            double r0 = double.NaN;
            foreach (double nA in new[] { 1.0, 2, 4, 8, 32, 128 })
            {
                var g = (float[])st.g.Clone(); double others = 0;
                for (int i = 0; i < G.NA; i++) if (i != G.ADH) others += Math.Exp(Cfg.KAPPA * g[i]);
                double p = nA <= 1 ? Math.Min(st.t.phi[G.ADH], Cfg.ADH0 * 0.99) : Cfg.ADH0 * Math.Pow(nA, 1 / Cfg.ADH_EXP);
                double x = p * others / (1 - Cfg.PHI_Q - p), gv = Math.Log(x) / Cfg.KAPPA; if (gv > 1) break;
                g[G.ADH] = (float)Math.Max(0, gv); var t = Traits.Of(g); var b = Body.Of(g, t);
                double r = pl.Fit(t, b, v, st.slot, lim), eat = pl.Eaten(t, b, v, st.slot);
                double w = 2.0 / 9 * 50 * 9.81 * Math.Pow(b.size / 2 * 1e-6, 2) / 1e-3 * 86400 * (1 - t.Sat(G.MOT, 0.01)) * (t.nA > 1 ? 0.5 : 1);
                if (double.IsNaN(r0)) r0 = r;
                sb.Append($" {t.MeanCells:0}세포 {b.size:0}μm 겉 {b.exposed:0.00} Δr {r - r0:+0.000;-0.000} 먹힘 {eat:0.000} 침강 {w:0.0}m/일 |");
            }
            var g9 = (float[])st.g.Clone(); g9[G.ADH] = Math.Min(1, g9[G.ADH] + (float)Cfg.STEP_SHAPE); var t9 = Traits.Of(g9);
            var b9 = Body.Of(g9, t9); var e9 = pl.EnvAt(v); var q0 = Cell.Grow(st.t, st.b, e9, pl.Intake(st.t, st.b, v, st.slot), 0); var q1 = Cell.Grow(t9, b9, e9, pl.Intake(t9, b9, v, st.slot), 0);
            sb.Append($" 붙음 g {st.g[G.ADH]:0.000} φ {st.t.phi[G.ADH]:0.0000} 한 걸음 Δr {pl.Fit(t9, b9, v, st.slot, lim) - pl.Fit(st.t, st.b, v, st.slot, lim):+0.0000;-0.0000} (Δg {q1.g - q0.g:+0.0000;-0.0000} Δ죽음 {q1.mort - q0.mort:+0.0000;-0.0000} Δ먹힘 {pl.Eaten(t9, b9, v, st.slot) - pl.Eaten(st.t, st.b, v, st.slot):+0.0000;-0.0000} 세포 {st.t.MeanCells:0.00}→{t9.MeanCells:0.00} 크기 {st.b.size:0.00}→{b9.size:0.00})");
            Console.WriteLine(sb);
        }
        var a = pl.adhDbg; Console.WriteLine($"    붙음 돌연변이: 평가 {a[0]} 위로 {a[1]}(정착 {a[2]}, 평균 sc {(a[1] > 0 ? a[5] / a[1] : 0):+0.000;-0.000}) 아래로 {a[3]}(정착 {a[4]})"); System.Array.Clear(a);
        double tot = pl.alive.Sum(s => s.total), adhM = pl.alive.Where(s => Math.Abs(s.g[G.ADH] - 0.3f) > 1e-4).Sum(s => s.total), respM = pl.alive.Where(s => Math.Abs(s.g[G.RESP] - 0.6f) > 1e-4).Sum(s => s.total);
        var pd = pl.pruneDbg; Console.WriteLine($"    지운 계통 나이: 0일 {pd[0]} 1–9일 {pd[1]} 10일+ {pd[2]} | 산 계통 나이 중앙 {pl.alive.Select(s => (int)pl.Day - s.born).OrderBy(x => x).ElementAt(pl.alive.Count / 2)}일 | 붙음 바뀐 계통 생물량 {adhM / tot:P1} 호흡 바뀐 {respM / tot:P1}"); System.Array.Clear(pd);
        var ed = pl.evoDbg; Console.WriteLine($"    정착 {ed[0]} 중 r≤0 {ed[1]} | 지운 어린 계통 {ed[3]} 중 느는 중 {ed[2]} | 표류(누적) {pl.driftN} 중 몸 꼴 {pl.driftShape}"); System.Array.Clear(ed);
    }
    // 산소 적응 탐침: 층마다 O2와 오늘 탄소 흐름(고정·녹은 유기물 흡수·태움·그중 산소 호흡), 가장 많이 태우는 계통 다섯의 호흡·항산화 한 걸음 선택
    static void Aer(Planet pl)
    {
        int st = Cfg.SMAX * pl.V; var sb = new System.Text.StringBuilder("    산소 층:");
        for (int k = 0; k <= Cfg.NY; k++)
        {
            double o2 = 0, w = 0, fix = 0, doc = 0, cat = 0, aer = 0;
            for (int v = 0; v < pl.V; v++)
            {
                if (!pl.live[v] || (k < Cfg.NY ? v >= pl.NV0 || v % Cfg.NY != k : v < pl.NV0)) continue;
                o2 += pl.pool[P.O2][v] * pl.vol[v]; w += pl.vol[v];
                foreach (var s in pl.alive) { int o = s.slot * pl.V + v; fix += pl.inc[o] + pl.inc[st + o]; doc += pl.inc[4 * st + o]; cat += pl.inc[7 * st + o]; aer += pl.inc[6 * st + o]; }
            }
            sb.Append($" [{(k < Cfg.NY ? "층" + k : "열수공")} O2 {o2 / Math.Max(1, w):0.00}μM 고정 {fix:0.0e0} 흡수 {doc:0.0e0} 태움 {cat:0.0e0} 호흡 {(cat > 0 ? aer / cat : 0):P0}]");
        }
        Console.WriteLine(sb);
        foreach (var s in pl.alive.OrderByDescending(s => Enumerable.Range(0, pl.V).Sum(v => pl.inc[7 * st + s.slot * pl.V + v])).Take(5))
        {
            int v = 0; for (int u = 0; u < pl.V; u++) if (pl.inc[7 * st + s.slot * pl.V + u] > pl.inc[7 * st + s.slot * pl.V + v]) v = u;
            int si = s.slot * pl.V + v; double lim = pl.limA[si] > 0 ? pl.limA[si] : 1, r0 = pl.Fit(s.t, s.b, v, s.slot, lim);
            var e = pl.EnvAt(v); var rr = Cell.Grow(s.t, s.b, e, pl.Intake(s.t, s.b, v, s.slot), 0);
            var sb2 = new System.Text.StringBuilder($"    태움 {pl.species[s.species].name}({pl.species[s.species].role}) 칸 {v} O2 {e.O2:0.00} H2S {e.H2S:0.0} | 호흡 φ {s.t.phi[G.RESP]:0.000} 항산 φ {s.t.phi[G.SOD]:0.000} aer {rr.aerFrac:0.00} 태움 {rr.catab:0.000} 동화 {rr.assim:0.000} g {rr.g:0.000} 죽음 {rr.mort:0.000} r {r0:0.000} |");
            foreach (int gi in new[] { G.RESP, G.SOD, G.OSMO, G.PHOTO })
                foreach (double dd in new[] { Cfg.STEP, -Cfg.STEP })
                {
                    var g2 = (float[])s.g.Clone(); g2[gi] = (float)Math.Clamp(g2[gi] + dd, 0, 1); var t2 = Traits.Of(g2);
                    sb2.Append($" {G.Name[gi]}{(dd > 0 ? "+" : "−")} {pl.Fit(t2, Body.Of(g2, t2), v, s.slot, lim) - r0:+0.000;-0.000}");
                }
            Console.WriteLine(sb2);
        }
    }
    // 독점 탐침: 생물량 상위 계통 여섯 — 몫·덩이 세포 수·크기·수용체 칸, 가장 빽빽한 칸에서 숙주 밀도·바이러스 죽음·분열·먹힘·자원 몫·주 수입
    static void Dom(Planet pl)
    {
        double W = pl.alive.Sum(s => s.total), M = pl.alive.Where(s => s.t.MeanCells >= 8).Sum(s => s.total);
        var top = pl.alive.OrderByDescending(s => s.total).Take(6).ToList();
        Console.WriteLine($"    독점: 덩이 {M / Math.Max(1e-30, W):P0} · 덩이 종 {pl.alive.Where(s => s.t.MeanCells >= 8).Select(s => s.species).Distinct().Count()} · 덩이 수용체 칸 {pl.alive.Where(s => s.t.MeanCells >= 8).Select(s => s.t.vbin).Distinct().Count()}");
        foreach (var s in top)
        {
            int vb = 0; double bb = -1; for (int v = 0; v < pl.V; v++) if (pl.live[v] && pl.B[s.slot * pl.V + v] > bb) { bb = pl.B[s.slot * pl.V + v]; vb = v; }
            int o = s.slot * pl.V + vb; double hb = pl.binB[vb * 125 + s.t.vbin], vm = pl.VirusMort(s.t, s.b, vb);
            double ci = s.cFix + s.cPrey + s.cDet + s.cDoc; string role = ci <= 0 ? "-" : new[] { ("고정", s.cFix), ("포식", s.cPrey), ("청소", s.cDet), ("분해", s.cDoc) }.OrderByDescending(x => x.Item2).First().Item1;
            Console.WriteLine($"      종 {s.species} 몫 {s.total / W:P0} 세포 {s.t.MeanCells:0.0} 몸 {s.b.size:0} μm 칸 {vb}{(vb >= pl.NV0 ? "(열수공)" : $"(층{vb % Cfg.NY})")} 밀도 {bb:0.00} 수용체 {s.t.vbin} 숙주 {hb:0.00} → 바이러스 {vm:0.000} · 분열 {pl.grow[o]:0.000} 먹힘 {pl.eatenRate[o]:0.000} 자원몫 {pl.limA[o]:0.00} · {role}");
        }
    }
    // 광합성 탐침: 층마다 물의 공여체(H2·H2S·Fe2+)와 영양염, 빛 고정이 많은 계통 다섯의 광계 산화력·쓰는 공여체 몫·한 걸음 선택
    static void Photo(Planet pl)
    {
        int st = Cfg.SMAX * pl.V; var sb = new System.Text.StringBuilder("    광 층:");
        for (int k = 0; k < Cfg.NY; k++)
        {
            double w = 0, h2 = 0, hs = 0, fe = 0, po = 0, nh = 0, fx = 0, ox = 0, so = 0;
            for (int v = 0; v < pl.NV0; v++)
            {
                if (!pl.live[v] || v % Cfg.NY != k) continue; double vv = pl.vol[v]; w += vv;
                h2 += pl.pool[P.H2][v] * vv; hs += pl.pool[P.H2S][v] * vv; fe += pl.pool[P.FE2][v] * vv; po += pl.pool[P.PO4][v] * vv; nh += pl.pool[P.NH4][v] * vv; so += pl.pool[P.SO4][v] * vv;
                foreach (var s in pl.alive) { int o = s.slot * pl.V + v; fx += pl.inc[o]; ox += pl.inc[5 * st + o]; }
            }
            w = Math.Max(w, 1e-30);
            sb.Append($" [층{k} H2 {h2 / w:0.000} H2S {hs / w:0.00} Fe2+ {fe / w:0.00} SO4 {so / w:0} PO4 {po / w:0.000} NH4 {nh / w:0.00} 빛고정 {fx:0.0e0} 산소몫 {(fx > 0 ? ox / fx : 0):P0}]");
        }
        Console.WriteLine(sb);
        double[] E = { -0.41, -0.24, 0.20, 0.82 };
        foreach (var s in pl.alive.OrderByDescending(s => Enumerable.Range(0, pl.V).Sum(v => pl.inc[s.slot * pl.V + v])).Take(5))
        {
            int v = 0; for (int u = 0; u < pl.V; u++) if (pl.inc[s.slot * pl.V + u] > pl.inc[s.slot * pl.V + v]) v = u;
            int si = s.slot * pl.V + v; double lim = pl.limA[si] > 0 ? pl.limA[si] : 1, r0 = pl.Fit(s.t, s.b, v, s.slot, lim);
            var e = pl.EnvAt(v); double[] c = { e.H2, e.H2S, e.FE2, 1 }, K = { 0.1, 1, 5, 1 }; var a = new double[4]; double sum = 0;
            for (int q = 0; q < 4; q++) { a[q] = 1 / (1 + Math.Exp(-(s.t.eox - E[q] - 0.1) / 0.15)) * c[q] / (c[q] + K[q]); sum += a[q]; }
            sum = Math.Max(sum, 1e-12);
            var sb2 = new System.Text.StringBuilder($"    광 {pl.species[s.species].name}({pl.species[s.species].role}) 칸 {v} 크기 {s.b.size:0.0} 세포 {s.t.MeanCells:0.0} 산화력 {s.t.eox:0.00} V 공여체 H2 {a[0] / sum:P0} H2S {a[1] / sum:P0} Fe {a[2] / sum:P0} 물 {a[3] / sum:P0} | 광계 φ {s.t.phi[G.PHOTO]:0.000} r {r0:0.000} |");
            foreach (int gi in new[] { G.EOX, G.PHOTO, G.NUT, G.SOD })
                foreach (double dd in new[] { Cfg.STEP, -Cfg.STEP })
                {
                    var g2 = (float[])s.g.Clone(); g2[gi] = (float)Math.Clamp(g2[gi] + dd, 0, 1); var t2 = Traits.Of(g2);
                    sb2.Append($" {G.Name[gi]}{(dd > 0 ? "+" : "−")} {pl.Fit(t2, Body.Of(g2, t2), v, s.slot, lim) - r0:+0.000;-0.000}");
                }
            Console.WriteLine(sb2);
        }
    }
    // 저장 시험: 저장 → 불러오기 → 다시 저장한 파일이 바이트까지 같은지, 둘을 300일 더 나란히 돌려 매일 같은지
    static int SaveCheck(Planet pl, string path)
    {
        var sw = Stopwatch.StartNew(); Save.Write(pl, path, 1); double tw = sw.Elapsed.TotalSeconds;
        sw.Restart(); var q = Save.Read(path, out var h); double tr = sw.Elapsed.TotalSeconds;
        q.po.MaxDegreeOfParallelism = pl.po.MaxDegreeOfParallelism;
        Console.WriteLine($"저장 {tw:0.00} s · 불러오기 {tr:0.00} s · 파일 {new FileInfo(path).Length / 1e6:0.00} MB · {h.day}일 · 시드 {h.seed}");
        Save.Write(q, path + "2", 1);
        static byte[] Body(string f) { using var z = new System.IO.Compression.GZipStream(File.OpenRead(f), System.IO.Compression.CompressionMode.Decompress); var m = new MemoryStream(); z.CopyTo(m); return m.ToArray()[36..]; }   // 머리(시각) 36바이트 뺌
        Say("저장 되살림(다시 저장한 파일이 바이트까지 같음)", Body(path).AsSpan().SequenceEqual(Body(path + "2")), $"{Body(path).Length / 1e6:0.0} MB(압축 전)");
        int bad = -1;
        for (int d = 1; d <= 300 && bad < 0; d++)
        {
            for (int k = 0; k < Cfg.TPD; k++) { pl.Step(); q.Step(); }
            if (!pl.history[^1].Equals(q.history[^1]) || pl.atmO2 != q.atmO2 || pl.alive.Count != q.alive.Count || pl.events.Count != q.events.Count) bad = d;
        }
        Say("저장 되살림(300일 더 나란히 돌려 매일 같음)", bad < 0, bad < 0 ? $"{pl.Day:0}일까지 같음" : $"{bad}일째 다름: O2 {pl.O2PAL:0.0000e0} / {q.O2PAL:0.0000e0}, 종 {pl.nAliveSp} / {q.nAliveSp}");
        return bad < 0 ? 0 : 1;
    }

    // 사건 반응: 같은 상태(저장 파일)에서 사건 하나만 일으키고(저절로 나는 사건은 끔) 대조와 견준다. 인 공급·유기물 묻힘 손잡이도 같은 식으로.
    // 잡음 = 난수만 한 번 흔든 대조: 진화가 갈라져 생기는 차이의 크기. 이보다 작은 차이는 사건 효과라고 말할 수 없다
    static int Response(Planet pl, string path, double m)
    {
        Save.Write(pl, path, 1);
        int[] at = { 1, 10, 30, 100, 300, 1000, 1500 };
        var nm = new List<string> { "대조(사건 없음)", "잡음(난수만 흔듦)" }; nm.AddRange(Planet.KINDS.Select(k => k.name)); nm.Add("인 공급 ×3"); nm.Add("인 공급 ×10"); nm.Add("열수공 모두 ×0.3");
        var only = Environment.GetEnvironmentVariable("RCASES")?.Split(',').Select(int.Parse).ToHashSet();   // 고른 경우만(대조는 늘)
        int N = nm.Count, D = 1500; var T = new double[N][]; var O = new double[N][]; var F = new double[N][]; var Bm = new double[N][];
        int nk = Planet.KINDS.Length;
        for (int c = 0; c < N; c++)
        {
            if (c > 0 && only != null && !only.Contains(c)) continue;
            var q = Save.Read(path, out _); q.knobs.eventRate = 0; q.po.MaxDegreeOfParallelism = pl.po.MaxDegreeOfParallelism;
            if (c == 1) q.rng.U(); else if (c >= 2 && c < 2 + nk) q.Trigger(c - 2, m);
            else if (c == 2 + nk) q.knobs.phosphate = 3; else if (c == 3 + nk) q.knobs.phosphate = 10; else if (c == 4 + nk) for (int v = q.NV0; v < q.V; v++) q.ventMul[v] *= 0.3;
            T[c] = new double[D + 1]; O[c] = new double[D + 1]; F[c] = new double[D + 1]; Bm[c] = new double[D + 1];
            for (int d = 1; d <= D; d++)
            {
                for (int i = 0; i < Cfg.TPD; i++) q.Step();
                var h = q.history[q.history.Count - 1]; T[c][d] = q.Tglobal; O[c][d] = q.O2PAL; F[c][d] = h.photo; Bm[c][d] = h.bio;
            }
            double Mean(double[] x, int a, int b) { double z = 0; for (int d = a; d <= b; d++) z += x[d]; return z / (b - a + 1); }
            string R(double[][] x, int a, int b) => $"×{Mean(x[c], a, b) / Math.Max(1e-30, Mean(x[0], a, b)):0.00}";
            Console.WriteLine($"{nm[c],-12} 크기 {m}: 기온 " + (c == 0 ? string.Join(" · ", at.Select(d => $"{d}일 {T[0][d]:0.0}")) + " °C"
                : string.Join(" · ", at.Select(d => $"{d}일 {T[c][d] - T[0][d]:+0.0;-0.0;0.0}")) + $" | 평균 기온차 {Mean(T[c], 1, D) - Mean(T[0], 1, D):+0.0;-0.0;0.0} °C · 대조 대비(1–100일 / 1–1500일) 산소 {R(O, 1, 100)} / {R(O, 1, D)} · 빛 고정 {R(F, 1, 100)} / {R(F, 1, D)} · 생물량 {R(Bm, 1, 100)} / {R(Bm, 1, D)}"));
        }
        return 0;
    }

    // 몸 꼴 손익(SHAPE, =pred면 포식 덩이만): 생물량 큰 덩이 종 셋을 제 칸에서 가지·각 유전자만 바꿔 다시 잰다. 실이 아닌 꼴이 왜 지는지(나눔·가라앉기·바이러스·먹힘)
    static void Shapes(Planet pl)
    {
        bool pred = Environment.GetEnvironmentVariable("SHAPE") == "pred";   // pred = 포식 막 몫 0.1 넘는 덩이만(동물형 몸이 왜 실인지)
        foreach (var st in pl.alive.Where(s => s.t.MeanCells >= 2 && (!pred || s.t.phi[G.PHAGO] >= 0.1)).OrderByDescending(s => s.total).Take(3))
        {
            int vb = Enumerable.Range(0, pl.V).OrderByDescending(v => pl.B[st.slot * pl.V + v]).First(), si = st.slot * pl.V + vb; double lim = pl.limA[si] > 0 ? pl.limA[si] : 1;
            Console.WriteLine($"꼴 손익: 종 {st.species} 칸 {vb} 세포 {st.t.MeanCells:0.0} 가지 {st.g[G.BRANCH]:0.00} 각 {st.g[G.ANGLE]:0.00} 자원몫 {lim:0.00} 분화 {st.t.diff:0.00} 포식막 {st.t.phi[G.PHAGO]:0.00} 편모 {st.t.phi[G.MOT]:0.00} 크기 {st.b.size:0} μm");
            foreach (float br in new[] { 0.3f, 0.6f, 0.9f, 1f }) foreach (float an in new[] { 0.05f, 0.2f, 0.5f })
            {
                var g = (float[])st.g.Clone(); g[G.BRANCH] = br; g[G.ANGLE] = an; var t = Traits.Of(g); var b = Body.Of(g, t);
                var e = pl.EnvAt(vb); double vm = pl.VirusMort(t, b, vb); var r = Cell.Grow(t, b, e, pl.Intake(t, b, vb, st.slot), vm);
                double rr = t.dCell / 2 * 1e-6, w = 2.0 / 9 * 50 * 9.81 * rr * rr / 1e-3 * 86400 / (t.elong * b.share) * (1 - t.Sat(G.MOT, 0.01));
                Console.WriteLine($"   가지 {br:0.0} 각 {an:0.00} | 차원 {b.dim:0.0} 나눔 {b.share:0.00} 바깥 {b.exposed:0.00} 크기 {b.size:0} μm 먹는 {b.feed:0} | 가라앉기 {w:0.00} m/일 | 자람 {r.g:+0.000;-0.000} 죽음 {r.mort:0.000}(바이러스 {vm:0.000}) 먹힘 {pl.Eaten(t, b, vb, st.slot):0.000} → 적합 {pl.Fit(t, b, vb, st.slot, lim):+0.000;-0.000}");
            }
        }
    }

    // 몸 꼴 분포(살아 있는 종, 생물량 가중): 단세포 구균·막대·긴 막대, 덩이 실·판·덩어리. 모양 유전자(길쭉함·가지·각) 범위
    static void Forms(Planet pl)
    {
        var w = new double[7]; var sh = new double[7]; string[] nm = { "단세포 공 a<1.3", "단세포 막대 1.3–3", "단세포 긴 막대 >3", "작은 덩이 2–7세포", "덩이 실 차원<1.5", "덩이 판 1.5–2.3", "덩이 덩어리 >2.3" };
        float aMin = 9, aMax = -9, bMin = 9, bMax = -9, nMin = 9, nMax = -9; double tot = 0;
        foreach (var sp in pl.species)
        {
            if (!sp.Alive || sp.rep < 0 || pl.strains[sp.rep] == null) continue; var st = pl.strains[sp.rep]; double m = Math.Max(0, sp.biomass); tot += m;
            int k = st.t.MeanCells < 2 ? (st.t.aspect < 1.3 ? 0 : st.t.aspect < 3 ? 1 : 2) : st.t.MeanCells < 8 ? 3 : (st.b.dim < 1.5 ? 4 : st.b.dim < 2.3 ? 5 : 6);
            w[k] += m; sh[k] += m * st.b.share; aMin = Math.Min(aMin, st.g[G.ASPECT]); aMax = Math.Max(aMax, st.g[G.ASPECT]); bMin = Math.Min(bMin, st.g[G.BRANCH]); bMax = Math.Max(bMax, st.g[G.BRANCH]); nMin = Math.Min(nMin, st.g[G.ANGLE]); nMax = Math.Max(nMax, st.g[G.ANGLE]);
        }
        Console.WriteLine("몸 꼴(생물량 몫): " + string.Join(" · ", Enumerable.Range(0, 7).Select(k => $"{nm[k]} {w[k] / Math.Max(1e-30, tot):P0}" + (w[k] > 0 ? $"(나눔 {sh[k] / w[k]:0.00})" : ""))) + $" | 유전자 범위 길쭉함 {aMin:0.00}–{aMax:0.00} 가지 {bMin:0.00}–{bMax:0.00} 각 {nMin:0.00}–{nMax:0.00}");
    }

    // 다양성 지표(끝날 때, 살아 있는 종의 대표 계통): 종 수, 실효 종 수 exp(섀넌, 생물량 몫), 형태 불일치 = 몸 모양 좌표 4개(log2 크기·log2 세포 수·차원·겉 몫)의
    // 분산 합(Foote 1993·Wills 1994의 '분산 합'), 크기 폭(log10 최대/최소), 하는 일 가짓수(생물량 1 % 넘는 역할). 8세포 밑은 차원을 1로 둔다(그 밑 차원은 이름표일 뿐)
    // 몸 크기 탐침: 생물량 많은 계통 6개가 가장 많은 칸에서 붙음·세포 크기 유전자를 몇 걸음 바꾼 몸이 얼마나 더(덜) 사는가.
    // 적합도(/일) = 성장 − 죽음 − 먹힘(진화가 견주는 값). 가라앉음(/일) = Stokes 속도 ÷ 칸 두께(04와 같은 식). 그 칸 산소 그대로와 1 PAL로 따로
    // 나노 탐침(NANO=1): 생물량 많은 광합성·분해 계통을 홀세포로 두고 세포 지름만 0.7–12 μm로 바꿔 g·m(바이러스)·e·f를 잰다(1–10 μm 몸이 왜 없는가)
    static void NanoProbe(Planet pl)
    {
        Console.WriteLine("나노 탐침: 홀세포 지름별 g 성장 · m 죽음(바이러스) · e 먹힘 · s 섭취/최대 · f 적합도(/일)");
        foreach (var role in new[] { Role.광합성, Role.분해, Role.포식 })
            foreach (var st in pl.alive.Where(s => s.species >= 0 && pl.species[s.species].role == role).OrderByDescending(s => s.total).Take(2))
            {
                int v = Enumerable.Range(0, pl.NV0).OrderByDescending(x => pl.B[st.slot * pl.V + x]).First();
                var sb = new System.Text.StringBuilder($"  {role} 계통 {st.id} 세포 {st.t.MeanCells:0.#}개 {st.t.dCell:0.0} μm 칸 {v}");
                foreach (double d in new[] { 0.7, 1.5, 3, 6, 12 })
                {
                    var g2 = (float[])st.g.Clone(); g2[G.ADH] = 0; g2[G.SIZE] = (float)(Math.Log(d / 0.5) / Math.Log(200));
                    var t = Traits.Of(g2); var b = Body.Of(g2, t, false); double vm = pl.VirusMort(t, b, v), e = pl.Eaten(t, b, v, st.slot), inn = pl.Intake(t, b, v, st.slot);
                    var r = Cell.Grow(t, b, pl.EnvAt(v), inn, vm);
                    sb.Append($" | {d} μm g {r.g:0.000} m {r.mort:0.000}({vm:0.000}) e {e:0.000} s {inn * Cfg.HANDLE / Planet.Allo(t, b) / (1 - Cfg.EGEST):0.00} f {r.g - r.mort - e:+0.000;-0.000}");
                }
                Console.WriteLine(sb);
            }
    }

    static void SizeProbe(Planet pl)
    {
        Console.WriteLine("몸 크기 탐침(Δ = 바꾼 몸 − 지금 몸, /일): g 성장 · m 죽음 · e 먹힘 · f 적합도 · w 가라앉음");
        (string, int, double)[] vars = { ("붙음+2", G.ADH, 2), ("붙음+6", G.ADH, 6), ("붙음−2", G.ADH, -2), ("크기+2", G.SIZE, 2), ("크기−2", G.SIZE, -2), ("분화+2", G.DIFF, 2), ("포식막+2", G.PHAGO, 2), ("벽+2", G.WALL, 2), ("편모+2", G.MOT, 2) };
        var top = pl.alive.OrderByDescending(s => s.total).ToList();   // 생물량 많은 4개 + 포식 계통 많은 3개
        foreach (var st in top.Take(4).Concat(top.Where(s => s.species >= 0 && pl.species[s.species].role == Role.포식).Take(3)).Distinct())
        {
            int v = Enumerable.Range(0, pl.NV0).OrderByDescending(x => pl.B[st.slot * pl.V + x]).First(); double o0 = pl.pool[P.O2][v];
            (double g, double m, double e, double f, double w, double sz, double n) Of(float[] g)
            {
                var t = Traits.Of(g); var b = Body.Of(g, t, false); var r = Cell.Grow(t, b, pl.EnvAt(v), pl.Intake(t, b, v, st.slot), pl.VirusMort(t, b, v)); double e = pl.Eaten(t, b, v, st.slot);
                double rr = t.dCell / 2 * 1e-6, w = 2.0 / 9 * 50 * 9.81 * rr * rr / 1.0e-3 * 86400 / (t.elong * b.share) * (1 - t.Sat(G.MOT, 0.01)) / pl.thick[v];
                return (r.g, r.mort, e, r.g - r.mort - e, w, b.size, t.MeanCells);
            }
            foreach (double o2 in new[] { o0, Cfg.O2_SAT })
            {
                pl.pool[P.O2][v] = o2; var z = Of(st.g);
                var sb = new System.Text.StringBuilder($"  계통 {st.id} {(st.species >= 0 ? pl.species[st.species].role.ToString() : "?")} 세포 {z.n:0.#} 크기 {z.sz:0} μm · 칸 {v} O2 {o2:0.0} μM | g {z.g:0.000} m {z.m:0.000} e {z.e:0.000} f {z.f:0.000} w {z.w:0.000} (m 중 바이러스 {pl.VirusMort(st.t, st.b, v):0.000}[수용체 칸 숙주 중 같은 종 {pl.alive.Where(a => a.t.vbin == st.t.vbin && a.species == st.species).Sum(a => pl.B[a.slot * pl.V + v]) / Math.Max(1e-30, pl.binB[v * 125 + st.t.vbin]):P0}] · 시계 {Cell.Clock(st.t, st.b):0.00} · 분화 {st.t.diff:0.00} · 섭취/최대 {pl.Intake(st.t, st.b, v, st.slot) * Cfg.HANDLE / Planet.Allo(st.t, st.b) / (1 - Cfg.EGEST):0.00})");
                foreach (var (nm, gi, k) in vars)
                {
                    var g2 = (float[])st.g.Clone(); g2[gi] = (float)Math.Clamp(g2[gi] + k * Cfg.STEP, 0, 1); var y = Of(g2);
                    sb.Append($" | {nm}({y.n:0.#}세포 {y.sz:0}μm) f{y.f - z.f:+0.000;-0.000}(g{y.g - z.g:+0.000;-0.000} m{y.m - z.m:+0.000;-0.000} e{y.e - z.e:+0.000;-0.000}{(Math.Abs(y.w - z.w) > 0.002 ? $" w{y.w - z.w:+0.000;-0.000}" : "")})");
                }
                Console.WriteLine(sb);
            }
            pl.pool[P.O2][v] = o0;
        }
        // 빈자리: 먹히지 않는 큰 덩이(먹힘 0)가 사는 칸에 포식 계통을 데려가 몸·먹이 크기를 바꾸면 살 수 있나(적합도 > 0). 가장 좋은 변이 셋
        var big = top.Where(s => s.t.MeanCells >= 8).Take(2).ToList();
        foreach (var c in big)
        {
            int v = Enumerable.Range(0, pl.NV0).OrderByDescending(x => pl.B[c.slot * pl.V + x]).First();
            foreach (var pr in top.Where(s => s.species >= 0 && pl.species[s.species].role == Role.포식).Take(3))
            {
                var res = new List<(double f, string nm)>();
                for (int ds = 0; ds <= 8; ds += 2) for (int dl = 0; dl >= -6; dl -= 2) for (int dd = 0; dd <= 8; dd += 4) for (int da = 0; da <= 4; da += 4)
                {
                    var g2 = (float[])pr.g.Clone(); g2[G.SIZE] = (float)Math.Clamp(g2[G.SIZE] + ds * Cfg.STEP, 0, 1); g2[G.PREY] = (float)Math.Clamp(g2[G.PREY] + dl * Cfg.STEP, 0, 1);
                    g2[G.DIFF] = (float)Math.Clamp(g2[G.DIFF] + dd * Cfg.STEP, 0, 1); g2[G.ADH] = (float)Math.Clamp(g2[G.ADH] + da * Cfg.STEP, 0, 1);
                    var t = Traits.Of(g2); var b = Body.Of(g2, t, false); double I = pl.Intake(t, b, v, pr.slot); var r = Cell.Grow(t, b, pl.EnvAt(v), I, pl.VirusMort(t, b, v));
                    res.Add((r.g - r.mort - pl.Eaten(t, b, v, pr.slot), $"크기{ds:+0;-0} 먹이비{dl:+0;-0} 분화{dd:+0} 붙음{da:+0} → 몸 {b.size:0} μm·먹는 크기 {b.feed:0} μm·섭취 {I:0.00}·g {r.g:0.000}"));
                }
                if (Environment.GetEnvironmentVariable("SIZEPROBE") == "2") foreach (var x in res.Where(x => x.nm.Contains("먹이비-4"))) Console.WriteLine($"    f {x.f:+0.000;-0.000} {x.nm}");
                Console.WriteLine($"  빈자리 칸 {v}(덩이 {c.id} {c.b.size:0} μm, 먹힘 {pl.Eaten(c.t, c.b, v, c.slot):0.000}) ← 포식 {pr.id}({pr.b.size:0} μm, 먹는 크기 {pr.b.feed:0}): " + string.Join(" | ", res.OrderByDescending(x => x.f).Take(3).Select(x => $"f {x.f:+0.000;-0.000} {x.nm}")) + $" | 그대로 f {res[0].f:+0.000;-0.000}");
            }
        }
    }

    // 영양 단계(먹이 사슬 길이): TL = 1 + Σ 먹이 몫 × 그 먹이의 TL. 스스로 고정한 탄소는 먹이가 아니다(광합성·화학합성 = 1).
    // 먹이 몫은 칸마다 07 Intake와 같은 짝 무게(공격 × 선호 × 약함 × 생물량)로 나누고, 녹은 유기물·사체는 TL 1로 친다(분해·청소 = 2)
    static (List<Strain>, double[]) Trophic(Planet pl)
    {
        var A = pl.alive.Where(s => s.total > 0).ToList(); int n = A.Count; if (n == 0) return (null, null);
        var auto = new double[n]; var dead = new double[n]; var F = new double[n, n]; var loc = new List<(int j, int v, double mj, double au, double de, double eat, double[] w, double tot)>();   // 칸별 영양 단계(산소 줄)
        for (int j = 0; j < n; j++)
        {
            var sj = A[j]; double a = Planet.Attack(sj.t, sj.b), lf = Math.Log10(sj.b.feed);
            for (int v = 0; v < pl.NV0; v++)
            {
                double bj = pl.B[sj.slot * pl.V + v]; if (bj <= Cfg.BMIN) continue; double mj = bj * pl.vol[v];   // 칸 몫은 mol로(칸 부피가 다르다)
                var r = Cell.Grow(sj.t, sj.b, pl.EnvAt(v), pl.Intake(sj.t, sj.b, v, sj.slot), pl.VirusMort(sj.t, sj.b, v));
                auto[j] += r.fix * mj; dead[j] += r.docUp * mj; if (r.phagoUsed <= 0 || a <= 0) { loc.Add((j, v, mj, r.fix * mj, r.docUp * mj, 0, null, 1)); continue; }
                var w = new double[n]; double wd = 0, tot = 0;
                for (int i = 0; i < n; i++)
                {
                    var si = A[i]; if (i == j || si.species == sj.species && si.species >= 0 || si.b.size > Cfg.PREY_MAX * sj.b.feed) continue;
                    double x = (si.lsz - lf + sj.t.lr) / Cfg.PREY_SD; if (x * x <= 9) tot += w[i] = Math.Exp(-0.5 * x * x) * si.vul * pl.B[si.slot * pl.V + v];
                }
                for (int k = 0; k < 3; k++) tot += Planet.Pref(Cfg.POC_SIZE[k], sj.b.feed, sj.t.lr) * pl.pool[P.POC0 + k][v];
                if (tot <= 0) { loc.Add((j, v, mj, r.fix * mj, r.docUp * mj, 0, null, 1)); continue; } wd = tot; for (int i = 0; i < n; i++) wd -= w[i];
                double eat = r.phagoUsed * mj; dead[j] += eat * wd / tot; for (int i = 0; i < n; i++) F[j, i] += eat * w[i] / tot;
                loc.Add((j, v, mj, r.fix * mj, r.docUp * mj + eat * wd / tot, eat, w, tot));
            }
        }
        var TL = Enumerable.Repeat(1.0, n).ToArray();
        for (int it = 0; it < 40; it++)
            for (int j = 0; j < n; j++)
            {
                double inc = auto[j] + dead[j], fed = dead[j]; for (int i = 0; i < n; i++) { inc += F[j, i]; fed += F[j, i] * TL[i]; }
                TL[j] = inc > 0 ? 1 + fed / inc : 1;
            }
        double bt = A.Sum(s => s.total), mean = Enumerable.Range(0, n).Sum(j => A[j].total * TL[j]) / bt;
        var big = Enumerable.Range(0, n).Where(j => A[j].total >= 1e-3 * bt).ToList(); int top = big.OrderByDescending(j => TL[j]).First();
        double sh25 = big.Where(j => TL[j] >= 2.5).Sum(j => A[j].total) / bt;
        Console.WriteLine($"먹이 사슬: 평균 영양 단계 {mean:0.00} · 가장 높은 단계 {TL[top]:0.00}(계통 {A[top].id}, {A[top].b.size:0} μm, 세포 {A[top].t.MeanCells:0.#}) · 2.5 넘는 생물량 {sh25:P1} · 3 넘는 계통 {big.Count(j => TL[j] >= 3)}");
        // 크기 스펙트럼: 몸 크기 반 자리(0.5 decade)마다 생물량 몫·평균 영양 단계·스스로 만든 탄소 몫(광합성·화학합성)
        var sb = new System.Text.StringBuilder("크기 스펙트럼(몸 μm: 몫 단계 자급):");
        for (double lo = -0.5; lo < 3.5; lo += 0.5)
        {
            var ix = Enumerable.Range(0, n).Where(j => Math.Log10(A[j].b.size) >= lo && Math.Log10(A[j].b.size) < lo + 0.5).ToList(); double bb = ix.Sum(j => A[j].total); if (bb <= 0) continue;
            double au = ix.Sum(j => { double inc = auto[j] + dead[j]; for (int i = 0; i < n; i++) inc += F[j, i]; return inc > 0 ? A[j].total * auto[j] / inc : 0; });
            sb.Append($" {Math.Pow(10, lo):0.#}–{Math.Pow(10, lo + 0.5):0.#}: {bb / bt:P0} {ix.Sum(j => A[j].total * TL[j]) / bb:0.00} {au / bb:P0} 벽 {ix.Sum(j => A[j].total * A[j].t.Sat(G.WALL, 0.02)) / bb:0.00} ·");
        }
        Console.WriteLine(sb.ToString().TrimEnd('·', ' '));
        // 산소와 먹이 사슬: 칸 O2 구간마다 그 칸에서 먹은 것으로 잰 영양 단계. 무산소 먹이 사슬은 짧고(성장 효율 낮음, Fenchel & Finlay 1990),
        // 저산소에선 육식이 준다(Sperling 2013 PNAS 110:13446 — 산소 최소층 다모류). 남의 것 먹는 생물량(TL ≥ 1.5) 중 TL 2.5 넘는 몫 = 육식 몫
        var ob = new System.Text.StringBuilder("산소와 먹이 사슬(칸 O2 μM: 생물량 몫 · 평균 단계 · 소비자 단계 · 육식 몫 · 최고):"); double[] edge = { 0, 1, 10, 50, 1e9 }; double btot = loc.Sum(x => x.mj);
        for (int c = 0; c < 4; c++)
        {
            var L = loc.Where(x => pl.pool[P.O2][x.v] >= edge[c] && pl.pool[P.O2][x.v] < edge[c + 1]).Select(x =>
            {
                double inc = x.au + x.de + (x.w == null ? 0 : x.eat * (1 - (x.tot - x.w.Sum()) / x.tot)), fed = x.de; if (x.w != null) for (int i = 0; i < n; i++) fed += x.eat * x.w[i] / x.tot * TL[i];
                return (m: x.mj, tl: inc > 0 ? 1 + fed / inc : 1);
            }).ToList();
            double m = L.Sum(x => x.m); if (m <= 0) continue; var het = L.Where(x => x.tl >= 1.5).ToList(); double mh = het.Sum(x => x.m);
            ob.Append($" {edge[c]:0}–{(c == 3 ? "" : edge[c + 1].ToString("0"))}: {m / btot:P0} · {L.Sum(x => x.m * x.tl) / m:0.00} · {(mh > 0 ? (het.Sum(x => x.m * x.tl) / mh).ToString("0.00") : "-")} · {(mh > 0 ? het.Where(x => x.tl >= 2.5).Sum(x => x.m) / mh : 0):P0} · {L.Max(x => x.tl):0.00} |");
        }
        Console.WriteLine(ob.ToString().TrimEnd('|', ' '));
        return (A, TL);
    }

    // 윗 포식자 빈자리: 많은 포식자(종 역할 포식) 둘이 사는 칸에 포식 계통 셋을 데려가 크기·먹이비·분화·붙음을 바꾸면 그 포식자를 먹고(먹이 중 포식자 몫 ≥ 1/2) 살 수 있나
    static void TopProbe(Planet pl, List<Strain> A, double[] TL)
    {
        var hi = new HashSet<Strain>(A.Where(s => s.species >= 0 && pl.species[s.species].role == Role.포식)); var preds = hi.OrderByDescending(s => s.total).Take(3).ToList();   // TL ≥ 1.8로 고르면 녹은 유기물을 먹는 분해자(TL 2)가 섞였다
        foreach (var c in preds.Take(2))
        {
            int v = Enumerable.Range(0, pl.NV0).OrderByDescending(x => pl.B[c.slot * pl.V + x]).First();
            foreach (var pr in preds)
            {
                var res = new List<(double f, double sh, string nm)>();
                for (int ds = 0; ds <= 12; ds += 2) for (int dl = 0; dl >= -6; dl -= 2) for (int dd = 0; dd <= 8; dd += 4) for (int da = 0; da <= 8; da += 4)
                {
                    var g2 = (float[])pr.g.Clone(); g2[G.SIZE] = (float)Math.Clamp(g2[G.SIZE] + ds * Cfg.STEP, 0, 1); g2[G.PREY] = (float)Math.Clamp(g2[G.PREY] + dl * Cfg.STEP, 0, 1);
                    g2[G.DIFF] = (float)Math.Clamp(g2[G.DIFF] + dd * Cfg.STEP, 0, 1); g2[G.ADH] = (float)Math.Clamp(g2[G.ADH] + da * Cfg.STEP, 0, 1);
                    var t = Traits.Of(g2); var b = Body.Of(g2, t, false); double I = pl.Intake(t, b, v, pr.slot); double vm = pl.VirusMort(t, b, v), ea = pl.Eaten(t, b, v, pr.slot); var r = Cell.Grow(t, b, pl.EnvAt(v), I, vm);
                    double lf = Math.Log10(b.feed), wp = 0, tot = 0;
                    foreach (var si in A)
                    {
                        if (si == pr || si.species == pr.species && si.species >= 0 || si.b.size > Cfg.PREY_MAX * b.feed) continue;
                        double x = (si.lsz - lf + t.lr) / Cfg.PREY_SD; if (x * x > 9) continue; double w = Math.Exp(-0.5 * x * x) * si.vul * pl.B[si.slot * pl.V + v]; tot += w; if (hi.Contains(si)) wp += w;
                    }
                    for (int k = 0; k < 3; k++) tot += Planet.Pref(Cfg.POC_SIZE[k], b.feed, t.lr) * pl.pool[P.POC0 + k][v];
                    res.Add((r.g - r.mort - ea, tot > 0 && I * wp / tot >= 0.02 ? wp / tot : 0,   // 포식자 몫은 포식자에서 하루 몸의 2 % 넘게 먹을 때만 센다(섭취 0인데 무게만 큰 변이를 빼려고)
                         $"크기{ds:+0} 먹이비{dl:+0;-0} 분화{dd:+0} 붙음{da:+0} → 몸 {b.size:0} μm·먹는 {b.feed:0} μm·섭취 {I:0.00}(최대의 {I * Cfg.HANDLE / Planet.Allo(t, b) / (1 - Cfg.EGEST):0.00})·g {r.g:0.000} m {r.mort:0.000}(바이러스 {vm:0.000}) e {ea:0.000}"));
                }
                if (Environment.GetEnvironmentVariable("TOPPROBE") == "2") foreach (var x in res.Where(x => x.nm.Contains("먹이비+0 분화+0 붙음+0") || x.nm.Contains("먹이비+0 분화+4 붙음+0"))) Console.WriteLine($"    f {x.f:+0.000;-0.000} 몫 {x.sh:P0} {x.nm}");
                var eatP = res.Where(x => x.sh >= 0.5).OrderByDescending(x => x.f).Take(2).ToList();
                Console.WriteLine($"  윗 포식 칸 {v}(포식자 {c.id} {c.b.size:0} μm, 생물 {pl.B[c.slot * pl.V + v]:0.0e0} μM) ← {pr.id}({pr.b.size:0} μm, 그대로 f {res[0].f:+0.000;-0.000}): 포식자를 먹는 변이 {res.Count(x => x.sh >= 0.5)}개, f>0 {res.Count(x => x.sh >= 0.5 && x.f > 0)}개 | "
                    + string.Join(" | ", eatP.Select(x => $"f {x.f:+0.000;-0.000} 몫 {x.sh:P0} {x.nm}")));
            }
        }
    }

    static void Diversity(Planet pl)
    {
        var L = pl.species.Where(sp => sp.Alive && sp.rep >= 0 && pl.strains[sp.rep] != null && sp.biomass > 0).ToList(); if (L.Count == 0) { Console.WriteLine("다양성: 산 종 없음"); return; }
        double tot = L.Sum(sp => sp.biomass), H = -L.Sum(sp => sp.biomass / tot * Math.Log(sp.biomass / tot));
        var X = L.Select(sp => { var st = pl.strains[sp.rep]; return new[] { Math.Log2(st.b.size), Math.Log2(st.t.MeanCells), st.b.cells.Length >= 8 ? st.b.dim : 1, st.b.exposed }; }).ToList();
        double disp = 0; for (int k = 0; k < 4; k++) { double m = X.Average(x => x[k]); disp += X.Average(x => (x[k] - m) * (x[k] - m)); }
        double sMax = L.Max(sp => pl.strains[sp.rep].b.size), sMin = L.Min(sp => pl.strains[sp.rep].b.size);
        var roles = L.GroupBy(sp => sp.role).Where(g => g.Sum(sp => sp.biomass) > 0.01 * tot).Select(g => g.Key.ToString()).ToList();
        Console.WriteLine($"다양성: 종 {L.Count} · 실효 종 수 {Math.Exp(H):0.0} · 형태 불일치(분산 합) {disp:0.00} · 크기 폭 {Math.Log10(sMax / sMin):0.0}자리({sMin:0.#}–{sMax:0.#} μm) · 하는 일 {roles.Count}가지({string.Join("·", roles)}) · 표류 {pl.driftN}(몸 꼴 {pl.driftShape}) · 분화 평균 {Dif(L)}(포식 {Dif(L.Where(sp => sp.role == Role.포식).ToList())})");
        string Dif(List<Species> S) { double w = S.Sum(sp => sp.biomass); return w > 0 ? $"{S.Sum(sp => sp.biomass * pl.strains[sp.rep].t.diff) / w:0.00}" : "-"; }   // 생물량 가중
    }

    static void Bench(Planet pl)
    {
        var rng = new Evo.Rng(7); var sw = new Stopwatch(); double tT = 0, tB = 0, tI = 0, tE = 0, tF = 0; int n = 20000, multi = 0; double cells = 0;
        for (int k = 0; k < n; k++)
        {
            var st = pl.alive[(int)(rng.U() * pl.alive.Count)]; int v = Enumerable.Range(0, pl.V).OrderByDescending(x => pl.B[st.slot * pl.V + x]).First();
            var g = Genome.Mutate(st.g, ref rng, out _);
            sw.Restart(); var t = Traits.Of(g); tT += sw.Elapsed.TotalMilliseconds;
            sw.Restart(); var b = Body.Of(g, t); tB += sw.Elapsed.TotalMilliseconds; cells += b.cells.Length; if (b.cells.Length > 1) multi++;
            sw.Restart(); double I = pl.Intake(t, b, v, st.slot); tI += sw.Elapsed.TotalMilliseconds;
            sw.Restart(); double E = pl.Eaten(t, b, v, st.slot); tE += sw.Elapsed.TotalMilliseconds;
            sw.Restart(); pl.Fit(t, b, v, st.slot, 1); tF += sw.Elapsed.TotalMilliseconds;
        }
        Console.WriteLine($"후보 하나(μs, 한 스레드): 형질 {tT / n * 1e3:0.0} · 몸 {tB / n * 1e3:0.0}(배치 세포 평균 {cells / n:0}, 덩이 {multi * 100 / n}%) · 섭취 {tI / n * 1e3:0.0} · 먹힘 {tE / n * 1e3:0.0} · Fit 전체 {tF / n * 1e3:0.0} | 산 계통 {pl.alive.Count}");
    }

    static void Say(string what, bool ok, string detail) => Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what} — {detail}");
    // 몸 속 산소(05 OxicFrac) 확인: 지름 10 μm 세포로 된 공을 키우며 산소가 가운데까지 닿는 가장 큰 몸을 찾고, 풀이가 식을 만족하는지 본다.
    // 동물 조직(몸 탄소당 하루 0.01 호흡)이면 10 % PAL(25 μM)에서 지름 1 mm 안팎이어야 한다(Payne 2010: 원통 두께 1 mm)
    static int OxCal(Planet pl)
    {
        var t = Traits.Of(pl.alive[0].g); t.dCell = 10; t.aspect = 1; t.cellC = 0.216e-12 * Math.Pow(Math.PI / 6 * 1000, 0.939) / 12;
        double Crit(double o2, double need)   // 산소가 다 닿는 가장 큰 공 지름(mm): 겉 세포 비율을 줄여 가며 찾는다
        {
            double lo = 1e-9, hi = 1;   // exposed
            for (int i = 0; i < 60; i++) { double m = Math.Sqrt(lo * hi); if (Cell.OxicFrac(t, new Body { exposed = m }, o2, need) < 1) lo = m; else hi = m; }
            double c = Math.Cbrt(1 - hi); return 2 * 5e-3 * (1 + c) / (1 - c);
        }
        bool ok = true;
        foreach (var (o2, need, nm) in new[] { (25.0, 0.01, "조직 0.01/일, 10 % PAL"), (250.0, 0.01, "조직 0.01/일, 1 PAL"), (25.0, 1.0, "미생물 1/일, 10 % PAL"), (2.5, 1.0, "미생물 1/일, 1 % PAL") })
            Console.WriteLine($"산소가 가운데까지 닿는 가장 큰 공 지름 — {nm}: {Crit(o2, need):0.000} mm");
        double d = Crit(25, 0.01); ok &= d > 0.5 && d < 2;
        ok &= Math.Abs(Crit(100, 0.01) / d - 2) < 0.01;   // 지름 ∝ √O2
        for (double ex = 0.001; ex < 1; ex *= 1.5)          // 핵 반지름 풀이: 1 − 3x² + 2x³ = s, 몫은 몸이 클수록 줄어든다
        {
            double f = Cell.OxicFrac(t, new Body { exposed = ex }, 25, 0.01), x = Math.Cbrt(1 - f);
            if (f < 1) { double c = Math.Cbrt(1 - ex), R = 5e-6 * (1 + c) / (1 - c), s = 6 * Cfg.D_O2 * 25e-3 / (0.01 * t.cellC / (4.18879 * 1.25e-16) * R * R); ok &= Math.Abs(1 - 3 * x * x + 2 * x * x * x - s) < 1e-6; }
        }
        Console.WriteLine(ok ? "PASS 몸 속 산소" : "FAIL 몸 속 산소"); return ok ? 0 : 1;
    }

}
