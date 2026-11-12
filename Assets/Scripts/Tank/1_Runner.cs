// 1단계 — 시뮬레이션 스레드. 화면(메인 스레드)과 따로 돌아서 창을 내리거나 다른 작업 공간에 있어도 계속 간다.
// 화면은 잠금 없이 '사진'(Snap)만 읽는다. 화면에서 바꾸는 것(손잡이·사건)은 cmds 줄에 넣으면 틱 사이에 처리한다.
// 자동 저장: 5분마다(실제 시간) 행성 전체를 savePath에 쓴다(Life/11_Save). 시뮬레이션은 메모리에 담는 동안만 멈추고 압축·쓰기는 뒤 스레드가 한다. 앱을 내리거나 끌 때도 화면 쪽이 SaveNow(true)를 부른다.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Life;

namespace Tank
{
    // 종 하나의 사진. 멸종한 종은 한 번 만든 뒤 그대로 쓴다
    public sealed class SpView
    {
        public int id, parent, clade, born, died, strains;
        public string name; public Role role;
        public double biomass, peak;
        public float[] g;
        // 형질·몸은 처음 볼 때 만든다: 멸종한 종 대부분은 아무도 안 본다. Traits.Of가 종 하나에 ~1.5 ms(몸 배치 GeoOf)라
        // 첫 사진에서 다 만들면 2만 종 지구(11000일)가 Mono에서 230초 멈췄다
        Traits tr; Body b;
        public Traits t { get => tr ??= g != null ? Traits.Of(g) : null; set => tr = value; }
        public Body body { get => b ??= t != null ? Body.Of(g, t) : null; set => b = value; }
        public double[] income;                 // 빛·화학·산 먹이·사체·녹은 유기물(오늘)
        public double[] energy;                 // 쓴 ATP: 빛·화학·유기물 태우기(오늘)
        public int[] where; public float[] share;   // 많이 사는 칸(최대 8)과 그 몫
        public float[] conc;                    // 칸별 생물량 농도(μM C): 물속 탐방에서 보이는 마리 수
        public bool Alive => died < 0;
    }

    // 종 사진 표: 사진마다 종 수만큼 새 배열을 잡지 않고 시뮬 쪽 캐시를 같이 본다(18만 종 지구에서 사진마다 1.5 MB → 앱 GC).
    // 멸종한 종의 칸은 한 번 쓴 뒤 안 바뀐다. 살아 있는 종의 칸은 다음 사진이 새 SpView로 갈아 끼울 수 있다(칸 하나는 늘 온전한 한 사진)
    public sealed class SpTable
    {
        readonly SpView[] a; public readonly int Length;
        public SpTable(SpView[] a, int n) { this.a = a; Length = n; }
        public SpView this[int i] => a[i];
    }

    public sealed class Snap
    {
        public double day, O2, CO2, CH4, T, rate;   // rate: 지금 속도(일/초)
        public int nStrain, nSp;
        public SpTable sp;                      // id 순, 멸종 포함
        public int[] alive;                     // 살아 있는 종 id
        public Clade[] clades;
        public float[] vO2, vH2S, vFe2, vLight, vT, vPOC; // 칸별 화학(μM)·빛·수온(°C)·사체 입자(μM C, 바다눈)
        public float[] ice, ventMul;
        public string[] fx;                     // 지금 남은 사건 효과(한 줄씩)
        public string lim;                      // 어제 광합성을 막은 것(Planet.PhotoLimits)
    }

