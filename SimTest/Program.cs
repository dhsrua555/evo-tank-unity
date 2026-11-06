// 시뮬레이션 시나리오 시험: dotnet run -c Release -- <시나리오> <일수> <시드> [보고 간격(일)]
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Evo;

static class Program
{
    // 수온, 염분, 햇빛, 자외선, 대기 산소, 영양염
    static readonly Dictionary<string, float[]> Sc = new()
    {
        ["control"] = new float[] { 25, 35, 60, 30, 2, 50 },
        ["hisalt"] = new float[] { 25, 150, 60, 30, 2, 50 },
        ["fresh"] = new float[] { 25, 0, 60, 30, 2, 50 },
        ["uv"] = new float[] { 25, 35, 90, 100, 2, 50 },
        ["hot"] = new float[] { 45, 35, 60, 30, 2, 50 },
        ["cold"] = new float[] { 6, 35, 60, 30, 2, 50 },
        ["oxic"] = new float[] { 25, 35, 60, 30, 21, 50 },
        ["anoxic"] = new float[] { 25, 35, 60, 30, 0, 50 },
        ["sunny"] = new float[] { 25, 35, 100, 30, 2, 30 },
        ["dark"] = new float[] { 25, 35, 0, 30, 2, 50 },
        ["oligo"] = new float[] { 25, 35, 60, 30, 2, 5 },
        ["rich"] = new float[] { 25, 35, 60, 30, 2, 100 },
        ["fish"] = new float[] { 18, 35, 70, 20, 21, 40 },   // 산소 많은 온대 연안
        ["fishlow"] = new float[] { 18, 35, 70, 20, 4, 40 }, // 같은데 산소 4%
    };

    static int Main(string[] a)
    {
        string sc = a.Length > 0 ? a[0] : "control"; int days = a.Length > 1 ? int.Parse(a[1]) : 30, seed = a.Length > 2 ? int.Parse(a[2]) : 1;
        int every = a.Length > 3 ? int.Parse(a[3]) : Math.Max(1, days / 10);
        var w = new World(seed);
        foreach (var f in typeof(Params).GetFields()) if (Environment.GetEnvironmentVariable("P_" + f.Name) is string pv) { f.SetValue(w.P, float.Parse(pv)); Console.WriteLine($"# P.{f.Name} = {pv}"); }   // 시험: P_이름=값으로 매개변수 바꾸기
        // 처음엔 원시 바다에서 시작해 목표 환경으로 천천히 바꾼다(진화적 구조)
        var tg = Sc[sc]; for (int e = 0; e < Ev.N; e++) w.SetTarget(e, tg[e]);
        var sw = Stopwatch.StartNew(); double ms = 0;
        Console.WriteLine($"# {sc} seed {seed} days {days}");
        for (int d = 1; d <= days; d++)
        {
            var s1 = Stopwatch.StartNew();
            int tpd = (int)(24 / w.P.DT); for (int h = 0; h < tpd; h++) w.Step();   // 하루 = 24 h / DT 틱
            ms = s1.Elapsed.TotalMilliseconds / tpd;
            if (d % every == 0 || d == days) Report(w, d, ms);
            if (w.live.Count == 0) { Console.WriteLine("ALL EXTINCT"); break; }
        }
        var pp = Environment.GetEnvironmentVariable("PATHP"); if (pp != null) PathProbe(w, int.Parse(pp));
        var pe = Environment.GetEnvironmentVariable("PROBE"); if (pe != null) Probe(w, int.Parse(pe), Environment.GetEnvironmentVariable("PROBE_SP"));
        Console.WriteLine($"# wall {sw.Elapsed.TotalSeconds:F1}s  prof(ms/tick) cache {w.prof[0]/w.tick:F2} voxel {w.prof[1]/w.tick:F2} move {w.prof[2]/w.tick:F2} chem {w.prof[3]/w.tick:F2} mut {w.prof[4]/w.tick:F2} tot {w.prof[5]/w.tick:F2}");
        foreach (var e in w.events.Where(e => e.type == "grade")) Console.WriteLine($"  [{w.Time(e.t)}] {e.text}");
        Console.WriteLine($"# species ever {w.species.Count} alive {w.species.Values.Count(s => s.total > 0)} max grade {w.gradesSeen.Max()}");
        return 0;
    }

