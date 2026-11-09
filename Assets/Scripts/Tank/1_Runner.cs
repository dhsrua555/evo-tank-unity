// 1단계 — 시뮬레이션 스레드. 화면(메인 스레드)과 따로 돌아서 창을 내리거나 다른 작업 공간에 있어도 계속 간다.
// 화면은 잠금 없이 '사진'(Snap)만 읽는다. 화면에서 바꾸는 것(손잡이·사건)은 cmds 줄에 넣으면 틱 사이에 처리한다.
// 태블릿: 앱이 멈춰 있던 동안 못 간 날을 다시 열 때 최대 속도로 따라잡는다(ffDays). 따라잡는 동안은 코어를 다 쓰고 사진을 1초에 한 번만 찍는다(화면도 3D를 멈춘다).
// 자동 저장: 2분마다(실제 시간) 행성 전체를 savePath에 쓴다(Life/11_Save). 앱을 내리거나 끌 때도 화면 쪽이 SaveNow를 부른다.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Life;

namespace Tank
{
    // 종 하나의 사진. 멸종한 종은 한 번 만든 뒤 그대로 쓴다
    public sealed class SpView
    {
        public int id, parent, clade, born, died, strains;
        public string name; public Role role;
        public double biomass, peak;
        public Body body; public Traits t; public float[] g;
        public double[] income;                 // 빛·화학·산 먹이·사체·녹은 유기물(오늘)
        public double[] energy;                 // 쓴 ATP: 빛·화학·유기물 태우기(오늘)
        public int[] where; public float[] share;   // 많이 사는 칸(최대 8)과 그 몫
        public float[] conc;                    // 칸별 생물량 농도(μM C): 물속 탐방에서 보이는 마리 수
        public bool Alive => died < 0;
    }

    public sealed class Snap
    {
        public double day, O2, CO2, CH4, T, rate;   // rate: 지금 속도(일/초)
        public int nStrain, nSp;
        public SpView[] sp;                     // id 순, 멸종 포함
        public int[] alive;                     // 살아 있는 종 id
        public Clade[] clades;
        public float[] vO2, vH2S, vFe2, vLight, vT, vPOC; // 칸별 화학(μM)·빛·수온(°C)·사체 입자(μM C, 바다눈)
        public float[] ice, ventMul;
        public string[] fx;                     // 지금 남은 사건 효과(한 줄씩)
        public string lim;                      // 어제 광합성을 막은 것(Planet.PhotoLimits)
    }

    public sealed class Runner
    {
        public readonly Planet pl;
        public volatile Snap snap;
        public double speed;                    // 목표 속도(일/초), 0 = 최대
        public volatile bool paused;
        public double ffDays;                   // 따라잡을 날(최대 속도로)
        public readonly string savePath;        // null = 저장 안 함(시드·사진 시험 실행)
        const double SAVE_EVERY = 120;          // 자동 저장 간격(초, 실제 시간)
        public readonly ConcurrentQueue<Action<Planet>> cmds = new();
        public readonly ConcurrentQueue<DayStat> days = new();
        public readonly ConcurrentQueue<Event> news = new();
        readonly Thread th; volatile bool stop;
        SpView[] cache = new SpView[0];
        int nDays, nEv;

        public Runner(Planet pl, string savePath)
        {
            this.pl = pl; this.savePath = savePath;
            MakeSnap(0);
            th = new Thread(Loop) { IsBackground = true, Name = "life" };
            th.Start();
        }

        public void Stop() { stop = true; th.Join(2000); }

        // 앱이 내려가 있거나 꺼져 있던 동안 따라잡을 속도(일/초): 정한 속도, '최대'면 지금 잰 속도, 멈춤이면 0
        public double CatchRate => paused ? 0 : speed > 0 ? speed : Math.Max(1, snap?.rate ?? 1);

        // 틱 사이에만 쓴다(gate): 시뮬레이션 스레드의 자동 저장, 화면 스레드의 '내릴 때·끌 때' 저장 둘 다
        public void SaveNow()
        {
            if (savePath == null) return;
            try { var sw = Stopwatch.StartNew(); lock (pl.gate) Save.Write(pl, savePath, CatchRate); UnityEngine.Debug.Log($"[EvoTank] 저장 {pl.Day:0}일 · {sw.ElapsedMilliseconds} ms"); }
            catch (Exception e) { UnityEngine.Debug.LogError("[EvoTank] 저장 실패: " + e.Message); }
        }