    // 시뮬레이션 일꾼 스레드: 우선순위를 낮춘 전용 스레드들(Linux·안드로이드 nice 10, 윈도우 BelowNormal). 컴퓨터가 붐빌 때(다른 프로그램·실험)
    // 시뮬이 화면 스레드의 CPU를 뺏지 않게 하고, 한가하면 그대로 다 쓴다. 바쁜 루프 16개와 같이 재 보니 바꾸기 전 프레임 최대 55–250 ms
    static class Low
    {
        public static readonly TaskScheduler Sched = new S(Math.Max(1, Environment.ProcessorCount - 2));
        [ThreadStatic] static bool mine;
        [DllImport("libc")] static extern int gettid();
        [DllImport("libc")] static extern int setpriority(int which, int who, int prio);
        public static void Me()
        {
            mine = true;
            try { Thread.CurrentThread.Priority = System.Threading.ThreadPriority.BelowNormal; } catch { }
            if (UnityEngine.Application.platform is UnityEngine.RuntimePlatform.LinuxPlayer or UnityEngine.RuntimePlatform.Android)
                try { setpriority(0, gettid(), 10); } catch (Exception e) { UnityEngine.Debug.LogWarning("[EvoTank] 시뮬 스레드 우선순위 못 낮춤: " + e.Message); }   // Linux는 nice가 스레드마다
        }
        sealed class S : TaskScheduler
        {
            readonly BlockingCollection<Task> q = new(); readonly int n;
            public S(int n) { this.n = n; for (int i = 0; i < n; i++) new Thread(() => { Me(); foreach (var t in q.GetConsumingEnumerable()) TryExecuteTask(t); }) { IsBackground = true, Name = "life " + i }.Start(); }
            protected override void QueueTask(Task t) => q.Add(t);
            protected override bool TryExecuteTaskInline(Task t, bool queued) => mine && TryExecuteTask(t);
            protected override IEnumerable<Task> GetScheduledTasks() => q.ToArray();
            public override int MaximumConcurrencyLevel => n;
        }
    }

    public sealed class Runner
    {
        public readonly Planet pl;
        public volatile Snap snap;
        public double speed;                    // 목표 속도(일/초), 0 = 최대
        public volatile bool paused;
        public readonly string savePath;        // null = 저장 안 함(시드·사진 시험 실행)
        const double SAVE_EVERY = 300;          // 자동 저장 간격(초, 실제 시간). 내릴 때·끌 때는 따로 저장하므로 갑자기 꺼질 때만 이만큼 잃는다
        public readonly ConcurrentQueue<Action<Planet>> cmds = new();
        public readonly ConcurrentQueue<DayStat> days = new();
        public readonly ConcurrentQueue<Event> news = new();
        readonly Thread th; volatile bool stop;
        int[] lastAlive = new int[0]; int lastNs;   // 지난 사진의 살아 있는 종·종 수: 그 뒤 바뀔 수 있는 종은 이것뿐이다(멸종 종 30만은 다시 안 훑음)
        SpView[] cache = new SpView[0]; Clade[] cladeArr = new Clade[0];
        // 사진 만들 때 쓰고 돌려받는 것(시뮬레이션 스레드만): 사진마다 종 수 × 칸 수 배열을 새로 잡으면 앱이 GC로 끊겼다
        readonly Dictionary<int, double[]> accMap = new(); readonly Stack<double[]> accPool = new(); readonly int[] topBuf = new int[8];
        int nDays, nEv;

        public Runner(Planet pl, string savePath)
        {
            this.pl = pl; this.savePath = savePath;
            pl.po.TaskScheduler = Low.Sched;
            MakeSnap(0);
            th = new Thread(Loop) { IsBackground = true, Name = "life" };
            th.Start();
        }

        public void Stop() { stop = true; th.Join(2000); }

