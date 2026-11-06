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
        // 처음엔 원시 바다에서 시작해 목표 환경으로 천천히 바꾼다(진화적 구조)
        var tg = Sc[sc]; for (int e = 0; e < Ev.N; e++) w.SetTarget(e, tg[e]);
        var sw = Stopwatch.StartNew(); double ms = 0;
        Console.WriteLine($"# {sc} seed {seed} days {days}");
        for (int d = 1; d <= days; d++)
        {
            var s1 = Stopwatch.StartNew();
            for (int h = 0; h < 24; h++) w.Step();
            ms = s1.Elapsed.TotalMilliseconds / 24;
            if (d % every == 0 || d == days) Report(w, d, ms);
            if (w.live.Count == 0) { Console.WriteLine("ALL EXTINCT"); break; }
        }
        Console.WriteLine($"# wall {sw.Elapsed.TotalSeconds:F1}s  prof(ms/tick) cache {w.prof[0]/w.tick:F2} voxel {w.prof[1]/w.tick:F2} move {w.prof[2]/w.tick:F2} chem {w.prof[3]/w.tick:F2} mut {w.prof[4]/w.tick:F2} tot {w.prof[5]/w.tick:F2}");
        foreach (var e in w.events.Where(e => e.type == "grade")) Console.WriteLine($"  [{World.Time(e.t)}] {e.text}");
        return 0;
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
        if (Environment.GetEnvironmentVariable("TOP") != null)
            foreach (var st in w.live.Select(i => w.strains[i]).OrderByDescending(x => x.total).Take(5))
            {
                var diffs = Enumerable.Range(0, Gn.N).Where(i => Math.Abs(st.g[i] - Gn.Def[i].init) > Gn.Def[i].sd).Select(i => $"{Gn.Def[i].k}={st.g[i]:G3}");
                Console.WriteLine($"      top id{st.id} sp{st.sp} n={st.total:G3} trend {st.trend * 1000:F2}e-3 age {World.Time(w.tick - st.born)} | {string.Join(" ", diffs)}");
            }
        if (big != null && big.g[Gn.Cells] >= 1)
        {
            var g = big.g;
            Console.WriteLine($"      biggest: {World.GradeName[World.Grade(big)]} n={big.total:G3} cells {g[Gn.Cells]:F1} K {big.K} diff {g[Gn.Diff]:F2} gut {g[Gn.Gut]:F2} neuro {g[Gn.Neuro]:F2} bilat {g[Gn.Bilat]:F2} circ {g[Gn.Circ]:F2} skel {g[Gn.Skel]:F2} egg {g[Gn.Egg]:F1}");
        }
    }
}
