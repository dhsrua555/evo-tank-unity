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
        if (Environment.GetEnvironmentVariable("GEO") is string gs) { GeoDump(int.Parse(gs)); return 0; }
        if (Environment.GetEnvironmentVariable("MIXP") is string mp) { for (int sd = 1; sd <= int.Parse(mp); sd++) { var (f, c) = MixF(new World(sd)); if (f > 1) Console.WriteLine($"seed {sd} maxF {f:F2} at {c}"); } return 0; }
        string sc = a.Length > 0 ? a[0] : "control"; int days = a.Length > 1 ? int.Parse(a[1]) : 30, seed = a.Length > 2 ? int.Parse(a[2]) : 1;
        int every = a.Length > 3 ? int.Parse(a[3]) : Math.Max(1, days / 10);
        var w = new World(seed);
        foreach (var f in typeof(Params).GetFields()) if (Environment.GetEnvironmentVariable("P_" + f.Name) is string pv) { f.SetValue(w.P, float.Parse(pv)); Console.WriteLine($"# P.{f.Name} = {pv}"); }   // 시험: P_이름=값으로 매개변수 바꾸기
        if (Environment.GetEnvironmentVariable("P_PRIMO") != null) w.Reset(seed);   // 지도는 Reset에서 만들어지니 원시 바다 값을 바꿨으면 다시 만든다
        // 처음엔 원시 바다에서 시작해 목표 환경으로 천천히 바꾼다(진화적 구조)
        var tg = Sc[sc]; for (int e = 0; e < Ev.N; e++) w.SetTarget(e, tg[e]);
        if (Environment.GetEnvironmentVariable("O2BIO") != null) w.o2Bio = true;
        if (Environment.GetEnvironmentVariable("AUTOQ") is string aq) { w.autoQ = true; w.activity = float.Parse(aq); }
        // 시험 사건: QUAKE="종류:규모:날짜[:x,z[:세기:반경:기간]];…" 종류 = vol|upl|warm|ice|imp
        var qs = (Environment.GetEnvironmentVariable("QUAKE") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split(':')).Select(f => (kind: f[0] switch { "vol" => Qk.Volcano, "upl" => Qk.Uplift, "warm" => Qk.Warm, "ice" => Qk.IceAge, _ => Qk.Impact }, size: int.Parse(f[1]), day: int.Parse(f[2]),
                cell: f.Length > 3 ? int.Parse(f[3].Split(',')[1]) * Geo.G + int.Parse(f[3].Split(',')[0]) : -1, amp: f.Length > 4 ? float.Parse(f[4]) : 0f, rad: f.Length > 5 ? float.Parse(f[5]) : 2f, len: f.Length > 6 ? float.Parse(f[6]) : 100f)).ToList();
        var sw = Stopwatch.StartNew(); double ms = 0;
        Console.WriteLine($"# {sc} seed {seed} days {days}");
        for (int d = 1; d <= days; d++)
        {
            foreach (var q in qs.Where(q => q.day == d)) { var qq = w.Fire(q.kind, q.size, q.cell, q.kind == Qk.IceAge && q.amp == 0 ? World.IceAmp(q.size) : q.amp, q.rad, q.len); Console.WriteLine($"# 사건 d{d}: {qq.text}"); }
            if (Environment.GetEnvironmentVariable("STRESS") is string sx && int.Parse(sx) == d) Stress(w, d);
            var s1 = Stopwatch.StartNew();
            int tpd = (int)(24 / w.P.DT); for (int h = 0; h < tpd; h++) w.Step();   // 하루 = 24 h / DT 틱
            ms = s1.Elapsed.TotalMilliseconds / tpd;
            if (d % every == 0 || d == days) Report(w, d, ms);
            if (Environment.GetEnvironmentVariable("NEGP") != null) foreach (var (n, arr) in new[] { ("nut", w.nut), ("bac", w.bac), ("dox", w.dox), ("pom", w.pom), ("dom", w.dom), ("cya", w.cya) }) for (int v = 0; v < World.V; v++) if (arr[v] < 0) { Console.WriteLine($"NEG day {d} {n}={arr[v]:G3} at {w.geo.Name(v % World.LS)} y {v / World.LS}"); break; }
            if (Nan(w) is string bad) { Console.WriteLine($"NAN day {d}: {bad}"); foreach (var e in w.events.TakeLast(8)) Console.WriteLine($"  [{w.Time(e.t)}] {e.type} {e.text}"); return 1; }
            if (w.live.Count == 0) { Console.WriteLine("ALL EXTINCT"); break; }
        }
        var pp = Environment.GetEnvironmentVariable("PATHP"); if (pp != null) PathProbe(w, int.Parse(pp));
        var lp = Environment.GetEnvironmentVariable("LANDP"); if (lp != null) LandProbe(w, int.Parse(lp));
        if (Environment.GetEnvironmentVariable("RECS") != null) RecCheck(w);
        var hp = Environment.GetEnvironmentVariable("HETP"); if (hp != null) HetProbe(w, int.Parse(hp));
        var pe = Environment.GetEnvironmentVariable("PROBE"); if (pe != null) Probe(w, int.Parse(pe), Environment.GetEnvironmentVariable("PROBE_SP"));
        Console.WriteLine($"# wall {sw.Elapsed.TotalSeconds:F1}s cpu {Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds:F0}s  prof(ms/tick) cache {w.prof[0]/w.tick:F2} voxel {w.prof[1]/w.tick:F2} move {w.prof[2]/w.tick:F2} chem {w.prof[3]/w.tick:F2} mut {w.prof[4]/w.tick:F2} tot {w.prof[5]/w.tick:F2}  mutants {w.mutKeep}/{w.mutTry}");
        foreach (var e in w.events.Where(e => e.type == "grade")) Console.WriteLine($"  [{w.Time(e.t)}] {e.text}");
        foreach (var e in w.events.Where(e => e.type == "earth" && (e.text.StartsWith("땅이") || e.text.StartsWith("조산"))).TakeLast(30)) Console.WriteLine($"  [{w.Time(e.t)}] {e.text}");
        Console.WriteLine($"# species ever {w.species.Count} alive {w.species.Values.Count(s => s.total > 0)} looks {string.Join(",", w.looksSeen.Select(Taxa.Name))}");
        return 0;
    }

    // 성능 시험(STRESS=<날>): 그날 다세포 몸 설계 40계통을 부모 칸에 넣는다(부모 개체 30%, 세포 수를 단계마다 나눠 생물량은 그대로) → 다세포가 많은 상태의 틱 비용
    static void Stress(World w, int d)
    {
        (int, float)[] B = { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f) };
        var sets = new[] {
            B.Concat(new[] { (Gn.Elong, 2f), (Gn.Seg, 4f), (Gn.Burrow, 0.8f) }).ToArray(),
            B.Concat(new[] { (Gn.Cells, 18f), (Gn.Elong, 1.2f), (Gn.Seg, 4f), (Gn.Limb, 0.7f), (Gn.Exo, 0.6f), (Gn.Circ, 0.5f), (Gn.Eye, 0.4f) }).ToArray(),
            B.Concat(new[] { (Gn.Elong, 2f), (Gn.Jaw, 0.6f), (Gn.Transp, 0.7f), (Gn.Ambush, 0.7f), (Gn.Eye, 0.3f) }).ToArray(),
            new[] { (Gn.Cells, 28f), (Gn.Diff, 1f), (Gn.Form, 1f), (Gn.Gut, 0.9f), (Gn.Neuro, 0.8f), (Gn.Bilat, 0.9f), (Gn.Skel, 0.8f), (Gn.Circ, 0.8f), (Gn.Eye, 0.8f), (Gn.Elong, 1.5f), (Gn.Photo, 0f), (Gn.Phago, 0.3f), (Gn.Aerobic, 1f), (Gn.Egg, 10f), (Gn.Jaw, 0.7f) },
            new[] { (Gn.Cells, 12f), (Gn.Diff, 0.8f), (Gn.Form, 0.5f), (Gn.Photo, 0.8f), (Gn.Attach, 0.7f), (Gn.Wall, 0.6f), (Gn.Phago, 0f), (Gn.Dry, 0.9f) },
        };
        w.PruneTo(48); int made = 0; var par0 = w.live.OrderByDescending(s => w.strains[s].total).Take(8).ToArray();
        for (int i = 0; i < 40; i++)
        {
            var par = w.strains[par0[i % par0.Length]]; var g = (float[])par.g.Clone();
            foreach (var (j, x) in sets[i % sets.Length]) g[j] = x;
            var ns = w.Inject(par.slot, g, 0.3); if (ns == null) continue; made++;
            for (int v = 0; v < World.V; v++)
            {
                double c = 0; for (int k = 0; k < ns.K; k++) c += w.cnt[World.Ix(ns.slot, v, k)] * ns.C[k];
                for (int k = 0; k < ns.K; k++) w.cnt[World.Ix(ns.slot, v, k)] = Math.Round(c / (ns.K * ns.C[k]));
            }
        }
        Console.WriteLine($"# STRESS d{d}: 다세포 {made}계통 넣음 (live {w.live.Count}, dev {w.live.Count(s => w.strains[s].dev)})");
    }

    // 수평 섞기(양해법)의 틱당 바깥 교환 몫 최댓값: 2를 넘으면 진동하며 커진다
    static (double, string) MixF(World w)
    {
        var g = w.geo; var sum = new double[World.V]; double kh = w.P.KH_H * 3600 * w.P.DT;
        foreach (var (a, b, e, dist) in g.links)
        {
            double f = g.rKind[a] == 2 || g.rKind[b] == 2 ? w.P.LANDX : 1;
            for (int y = 0; y < Math.Min(w.nyR[a], w.nyR[b]); y++)
            {
                int va = y * World.LS + a, vb = y * World.LS + b; double Q = f * e * Geo.CELL * Math.Min(w.dz[va], w.dz[vb]) / dist * kh;
                sum[va] += Q / (w.vol[va] * 1e-6); sum[vb] += Q / (w.vol[vb] * 1e-6);
            }
        }
        int m = 0; for (int v = 1; v < World.V; v++) if (sum[v] > sum[m]) m = v;
        return (sum[m], $"{g.Name(m % World.LS)} y {m / World.LS} cells {g.rCells[m % World.LS]}");
    }

    // 수치 오류 감시: 처음 NaN·무한대가 생긴 곳
    static string Nan(World w)
    {
        static bool Bad(double x) => !double.IsFinite(x);
        for (int e = 0; e < Ev.N; e++) if (Bad(w.env[e])) return $"env[{e}]";
        foreach (var (n, a) in new[] { ("T", w.T), ("I", w.I), ("U", w.U) }) for (int v = 0; v < World.V; v++) if (Bad(a[v])) return $"{n}[{v}] reg {v % World.LS} y {v / World.LS} vol {w.vol[v]}";
        foreach (var (n, a) in new[] { ("nut", w.nut), ("bac", w.bac), ("dox", w.dox), ("pom", w.pom), ("dom", w.dom), ("cya", w.cya), ("vol", w.vol) }) for (int v = 0; v < World.V; v++) if (Bad(a[v])) return $"{n}[{v}] reg {v % World.LS} y {v / World.LS} vol {w.vol[v]}";
        foreach (int s in w.live)
        {
            var st = w.strains[s];
            for (int i = 0; i < st.g.Length; i++) if (Bad(st.g[i])) return $"gene {Gn.Def[i].k} strain {st.id}";
            for (int v = 0; v < World.V; v++) for (int k = 0; k < st.K; k++)
            {
                double c = w.cnt[World.Ix(s, v, k)]; if (Bad(c)) return $"cnt strain {st.id} v {v} reg {v % World.LS} y {v / World.LS} k {k} vol {w.vol[v]}";
                if (c > 0 && w.vol[v] <= 0) return $"cnt>0 in dry cell strain {st.id} reg {v % World.LS} y {v / World.LS}";
                if (c > 0) { var m = w.M(s, v, k); if (Bad(m.sink) || Bad(m.L)) return $"morph strain {st.id} reg {v % World.LS} y {v / World.LS} k {k} sink {m.sink} L {m.L} T {w.T[v]} C {m.C} esd {m.esd}"; }
            }
        }
        return null;
    }

    static void GeoDump(int seed)
    {
        var g = new Geo(); g.Generate(seed); g.Regions(World.RMAX - Geo.NLAND);
        const string ch = "0123456789abcdefghijklmnopqrstuvwxyz";
        for (int z = Geo.G - 1; z >= 0; z--)
        {
            var sb = new System.Text.StringBuilder($"{Geo.Lat(z),4:0}° ");
            for (int x = 0; x < Geo.G; x++) { int i = z * Geo.G + x; sb.Append(g.reg[i] >= 0 ? ch[g.reg[i] % ch.Length] : g.sreg[i] >= 0 ? '+' : '#'); }
            Console.WriteLine(sb);
        }
        int land = 0; foreach (int r in g.reg) if (r < 0) land++;
        Console.WriteLine($"regions {g.NR} land {land} cells, links {g.links.Count}");
        for (int a = 0; a < g.NR; a++) Console.WriteLine($"  {ch[a % ch.Length]} {g.Name(a)} area {g.rArea[a] * 1e-6:G3} km2 links {string.Join(",", g.links.Where(l => l.a == a || l.b == a).Select(l => $"{(l.a == a ? l.b : l.a)}:{l.e}"))} cells {g.rCells[a]} depth {g.rDepth[a]:0} m lat {g.rLat[a]:0} coast {g.rCoast[a]:0.00}");
    }

    // 계통 기록 점검: 살아 있는 계통마다 기록이 있고 조상 사슬이 공통 조상(부모 0)까지 끊기지 않는지
    static void RecCheck(World w)
    {
        int bad = 0, maxD = 0;
        foreach (int s in w.live)
        {
            var st = w.strains[s]; if (!w.recs.TryGetValue(st.id, out var r) || r.died >= 0) { bad++; continue; }
            int d = 0; while (r.parent != 0 && d < 100000) { if (!w.recs.TryGetValue(r.parent, out r)) { bad++; break; } d++; }
            maxD = Math.Max(maxD, d);
        }
        var dead = w.recs.Values.Where(r => r.died >= 0).ToList();
        bad += dead.Count(r => r.why < 1 || r.why > 3 || r.why == 2 && r.into < 0);
        Console.WriteLine($"# recs {(bad == 0 ? "ok" : "FAIL " + bad)} n {w.recs.Count} live {w.live.Count} maxDepth {maxD} extinct {dead.Count(r => r.why == 1)} (eaten {dead.Count(r => r.why == 1 && r.eaten)}) merged {dead.Count(r => r.why == 2)} pruned {dead.Count(r => r.why == 3)}");
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
        Console.WriteLine($"# probe parent id{par.id} {Taxa.Name(par)} n={par.total:G3} cells {par.g[Gn.Cells]:F1} size {par.g[Gn.Size]:F2} photo {par.g[Gn.Photo]:F2} phago {par.g[Gn.Phago]:F2} diff {par.g[Gn.Diff]:F2}");
        for (int t = 0; t < days * 12; t++) w.Step();
        double rc = Math.Log(Math.Max(1, ctl.total) / t0[ctl.id]) / days;
        foreach (var (name, st) in list.OrderByDescending(x => x.st.total / t0[x.st.id]))
            Console.Write($"{name} {(Math.Log(Math.Max(1, st.total) / t0[st.id]) / days - rc) * 100:+0.0;-0.0}  ");
        Console.WriteLine("(%/일, 대조 대비)");
    }

    // 영양 방식 탐침: 진화한 군집에 순수 종속영양·순수 광합성·혼합영양 단세포를 조금 넣고 순성장(/일)과 먹이 이득을 잰다
    static void HetProbe(World w, int days)
    {
        w.P.MUT_NEW = 0; w.PruneTo(12);
        var par = w.live.Select(i => w.strains[i]).OrderByDescending(x => x.total).First();
        var a0 = Gn.Def.Select(d => d.init).ToArray();
        var list = new List<(string, Strain)>();
        foreach (var (name, set) in new (string, (int, float)[])[] {
            ("het3um", new[] { (Gn.Size, 1.6f), (Gn.Phago, 1f), (Gn.Photo, 0f) }), ("het6um", new[] { (Gn.Size, 2.6f), (Gn.Phago, 1f), (Gn.Photo, 0f) }),
            ("het12um", new[] { (Gn.Size, 3.6f), (Gn.Phago, 1f), (Gn.Photo, 0f) }), ("het25um", new[] { (Gn.Size, 4.6f), (Gn.Phago, 1f), (Gn.Photo, 0f), (Gn.Cilia, 0.8f) }),
            ("auto2um", new[] { (Gn.Size, 1f), (Gn.Phago, 0f), (Gn.Photo, 1f) }), ("auto6um", new[] { (Gn.Size, 2.6f), (Gn.Phago, 0f), (Gn.Photo, 1f) }),
            ("mixo6um", new[] { (Gn.Size, 2.6f), (Gn.Phago, 1f), (Gn.Photo, 1f) }) })
        {
            var g = (float[])a0.Clone(); g[Gn.Topt] = par.g[Gn.Topt]; g[Gn.Osmo] = par.g[Gn.Osmo]; g[Gn.Aerobic] = par.g[Gn.Aerobic];
            foreach (var (j, x) in set) g[j] = x;
            var ns = w.Inject(par.slot, g, 1e-4); if (ns != null) list.Add((name, ns));
        }
        double Tot(Strain st) { double t = 0; for (int v = 0; v < World.V; v++) for (int k = 0; k < World.KM; k++) t += w.cnt[World.Ix(st.slot, v, k)]; return t; }
        var t0 = list.ToDictionary(x => x.Item2.id, x => Tot(x.Item2));
        double bs = 0, a = 0; for (int c = 0; c < w.geo.NO; c++) { bs += w.geo.rArea[c] * w.bac[c]; a += w.geo.rArea[c]; }
        Console.WriteLine($"# hetprobe bac(surface) {bs / a:G3}/mL  parent {Taxa.Name(par)}");
        for (int t = 0; t < days * 12; t++) w.Step();
        foreach (var (name, st) in list)
        {
            int bv = 0; double bc = -1; for (int v = 0; v < World.V; v++) { double c = w.cnt[World.Ix(st.slot, v, 0)]; if (c > bc) { bc = c; bv = v; } }
            int q = World.Ix(st.slot, bv, 0);
            Console.WriteLine($"  {name,-8} net {Math.Log(Math.Max(1, Tot(st)) / t0[st.id]) / days:+0.00;-0.00}/일  mu {w.mu[q] * 24:+0.00;-0.00}/일 @{w.geo.Name(bv % World.LS)} y{bv / World.LS}  gainB {w.gB[q]:G3} gainP {w.gP[q]:G3} killed {w.kR[q] * 24:G3}/일 bac {w.bac[bv]:G3}");
        }
    }

    // 땅: 마름 견디기가 다른 변이를 넣고(조간대·육지에도 조금 뿌림) 바다·조간대·육지별 생물량과 그 자리의 순이득(%/일)을 잰다
    static void LandProbe(World w, int days)
    {
        w.P.MUT_NEW = 0; w.P.MUT_FLOOR = 0; w.PruneTo(30);
        var par = w.live.Select(i => w.strains[i]).OrderByDescending(x => x.total).First();
        var body = new (string name, (int g, float v)[] set)[] {
            ("cell", new (int, float)[0]),
            ("mat", new[] { (Gn.Attach, 0.8f), (Gn.Photo, 0.6f), (Gn.Wall, 0.4f) }),
            ("weed", new[] { (Gn.Cells, 12f), (Gn.Diff, 0.8f), (Gn.Form, 0.5f), (Gn.Photo, 0.8f), (Gn.Attach, 0.7f), (Gn.Wall, 0.6f), (Gn.Phago, 0f) }),
            ("worm", new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f), (Gn.Elong, 2f), (Gn.Seg, 4f), (Gn.Burrow, 0.8f), (Gn.Detr, 0.9f) }),
            ("arthro", new[] { (Gn.Cells, 18f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f), (Gn.Elong, 1.2f), (Gn.Seg, 4f), (Gn.Limb, 0.7f), (Gn.Exo, 0.6f), (Gn.Circ, 0.5f), (Gn.Eye, 0.4f), (Gn.Burrow, 0.6f) }),
        };
        var list = new List<(string name, Strain st)>();
        foreach (var (bn, set) in body) foreach (float dry in new[] { 0f, 0.5f, 0.9f })
        {
            var g = (float[])par.g.Clone(); g[Gn.Size] = bn == "cell" || bn == "mat" ? par.g[Gn.Size] : 3.3f; foreach (var (j, v) in set) g[j] = v; g[Gn.Dry] = dry;
            var ns = w.Inject(par.slot, g, 1e-4); if (ns == null) continue; list.Add(($"{bn}{dry * 10:0}", ns));
            for (int c = w.geo.NO; c < w.NR; c++) for (int k = 0; k < ns.K; k++)
            {
                double n = 0; for (int v = 0; v < World.V; v++) n += w.cnt[World.Ix(ns.slot, v, k)];
                w.cnt[World.Ix(ns.slot, c, k)] += Math.Round(n / w.volTot * w.vol[c] * 0.1);
            }
        }
        (double sea, double shore, double land) Bio(Strain st)
        {
            double a = 0, b = 0, c = 0;
            for (int v = 0; v < World.V; v++) { int r = v % World.LS; if (r >= w.NR) continue; double x = 0; for (int k = 0; k < st.K; k++) x += w.cnt[World.Ix(st.slot, v, k)] * st.C[k]; if (w.geo.rKind[r] == 0) a += x; else if (w.geo.rKind[r] == 1) b += x; else c += x; }
            return (a, b, c);
        }
        string Net(Strain st, int kind)
        {
            var B = new double[14]; var o = new List<string>();
            for (int c = w.geo.NO; c < w.NR; c++) if (w.geo.rKind[c] == kind)
            {
                int k = st.K - 1; var m = w.M(st.slot, c, k); int q = World.Ix(st.slot, c, k);
                w.Budget(st, c, ref m, w.gP[q], w.gB[q], B); o.Add($"{B[13] / Math.Max(1e-30, m.M) * 2400:+0;-0}(dry {B[8] / Math.Max(1e-30, m.M) * 2400:0})");
            }
            return string.Join(" ", o);
        }
        var b0 = list.ToDictionary(x => x.st.id, x => Bio(x.st));
        Console.WriteLine($"# land probe parent id{par.id} {Taxa.Name(par)} regions sea {w.geo.NO} land {w.NR - w.geo.NO}: " + string.Join(" ", Enumerable.Range(w.geo.NO, w.NR - w.geo.NO).Select(c => $"r{c} {w.geo.Name(c)} T {w.T[c]:F0} nut {w.nut[c]:F1} I {w.I[c]:F2} U {w.U[c]:F2}")));
        for (int t = 0; t < 12; t++) w.Step();
        Console.WriteLine("  name    net%/day(adult, desiccation) shore | land");
        foreach (var (name, st) in list) if (w.strains[st.slot] == st) Console.WriteLine($"  {name,-8} {Net(st, 1)} | {Net(st, 2)}");
        for (int t = 12; t < days * 12; t++) w.Step();
        Console.WriteLine($"  after {days}d: biomass sea/shore/land (×start)");
        foreach (var (name, st) in list)
        {
            var (a0, s0, l0) = b0[st.id]; bool alive = w.strains[st.slot] == st; var (a, s, l) = alive ? Bio(st) : (0, 0, 0);
            Console.WriteLine($"  {name,-8} {Taxa.Name(st),-12} sea {a / Math.Max(1e-30, a0):G2} shore {s / Math.Max(1e-30, s0):G2} land {l / Math.Max(1e-30, l0):G2} {(alive ? "" : "멸종")}");
        }
    }

    // 어류로 가는 길의 단계별 가상 유전형을 넣고 생물량 성장률을 잰다(그 생태계에서 살 수 있나)
    static void PathProbe(World w, int days)
    {
        w.P.MUT_NEW = 0; w.P.MUT_FLOOR = 0; w.PruneTo(26);
        var par = w.live.Select(i => w.strains[i]).OrderByDescending(x => x.total).First();
        var steps = new (string name, (int g, float v)[] set)[] {
            ("colony4", new[] { (Gn.Cells, 2f) }),
            ("colony16", new[] { (Gn.Cells, 4f) }),
            ("dev16", new[] { (Gn.Cells, 4f), (Gn.Diff, 0.6f) }),
            ("dev1k", new[] { (Gn.Cells, 10f), (Gn.Diff, 0.7f), (Gn.Form, 1f) }),
            ("polyp", new[] { (Gn.Cells, 12f), (Gn.Diff, 0.8f), (Gn.Form, 1f), (Gn.Gut, 0.6f), (Gn.Neuro, 0.3f), (Gn.Attach, 0.7f), (Gn.Photo, 0f), (Gn.Phago, 0.5f) }),
            ("medusa", new[] { (Gn.Cells, 14f), (Gn.Diff, 0.8f), (Gn.Form, 1f), (Gn.Gut, 0.6f), (Gn.Neuro, 0.4f), (Gn.Photo, 0f), (Gn.Phago, 0.5f) }),
            ("worm", new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Elong, 2f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f) }),
            ("wormE6", new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Elong, 2f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f), (Gn.Egg, 6f) }),
            ("chordate", new[] { (Gn.Cells, 20f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.8f), (Gn.Neuro, 0.7f), (Gn.Bilat, 0.8f), (Gn.Skel, 0.5f), (Gn.Circ, 0.4f), (Gn.Eye, 0.3f), (Gn.Elong, 2f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f) }),
            ("fish1cm", new[] { (Gn.Cells, 28f), (Gn.Diff, 1f), (Gn.Form, 1f), (Gn.Gut, 0.9f), (Gn.Neuro, 0.8f), (Gn.Bilat, 0.9f), (Gn.Skel, 0.8f), (Gn.Circ, 0.8f), (Gn.Eye, 0.8f), (Gn.Elong, 1.5f), (Gn.Photo, 0f), (Gn.Phago, 0.3f), (Gn.Aerobic, 1f), (Gn.Buoy, 0.3f), (Gn.Egg, 10f) }),
            ("fishE13", new[] { (Gn.Cells, 28f), (Gn.Diff, 1f), (Gn.Form, 1f), (Gn.Gut, 0.9f), (Gn.Neuro, 0.8f), (Gn.Bilat, 0.9f), (Gn.Skel, 0.8f), (Gn.Circ, 0.8f), (Gn.Eye, 0.8f), (Gn.Elong, 1.5f), (Gn.Photo, 0f), (Gn.Phago, 0.3f), (Gn.Aerobic, 1f), (Gn.Buoy, 0.3f), (Gn.Egg, 13f) }),
            // 4단계 부품 조합
            ("burrower", new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f), (Gn.Elong, 2f), (Gn.Seg, 4f), (Gn.Burrow, 0.8f), (Gn.Detr, 0.9f) }),
            ("polychaete", new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f), (Gn.Elong, 2f), (Gn.Seg, 4f), (Gn.Limb, 0.5f) }),
            ("arthro", new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f), (Gn.Cells, 18f), (Gn.Elong, 1.2f), (Gn.Seg, 4f), (Gn.Limb, 0.7f), (Gn.Exo, 0.6f), (Gn.Circ, 0.5f), (Gn.Eye, 0.4f) }),
            ("bigarthro", new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f), (Gn.Cells, 25f), (Gn.Elong, 1.2f), (Gn.Seg, 4f), (Gn.Limb, 0.7f), (Gn.Exo, 0.6f), (Gn.Circ, 0.5f), (Gn.Eye, 0.4f) }),
            ("urchin", new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.4f), (Gn.Bilat, 0f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f), (Gn.Exo, 0.6f), (Gn.Mineral, 0.7f) }),
            ("copepod", new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f), (Gn.Cells, 14f), (Gn.Elong, 1f), (Gn.Seg, 3f), (Gn.Limb, 0.7f), (Gn.Exo, 0.5f), (Gn.Eye, 0.3f), (Gn.PreyR, 2.5f) }),
            ("trilobite", new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f), (Gn.Cells, 18f), (Gn.Elong, 1f), (Gn.Seg, 4f), (Gn.Limb, 0.6f), (Gn.Exo, 0.6f), (Gn.Mineral, 0.6f), (Gn.Circ, 0.5f), (Gn.Eye, 0.5f), (Gn.Detr, 0.9f), (Gn.Burrow, 0.6f) }),
            ("snail", new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f), (Gn.Elong, 0.5f), (Gn.Exo, 0.6f), (Gn.Mineral, 0.7f), (Gn.Jaw, 0.5f), (Gn.Herb, 0.6f), (Gn.Circ, 0.5f) }),
            ("clam", new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f), (Gn.Neuro, 0.3f), (Gn.Elong, 0.3f), (Gn.Exo, 0.6f), (Gn.Mineral, 0.7f), (Gn.Burrow, 0.7f), (Gn.Cilia, 0.7f), (Gn.Phago, 0.7f), (Gn.Circ, 0.5f) }),
            ("arrow", new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f), (Gn.Elong, 2f), (Gn.Jaw, 0.6f), (Gn.Transp, 0.7f), (Gn.Ambush, 0.7f), (Gn.Eye, 0.3f), (Gn.Pigment, 0f) }),
            ("jawless", new[] { (Gn.Cells, 28f), (Gn.Diff, 1f), (Gn.Form, 1f), (Gn.Gut, 0.9f), (Gn.Neuro, 0.8f), (Gn.Bilat, 0.9f), (Gn.Skel, 0.8f), (Gn.Circ, 0.8f), (Gn.Eye, 0.8f), (Gn.Elong, 1.5f), (Gn.Photo, 0f), (Gn.Phago, 0.3f), (Gn.Aerobic, 1f), (Gn.Buoy, 0.3f), (Gn.Egg, 10f) }),
            ("shark", new[] { (Gn.Cells, 28f), (Gn.Diff, 1f), (Gn.Form, 1f), (Gn.Gut, 0.9f), (Gn.Neuro, 0.8f), (Gn.Bilat, 0.9f), (Gn.Skel, 0.8f), (Gn.Circ, 0.8f), (Gn.Eye, 0.8f), (Gn.Elong, 1.5f), (Gn.Photo, 0f), (Gn.Phago, 0.3f), (Gn.Aerobic, 1f), (Gn.Buoy, 0.3f), (Gn.Egg, 10f), (Gn.Jaw, 0.7f), (Gn.Mineral, 0f), (Gn.Limb, 0.5f) }),
            ("bony", new[] { (Gn.Cells, 28f), (Gn.Diff, 1f), (Gn.Form, 1f), (Gn.Gut, 0.9f), (Gn.Neuro, 0.8f), (Gn.Bilat, 0.9f), (Gn.Skel, 0.8f), (Gn.Circ, 0.8f), (Gn.Eye, 0.8f), (Gn.Elong, 1.5f), (Gn.Photo, 0f), (Gn.Phago, 0.3f), (Gn.Aerobic, 1f), (Gn.Buoy, 0.3f), (Gn.Egg, 10f), (Gn.Jaw, 0.7f), (Gn.Mineral, 0.6f), (Gn.Limb, 0.5f) }),
            ("school", new[] { (Gn.Cells, 28f), (Gn.Diff, 1f), (Gn.Form, 1f), (Gn.Gut, 0.9f), (Gn.Neuro, 0.8f), (Gn.Bilat, 0.9f), (Gn.Skel, 0.8f), (Gn.Circ, 0.8f), (Gn.Eye, 0.8f), (Gn.Elong, 1.5f), (Gn.Photo, 0f), (Gn.Phago, 0.3f), (Gn.Aerobic, 1f), (Gn.Buoy, 0.3f), (Gn.Egg, 10f), (Gn.Jaw, 0.7f), (Gn.Mineral, 0.6f), (Gn.Limb, 0.5f), (Gn.School, 0.8f), (Gn.Cells, 24f) }),
        };
        if (Environment.GetEnvironmentVariable("PRE") is string pre)   // 먼저 동물 플랑크톤(벌레형·자포형)을 넣어 자리 잡게 한 뒤 나머지를 넣는다(먹이 사슬 순서)
        {
            foreach (var (name, set) in steps.Where(x => x.name == "worm" || x.name == "medusa"))
            { var g = (float[])par.g.Clone(); g[Gn.Attach] = 0; g[Gn.Size] = 3.3f; g[Gn.Buoy] = 0; foreach (var (j, v) in set) g[j] = v; w.Inject(par.slot, g, 1e-5); }
            for (int t = 0; t < int.Parse(pre) * 12; t++) w.Step();
            par = w.live.Select(i => w.strains[i]).OrderByDescending(x => x.total).First();   // 그새 합쳐졌을 수 있다
            Console.WriteLine($"# pre {pre}d: " + string.Join(" ", w.live.Select(i => w.strains[i]).Where(s => Taxa.Of(s) >= Look.Polyp).Select(s => $"{Taxa.Name(s)} {s.total:G3}")));
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
            Console.WriteLine($"     {name} Topt {st.g[Gn.Topt]:F1} Tb {st.g[Gn.Tbreadth]:F1} rho {st.rho:F0} Tsurf {w.T[0]:F1} Tbot {w.T[w.Bot(0)]:F1} perfS {World.ThermalPerf(st.g, w.T[0]):F2} perfB {World.ThermalPerf(st.g, w.T[w.Bot(0)]):F2}");
            for (int k = 0; k < st.K; k++) { var m0 = w.MorphAt(st, 0, k, 0); Console.WriteLine($"       k{k} sink {m0.sink:G3} m/h speed {m0.speed * 3600e-6:G3} m/h gapeB {m0.gapeB:G3} Fc {m0.Fc:G3} pcrit {m0.pcrit:G3}"); }
            for (int k = 0; k < st.K; k++)
            {
                int bv = 0; double bc = 0; for (int v = 0; v < World.V; v++) { double c = w.cnt[World.Ix(st.slot, v, k)]; if (c > bc) { bc = c; bv = v; } }
                var m = w.M(st.slot, bv, k); int q = World.Ix(st.slot, bv, k);
                w.Budget(st, bv, ref m, w.gP[q], w.gB[q], B);
                Console.WriteLine($"     {name} k{k} C {st.C[k]:G3} L {m.L:G3}um n_tot {Enumerable.Range(0, World.V).Sum(v => w.cnt[World.Ix(st.slot, v, k)]):G3} @r{bv % World.LS}y{bv / World.LS} mu {w.mu[q]:G3}/h kill {w.kR[q]:G3}/h | gain osm {B[0]:G3} ph {B[1]:G3} bac {B[2]:G3} pred {B[3]:G3} | basal {B[4]:G3} traits {B[5]:G3} body {B[6]:G3} osmo {B[7] + B[8]:G3} uv {B[9]:G3} o2 {B[10]:G3} temp {B[11]:G3} net {B[13]:G3}");
            }
        }
        double Bio(Strain st) { double b = 0; for (int v = 0; v < World.V; v++) for (int k = 0; k < st.K; k++) b += w.cnt[World.Ix(st.slot, v, k)] * st.C[k]; return b; }
        var b0 = list.ToDictionary(x => x.Item2.id, x => Bio(x.Item2));
        Console.WriteLine($"# path probe parent id{par.id} n={par.total:G3}");
        for (int t = 0; t < 12; t++) w.Step();
        if (Environment.GetEnvironmentVariable("DBG") is string dn) foreach (var (name, st) in list) if (name == dn) Dbg(name, st);
        var last = list.ToDictionary(x => x.Item2.id, x => (day: 0, bio: b0[x.Item2.id]));   // 멸종한 계통은 마지막으로 살아 있던 날까지로 잰다(예전엔 1e-30을 넣어 −59%/일 같은 허수가 나왔다)
        for (int t = 12; t < days * 12; t++)
        {
            w.Step();
            if (t % 12 == 0) foreach (var (_, st) in list) if (w.strains[st.slot] == st) last[st.id] = (t / 12, Bio(st));
            if (t % 120 == 0 && Environment.GetEnvironmentVariable("TL") != null)
                Console.WriteLine($"   t{t / 12,4}d " + string.Join(" ", list.Select(x => w.strains[x.Item2.slot] != x.Item2 ? $"{x.Item1}:-" : $"{x.Item1}:" + string.Join(",", Enumerable.Range(0, x.Item2.K).Select(k => x.Item2.stage[k] < 1 ? "0" : Math.Log10(x.Item2.stage[k]).ToString("F0"))))));
            if (t == 12 * int.Parse(Environment.GetEnvironmentVariable("DBGT") ?? "100") && Environment.GetEnvironmentVariable("DBG") is string dn2) foreach (var (name, st) in list) if (name == dn2 && w.strains[st.slot] == st) Dbg(name, st);
        }
        foreach (var (name, st) in list)
        {
            var m = w.MorphT(st, 15, st.K - 1, 0);
            bool alive = w.strains[st.slot] == st; var (ld, lb) = alive ? (days, Bio(st)) : last[st.id];
            string stg = alive ? string.Join(",", Enumerable.Range(0, st.K).Select(k => st.stage[k] < 1 ? "0" : Math.Log10(st.stage[k]).ToString("F0"))) : "";
            Console.WriteLine($"  {name,-9} {Taxa.Name(st),-6} K{st.K} adult L {m.L:G3}um pcrit {m.pcrit:F1}% speed {m.speed:G3}um/s  bio growth {Math.Log(Math.Max(1e-30, lb) / b0[st.id]) / Math.Max(1, ld) * 100:+0.0;-0.0}%/일  {(alive ? "alive " + stg : $"멸종 {ld + 1}일째")}");
        }
    }

    static readonly double[] profLast = new double[6]; static long tickLast;   // PERF: 지난 보고 뒤 단계별 ms/틱
    static void Report(World w, int d, double ms)
    {
        var m = w.MeanGenes();
        int alive = w.species.Values.Count(s => s.total > 0);
        int maxG = w.live.Select(s => Taxa.Of(w.strains[s])).Distinct().Count();   // 지금 있는 몸 설계 수
        double maxL = 0, maxC = 0; Strain big = null;
        foreach (int s in w.live) { var st = w.strains[s]; if (st.total < 10) continue; double L = w.AdultL(st); if (L > maxL) { maxL = L; big = st; } maxC = Math.Max(maxC, st.C[st.K - 1]); }
        double ds = 0, db = 0, ns = 0, nb = 0, bs = 0, cy = 0, pm = 0, dm = 0, a = 0;   // 표층·바닥 평균(해역 넓이 가중)
        for (int c = 0; c < w.geo.NO; c++) { double ar = w.geo.rArea[c]; a += ar; ds += ar * w.dox[c]; db += ar * w.dox[w.Bot(c)]; ns += ar * w.nut[c]; nb += ar * w.nut[w.Bot(c)]; bs += ar * w.bac[c]; cy += ar * w.cya[c]; pm += ar * w.pom[c]; dm += ar * w.dom[c]; }
        ds /= a; db /= a; ns /= a; nb /= a; bs /= a; cy /= a; pm /= a; dm /= a;
        Console.WriteLine($"d{d,5} ms {ms,5:F2} strains {w.live.Count,3} sp {alive,3} N/mL {w.totalCount / w.volTot,8:G3} bio {w.totalBio / w.volTot,8:G3} " +
            $"DO {ds:F1}/{db:F1} nut {ns:F2}/{nb:F2} bac {bs:G2} cya {cy:G2} pom {pm:F2} dom {dm:F2} looks {maxG} maxL {maxL:G3}um maxC {maxC:G3}");
        if (Environment.GetEnvironmentVariable("PERF") != null)   // 마지막 틱의 칸별 부하: 항목 수(최대·평균·NI에 닿은 칸), 포식 짝 수, 칸 시간(최대·합) → 병렬 효율
        {
            double f = 1000.0 / Stopwatch.Frequency; int occ = 0, full = 0, nMax = 0; long pr = 0, tMax = 0, tSum = 0, nSum = 0, tg = 0, tp = 0;
            for (int v = 0; v < World.V; v++) { int n = w.vN[v]; if (n > 0) occ++; if (n >= World.NI) full++; nMax = Math.Max(nMax, n); nSum += n; pr += w.vPair[v]; tMax = Math.Max(tMax, w.vT[v]); tSum += w.vT[v]; tg += w.vTg[v]; tp += w.vTp[v]; }
            int dev = w.live.Count(s => w.strains[s].dev); double kAvg = w.live.Average(s => (double)w.strains[s].K);
            var ph = new double[6]; for (int i = 0; i < 6; i++) { ph[i] = (w.prof[i] - profLast[i]) / Math.Max(1, w.tick - tickLast); profLast[i] = w.prof[i]; }
            tickLast = w.tick;
            Console.WriteLine($"      PHASE ms/tick cache {ph[0]:F1} voxel {ph[1]:F1} move {ph[2]:F1} chem {ph[3]:F1} mut {ph[4]:F1} tot {ph[5]:F1}");
            Console.WriteLine($"      PERF n max {nMax} avg {(occ > 0 ? nSum / occ : 0)} full {full} occ {occ} pairs {pr / 1e3:F0}k voxT max {tMax * f:F2} sum {tSum * f:F1} ms (gather {tg * f:F1} pred {tp * f:F1})  dev {dev} K {kAvg:F1}");
        }
        Console.WriteLine($"      earth T {w.env[Ev.Temp]:F1} (base {w.tBase:F1}) CO2 {w.co2:F0} O2 {w.env[Ev.O2]:F1} bur {w.burRate:G3} wMul {w.wMul:F2} ice {w.ice:F2} sea {w.geo.sea:+0;-0} aer {w.aer:F2} dark {w.dark:F2} regions {w.NR} land {w.geo.LandCells()} regrid {w.regridN} iceR {w.iceR.Take(w.NR).Count(x => x > 0.5f)}");
        if (Environment.GetEnvironmentVariable("BUD") != null && big != null || Environment.GetEnvironmentVariable("BUD") != null && w.live.Count > 0)
        {
            var top = w.live.Select(i => w.strains[i]).OrderByDescending(x => x.total).First(); int bv = 0; double bc = 0;
            for (int v = 0; v < World.V; v++) { double c = w.cnt[World.Ix(top.slot, v, 0)]; if (c > bc) { bc = c; bv = v; } }
            var mo = w.M(top.slot, bv, 0); int q = World.Ix(top.slot, bv, 0); var B = new double[14]; w.Budget(top, bv, ref mo, w.gP[q], w.gB[q], B);
            Console.WriteLine($"      top id{top.id} @r{bv % World.LS}y{bv / World.LS} n {top.total:G3} mu {w.mu[q]:G3}/h kill {w.kR[q]:G3}/h | osm {B[0]:G3} ph {B[1]:G3} bac {B[2]:G3} pred {B[3]:G3} | basal {B[4]:G3} traits {B[5]:G3} body {B[6]:G3} osmo {B[7] + B[8]:G3} uv {B[9]:G3} o2 {B[10]:G3} temp {B[11]:G3} net {B[13]:G3}");
        }
        Console.WriteLine($"      size {m[Gn.Size]:F2} cells {m[Gn.Cells]:F2} photo {m[Gn.Photo]:F2} phago {m[Gn.Phago]:F2} aer {m[Gn.Aerobic]:F2} osmo {m[Gn.Osmo]:F0} topt {m[Gn.Topt]:F1} pig {m[Gn.Pigment]:F2} vac {m[Gn.Vacuole]:F2} wall {m[Gn.Wall]:F2} gut {m[Gn.Gut]:F2} neuro {m[Gn.Neuro]:F2} diff {m[Gn.Diff]:F2}");
        if (Environment.GetEnvironmentVariable("LOOKS") != null)   // 다양성: 종 유효 수(생물량 Shannon), 몸 설계 수, 단세포·군체·발생 몫, 영양 방식 몫(광합성·혼합·종속)
        {
            var bio = new double[3]; var nC = new int[3]; var tro = new double[3]; var spB = new Dictionary<int, double>();
            foreach (int s in w.live)
            {
                var st = w.strains[s]; int c = st.dev ? 2 : st.g[Gn.Cells] >= 1 ? 1 : 0; double b = 0;
                for (int k = 0; k < st.K; k++) b += st.stage[k] * st.C[k] * Math.Pow(st.esd0, 3) * 1e-3;
                bio[c] += b; nC[c]++; spB[st.sp] = spB.GetValueOrDefault(st.sp) + b;
                double ph = st.g[Gn.Photo], pg = st.g[Gn.Phago] * (1 - st.g[Gn.Wall]) * (1 - st.g[Gn.Wall]);
                tro[ph >= 0.5 && pg < 0.3 ? 0 : ph < 0.3 ? 2 : 1] += b;
            }
            double tb = bio.Sum(), H = -spB.Values.Where(x => x > 0).Sum(x => x / tb * Math.Log(x / tb));
            Console.WriteLine($"      LOOKS effSp {Math.Exp(H):F2} uni {bio[0] / tb:P0} ({nC[0]}) col {bio[1] / tb:P0} ({nC[1]}) dev {bio[2] / tb:P0} ({nC[2]}) | auto {tro[0] / tb:P0} mixo {tro[1] / tb:P0} het {tro[2] / tb:P0}");
            var hab = new double[3]; int nL = 0, nS = 0;   // 사는 곳: 바다·조간대·육지 개체 탄소 몫, 대부분 조간대·육지에 사는 계통 수
            foreach (int s in w.live)
            {
                var st = w.strains[s]; var x = new double[3];
                for (int v = 0; v < World.V; v++) { int r = v % World.LS; if (r >= w.NR) continue; for (int k = 0; k < st.K; k++) x[w.geo.rKind[r]] += w.cnt[World.Ix(st.slot, v, k)] * st.C[k]; }
                double t = x.Sum(); for (int i = 0; i < 3; i++) hab[i] += x[i]; if (x[2] > 0.5 * t) nL++; else if (x[1] > 0.5 * t) nS++;
            }
            double ht = Math.Max(1e-30, hab.Sum());
            { int best = -1; double bx = 0; foreach (int s in w.live) { var st = w.strains[s]; double x = 0; for (int v = 0; v < World.V; v++) { int r = v % World.LS; if (r < w.NR && w.geo.rKind[r] > 0) for (int k = 0; k < st.K; k++) x += w.cnt[World.Ix(st.slot, v, k)] * st.C[k]; } if (x > bx) { bx = x; best = s; } }
              if (best >= 0) { var st = w.strains[best]; Console.WriteLine($"      HAB top shore+land strain {Taxa.Name(st)} dry {st.g[Gn.Dry]:F2} cells {st.g[Gn.Cells]:F1} C {bx:G3} of {hab[1] + hab[2]:G3}"); } }
            Console.WriteLine($"      HAB sea {hab[0] / ht:P1} shore {hab[1] / ht:P1} land {hab[2] / ht:P1} | strains shore {nS} land {nL} | land cells {w.geo.LandCells()} | mut try/keep sea {w.mutHab[0]}/{w.mutHab[3]} shore {w.mutHab[1]}/{w.mutHab[4]} land {w.mutHab[2]}/{w.mutHab[5]} | births/ind-tick sea {w.birthHab[0] / Math.Max(1, w.cntHab[0]):G3} shore {w.birthHab[1] / Math.Max(1, w.cntHab[1]):G3} land {w.birthHab[2] / Math.Max(1, w.cntHab[2]):G3} ind {w.cntHab[0]:G3}/{w.cntHab[1]:G3}/{w.cntHab[2]:G3}");
        }
        if (Environment.GetEnvironmentVariable("REG") != null)   // 해역마다: 표층 수온·혼합층·영양염·엽록소·생물량, 바닥 산소, 표층에 가장 많은 계통
            for (int c = 0; c < w.NR; c++)
            {
                double bio = 0, best = 0; Strain top = null;
                foreach (int s in w.live) { var st = w.strains[s]; double n = 0; for (int k = 0; k < st.K; k++) { n += w.cnt[World.Ix(s, c, k)]; bio += w.cnt[World.Ix(s, c, k)] * st.C[k] * Math.Pow(st.esd0 / 10, 3); } if (n > best) { best = n; top = st; } }
                Console.WriteLine($"      r{c,-2} {w.geo.Name(c),-8} {w.geo.rDepth[c],5:0}m lat {w.geo.rLat[c],2:0} SST {w.sst[c],4:F1} MLD {w.mld[c],3:0} nut {w.nut[c],5:F2} chl {w.chl[c],7:G2} bio {bio / w.vol[c],7:G2} DObot {w.dox[w.Bot(c)],4:F1} I {w.I[c]:F2} top id{top?.id} {(top != null ? Taxa.Name(top) : "")} size {top?.g[Gn.Size]:F1} photo {top?.g[Gn.Photo]:F2} topt {top?.g[Gn.Topt]:F0} dry {top?.g[Gn.Dry]:F2}");
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
            Console.WriteLine($"      biggest: {Taxa.Name(big)} n={big.total:G3} cells {g[Gn.Cells]:F1} K {big.K} diff {g[Gn.Diff]:F2} gut {g[Gn.Gut]:F2} neuro {g[Gn.Neuro]:F2} bilat {g[Gn.Bilat]:F2} circ {g[Gn.Circ]:F2} skel {g[Gn.Skel]:F2} egg {g[Gn.Egg]:F1}");
        }
    }
}