        // 틱 사이에만 담는다(gate): 시뮬레이션 스레드의 자동 저장, 화면 스레드의 '내릴 때·끌 때' 저장(wait = 파일까지 다 쓰고 돌아옴) 둘 다.
        // 시뮬레이션은 메모리에 담는 동안만 멈추고, 압축·파일 쓰기는 낮은 우선순위 스레드가 뒤에서 한다(전엔 18만 종 세계에서 2분마다 4–7초 멈춤)
        readonly object saveLock = new(); MemoryStream saveBuf; Thread saver;
        public void SaveNow(bool wait = false)
        {
            if (savePath == null) return;
            lock (saveLock)
                try
                {
                    saver?.Join();   // 앞 저장이 아직 쓰는 중이면 기다림(버퍼를 같이 쓴다)
                    var sw = Stopwatch.StartNew(); double day;
                    lock (pl.gate) { saveBuf = Save.Snap(pl, speed, saveBuf); day = pl.Day; }
                    long snapMs = sw.ElapsedMilliseconds; var m = saveBuf;
                    saver = new Thread(() =>
                    {
                        Low.Me();
                        try { Save.Flush(m, savePath); UnityEngine.Debug.Log($"[EvoTank] 저장 {day:0}일 · 멈춤 {snapMs} ms · 다 씀 {sw.ElapsedMilliseconds} ms · {m.Length / 1e6:0} MB"); }
                        catch (Exception e) { UnityEngine.Debug.LogError("[EvoTank] 저장 실패: " + e.Message); }
                    }) { IsBackground = true, Name = "save" };
                    saver.Start();
                    if (wait) saver.Join();
                }
                catch (Exception e) { UnityEngine.Debug.LogError("[EvoTank] 저장 실패: " + e.Message); }
        }

        void Loop()
        {
            Low.Me();
            var sw = Stopwatch.StartNew(); double t0 = 0, d0 = pl.Day, lastSnap = 0, rate = 0, rT = 0, rD = pl.Day, lastSave = 0, savedDay = pl.Day;
            while (!stop)
            {
                if (!cmds.IsEmpty) lock (pl.gate) while (cmds.TryDequeue(out var a)) a(pl);
                double now = sw.Elapsed.TotalSeconds;
                if (savePath != null && now - lastSave > SAVE_EVERY && pl.Day != savedDay) { SaveNow(); lastSave = now; savedDay = pl.Day; }
                pl.po.MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 2);   // 화면 몫으로 둘을 남긴다
                if (paused) { Thread.Sleep(30); if (now - lastSnap > 0.3) { MakeSnap(rate = 0); lastSnap = now; } t0 = now; d0 = pl.Day; continue; }
                // 목표 속도보다 앞서면 쉰다
                if (speed > 0 && pl.Day - d0 > (now - t0) * speed) { Thread.Sleep(5); continue; }
                if (now - t0 > 2) { t0 = now; d0 = pl.Day; }
                try { pl.Step(); }
                catch (Exception e) { UnityEngine.Debug.LogError("[EvoTank] 시뮬레이션 오류: " + e); paused = true; continue; }
                while (nDays < pl.history.Count) days.Enqueue(pl.history[nDays++]);
                while (nEv < pl.events.Count) news.Enqueue(pl.events[nEv++]);
                if (now - rT > 1) { rate = (pl.Day - rD) / (now - rT); rT = now; rD = pl.Day; }
                if (now - lastSnap > 0.15) { MakeSnap(rate); lastSnap = now; }
            }
        }

