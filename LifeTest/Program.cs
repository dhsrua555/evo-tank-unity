// 머리 없는 시험: dotnet LifeTest.dll <일수> <시드> [보고 간격] [돌연변이율] [csv]
// 매 간격 상태 한 줄, 사건은 일어날 때, 끝에 '됐다' 기준 판정(PASS/FAIL).
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Life;

static class Program
{
    static int Main(string[] a)
    {
        int days = a.Length > 0 ? int.Parse(a[0]) : 100, seed = a.Length > 1 ? int.Parse(a[1]) : 1, every = a.Length > 2 ? int.Parse(a[2]) : 10;
        var pl = new Planet(seed);
        if (a.Length > 3) pl.knobs.mu = double.Parse(a[3]);
        if (Environment.GetEnvironmentVariable("THREADS") is string th) pl.po.MaxDegreeOfParallelism = int.Parse(th);   // 여러 시드를 한꺼번에 돌릴 때 실행마다 스레드 수를 묶는다
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
        if (Environment.GetEnvironmentVariable("CELL") != null)
            foreach (var st in pl.alive) for (int v = 0; v < pl.V; v++)
            {
                if (pl.B[st.slot * pl.V + v] <= 0) continue; var e = pl.EnvAt(v); var r = Cell.Grow(st.t, st.b, e, 0, 0);
                Console.WriteLine($"칸 {v} T {e.T:0.0} 빛 {e.light:0.000} H2 {e.H2:0.00} CO2 {e.CO2:0} DOC {e.DOC:0.0} NH4 {e.NH4:0.0} PO4 {e.PO4:0.00} | g {r.g:0.000} mort {r.mort:0.000} fix {r.fix:0.000} assim {r.assim:0.000} catab {r.catab:0.000} chemoATP {r.chemoATP:0.00} nh4 {r.nh4:0.0000} po4 {r.po4:0.00000} | φ " + string.Join(" ", st.t.phi.Select(x => x.ToString("0.000"))));
            }
        var sw = Stopwatch.StartNew(); int ev = 0;
        for (int d = 1; d <= days; d++)
        {
            for (int k = 0; k < Cfg.TPD; k++) pl.Step();
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
        { var L = pl.Ledger(); L[1] -= pl.nfixV.Sum(); string[] en = { "C", "N", "P", "Fe", "S" }; Console.WriteLine("장부 원소별 어긋남: " + string.Join(" ", Enumerable.Range(0, 5).Select(k => $"{en[k]} {(L[k] - pl.ledger0[k]) / Math.Abs(pl.ledger0[k]):+0.0e0;-0.0e0}"))); }
        { var hg = new int[G.N]; foreach (var st in pl.alive) if (st.gene >= 0) hg[st.gene]++; Console.WriteLine("살아 있는 계통의 창시 돌연변이 유전자: " + string.Join(" ", Enumerable.Range(0, G.N).Select(i => $"{G.Name[i]} {hg[i]}")));
          Console.WriteLine("g[EOX] 분포: " + string.Join(" ", pl.alive.Select(st => st.g[G.EOX]).Distinct().Take(12).Select(x => x.ToString("0.000")))); }
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
        var ed = pl.evoDbg; Console.WriteLine($"    정착 {ed[0]} 중 r≤0 {ed[1]} | 지운 어린 계통 {ed[3]} 중 느는 중 {ed[2]}"); System.Array.Clear(ed);
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
    static void Say(string what, bool ok, string detail) => Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what} — {detail}");
}
