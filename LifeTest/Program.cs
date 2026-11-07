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
            File.WriteAllLines(csv, new[] { "day,O2,CO2,CH4,T,bio,nSp,nStrain,nClade,oxy,multi,aer,sod,rPhoto,rChemo,rPrey,rDet,rDoc,photoSh,chemoSh,preySh,detSh,docSh,maxCells,maxSize,drift,kept" }
                .Concat(H.Select(h => $"{h.day},{h.O2:g4},{h.CO2:g4},{h.CH4:g4},{h.T:0.00},{h.bio:g4},{h.nSp},{h.nStrain},{h.nClade},{h.oxyShare:0.0000},{h.multi:0.0000},{h.aer:0.0000},{h.sod:0.0000},{h.rPhoto},{h.rChemo},{h.rPrey},{h.rDet},{h.rDoc},{h.photoSh:0.000},{h.chemoSh:0.000},{h.preySh:0.000},{h.detSh:0.000},{h.docSh:0.000},{h.maxCells:0.0},{h.maxSize:0.0},{h.drift:g3},{h.kept}")));

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
    static void Say(string what, bool ok, string detail) => Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {what} — {detail}");
}