        // 사진 찍기: 살아 있는 종마다 칸별 생물량을 모아 가장 많은 칸 8개를 고른다
        void MakeSnap(double rate) { long t = Perf.T(); Snap1(rate); Perf.Add(7, t); }
        void Snap1(double rate)
        {
            var p = pl; int V = p.V;
            var s = new Snap { day = p.Day, O2 = p.O2PAL, CO2 = p.CO2PAL, CH4 = p.CH4ppm, T = p.Tglobal, rate = rate, nStrain = p.alive.Count, nSp = p.nAliveSp };
            int ns = p.species.Count;
            if (cache.Length < ns) Array.Resize(ref cache, Math.Max(ns, cache.Length * 2));
            var acc = accMap; acc.Clear();
            foreach (var st in p.alive)
            {
                if (st.species < 0) continue;
                if (!acc.TryGetValue(st.species, out var a)) { a = accPool.Count > 0 && accPool.Peek().Length == V ? accPool.Pop() : new double[V]; Array.Clear(a, 0, V); acc[st.species] = a; }
                for (int v = 0; v < V; v++) a[v] += p.B[st.slot * V + v] * p.vol[v];
            }
            var alive = new List<int>();
            for (int ix = 0; ix < lastAlive.Length + ns - lastNs; ix++)   // 지난번에 살아 있던 종, 그다음 새 종(번호 차례 그대로)
            {
                int i = ix < lastAlive.Length ? lastAlive[ix] : lastNs + ix - lastAlive.Length;
                var sp = p.species[i]; var old = cache[i];
                if (!sp.Alive && old != null && !old.Alive) continue;
                var w = new SpView { id = sp.id, parent = sp.parent, clade = sp.clade, born = sp.born, died = sp.died, strains = sp.strains, name = sp.name, role = sp.role, biomass = sp.biomass, peak = sp.peak, g = sp.g, income = (double[])sp.income.Clone(), energy = (double[])sp.energy.Clone() };
                var rep = sp.rep >= 0 ? p.strains[sp.rep] : null;
                if (rep != null) { w.body = rep.b; w.t = rep.t; }
                else if (old != null) { w.body = old.body; w.t = old.t; }
                if (sp.Alive && acc.TryGetValue(sp.id, out var a))
                {
                    double tot = 0; for (int v = 0; v < V; v++) tot += a[v];
                    int nt = 0; var tb = topBuf;   // 가장 많은 8칸(많은 차례): 다 정렬하지 않고 8칸짜리 줄에 끼워 넣는다
                    for (int v = 0; v < V; v++)
                    {
                        if (!(a[v] > 0) || nt == 8 && a[v] <= a[tb[7]]) continue;
                        int k = nt < 8 ? nt++ : 7; while (k > 0 && a[tb[k - 1]] < a[v]) { tb[k] = tb[k - 1]; k--; }
                        tb[k] = v;
                    }
                    w.where = new int[nt]; Array.Copy(tb, w.where, nt); w.share = new float[nt];
                    w.conc = new float[V]; for (int v = 0; v < V; v++) if (p.vol[v] > 0) w.conc[v] = (float)(a[v] / p.vol[v]);
                    for (int k = 0; k < w.where.Length; k++) w.share[k] = (float)(a[w.where[k]] / Math.Max(1e-300, tot));
                }
                cache[i] = w;
                if (sp.Alive) alive.Add(i);
            }
            foreach (var a in acc.Values) accPool.Push(a);
            s.sp = new SpTable(cache, ns);
            s.alive = lastAlive = alive.ToArray(); lastNs = ns;
            if (cladeArr.Length != p.clades.Count) cladeArr = p.clades.ToArray();   // 갈래는 붙기만 한다(같은 객체를 화면이 읽음)
            s.clades = cladeArr;
            s.vO2 = new float[V]; s.vH2S = new float[V]; s.vFe2 = new float[V]; s.vLight = new float[V]; s.vT = new float[V]; s.vPOC = new float[V];
            for (int v = 0; v < V; v++) { s.vO2[v] = (float)p.pool[P.O2][v]; s.vH2S[v] = (float)p.pool[P.H2S][v]; s.vFe2[v] = (float)p.pool[P.FE2][v]; s.vLight[v] = (float)p.light[v]; s.vT[v] = (float)p.temp[v]; s.vPOC[v] = (float)(p.pool[P.POC0][v] + p.pool[P.POC1][v] + p.pool[P.POC2][v]); }
            s.ice = p.ice != null ? Array.ConvertAll(p.ice, x => (float)x) : new float[p.NR];
            s.ventMul = Array.ConvertAll(p.ventMul, x => (float)x);
            s.fx = p.Effects().ToArray(); s.lim = p.PhotoLimits();
            snap = s;
        }
    }
}