    // 침입 적합도 탐침: 가장 많은 계통(또는 PROBE_SP=계통 id)의 유전자 하나씩 ±1걸음 바꾼 돌연변이를 아주 조금 넣고
    // days일 동안 대조(똑같은 복제) 대비 하루 성장률 차이를 잰다. 양수 = 그 방향으로 진화가 밀어 줌
    static void Probe(World w, int days, string spId)
    {
        w.P.MUT_NEW = 0; w.PruneTo(2 * Gn.N + 2);
        var par = spId != null ? w.live.Select(i => w.strains[i]).First(x => x.id == int.Parse(spId)) : w.live.Select(i => w.strains[i]).OrderByDescending(x => x.total).First();
        var list = new List<(string name, Strain st)>();
        var ctl = w.Inject(par.slot, (float[])par.g.Clone(), 1e-4); list.Add(("control", ctl));
        for (int j = 0; j < Gn.N; j++) for (int dir = -1; dir <= 1; dir += 2)
        {
            var G = Gn.Def[j]; var g = (float[])par.g.Clone();
            float d = j == Gn.Cells || j == Gn.Egg ? 1 : G.sd * w.P.MUT_SD * 2;
            g[j] = j == Gn.Mutrate ? Math.Clamp(g[j] * MathF.Exp(dir * d), G.lo, G.hi) : Math.Clamp(g[j] + dir * d, G.lo, G.hi);
            if (g[j] == par.g[j]) continue;
            var ns = w.Inject(par.slot, g, 1e-4); if (ns != null) list.Add(($"{G.k}{(dir > 0 ? "+" : "-")}", ns));
        }
        double Tot(Strain st) { double t = 0; for (int v = 0; v < World.V; v++) for (int k = 0; k < World.KM; k++) t += w.cnt[World.Ix(st.slot, v, k)]; return t; }
        var t0 = list.ToDictionary(x => x.st.id, x => Tot(x.st));
        Console.WriteLine($"# probe parent id{par.id} {World.GradeName[World.Grade(par)]} n={par.total:G3} cells {par.g[Gn.Cells]:F1} size {par.g[Gn.Size]:F2} photo {par.g[Gn.Photo]:F2} phago {par.g[Gn.Phago]:F2} diff {par.g[Gn.Diff]:F2}");
        for (int t = 0; t < days * 12; t++) w.Step();
        double rc = Math.Log(Math.Max(1, ctl.total) / t0[ctl.id]) / days;
        foreach (var (name, st) in list.OrderByDescending(x => x.st.total / t0[x.st.id]))
            Console.Write($"{name} {(Math.Log(Math.Max(1, st.total) / t0[st.id]) / days - rc) * 100:+0.0;-0.0}  ");
        Console.WriteLine("(%/일, 대조 대비)");
    }