        void Loop()
        {
            var sw = Stopwatch.StartNew(); double t0 = 0, d0 = pl.Day, lastSnap = 0, rate = 0, rT = 0, rD = pl.Day, lastSave = 0, savedDay = pl.Day;
            while (!stop)
            {
                if (!cmds.IsEmpty) lock (pl.gate) while (cmds.TryDequeue(out var a)) a(pl);
                double now = sw.Elapsed.TotalSeconds;
                if (savePath != null && now - lastSave > SAVE_EVERY && pl.Day != savedDay) { SaveNow(); lastSave = now; savedDay = pl.Day; }
                bool ff = ffDays > 0;
                pl.po.MaxDegreeOfParallelism = ff ? Environment.ProcessorCount : Math.Max(1, Environment.ProcessorCount - 2);   // 평소엔 화면 몫으로 둘을 남긴다
                if (paused && !ff) { Thread.Sleep(30); if (now - lastSnap > 0.3) { MakeSnap(rate = 0); lastSnap = now; } t0 = now; d0 = pl.Day; continue; }
                // 목표 속도보다 앞서면 쉰다
                if (!ff && speed > 0 && pl.Day - d0 > (now - t0) * speed) { Thread.Sleep(5); continue; }
                if (now - t0 > 2) { t0 = now; d0 = pl.Day; }
                double before = pl.Day;
                try { pl.Step(); }
                catch (Exception e) { UnityEngine.Debug.LogError("[EvoTank] 시뮬레이션 오류: " + e); paused = true; continue; }
                if (ff) ffDays = Math.Max(0, ffDays - (pl.Day - before));
                while (nDays < pl.history.Count) days.Enqueue(pl.history[nDays++]);
                while (nEv < pl.events.Count) news.Enqueue(pl.events[nEv++]);
                if (now - rT > 1) { rate = (pl.Day - rD) / (now - rT); rT = now; rD = pl.Day; }
                if (now - lastSnap > (ff ? 1 : 0.15)) { MakeSnap(rate); lastSnap = now; }
            }
        }

        // 사진 찍기: 살아 있는 종마다 칸별 생물량을 모아 가장 많은 칸 8개를 고른다
        void MakeSnap(double rate)
        {
            var p = pl; int V = p.V;
            var s = new Snap { day = p.Day, O2 = p.O2PAL, CO2 = p.CO2PAL, CH4 = p.CH4ppm, T = p.Tglobal, rate = rate, nStrain = p.alive.Count, nSp = p.nAliveSp };
            int ns = p.species.Count;
            if (cache.Length < ns) Array.Resize(ref cache, Math.Max(ns, cache.Length * 2));
            var acc = new Dictionary<int, double[]>();
            foreach (var st in p.alive)
            {
                if (st.species < 0) continue;
                if (!acc.TryGetValue(st.species, out var a)) acc[st.species] = a = new double[V];
                for (int v = 0; v < V; v++) a[v] += p.B[st.slot * V + v] * p.vol[v];
            }
            var alive = new List<int>();
            for (int i = 0; i < ns; i++)
            {
                var sp = p.species[i]; var old = cache[i];
                if (!sp.Alive && old != null && !old.Alive) continue;
                var w = new SpView { id = sp.id, parent = sp.parent, clade = sp.clade, born = sp.born, died = sp.died, strains = sp.strains, name = sp.name, role = sp.role, biomass = sp.biomass, peak = sp.peak, g = sp.g, income = (double[])sp.income.Clone(), energy = (double[])sp.energy.Clone() };
                var rep = sp.rep >= 0 ? p.strains[sp.rep] : null;
                if (rep != null) { w.body = rep.b; w.t = rep.t; }
                else if (old != null) { w.body = old.body; w.t = old.t; }
                else { w.t = Traits.Of(sp.g); w.body = Body.Of(sp.g, w.t); }
                if (sp.Alive && acc.TryGetValue(sp.id, out var a))
                {
                    double tot = 0; for (int v = 0; v < V; v++) tot += a[v];
                    var top = new List<int>();
                    for (int v = 0; v < V; v++) if (a[v] > 0) top.Add(v);
                    top.Sort((x, y) => a[y].CompareTo(a[x]));
                    if (top.Count > 8) top.RemoveRange(8, top.Count - 8);
                    w.where = top.ToArray(); w.share = new float[w.where.Length];
                    w.conc = new float[V]; for (int v = 0; v < V; v++) if (p.vol[v] > 0) w.conc[v] = (float)(a[v] / p.vol[v]);
                    for (int k = 0; k < w.where.Length; k++) w.share[k] = (float)(a[w.where[k]] / Math.Max(1e-300, tot));
                }
                cache[i] = w;
                if (sp.Alive) alive.Add(i);
            }
            s.sp = new SpView[ns]; Array.Copy(cache, s.sp, ns);
            s.alive = alive.ToArray();
            s.clades = p.clades.ToArray();
            s.vO2 = new float[V]; s.vH2S = new float[V]; s.vFe2 = new float[V]; s.vLight = new float[V]; s.vT = new float[V]; s.vPOC = new float[V];
            for (int v = 0; v < V; v++) { s.vO2[v] = (float)p.pool[P.O2][v]; s.vH2S[v] = (float)p.pool[P.H2S][v]; s.vFe2[v] = (float)p.pool[P.FE2][v]; s.vLight[v] = (float)p.light[v]; s.vT[v] = (float)p.temp[v]; s.vPOC[v] = (float)(p.pool[P.POC0][v] + p.pool[P.POC1][v] + p.pool[P.POC2][v]); }
            s.ice = p.ice != null ? Array.ConvertAll(p.ice, x => (float)x) : new float[p.NR];
            s.ventMul = Array.ConvertAll(p.ventMul, x => (float)x);
            s.fx = p.Effects().ToArray(); s.lim = p.PhotoLimits();
            snap = s;
        }
    }
}
