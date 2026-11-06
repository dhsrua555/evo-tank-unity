// 진화 수조 3D. 시뮬레이션(Evo.World)은 물기둥 전체(10 m × 10 m × 20층 × 4기둥)의 모든 생물을 확률적으로 계산하고,
// 화면은 물속을 헤엄치는 관찰자 둘레(시야 폭 B m)를 보여 준다(Swim.cs). 개체는 그 위치의 실제 밀도에서 뽑는다(농축 배율은 화면에 표시).
// 축척: 처음 한 번만 지금 보는 곳의 가장 큰 생물에 맞춘다(B ≈ 몸길이 × 6). 그 뒤 축척은 사용자(휠·F·종 고르기)만 바꾼다.
// 상자 안 개체 수는 실제 밀도 × 농축 배율. 배율은 개체 수가 NMIN–NMAX를 벗어날 때만 바꿔서, 축소하면 더 많이·확대하면 더 적게 보인다.
// 보는 법: 시야가 2 mm보다 작으면 암시야 현미경, 크면 물속. 시야에 맞는 크기(시야의 0.6–50%)만 몸을 그리고 더 작은 것은 점.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Evo;

public partial class EvoTank : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { if (!FindAnyObjectByType<EvoTank>()) new GameObject("EvoTank").AddComponent<EvoTank>(); }

    const float BOX = 10, H = BOX / 2;   // 상자 한 변(화면 단위)
    const int NMIN = 40, NMAX = 200, PER = 200;   // 화면 개체 수 범위, 한 계통·단계당 최대
    const float MICRO = 1e-4f;           // 0.1 mm: 미생물 / 거시생물 경계(맨눈 한계)
    const float SELZ = 3;                // 고른 개체: 상자 한 변 = 몸길이 × 3
    bool noDots;

    World w; Camera cam;
    Material mMicro, mMacro, mDots; Mesh dots, floorM;
    MaterialPropertyBlock mpb; readonly double[] bud = new double[14];

    int speed = 1; double acc; float frameSim;
    static readonly float[] TPS = { 0, 12, 120, 1e9f };
    static readonly string[] SPN = { "멈춤", "1일/초", "10일/초", "최대" };
    float mutX = 1, rateSel = 0.0001f;

    float B = 3e-4f, Bt = 3e-4f; bool fit = true;    // 상자 한 변(m)과 목표값. fit = 다음 Scan에서 한 번 가장 큰 생물에 맞춤
    float conc = 1;
    float yaw = 35, pitch = 18, dist = 9, camDist = 9; Vector3 m0; bool dragging, overUI;
    (int slot, int k) want = (-1, 0);                  // 종 목록에서 고른 것: 다음 표본에서 하나 고른다

    sealed class Av { public int id, slot, k, grade; public Vector3 p, dir, vs = Vector3.one; public float L, spd, sink, t0, rate, grow = 1, pinch = -1, ph; public bool sessile; public Quaternion rot; }   // vs 개체마다 조금 다른 몸 비율, pinch 분열 진행(0–1)
    readonly List<Av> avs = new(); Av sel; float tRes, tScan;
    struct Cand { public int s, k; public float L; public double e; public Morph m; }
    readonly List<Cand> cand = new();

    int V => vNear;                       // 관찰자에게 가장 가까운 칸(모양·에너지 수지용). 밀도는 Dens로 섞는다
    bool MacroStyle => B > 2e-3f;

    void Start()
    {
        Application.targetFrameRate = 60;
        w = new World(Environment.TickCount & 0xffff) { o2Bio = true, autoQ = true, activity = 0.4f };   // 앱: 산소는 생물이, 사건은 자연이
        cam = new GameObject("Camera").AddComponent<Camera>();
        cam.fieldOfView = 50; cam.nearClipPlane = 0.05f; cam.farClipPlane = 300; cam.clearFlags = CameraClearFlags.SolidColor;
        mMicro = new Material(Shader.Find("Evo/Micro")); mMacro = new Material(Shader.Find("Evo/Macro"));
        mDots = new Material(Shader.Find("Evo/Dots"));
        mpb = new MaterialPropertyBlock(); LookInit();
        dots = DotsMesh(3000); floorM = FloorMesh(); Cells();
        Fonts();
        var args = Environment.GetCommandLineArgs(); int ai = Array.IndexOf(args, "-shots");
        if (ai >= 0 && ai + 1 < args.Length) StartCoroutine(Shots(args[ai + 1], Array.IndexOf(args, "-gallery") >= 0));
    }

    // 점검용: EvoTank.x86_64 -shots <폴더> [-gallery] → 화면 사진 몇 장 찍고 끝낸다. -gallery = 여러 체제를 미리 넣어 몸 그리기 확인
    static readonly (int g, float v)[][] GAL = {
        new[] { (Gn.Cells, 4f) }, new[] { (Gn.Wall, 0.8f), (Gn.Photo, 0.9f), (Gn.Elong, 2.5f), (Gn.Flagella, 0f) }, new[] { (Gn.Cilia, 0.9f), (Gn.Size, 5.5f), (Gn.Flagella, 0f) },
        new[] { (Gn.Lorica, 0.8f), (Gn.Spines, 0.7f), (Gn.Size, 4.5f) },
        // 단세포 몸 설계: 원형 돌말 · 와편모충(뿔) · 방산충 · 유글레나 · 아메바 · 깃편모충
        new[] { (Gn.Wall, 0.8f), (Gn.Photo, 0.9f), (Gn.Elong, -1f), (Gn.Flagella, 0f), (Gn.Size, 5f) }, new[] { (Gn.Wall, 0.5f), (Gn.Flagella, 0.7f), (Gn.Photo, 0.6f), (Gn.Spines, 0.6f), (Gn.Size, 5f) },
        new[] { (Gn.Wall, 0.6f), (Gn.Phago, 0.8f), (Gn.Flagella, 0f), (Gn.Size, 6f), (Gn.Spines, 0.3f) }, new[] { (Gn.Flagella, 0.5f), (Gn.Photo, 0.7f), (Gn.Elong, 2f), (Gn.Eye, 0.3f), (Gn.Size, 5f) },
        new[] { (Gn.Phago, 0.9f), (Gn.Flagella, 0f), (Gn.Size, 5.5f) }, new[] { (Gn.Phago, 0.7f), (Gn.Flagella, 0.6f), (Gn.Size, 3f) }, new[] { (Gn.Cells, 10f), (Gn.Diff, 0.7f), (Gn.Form, 1f) },
        new[] { (Gn.Cells, 14f), (Gn.Diff, 0.8f), (Gn.Form, 1f), (Gn.Gut, 0.6f), (Gn.Neuro, 0.4f) },
        new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Elong, 2f) },
        new[] { (Gn.Cells, 20f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.8f), (Gn.Neuro, 0.7f), (Gn.Bilat, 0.8f), (Gn.Skel, 0.5f), (Gn.Circ, 0.4f), (Gn.Eye, 0.3f), (Gn.Elong, 2f) },
        new[] { (Gn.Cells, 28f), (Gn.Diff, 1f), (Gn.Form, 1f), (Gn.Gut, 0.9f), (Gn.Neuro, 0.8f), (Gn.Bilat, 0.9f), (Gn.Skel, 0.8f), (Gn.Circ, 0.8f), (Gn.Eye, 0.8f), (Gn.Elong, 1.5f), (Gn.Buoy, 0.3f), (Gn.Egg, 10f) },
    };
    System.Collections.IEnumerator Shots(string dir, bool gallery)
    {
        System.IO.Directory.CreateDirectory(dir); var galS = new List<Strain>();
        if (gallery)
        {
            var par = w.strains[w.live[0]];
            foreach (var set in GAL)
            {
                var g = (float[])par.g.Clone(); foreach (var (j, x) in set) g[j] = x;
                var ns = w.Inject(par.slot, g, 1e-6); if (ns == null) continue; galS.Add(ns);
                for (int v = 0; v < World.V; v++) { double c = 0; for (int k = 0; k < ns.K; k++) c += w.cnt[World.Ix(ns.slot, v, k)]; for (int k = 0; k < ns.K; k++) w.cnt[World.Ix(ns.slot, v, k)] = Math.Round(c / ns.K); }
            }
        }
        System.Collections.IEnumerator Snap(string n)
        {
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            Debug.Log($"[EvoTank] shot {n} avs {avs.Count} B {B:G3} depth {py:0.00} conc {conc:G3} sel {sel != null} live {w.live.Count} ms {frameSim:0.0} | " + string.Join(" ", avs.GroupBy(a => (World.Grade(w.strains[a.slot]), a.k, (int)(a.L * 1e6))).OrderByDescending(gr => gr.Count()).Take(8).Select(gr => $"g{gr.Key.Item1}k{gr.Key.Item2}:{gr.Key.Item3}um×{gr.Count()}"))); yield return null;
        }
        var wait = new WaitForSecondsRealtime(3);
        yield return wait; yield return Snap("1-start"); noDots = true; yield return null; yield return Snap("1b-nodots"); noDots = false;
        if (gallery)   // 단세포 몸 설계마다 확대 카드 그림을 따로 저장(card-<설계>.png)
        {
            Unfollow(); var tex = new Texture2D(512, 512, TextureFormat.RGB24, false);
            foreach (var st in galS.Concat(w.live.Select(x => w.strains[x])).Where(x => w.strains[x.slot] == x && World.Grade(x) == 0).GroupBy(x => (Bodies.PlanOf(x), x.aspect < 1.5f)).Select(gp => gp.First()).ToList())
            {
                sel = new Av { id = st.id, slot = st.slot, dir = Vector3.forward, rot = Quaternion.identity, L = w.M(st.slot, V, 0).L * 1e-6f, rate = 1 };
                cYaw = 60; cPitch = 14; cDragT = Time.time; yield return null; yield return null;
                RenderTexture.active = cardRT; tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); RenderTexture.active = null;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"card-{(int)Bodies.PlanOf(st)}{Bodies.PlanOf(st)}{(st.aspect < 1.5f ? "" : "-long")}.png"), tex.EncodeToPNG());
                Debug.Log($"[EvoTank] plan {Bodies.PlanOf(st)} {StageName(st, 0)} verts {cardMesh?.vertexCount} parts {string.Join(",", Bodies.Parts(cardMesh).Select(x => x.name))}");
            }
            sel = null;
        }
        int n0 = avs.Count; float b0 = Bt *= 2; yield return wait;   // 축소한 축척이 그대로 있는지, 개체가 늘어나는지
        Debug.Log($"[EvoTank] zoomhold {(Bt == b0 && Mathf.Abs(B / b0 - 1) < 0.05f ? "ok" : "FAIL")} Bt {Bt:G3} B {B:G3} avs {n0}->{avs.Count}");
        var big = avs.OrderByDescending(a => a.L).FirstOrDefault();
        if (big != null) { sel = big; Bt = big.L * SELZ; tab = 1; Follow(big.id); }
        yield return wait; yield return Snap("3-select");
        if (sel != null && cardMesh != null)   // 몸통 단면(가운데, 불투명 꼭짓점) 높이·너비
        {
            var vv = cardMesh.vertices; var cc = cardMesh.colors; float my = 0, mxx = 0;
            for (int i = 0; i < vv.Length; i++) if (Mathf.Abs(vv[i].z) < 0.03f && cc[i].a > 0.99f) { my = Mathf.Max(my, Mathf.Abs(vv[i].y)); mxx = Mathf.Max(mxx, Mathf.Abs(vv[i].x)); }
            Debug.Log($"[EvoTank] body mid half-height {my:0.000} half-width {mxx:0.000} Form {w.strains[sel.slot].g[Gn.Form]:0.00}");
        }
        if (sel != null) Debug.Log($"[EvoTank] card {(cardMesh != null ? "ok" : "FAIL")} grade {World.Grade(w.strains[sel.slot])} k {sel.k} bounds {cardMesh?.bounds.size} parts {string.Join(",", Bodies.Parts(cardMesh).Select(x => x.name))}");
        // 혈통 점검: 후손 있는 계통을 없애면 그 후손으로, 후손 없는 계통을 없애면 '끊김' 카드(또는 그새 생긴 후손)로
        void Kill(int id) { int s = idSlot[id]; for (int v = 0; v < World.V; v++) for (int k = 0; k < World.KM; k++) w.cnt[World.Ix(s, v, k)] = 0; }
        Index(); int f0 = w.live.Select(x => w.strains[x].id).FirstOrDefault(id => BestDesc(id) >= 0);
        if (f0 > 0) { Follow(f0); Kill(f0); yield return wait; Debug.Log($"[EvoTank] linetest desc {(follow > 0 && follow != f0 && Chain(follow).Any(r => r.id == f0) ? "ok" : "FAIL")} {f0}->{follow}"); yield return Snap("3b-line"); }
        Index(); int f1 = w.live.Select(x => w.strains[x].id).FirstOrDefault(id => BestDesc(id) < 0);
        if (f1 > 0) { Follow(f1); Kill(f1); yield return wait; Debug.Log($"[EvoTank] linetest gone {(gone?.id == f1 || follow > 0 && Chain(follow).Any(r => r.id == f1) ? "ok" : "FAIL")} {f1} follow {follow} gone {gone?.id} why {gone?.why}"); yield return Snap("3c-gone"); }
        sel = null; tab = 2; yield return null; yield return Snap("4-species");
        showTray = true; yield return wait; yield return Snap("4b-tray"); trayReal = true; yield return null; yield return Snap("4c-tray-real"); showTray = trayReal = false;
        double d0 = py; MoveBy(new Vector3(0, -1f, 0)); yield return wait;   // 헤엄 점검: 1 m 내려가면 수심이 그만큼 바뀐다
        Debug.Log($"[EvoTank] swim depth {d0:0.00}->{py:0.00} {(Math.Abs(py - d0 - 1) < 1e-3 ? "ok" : "FAIL")} avs {avs.Count}"); yield return Snap("2-swim");
        float bk = Bt; Bt = 1; yield return wait; yield return Snap("2b-wide"); Bt = bk; MoveBy(new Vector3(0, 1f, 0)); yield return wait;   // 제자리로
        double pk = py; float bk2 = Bt; MoveBy(new Vector3(0, (float)(py - 0.08), 0)); Bt = 0.4f; float pp = pitch; pitch = -30; yield return wait; yield return Snap("6-surface"); pitch = pp;   // 물속 느낌: 수면 가까이·깊은 곳
        MoveBy(new Vector3(0, -6f, 0)); yield return wait; yield return Snap("6b-deep"); MoveBy(new Vector3(0, (float)(py - pk), 0)); Bt = bk2; yield return wait;
        int r0 = reg, rd = 0; for (int c = 1; c < w.NR; c++) if (w.geo.rDepth[c] > w.geo.rDepth[rd]) rd = c;   // 해역 옮기기: 가장 깊은 바다 300 m
        GoTo(rd, 300); yield return wait; Debug.Log($"[EvoTank] region {r0}->{reg} {Geo.DName(w.geo.rDepth[reg])} depth {py:0} floor {Floor:0} vNear y{vNear / World.LS} {(reg == rd && Math.Abs(py - 300) < 1e-3 ? "ok" : "FAIL")}"); yield return Snap("7-ocean");
        GoTo(r0, 1.25); yield return wait;
        // 자연현상 점검: 지금 해역에 거대 화산, 빙하기 → 빙상·해수면·해역 다시 나누기, 계통수 사건 줄, 고급 패널
        tab = 0; qSel = 0; qSize = 3; float t0 = w.env[Ev.Temp]; FireQ(obsCell); yield return wait;
        Debug.Log($"[EvoTank] quake volcano {(w.quakes.Count > 0 && w.aer > 1 ? "ok" : "FAIL")} aer {w.aer:0.00} T {t0:0.0}->{w.env[Ev.Temp]:0.0} text {w.quakes[^1].text}"); yield return Snap("8-volcano");
        int rg0 = w.regridN; w.Fire(Qk.IceAge, 3, -1, World.IceAmp(3), 0, 40); speed = 3; yield return new WaitForSecondsRealtime(12);
        Debug.Log($"[EvoTank] quake ice ice {w.ice:0.00} sea {w.geo.sea:0} regrid {rg0}->{w.regridN} reg {reg}/{w.NR} obs {obsCell}->{w.geo.reg[obsCell]} {(w.regridN > rg0 && reg < w.NR && w.geo.reg[obsCell] == reg ? "ok" : "FAIL")} T {w.env[Ev.Temp]:0.0}"); yield return Snap("8b-ice");
        speed = 0; tab = 3; yield return null; yield return Snap("8c-tree");
        tab = 0; advanced = true; yield return null; yield return Snap("8d-advanced"); advanced = false;
        speed = 3; tab = 3; yield return new WaitForSecondsRealtime(gallery ? 8 : 30); yield return Snap("5-fast");
        Application.Quit();
    }

    // ---------- 매 프레임 ----------
    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (speed > 0)
        {
            acc += dt * TPS[speed];
            var sw = System.Diagnostics.Stopwatch.StartNew(); float budget = speed == 3 ? 30 : 22;
            while (acc >= 1 && sw.ElapsedMilliseconds < budget) { w.Step(); acc -= 1; }
            if (acc > 4) acc = speed == 3 ? 0 : 4;   // 못 따라가면 버린다(화면이 멈추지 않게)
            frameSim = (float)sw.Elapsed.TotalMilliseconds;
        }
        if ((tScan -= dt) <= 0) { Scan(); tScan = 1; }
        if ((tRes -= dt) <= 0) { Resample(); tRes = 0.5f; }
        B = Mathf.Exp(Mathf.Lerp(Mathf.Log(B), Mathf.Log(Bt), 1 - Mathf.Exp(-dt * 4)));
        toastT -= dt; PlayStep(dt); Cells();
        Controls(dt); SwimKeys(dt); Move(dt); Divide(dt); Draw();
    }
    void LateUpdate() { RenderPortraits(); RenderCard(); }

    // 1초마다: 혈통 따라가기, (처음 한 번) 가장 큰 생물에 맞춤
    void Scan()
    {
        FollowStep();
        if (fit) { var b = Biggest(true); if (b.s < 0) b = Biggest(false); if (b.L > 0) { Bt = Mathf.Clamp(b.L * 6, 2e-5f, 3); fit = false; } }
    }

    // 가장 큰 것(계통, 단계, 길이 m). here = 관찰자 둘레에 있는 것만
    (int s, int k, float L) Biggest(bool here)
    {
        (int, int, float) b = (-1, 0, 0);
        foreach (int s in w.live)
        {
            var st = w.strains[s];
            for (int k = 0; k < st.K; k++)
            {
                if (st.stage[k] < 1 || here && Dens(s, k) <= 0) continue;
                float L = w.M(s, V, k).L * 1e-6f; if (L > b.Item3) b = (s, k, L);
            }
        }
        return b;
    }

    // 관찰자 위치의 실제 밀도 × 농축 배율 → 시야 안 개체 수. 이미 있는 개체는 되도록 그대로 둔다
    void Resample()
    {
        int v = V; double boxML = (double)B * B * B * 1e6, sum = 0, occ = 0; int big = -1; float bigL = 0, u0 = BOX / B;
        cand.Clear();
        foreach (int s in w.live)
        {
            var st = w.strains[s];
            for (int k = 0; k < st.K; k++)
            {
                double c = Dens(s, k); if (c <= 0) continue;
                var m = w.M(s, v, k); float L = m.L * 1e-6f;
                if (L < B * 0.006f || L > B * 0.5f || m.sessile && yF < -H) continue;   // 붙어 사는 것은 바닥이 보일 때만
                cand.Add(new Cand { s = s, k = k, L = L, e = c * boxML, m = m }); sum += cand[^1].e; occ += cand[^1].e * Math.Pow(L / B, 3);
                if (L > bigL) { bigL = L; big = cand.Count - 1; }
            }
        }
        double nOn = sum * conc; if (sum > 0 && (nOn > NMAX || nOn < NMIN)) conc = (float)((nOn > NMAX ? NMAX : NMIN) / sum);
        if (occ * conc > 0.2) conc = (float)(0.2 / occ);   // 큰 것을 크게 볼 때 화면이 몸으로 꽉 차지 않게(몸 길이³ 합 ≤ 시야³의 20%)
        var have = new Dictionary<long, int>(); var keep = new HashSet<long>();
        foreach (var c in cand) keep.Add(Key(c.s, c.k));
        for (int i = avs.Count - 1; i >= 0; i--)
        {
            var a = avs[i]; var st = w.strains[a.slot];
            if (st == null || st.id != a.id || !keep.Contains(Key(a.slot, a.k)) && a != sel) { if (a == sel) sel = null; avs.RemoveAt(i); continue; }
            have[Key(a.slot, a.k)] = have.TryGetValue(Key(a.slot, a.k), out int n) ? n + 1 : 1;
        }
        for (int i = 0; i < cand.Count; i++)
        {
            var c = cand[i]; long key = Key(c.s, c.k);
            float t = Mathf.Min(PER, (float)(c.e * conc));
            if (i == big || (c.s, c.k) == want) t = Mathf.Max(t, 1);
            int lo = Mathf.FloorToInt(t), hi = Mathf.CeilToInt(t), cur = have.TryGetValue(key, out int n) ? n : 0, add = 0;
            if (cur < lo) add = lo - cur; else if (cur > hi) add = hi - cur;
            else if (lo != hi) { if (cur == lo && UnityEngine.Random.value < (t - lo) * 0.3f) add = 1; else if (cur == hi && UnityEngine.Random.value < (hi - t) * 0.3f) add = -1; }
            for (; add > 0; add--) avs.Add(Spawn(c));
            for (int j = avs.Count - 1; j >= 0 && add < 0; j--) if (avs[j] != sel && Key(avs[j].slot, avs[j].k) == key) { avs.RemoveAt(j); add++; }
            if ((c.s, c.k) == want)
            {
                sel = avs.LastOrDefault(a => a.slot == c.s && a.k == c.k); want = (-1, 0);
                if (sel != null && birthAt is Vector3 bp) { sel.p = bp + UnityEngine.Random.insideUnitSphere * sel.L * u0 * 0.3f; sel.grow = 0; }   // 후손이 옛 대표 자리에서 나타난다
                birthAt = null;
            }
        }
    }
    static long Key(int s, int k) => ((long)s << 8) | (uint)k;

    Av Spawn(in Cand c)
    {
        var st = w.strains[c.s];
        var a = new Av { id = st.id, slot = c.s, k = c.k, L = c.L, spd = c.m.speed * 1e-6f, sink = c.m.sink / 3600f, sessile = c.m.sessile, t0 = UnityEngine.Random.value * 100, grade = World.Grade(st),
            rate = UnityEngine.Random.Range(0.8f, 1.25f), vs = new Vector3(UnityEngine.Random.Range(0.9f, 1.1f), UnityEngine.Random.Range(0.9f, 1.1f), UnityEngine.Random.Range(0.9f, 1.1f)) };   // 같은 계통도 개체마다 박동 빠르기·몸 비율이 조금씩 다르다
        a.p = UnityEngine.Random.insideUnitSphere * H; a.p.y = Mathf.Clamp(a.p.y, yF, yS);
        a.dir = UnityEngine.Random.onUnitSphere; a.rot = UnityEngine.Random.rotation;
        if (a.sessile) { a.p.y = yF; a.rot = Quaternion.LookRotation(Vector3.up, Vector3.forward) * Quaternion.Euler(0, 0, UnityEngine.Random.value * 360); }
        return a;
    }

    void Move(float dt)
    {
        float u = BOX / B, T = Time.timeSinceLevelLoad;   // 화면 단위 / m, 셰이더 시간(_Time.y)
        School(dt, u);
        foreach (var a in avs)
        {
            a.grow = Mathf.Min(1, a.grow + dt * 0.8f);
            if (a.pinch >= 0) { a.pinch += dt / 1.5f; if (a != sel && a.pinch > 1) a.pinch = -1; }   // 분열 중: 가운데가 잘록해진다(고른 개체는 Divide가 둘로 나눈다)
            if (a.sessile) continue;
            if (a.L < 20e-6f) a.p += UnityEngine.Random.insideUnitSphere * Mathf.Sqrt(10 * 2.2e-19f / (a.L * 0.5f) * dt) * u;   // 브라운 운동: D = kT/(6πηr), 물 20 °C
            if (a.spd > 0 && a.L < 2e-3f)
            {
                // 작은 헤엄이(낮은 레이놀즈 수): 관성이 없어 곧바로 돌고, 몸이 축 둘레를 돌며 나선을 그린다(Jennings 1901)
                a.dir = (a.dir + UnityEngine.Random.insideUnitSphere * dt * 0.8f).normalized;
                a.ph += dt * a.rate * 5;
                a.rot = Quaternion.LookRotation(a.dir) * Quaternion.Euler(0, 0, a.ph * Mathf.Rad2Deg) * Quaternion.Euler(20, 0, 0);
                a.p += a.rot * Vector3.forward * Mathf.Min(a.spd * u, BOX * 0.3f) * dt;
            }
            else if (a.spd > 0)
            {
                a.dir = (a.dir + UnityEngine.Random.insideUnitSphere * dt * (a.grade >= 6 ? 0.5f : 1.5f)).normalized;
                float th = a.grade == 4 ? 0.3f + 2.2f * Mathf.Max(0, Mathf.Cos(3 * (T * a.rate + a.t0))) : 1;   // 해파리: 갓이 오므라들 때 나아간다(셰이더 맥동과 같은 위상)
                a.p += a.dir * Mathf.Min(a.spd * u, BOX * 0.3f) * th * dt;
                a.rot = Quaternion.Slerp(a.rot, Quaternion.LookRotation(a.dir), 1 - Mathf.Exp(-dt * 5));
            }
            else a.rot *= Quaternion.Euler(dt * 9, dt * 6, 0);   // 헤엄 못 하는 것은 물살에 천천히 구른다
            a.p.y -= Mathf.Clamp(a.sink * u, -BOX * 0.2f, BOX * 0.2f) * dt;
            a.p = Wrap(a.p);
        }
        if (sel != null && !sel.sessile) MoveBy(sel.p / u);   // 고른 개체와 함께 헤엄친다(떠다니는 원점)
    }

    // 물고기 떼: 같은 계통끼리 방향 맞추기·모이기·부딪히지 않기(boids, Reynolds 1987)
    void School(float dt, float u)
    {
        foreach (var a in avs)
        {
            if (a.grade < 6 || a.spd <= 0 || a.sessile) continue;
            Vector3 al = Vector3.zero, co = Vector3.zero, se = Vector3.zero; int n = 0; float R = a.L * u * 6, Rs = a.L * u * 1.2f;
            foreach (var o in avs)
            {
                if (o == a || o.slot != a.slot || o.sessile) continue;
                var d = o.p - a.p; float dd = d.magnitude; if (dd > R) continue;
                al += o.dir; co += d; n++; if (dd < Rs) se -= d / Mathf.Max(dd * dd, 1e-6f) * Rs;
            }
            if (n > 0) a.dir = (a.dir + dt * (1.2f * al / n + 0.6f * co / (n * R) + 1.5f * se)).normalized;
        }
    }

    // ---------- 그리기 ----------
    void Draw()
    {
        bool ms = MacroStyle; int v = V; float I = Mix(w.I), sun = Mathf.Clamp01(I * 1.6f), u = BOX / B; LookBegin();
        // 이 깊이까지 내려온 햇빛 색: 빨강이 먼저 줄어 깊을수록 청록. 물빛 = 그 빛이 물에 흩어진 색
        var lc = new Vector3(Mathf.Exp(-SIGMA.x * (float)py), Mathf.Exp(-SIGMA.y * (float)py), Mathf.Exp(-SIGMA.z * (float)py)); lc /= Mathf.Max(lc.x, Mathf.Max(lc.y, lc.z));
        var water = Color.Lerp(new Color(0.01f, 0.03f, 0.06f), new Color(0.1f + 0.14f * lc.x, 0.42f * lc.y, 0.55f * lc.z), sun);
        cam.backgroundColor = ms ? water : new Color(0.01f, 0.012f, 0.02f);
        Shader.SetGlobalVector("_FogCol", water); Shader.SetGlobalFloat("_Sun", sun);
        Shader.SetGlobalVector("_Sigma", SIGMA / u); Shader.SetGlobalVector("_LightCol", new Vector4(lc.x, lc.y, lc.z, 1));
        var target = sel != null ? sel.p : Vector3.zero;
        if (sel != null && !dragging && Time.time - lastDrag > 3)   // 따라가기 카메라: 손을 떼고 있으면 옆모습이 보이게 천천히 돈다
        {
            var f = sel.dir;   // 몸 방향 대신 헤엄 축(나선으로 돌아도 카메라는 흔들리지 않게)
            if (Mathf.Abs(f.y) < 0.9f)
            {
                float h = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, a1 = h + 90, a2 = h - 90, k = 1 - Mathf.Exp(-Time.deltaTime * 0.6f);
                yaw = Mathf.LerpAngle(yaw, Mathf.Abs(Mathf.DeltaAngle(yaw, a1)) < Mathf.Abs(Mathf.DeltaAngle(yaw, a2)) ? a1 : a2, k);
                pitch = Mathf.Lerp(pitch, 10, k);
            }
        }
        var cp = target + Quaternion.Euler(pitch, yaw, 0) * new Vector3(0, 0, -dist); cp.y = Mathf.Clamp(cp.y, yF + 0.05f, yS - 0.05f);   // 카메라는 늘 물속(수면 위·바닥 밑으로 안 나감)
        cam.transform.position = cp; cam.transform.LookAt(target); camDist = (cp - target).magnitude;
        foreach (var a in avs)
        {
            var st = w.strains[a.slot]; if (st == null || st.id != a.id) continue;
            var m = w.M(a.slot, v, a.k); var mesh = Body(st, a.k, m);
            // 관찰자 둘레 구 가장자리에서 물빛으로 녹는다. 하나를 고르면 나머지는 흐리게
            mpb.SetFloat("_Fade", a == sel ? 1 : Mathf.Clamp01((H - (a.p - target).magnitude) / 1.2f) * (sel != null ? 0.45f : 1));
            mpb.SetFloat("_Sel", a == sel ? 1 : 0); mpb.SetFloat("_T0", a.t0); mpb.SetFloat("_Rate", a.rate); mpb.SetFloat("_Pinch", Mathf.Clamp01(a.pinch));
            var mx = Matrix4x4.TRS(a.p, a.rot, a.vs * (Mathf.Max(a.L, m.L * 1e-6f) * u * Mathf.SmoothStep(0.3f, 1, a.grow)));
            Graphics.DrawMesh(mesh, mx, ms ? mMacro : mMicro, 0, null, 0, mpb); LookBody(mesh, mx, mpb);
        }
        // 너무 작아 몸을 못 그리는 것: 미생물 모드는 세균, 거시 모드는 미생물 안개
        double boxML = (double)B * B * B * 1e6, small = 0;
        if (ms) foreach (int s in w.live) { var st = w.strains[s]; for (int k = 0; k < st.K; k++) { double c = Dens(s, k); if (c > 0 && w.M(s, v, k).L * 1e-6f < B * 0.006f) small += c; } }
        double nDots = ms ? Math.Log10(1 + small * boxML) * 400 : Math.Log10(1 + Mix(w.bac) * boxML * conc) * 150;   // 세균도 로그로(농축 배율이 10⁵배쯤이라 그대로 곱하면 화면이 하얗게 찬다)
        mDots.SetFloat("_Count", (float)Math.Min(1, nDots / 3000)); mDots.SetFloat("_Brown", ms ? 0 : 5e-7f * u); mDots.SetFloat("_Box", BOX);
        mDots.SetVector("_Off", new Vector4(dotOff.x - Mathf.Floor(dotOff.x), dotOff.y - Mathf.Floor(dotOff.y), dotOff.z - Mathf.Floor(dotOff.z), 0)); mDots.SetVector("_YLim", new Vector4(yF, yS));
        mDots.SetFloat("_Size", ms ? 0.025f : Mathf.Max(0.02f, 0.6e-6f * u));
        mDots.SetColor("_Color", ms ? new Color(0.7f, 0.9f, 0.7f, 0.35f) : new Color(0.75f, 0.8f, 1f, 0.5f));
        if (!noDots) { Graphics.DrawMesh(dots, Matrix4x4.identity, mDots, 0); Snow(u, sun, ms); }
        if (yF > -H)   // 바닥이 보이면: 모래 판을 시야를 덮게 깔고, 헤엄치면 함께 흘러간다
        {
            float ox = -(float)Frac(px * u / BOX) * BOX, oz = -(float)Frac(pz * u / BOX) * BOX;
            for (int i = 0; i < 2; i++) for (int j = 0; j < 2; j++) Graphics.DrawMesh(floorM, Matrix4x4.Translate(new Vector3(ox + i * BOX, yF + H, oz + j * BOX)), mMacro, 0);
        }
        LookEnd(ms, u, sun);
    }

    // ---------- 조작 ----------
    void Controls(float dt)
    {
        var mp = Input.mousePosition; var gp = new Vector2(mp.x, Screen.height - mp.y) / UIScale;
        overUI = panel.Contains(gp) || top.Contains(gp) || info.Contains(gp) || line.Contains(gp) || gauge.Contains(gp) || mapR.Contains(gp) || card.Contains(gp) || tray.Contains(gp);
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) { m0 = mp; dragging = !overUI; }
        if (dragging && (Input.GetMouseButton(0) || Input.GetMouseButton(1))) { var d = Input.mousePosition - m0; m0 = Input.mousePosition; if (d.sqrMagnitude > 0) lastDrag = Time.time; yaw += d.x * 0.25f; pitch = Mathf.Clamp(pitch - d.y * 0.25f, -80, 80); }
        if (Input.GetMouseButtonUp(0) && dragging && (Input.mousePosition - downAt).sqrMagnitude < 36) Pick(mp);
        if (Input.GetMouseButtonDown(0)) downAt = mp;
        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)) dragging = false;
        float sc = Input.mouseScrollDelta.y; if (!overUI && sc != 0) { Bt = Mathf.Clamp(Bt * Mathf.Exp(-sc * 0.15f), 2e-5f, 3); fit = false; }
        if (Input.GetKeyDown(KeyCode.Space)) speed = speed == 0 ? 1 : 0;
        for (int i = 0; i < 4; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) speed = i;
        if (Input.GetKeyDown(KeyCode.F)) Fit();
        if (Input.GetKeyDown(KeyCode.Escape)) { sel = null; gone = null; Unfollow(); }
        if (Input.GetKeyDown(KeyCode.T)) showLine = !showLine;
        if (Input.GetKeyDown(KeyCode.M)) showCard = !showCard;
        if (Input.GetKeyDown(KeyCode.P)) showTray = !showTray;
    }
    Vector3 downAt; float lastDrag = -10;

    void Pick(Vector3 mp)
    {
        Av best = null; float bz = 1e9f, f = Screen.height / (2 * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2)), u = BOX / B;
        foreach (var a in avs)
        {
            var sp = cam.WorldToScreenPoint(a.p); if (sp.z <= 0) continue;
            float r = a.L * u * 0.5f * f / sp.z + 8;
            if (((Vector2)sp - (Vector2)mp).sqrMagnitude < r * r && sp.z < bz) { bz = sp.z; best = a; }
        }
        sel = best; if (sel != null) { tab = 1; Follow(sel.id); } else Unfollow();   // 고르기 = 그 혈통 따라가기. 축척은 F로
    }


    // F: 고른 개체가 있으면 그것에 맞추고, 없으면 가장 큰 생물이 가장 많은 곳으로 가서 하나를 고른다
    void Fit()
    {
        if (sel != null) { Bt = Mathf.Clamp(sel.L * SELZ, 2e-5f, 3); return; }
        var (s, k, _) = Biggest(false); if (s >= 0) Focus(s, k, true);
    }
    void Focus(int s, int k, bool fitZoom)
    {
        int bv = vNear; double bn = 0;
        for (int v = 0; v < World.V; v++) if (w.vol[v] > 0) { double c = w.cnt[World.Ix(s, v, k)] / w.vol[v]; if (c > bn) { bn = c; bv = v; } }
        if (Dens(s, k) < 0.05 * bn) GoTo(bv % World.LS, w.zc[bv]);   // 여기에 거의 없으면 가장 짙은 해역·층 가운데로 간다
        sel = null; want = (s, k); tRes = 0;
        float L = w.M(s, vNear, k).L * 1e-6f;
        if (fitZoom || L < B * 0.006f || L > B * 0.5f) Bt = Mathf.Clamp(L * SELZ, 2e-5f, 3);   // 혈통이 넘어갈 때는 안 보일 때만 축척을 바꾼다
    }

    // ---------- 메시 ----------
    static Mesh DotsMesh(int n)
    {
        var r = new System.Random(7); var v = new Vector3[n * 4]; var uv = new Vector2[n * 4]; var id = new Vector2[n * 4]; var t = new int[n * 6];
        Vector2[] cr = { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) };
        for (int i = 0; i < n; i++)
        {
            var p = new Vector3((float)r.NextDouble(), (float)r.NextDouble(), (float)r.NextDouble()); float rid = (float)r.NextDouble();   // 순번은 네 모서리가 같아야 한다(다르면 일부만 잘려 줄무늬가 된다)
            for (int j = 0; j < 4; j++) { v[i * 4 + j] = p; uv[i * 4 + j] = cr[j]; id[i * 4 + j] = new Vector2(rid, 0); }
            int b = i * 4; t[i * 6] = b; t[i * 6 + 1] = b + 1; t[i * 6 + 2] = b + 2; t[i * 6 + 3] = b; t[i * 6 + 4] = b + 2; t[i * 6 + 5] = b + 3;
        }
        var m = new Mesh { vertices = v, uv = uv, uv2 = id, triangles = t }; m.bounds = new Bounds(Vector3.zero, Vector3.one * 1000); return m;
    }
    static Mesh FloorMesh()
    {
        var r = new System.Random(3); int n = 24; var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
        for (int i = 0; i <= n; i++) for (int j = 0; j <= n; j++)
        {
            float x = -H + BOX * i / n, z = -H + BOX * j / n; v.Add(new(x, -H + (float)r.NextDouble() * 0.06f, z));
            float k = 0.8f + 0.2f * (float)r.NextDouble(); c.Add(new Color(0.55f * k, 0.48f * k, 0.36f * k, 1));
        }
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) { int a = i * (n + 1) + j; t.AddRange(new[] { a, a + 1, a + n + 1, a + 1, a + n + 2, a + n + 1 }); }
        var m = new Mesh(); m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.SetUVs(0, Enumerable.Repeat(Vector4.zero, v.Count).ToList()); m.RecalculateNormals(); return m;
    }

    // ---------- 화면(IMGUI) ----------
    Font font, fontB; GUIStyle sL, sB, sS, sBtn, sOn, sBox, sT, sTd; Texture2D bg;
    Rect panel, top, info; int tab; Vector2 scroll;
    static readonly string[] TABS = { "자연", "생물", "종", "진화" };
    float UIScale => Mathf.Max(0.75f, Mathf.Min(Screen.height / 900f, Screen.width / 1600f));

    void Fonts()
    {
        font = Resources.Load<Font>("Fonts/Pretendard-Regular"); fontB = Resources.Load<Font>("Fonts/Pretendard-SemiBold");
        if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "Noto Sans CJK KR", "Malgun Gothic", "Arial" }, 14);
        if (!fontB) fontB = font;
        bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0.05f, 0.07f, 0.1f, 0.78f)); bg.Apply();
    }
    void Styles()
    {
        if (sL != null) return;
        GUIStyle S(Font f, int size, Color c) => new() { font = f, fontSize = size, wordWrap = true, richText = true, normal = { textColor = c } };
        sL = S(font, 14, new Color(0.88f, 0.92f, 0.96f)); sB = S(fontB, 16, Color.white); sS = S(font, 12, new Color(0.65f, 0.72f, 0.8f));
        sBtn = new GUIStyle(GUI.skin.button) { font = font, fontSize = 13 }; sOn = new GUIStyle(sBtn) { font = fontB, normal = { textColor = new Color(0.5f, 0.9f, 1f) } };
        sBox = new GUIStyle { normal = { background = bg } };
        sT = S(font, 11, new Color(0.88f, 0.92f, 0.96f)); sT.wordWrap = false; sT.clipping = TextClipping.Clip; sTd = new GUIStyle(sT) { normal = { textColor = new Color(0.5f, 0.56f, 0.62f) } };
    }

    void OnGUI()
    {
        Styles(); float s = UIScale; GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        float W = Screen.width / s, Hh = Screen.height / s;
        top = new Rect(10, 10, W - 20, 40); panel = new Rect(W - 370, 246, 360, Hh - 256); info = new Rect(10, Hh - 132, 520, 122);
        // 머리
        GUI.Box(top, GUIContent.none, sBox);
        int nsp = w.species.Values.Count(x => x.total > 0), gmax = w.gradesSeen.Count > 0 ? w.gradesSeen.Max() : 0;
        GUI.Label(new Rect(22, 18, W - 420, 26), $"<b>진화 수조</b>   {w.Time(w.tick)}   ·   종 {nsp}   ·   개체 {w.totalCount:0.0e0}   ·   가장 발달한 체제: <b>{World.GradeName[gmax]}</b>", sL);
        for (int i = 0; i < 4; i++) if (GUI.Button(new Rect(W - 372 + i * 90, 15, 86, 30), SPN[i], speed == i ? sOn : sBtn)) speed = i;
        // 아래 왼쪽: 축척·층·모드
        GUI.Box(info, GUIContent.none, sBox);
        ScaleBar(new Rect(22, Hh - 124, 300, 30));
        GUI.Label(new Rect(22, Hh - 92, 500, 22), $"시야 {Len(B)}  ·  수심 {py:0.00} m  ·  {(conc >= 1 ? $"실제보다 {Num(conc)}배 농축" : $"실제의 1/{Num(1 / conc)}만 표시")}", sS);
        if (GUI.Button(new Rect(22, Hh - 64, 150, 26), sel != null ? "고른 개체에 맞추기" : "가장 큰 생물에 맞추기", sBtn)) Fit();
        if (GUI.Button(new Rect(176, Hh - 64, 110, 26), "확대 카드 (M)", showCard ? sOn : sBtn)) showCard = !showCard;
        if (GUI.Button(new Rect(290, Hh - 64, 110, 26), "표본 쟁반 (P)", showTray ? sOn : sBtn)) showTray = !showTray;
        GUI.Label(new Rect(22, Hh - 34, 500, 22), "끌기: 둘러보기 · WASD: 헤엄 · E/Q: 위/아래 · Shift: 빠르게 · 휠: 내 크기 · 클릭: 고르기 · F: 맞추기 · T: 타임라인 · M: 확대 카드 · P: 표본 쟁반 · Space: 멈춤", sS);
        // 오른쪽 판
        GUI.Box(panel, GUIContent.none, sBox);
        for (int i = 0; i < TABS.Length; i++) if (GUI.Button(new Rect(panel.x + 8 + i * 86, panel.y + 8, 82, 28), TABS[i], tab == i ? sOn : sBtn)) { tab = i; scroll = Vector2.zero; }
        GUILayout.BeginArea(new Rect(panel.x + 10, panel.y + 44, panel.width - 20, panel.height - 52));
        scroll = GUILayout.BeginScrollView(scroll);
        if (tab == 0) EnvTab(); else if (tab == 1) OrgTab(); else if (tab == 2) SpeciesTab(); else EventTab();
        GUILayout.EndScrollView(); GUILayout.EndArea();
        LineStrip(Hh); DepthGauge(Hh); MapBox(new Rect(W - 370, 58, 360, 180));
        if (showTray) Tray(W, Hh); else { Card(Hh); Cards(W); }   // 쟁반을 펴면 그 위에 다른 카드를 띄우지 않는다
    }

    static readonly (string name, float[] env)[] PRESETS = {
        ("원시 바다", new[] { 25f, 35, 60, 30, 2, 50 }), ("어류의 바다(추천)", new[] { 18f, 35, 70, 20, 21, 40 }), ("민물 연못", new[] { 20f, 0.5f, 70, 30, 21, 40 }),
        ("고염 석호", new[] { 28f, 150, 80, 50, 21, 40 }), ("차가운 바다", new[] { 6f, 35, 50, 20, 21, 50 }), ("빛 없는 심해", new[] { 4f, 35, 0, 0, 10, 30 }),
    };
    // ---------- 자연 탭: 지구 상태 · 자연현상 일으키기 · 자동 사건 · 고급(숫자) ----------
    int qSel, qSize = 2; float qAmp = 2, qRad = 2, qDays = 100; bool placing, advanced;
    static readonly string[] QBTN = { "화산", "융기·침강", "온난화·냉각", "빙하기", "운석" };
    static bool Located(int k) => k == (int)Qk.Volcano || k == (int)Qk.Impact || k == (int)Qk.Uplift;
    void FireQ(int cell)
    {
        var k = (Qk)qSel; float amp = k == Qk.IceAge ? World.IceAmp(qSize) : qAmp;
        var q = w.Fire(k, qSize, cell, amp, qRad, k == Qk.Volcano ? 60 : qDays); placing = false;
        toast = "<b>" + q.text + "</b>"; toastT = 6;
    }
    void EnvTab()
    {
        float yrs = w.tick * w.P.DT / 24f * World.GEO_X;
        GUILayout.Label($"<b>지구</b>  적도 바다 {w.env[Ev.Temp] + 3:0.0} °C · CO₂ {w.co2:0} ppm · 대기 산소 {w.env[Ev.O2]:0.0}%\n" +
            $"빙상 {w.ice * 100:0}% · 해수면 {w.geo.sea:+0;-0;0} m · 화산재 냉각 {w.aer:0.0} °C · 먼지 어둠 {w.dark * 100:0}%\n" +
            $"<size=11>지질 시계: 생태 1일 = 지질 1000년 (지금까지 지질 {(yrs >= 1e6 ? $"{yrs / 1e6:0.0}백만" : $"{yrs / 1e4:0.#}만")} 년). 먹고 먹히기·진화는 실제 시간, 탄소·빙상·융기는 압축.</size>", sS);
        GUILayout.Space(6); GUILayout.Label("<b>자연현상 일으키기</b>", sL);
        for (int r0 = 0; r0 < QBTN.Length; r0 += 3)
        {
            GUILayout.BeginHorizontal();
            for (int k = r0; k < Mathf.Min(r0 + 3, QBTN.Length); k++) if (GUILayout.Button(QBTN[k], qSel == k ? sOn : sBtn)) { qSel = k; placing = false; }
            GUILayout.EndHorizontal();
        }
        var kind = (Qk)qSel;
        if (kind == Qk.Volcano || kind == Qk.Impact || kind == Qk.IceAge)
        {
            string note = kind == Qk.Volcano ? World.VolNote[qSize] : kind == Qk.Impact ? World.ImpNote[qSize] : $"−{World.IceAmp(qSize):0} °C";
            GUILayout.Label($"규모 <b>{World.SizeName[qSize]}</b> — {note}", sS);
            qSize = Mathf.RoundToInt(GUILayout.HorizontalSlider(qSize, 0, 4));
        }
        if (kind == Qk.Uplift)
        {
            GUILayout.Label($"{(qAmp >= 0 ? "융기" : "침강")} {Mathf.Abs(qAmp):0.0} m/일 (실제 {Mathf.Abs(qAmp):0.0} mm/년) · 반경 {qRad * 200:0} km", sS);
            qAmp = Mathf.Round(GUILayout.HorizontalSlider(qAmp, -5, 5) * 2) / 2; qRad = Mathf.Round(GUILayout.HorizontalSlider(qRad, 1, 6));
        }
        if (kind == Qk.Warm) { GUILayout.Label($"{(qAmp >= 0 ? "온난화 +" : "냉각 −")}{Mathf.Abs(qAmp):0.0} °C", sS); qAmp = Mathf.Round(GUILayout.HorizontalSlider(qAmp, -8, 8) * 2) / 2; }
        if (kind == Qk.Uplift || kind == Qk.Warm || kind == Qk.IceAge)
        {
            GUILayout.Label($"기간 {qDays:0}일 (지질 {qDays * World.GEO_X / 1e4:0.#}만 년)", sS);
            qDays = Mathf.Round(GUILayout.HorizontalSlider(qDays, 10, 600) / 10) * 10;
        }
        if (Located(qSel)) { if (GUILayout.Button(placing ? "지도에서 위치를 누르세요 (다시 누르면 취소)" : "위치 고르기 → 지도를 누르면 일어남", placing ? sOn : sBtn)) placing = !placing; }
        else if (GUILayout.Button("일으키기", sBtn)) FireQ(-1);
        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(w.autoQ ? "자동 사건: 켜짐" : "자동 사건: 꺼짐", w.autoQ ? sOn : sBtn, GUILayout.Width(130))) w.autoQ = !w.autoQ;
        GUILayout.Label($"<size=11>{(w.activity < 0.34f ? "조용한 행성" : w.activity < 0.67f ? "보통 행성" : "격동하는 행성")} · 작은 화산 약 {1 / (0.1 * Math.Pow(10, 2 * w.activity - 1.5)):0}일에 한 번</size>", sS);
        GUILayout.EndHorizontal();
        w.activity = GUILayout.HorizontalSlider(w.activity, 0, 1);
        double day = w.tick * w.P.DT / 24.0;
        foreach (var q in w.quakes) { double left = q.Day0 + q.days - day; if ((q.kind != Qk.Volcano || q.size == 4) && q.kind != Qk.Impact && left > 0 && day >= q.Day0) GUILayout.Label($"<color=#ffb070>진행 중</color> {q.text} — {left:0}일 남음", sS); }
        GUILayout.Space(8);
        if (GUILayout.Button(advanced ? "▼ 고급: 숫자로 조정" : "▶ 고급: 숫자로 조정", sBtn)) advanced = !advanced;
        if (advanced) Advanced();
        GUILayout.Space(6);
        GUILayout.Label($"<b>이 자리의 물질 순환</b>\n남세균 {Num(Mix(w.cya))}/mL · 세균 {Num(Mix(w.bac))}/mL · 영양염 {Mix(w.nut):0.00} μM\n유기물 입자(바다눈) {Mix(w.pom):0.00} μM N · 녹은 유기물 {Mix(w.dom):0.00} μM N · 산소 {Mix(w.dox):0.0}% atm\n퇴적물에 묻힌 유기물 누계 {Num(w.buried)}", sS);
        GUILayout.Label($"해역 {w.NR}곳 · 칸 {Enumerable.Range(0, World.V).Count(v => w.vol[v] > 0)}개 · 살아 있는 계통 {w.live.Count} · 계산 {frameSim:0} ms/프레임", sS);
    }

    void Advanced()
    {
        GUILayout.Label("슬라이더는 목표값이고 바다는 정한 속도로 따라간다. '수온'은 사건 없는 기후의 바탕(CO₂·빙상·사건 몫은 지구가 더한다).", sS);
        for (int e = 0; e < Ev.N; e++)
        {
            var d = Ev.Def[e];
            if (e == Ev.O2)
            {
                GUILayout.BeginHorizontal(); GUILayout.Label($"{d.name}  <b>{w.env[e]:0.#}{d.unit}</b>", sL);
                if (GUILayout.Button(w.o2Bio ? "생물이 정함" : "숫자로 고정", w.o2Bio ? sOn : sBtn, GUILayout.Width(100))) { w.o2Bio = !w.o2Bio; w.target[e] = w.env[e]; }
                GUILayout.EndHorizontal();
                if (w.o2Bio) { GUILayout.Label("<size=11>묻힌 유기물(광합성의 남은 몫)만큼 천천히 오르내린다</size>", sS); continue; }
            }
            else GUILayout.Label($"{d.name}  <b>{(e == Ev.Temp ? w.tBase : w.env[e]):0.#}{d.unit}</b>  → 목표 {w.target[e]:0.#}{d.unit}", sL);
            float t = GUILayout.HorizontalSlider(w.target[e], d.lo, d.hi); if (t != w.target[e]) w.SetTarget(e, t);
        }
        GUILayout.Label($"CO₂  <b>{w.co2:0} ppm</b> (기준 1000, 2배마다 +3 °C)", sL);
        float lc = GUILayout.HorizontalSlider(Mathf.Log10((float)w.co2), 2, 4); if (Mathf.Abs(lc - Mathf.Log10((float)w.co2)) > 1e-3f) w.co2 = Math.Pow(10, lc);
        GUILayout.Space(4); GUILayout.Label("변화 속도", sL);
        GUILayout.BeginHorizontal();
        foreach (var (n, r) in new[] { ("느리게", 0.0001f), ("보통", 0.0005f), ("빠르게", 0.003f), ("즉시", 1f) })
            if (GUILayout.Button(n, rateSel == r ? sOn : sBtn)) { rateSel = r; w.rate = r; }
        GUILayout.EndHorizontal();
        GUILayout.Space(4); GUILayout.Label($"돌연변이 속도 ×{mutX:0.00}  (높을수록 새 계통이 자주 생긴다)", sL);
        float mx = Mathf.Pow(2, GUILayout.HorizontalSlider(Mathf.Log(mutX, 2), -2, 2));
        if (Mathf.Abs(mx - mutX) > 1e-3f) { mutX = mx; var d0 = new Params(); w.P.MUT_NEW = d0.MUT_NEW * mx; w.P.MUT_U = d0.MUT_U * mx; w.P.MUT_FLOOR = d0.MUT_FLOOR * mx; }
        GUILayout.Space(4); GUILayout.Label($"<b>이 해역만</b> ({Geo.LName(w.geo.rLat[reg])} {Geo.DName(w.geo.rDepth[reg])})  수온 {w.rdT[reg]:+0.0;-0.0;0} °C · 영양염 ×{w.rnM[reg]:0.0}", sL);
        float rT = Mathf.Round(GUILayout.HorizontalSlider(w.rdT[reg], -10, 10) * 2) / 2, rN = Mathf.Round(GUILayout.HorizontalSlider(w.rnM[reg], 0, 5) * 10) / 10;
        if (rT != w.rdT[reg] || rN != w.rnM[reg]) w.SetRegionTweak(reg, rT, rN);
        GUILayout.Space(4); GUILayout.Label("미리 정한 바다", sL);
        for (int i = 0; i < PRESETS.Length; i += 2)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + 2, PRESETS.Length); j++) if (GUILayout.Button(PRESETS[j].name, sBtn)) for (int e = 0; e < Ev.N; e++) w.SetTarget(e, PRESETS[j].env[e]);
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(6);
        if (GUILayout.Button("이 환경에서 새로 시작(새 지도, 공통 조상부터)", sBtn)) { w.Reset(w.seed + 1, (float[])w.target.Clone()); avs.Clear(); sel = null; fit = true; reg = -1; obsCell = -1; }
    }

    void OrgTab()
    {
        var st = sel != null ? w.strains[sel.slot] : null;   // OnGUI 안에서는 상태를 바꾸지 않는다(Layout·Repaint 칸 수가 어긋남)
        if (st == null || st.id != sel.id) { GUILayout.Label("상자 안의 개체를 누르면 그 개체의 에너지 수지·형질이 나온다.", sL); return; }
        int v = V, q = World.Ix(sel.slot, v, sel.k); var m = w.M(sel.slot, v, sel.k); var sp = w.species.TryGetValue(st.sp, out var x) ? x : null;
        GUILayout.Label($"<b>{sp?.name}</b>", sB); GUILayout.Label(sp?.ko ?? "", sS);
        if (follow >= 0) GUILayout.Label("<color=#7fe0ff>혈통 따라가는 중</color> — 이 개체는 그 계통의 대표 표본이다(시뮬레이션은 개체가 아니라 무리를 계산한다). 계통이 사라지면 가장 많은 후손으로 넘어간다. T: 타임라인", sS);
        string stage = !st.dev ? "" : sel.k == 0 ? " · 알·유생" : sel.k == st.K - 1 ? " · 성체" : $" · 어린 개체 {sel.k}/{st.K - 1}";
        GUILayout.Label($"{World.GradeName[World.Grade(st)]}{stage}\n몸길이 {Len(m.L * 1e-6f)} · 세포 {Num(m.C)}개 · 헤엄 {Len(m.speed * 1e-6f)}/s\n임계 산소 {m.pcrit:0.0}% atm · 이 칸 {Num(w.cnt[q] / w.vol[v])}마리/mL · 세대 {st.gen:0}", sL);
        w.Budget(st, v, ref m, w.gP[q], w.gB[q], bud);
        GUILayout.Space(6); GUILayout.Label("<b>에너지 수지</b> (시간당, 들어옴 / 나감)", sL);
        string[] nm = { "녹은 유기물", "광합성", "세균 먹기", "포식", "기초 대사", "형질 유지", "몸 구조", "삼투 조절", "삼투 손상", "자외선", "산소", "온도" };
        double big = 1e-12; for (int i = 0; i < 12; i++) big = Math.Max(big, Math.Abs(bud[i]));
        for (int i = 0; i < 12; i++)
        {
            if (Math.Abs(bud[i]) < big * 0.01) continue;
            var r = GUILayoutUtility.GetRect(300, 18); GUI.Label(new Rect(r.x, r.y, 110, 18), nm[i], sS);
            var bar = new Rect(r.x + 112, r.y + 4, (float)(bud[i] / big) * (r.width - 120), 10);
            var c = GUI.color; GUI.color = i < 4 ? new Color(0.4f, 0.9f, 0.5f) : new Color(1f, 0.45f, 0.4f); GUI.DrawTexture(bar, Texture2D.whiteTexture); GUI.color = c;
        }
        GUILayout.Label($"순수지 {(bud[13] >= 0 ? "+" : "")}{bud[13] / Math.Max(1e-12, m.C * (m.esd * m.esd * m.esd * 1e-3 + w.P.FIXB)) * 2400:0.0}%/일", sL);
        GUILayout.Space(6); GUILayout.Label("<b>두드러진 형질</b>", sL);
        GUILayout.Label(string.Join(" · ", Names.Describe(st.g).Take(6).Select(d => d.ko)), sS);
        GUILayout.Label("<b>유전자</b>", sL);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < Gn.N; i++) { var d = Gn.Def[i]; if (Mathf.Abs(st.g[i] - d.init) > 0.05f * (d.hi - d.lo)) sb.Append($"{d.name} {st.g[i]:0.##}  "); }
        GUILayout.Label(sb.ToString(), sS);
    }

    void SpeciesTab()
    {
        GUILayout.Label("살아 있는 종(많은 순). 누르면 그 종의 혈통을 따라가며 가장 많은 곳으로 간다.", sS);
        foreach (var sp in w.species.Values.Where(x => x.total > 0).OrderByDescending(x => x.total).Take(30))
        {
            var c = Color.HSVToRGB(Mathf.Repeat(sp.hue / 360, 1), 0.6f, 1);
            if (GUILayout.Button($"<color=#{ColorUtility.ToHtmlStringRGB(c)}>●</color> <b>{sp.name}</b>  {World.GradeName[sp.grade]}\n<size=11>{sp.ko} · {sp.total:0.0e0}마리</size>", new GUIStyle(sBtn) { alignment = TextAnchor.MiddleLeft, richText = true }))
                GoSpecies(sp);
        }
    }

    void EventTab()
    {
        Tree();
        GUILayout.Space(8); GUILayout.Label("<b>사건 기록</b>", sL);
        for (int i = w.events.Count - 1; i >= Math.Max(0, w.events.Count - 60); i--)
        {
            var e = w.events[i]; bool big = e.type == "grade" || e.type == "scale";
            GUILayout.Label($"<color=#8fa3b8>{w.Time(e.t)}</color>  {(e.type == "earth" ? "<color=#ffb070>" + e.text + "</color>" : big ? "<b>" + e.text + "</b>" : e.text)}", sL);
        }
    }

    // 축척 막대: 1-2-5 단계에서 화면 60–160 px가 되는 길이
    void ScaleBar(Rect r)
    {
        float ppu = Screen.height / UIScale / (2 * dist * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2)), pxPerM = ppu * BOX / B;
        float len = 1e-7f; for (int i = 0; i < 40 && len * pxPerM < 60; i++) len *= (i % 3 == 1) ? 2.5f : 2;
        GUI.DrawTexture(new Rect(r.x, r.y + 14, len * pxPerM, 3), Texture2D.whiteTexture);
        GUI.Label(new Rect(r.x + len * pxPerM + 8, r.y + 4, 200, 22), Len(len), sL);
    }

    static string Len(float m) => m < 1e-3f ? $"{m * 1e6:0.#} μm" : m < 1e-2f ? $"{m * 1e3:0.##} mm" : m < 1 ? $"{m * 100:0.#} cm" : $"{m:0.##} m";
    static string Num(double x) => x >= 1e5 ? x.ToString("0.0e0") : x >= 100 ? x.ToString("0") : x.ToString("0.##");
}