    // 어류로 가는 길의 단계별 가상 유전형을 넣고 생물량 성장률을 잰다(그 생태계에서 살 수 있나)
    static void PathProbe(World w, int days)
    {
        w.P.MUT_NEW = 0; w.PruneTo(12);
        var par = w.live.Select(i => w.strains[i]).OrderByDescending(x => x.total).First();
        var steps = new (string name, (int g, float v)[] set)[] {
            ("colony4", new[] { (Gn.Cells, 2f) }),
            ("colony16", new[] { (Gn.Cells, 4f) }),
            ("dev16", new[] { (Gn.Cells, 4f), (Gn.Diff, 0.6f) }),
            ("dev1k", new[] { (Gn.Cells, 10f), (Gn.Diff, 0.7f), (Gn.Form, 1f) }),
            ("polyp", new[] { (Gn.Cells, 12f), (Gn.Diff, 0.8f), (Gn.Form, 1f), (Gn.Gut, 0.6f), (Gn.Neuro, 0.3f), (Gn.Attach, 0.7f), (Gn.Photo, 0f), (Gn.Phago, 0.5f) }),
            ("medusa", new[] { (Gn.Cells, 14f), (Gn.Diff, 0.8f), (Gn.Form, 1f), (Gn.Gut, 0.6f), (Gn.Neuro, 0.4f), (Gn.Photo, 0f), (Gn.Phago, 0.5f) }),
            ("worm", new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Elong, 2f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f) }),
            ("chordate", new[] { (Gn.Cells, 20f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.8f), (Gn.Neuro, 0.7f), (Gn.Bilat, 0.8f), (Gn.Skel, 0.5f), (Gn.Circ, 0.4f), (Gn.Eye, 0.3f), (Gn.Elong, 2f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f) }),
            ("fish1cm", new[] { (Gn.Cells, 28f), (Gn.Diff, 1f), (Gn.Form, 1f), (Gn.Gut, 0.9f), (Gn.Neuro, 0.8f), (Gn.Bilat, 0.9f), (Gn.Skel, 0.8f), (Gn.Circ, 0.8f), (Gn.Eye, 0.8f), (Gn.Elong, 1.5f), (Gn.Photo, 0f), (Gn.Phago, 0.3f), (Gn.Aerobic, 1f), (Gn.Buoy, 0.3f), (Gn.Egg, 10f) }),
            ("fishE13", new[] { (Gn.Cells, 28f), (Gn.Diff, 1f), (Gn.Form, 1f), (Gn.Gut, 0.9f), (Gn.Neuro, 0.8f), (Gn.Bilat, 0.9f), (Gn.Skel, 0.8f), (Gn.Circ, 0.8f), (Gn.Eye, 0.8f), (Gn.Elong, 1.5f), (Gn.Photo, 0f), (Gn.Phago, 0.3f), (Gn.Aerobic, 1f), (Gn.Buoy, 0.3f), (Gn.Egg, 13f) }),
        };
        if (Environment.GetEnvironmentVariable("PRE") is string pre)   // 먼저 동물 플랑크톤(벌레형·자포형)을 넣어 자리 잡게 한 뒤 나머지를 넣는다(먹이 사슬 순서)
        {
            foreach (var (name, set) in steps.Where(x => x.name == "worm" || x.name == "medusa"))
            { var g = (float[])par.g.Clone(); g[Gn.Attach] = 0; g[Gn.Size] = 3.3f; g[Gn.Buoy] = 0; foreach (var (j, v) in set) g[j] = v; w.Inject(par.slot, g, 1e-5); }
            for (int t = 0; t < int.Parse(pre) * 12; t++) w.Step();
            par = w.live.Select(i => w.strains[i]).OrderByDescending(x => x.total).First();   // 그새 합쳐졌을 수 있다
            Console.WriteLine($"# pre {pre}d: " + string.Join(" ", w.live.Select(i => w.strains[i]).Where(s => World.Grade(s) >= 4).Select(s => $"{World.GradeName[World.Grade(s)]} {s.total:G3}")));
        }
        var list = new List<(string, Strain)>();
        foreach (var (name, set) in steps)
        {
            var g = (float[])par.g.Clone(); g[Gn.Attach] = 0; g[Gn.Size] = 3.3f; g[Gn.Buoy] = 0; foreach (var (j, v) in set) g[j] = v;
            var ns = w.Inject(par.slot, g, 1e-5); if (ns != null) list.Add((name, ns));
        }
        void Dbg(string name, Strain st)
        {
            var B = new double[14];
            Console.WriteLine($"     {name} Topt {st.g[Gn.Topt]:F1} Tb {st.g[Gn.Tbreadth]:F1} rho {st.rho:F0} T0 {w.T[0]:F1} T19 {w.T[19]:F1} perf0 {World.ThermalPerf(st.g, w.T[0]):F2} perf19 {World.ThermalPerf(st.g, w.T[19]):F2}");
            for (int k = 0; k < st.K; k++) { var m0 = w.MorphAt(st, 4, k, 0); Console.WriteLine($"       k{k} sink {m0.sink:G3} m/h speed {m0.speed * 3600e-6:G3} m/h gapeB {m0.gapeB:G3} Fc {m0.Fc:G3} pcrit {m0.pcrit:G3}"); }
            for (int k = 0; k < st.K; k++)
            {
                int bv = 0; double bc = 0; for (int v = 0; v < World.V; v++) { double c = w.cnt[World.Ix(st.slot, v, k)]; if (c > bc) { bc = c; bv = v; } }
                var m = w.M(st.slot, bv, k); int q = World.Ix(st.slot, bv, k);
                w.Budget(st, bv, ref m, w.gP[q], w.gB[q], B);
                Console.WriteLine($"     {name} k{k} C {st.C[k]:G3} L {m.L:G3}um n_tot {Enumerable.Range(0, World.V).Sum(v => w.cnt[World.Ix(st.slot, v, k)]):G3} @y{bv / World.LS} mu {w.mu[q]:G3}/h kill {w.kR[q]:G3}/h | gain osm {B[0]:G3} ph {B[1]:G3} bac {B[2]:G3} pred {B[3]:G3} | basal {B[4]:G3} traits {B[5]:G3} body {B[6]:G3} osmo {B[7] + B[8]:G3} uv {B[9]:G3} o2 {B[10]:G3} temp {B[11]:G3} net {B[13]:G3}");
            }
        }
        double Bio(Strain st) { double b = 0; for (int v = 0; v < World.V; v++) for (int k = 0; k < st.K; k++) b += w.cnt[World.Ix(st.slot, v, k)] * st.C[k]; return b; }
        var b0 = list.ToDictionary(x => x.Item2.id, x => Bio(x.Item2));
        Console.WriteLine($"# path probe parent id{par.id} n={par.total:G3}");
        for (int t = 0; t < 12; t++) w.Step();
        if (Environment.GetEnvironmentVariable("DBG") is string dn) foreach (var (name, st) in list) if (name == dn) Dbg(name, st);
        for (int t = 12; t < days * 12; t++)
        {
            w.Step();
            if (t % 120 == 0 && Environment.GetEnvironmentVariable("TL") != null)
                Console.WriteLine($"   t{t / 12,4}d " + string.Join(" ", list.Select(x => w.strains[x.Item2.slot] != x.Item2 ? $"{x.Item1}:-" : $"{x.Item1}:" + string.Join(",", Enumerable.Range(0, x.Item2.K).Select(k => x.Item2.stage[k] < 1 ? "0" : Math.Log10(x.Item2.stage[k]).ToString("F0"))))));
            if (t == 12 * int.Parse(Environment.GetEnvironmentVariable("DBGT") ?? "100") && Environment.GetEnvironmentVariable("DBG") is string dn2) foreach (var (name, st) in list) if (name == dn2 && w.strains[st.slot] == st) Dbg(name, st);
        }
        foreach (var (name, st) in list)
        {
            var m = w.MorphAt(st, w.Vox(10, 1, 10), st.K - 1, 0);
            Console.WriteLine($"  {name,-9} {World.GradeName[World.Grade(st)],-6} K{st.K} adult L {m.L:G3}um pcrit {m.pcrit:F1}% speed {m.speed:G3}um/s  bio growth {Math.Log(Math.Max(1e-30, Bio(st)) / b0[st.id]) / days * 100:+0.0;-0.0}%/일  alive {(w.strains[st.slot] == st ? "yes" : "no")}");
        }
    }

    static void Report(World w, int d, double ms)
    {
        var m = w.MeanGenes();
        int alive = w.species.Values.Count(s => s.total > 0);
        int maxG = w.live.Count == 0 ? 0 : w.live.Max(s => World.Grade(w.strains[s]));
        double maxL = 0, maxC = 0; Strain big = null;
        foreach (int s in w.live) { var st = w.strains[s]; if (st.total < 10) continue; double L = w.AdultL(st); if (L > maxL) { maxL = L; big = st; } maxC = Math.Max(maxC, st.C[st.K - 1]); }
        double ds = 0, db = 0, ns = 0, nb = 0, bs = 0;
        for (int c = 0; c < World.LS; c++) { ds += w.dox[c]; db += w.dox[(World.NY - 1) * World.LS + c]; ns += w.nut[c]; nb += w.nut[(World.NY - 1) * World.LS + c]; bs += w.bac[c]; }
        ds /= World.LS; db /= World.LS; ns /= World.LS; nb /= World.LS; bs /= World.LS;
        Console.WriteLine($"d{d,5} ms {ms,5:F2} strains {w.live.Count,3} sp {alive,3} N/mL {w.totalCount / (World.V * World.VML),8:G3} bio {w.totalBio / (World.V * World.VML),8:G3} " +
            $"DO {ds:F1}/{db:F1} nut {ns:F2}/{nb:F2} bac {bs:G2} grade {maxG} maxL {maxL:G3}um maxC {maxC:G3}");
        Console.WriteLine($"      size {m[Gn.Size]:F2} cells {m[Gn.Cells]:F2} photo {m[Gn.Photo]:F2} phago {m[Gn.Phago]:F2} aer {m[Gn.Aerobic]:F2} osmo {m[Gn.Osmo]:F0} topt {m[Gn.Topt]:F1} pig {m[Gn.Pigment]:F2} vac {m[Gn.Vacuole]:F2} wall {m[Gn.Wall]:F2} gut {m[Gn.Gut]:F2} neuro {m[Gn.Neuro]:F2} diff {m[Gn.Diff]:F2}");
        if (Environment.GetEnvironmentVariable("DOPROF") != null)
        {
            var sb = new System.Text.StringBuilder("      y: DO/bio/mix/T ");
            for (int y = 0; y < World.NY; y += 2)
            {
                double bio = 0; int v = y * World.LS;
                foreach (int s in w.live) { var st = w.strains[s]; for (int k = 0; k < st.K; k++) bio += w.cnt[World.Ix(s, v, k)] * st.C[k] * Math.Pow(st.esd0 / 10, 3); }
                sb.Append($"| {y} {w.dox[v]:F1}/{bio / World.VML:G2}/{w.mixV[y]:G2}/{w.T[y]:F0} ");
            }
            Console.WriteLine(sb);
        }
        if (Environment.GetEnvironmentVariable("TOP") != null)
            foreach (var st in w.live.Select(i => w.strains[i]).OrderByDescending(x => x.total).Take(5))
            {
                var diffs = Enumerable.Range(0, Gn.N).Where(i => Math.Abs(st.g[i] - Gn.Def[i].init) > Gn.Def[i].sd).Select(i => $"{Gn.Def[i].k}={st.g[i]:G3}");
                Console.WriteLine($"      top id{st.id} sp{st.sp} n={st.total:G3} trend {st.trend * 1000:F2}e-3 age {w.Time(w.tick - st.born)} | {string.Join(" ", diffs)}");
            }
        if (big != null && big.g[Gn.Cells] >= 1)
        {
            var g = big.g;
            Console.WriteLine($"      biggest: {World.GradeName[World.Grade(big)]} n={big.total:G3} cells {g[Gn.Cells]:F1} K {big.K} diff {g[Gn.Diff]:F2} gut {g[Gn.Gut]:F2} neuro {g[Gn.Neuro]:F2} bilat {g[Gn.Bilat]:F2} circ {g[Gn.Circ]:F2} skel {g[Gn.Skel]:F2} egg {g[Gn.Egg]:F1}");
        }
    }
}
