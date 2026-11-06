// 진화 수조 3D. 시뮬레이션(Evo.World)은 물기둥 전체(10 m × 10 m × 20층 × 4기둥)의 모든 생물을 확률적으로 계산하고,
// 화면은 그중 한 칸 안의 '관찰 상자'(한 변 B m)를 보여 준다. 상자 안 개체는 그 칸의 실제 밀도에서 뽑는다(농축 배율은 화면에 표시).
// 축척: 처음 한 번만 지금 보는 곳의 가장 큰 생물에 맞춘다(B ≈ 몸길이 × 6). 그 뒤 축척은 사용자(휠·F·종 고르기)만 바꾼다.
// 상자 안 개체 수는 실제 밀도 × 농축 배율. 배율은 개체 수가 NMIN–NMAX를 벗어날 때만 바꿔서, 축소하면 더 많이·확대하면 더 적게 보인다.
// 모드: 1 mm 넘는 거시생물과 0.1 mm 안 되는 미생물이 함께 살면 둘을 나눠 본다(미생물 = 암시야 현미경, 거시생물 = 물속).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Evo;

public class EvoTank : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { if (!FindAnyObjectByType<EvoTank>()) new GameObject("EvoTank").AddComponent<EvoTank>(); }

    const float BOX = 10, H = BOX / 2;   // 상자 한 변(화면 단위)
    const int NMIN = 40, NMAX = 400, PER = 200;   // 화면 개체 수 범위, 한 계통·단계당 최대
    const float MICRO = 1e-4f;           // 0.1 mm: 미생물 / 거시생물 경계(맨눈 한계)
    const float SELZ = 3;                // 고른 개체: 상자 한 변 = 몸길이 × 3
    bool noDots;

    World w; Camera cam;
    Material mMicro, mMacro, mDots, mLine; Mesh dots, box, grid, floorM;
    MaterialPropertyBlock mpb; readonly double[] bud = new double[14];

    int speed = 1; double acc; float frameSim;
    static readonly float[] TPS = { 0, 12, 120, 1e9f };
    static readonly string[] SPN = { "멈춤", "1일/초", "10일/초", "최대" };
    float mutX = 1, rateSel = 0.0001f;

    float B = 3e-4f, Bt = 3e-4f; bool fit = true;    // 상자 한 변(m)과 목표값. fit = 다음 Scan에서 한 번 가장 큰 생물에 맞춤
    int fy = 2, fc; bool macro, split; float maxL, conc = 1;
    float yaw = 35, pitch = 18, dist = 18; Vector3 m0; bool dragging, overUI;
    (int slot, int k) want = (-1, 0);                  // 종 목록에서 고른 것: 다음 표본에서 하나 고른다

    sealed class Av { public int id, slot, k; public Vector3 p, dir; public float L, spd, sink, t0, rate; public bool sessile; public Quaternion rot; }
    readonly List<Av> avs = new(); Av sel; float tRes, tScan;
    struct Cand { public int s, k; public float L; public double e; public Morph m; }
    readonly List<Cand> cand = new();

    int V => fy * World.LS + fc;
    bool MacroStyle => split ? macro : B > 4e-3f;
    bool InMode(float L) => !split || (macro ? L >= MICRO : L < MICRO);

    void Start()
    {
        Application.targetFrameRate = 60;
        w = new World(Environment.TickCount & 0xffff);
        cam = new GameObject("Camera").AddComponent<Camera>();
        cam.fieldOfView = 50; cam.nearClipPlane = 0.05f; cam.farClipPlane = 300; cam.clearFlags = CameraClearFlags.SolidColor;
        mMicro = new Material(Shader.Find("Evo/Micro")); mMacro = new Material(Shader.Find("Evo/Macro"));
        mDots = new Material(Shader.Find("Evo/Dots")); mLine = new Material(Shader.Find("Evo/Line"));
        mpb = new MaterialPropertyBlock();
        dots = DotsMesh(3000); box = BoxMesh(); grid = GridMesh(); floorM = FloorMesh();
        Fonts();
        var args = Environment.GetCommandLineArgs(); int ai = Array.IndexOf(args, "-shots");
        if (ai >= 0 && ai + 1 < args.Length) StartCoroutine(Shots(args[ai + 1], Array.IndexOf(args, "-gallery") >= 0));
    }

    // 점검용: EvoTank.x86_64 -shots <폴더> [-gallery] → 화면 사진 몇 장 찍고 끝낸다. -gallery = 여러 체제를 미리 넣어 몸 그리기 확인
    static readonly (int g, float v)[][] GAL = {
        new[] { (Gn.Cells, 4f) }, new[] { (Gn.Wall, 0.8f), (Gn.Photo, 0.9f), (Gn.Elong, 2.5f), (Gn.Flagella, 0f) }, new[] { (Gn.Cilia, 0.9f), (Gn.Size, 5.5f), (Gn.Flagella, 0f) },
        new[] { (Gn.Lorica, 0.8f), (Gn.Spines, 0.7f), (Gn.Size, 4.5f) }, new[] { (Gn.Cells, 10f), (Gn.Diff, 0.7f), (Gn.Form, 1f) },
        new[] { (Gn.Cells, 14f), (Gn.Diff, 0.8f), (Gn.Form, 1f), (Gn.Gut, 0.6f), (Gn.Neuro, 0.4f) },
        new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Elong, 2f) },
        new[] { (Gn.Cells, 20f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.8f), (Gn.Neuro, 0.7f), (Gn.Bilat, 0.8f), (Gn.Skel, 0.5f), (Gn.Circ, 0.4f), (Gn.Eye, 0.3f), (Gn.Elong, 2f) },
        new[] { (Gn.Cells, 28f), (Gn.Diff, 1f), (Gn.Form, 1f), (Gn.Gut, 0.9f), (Gn.Neuro, 0.8f), (Gn.Bilat, 0.9f), (Gn.Skel, 0.8f), (Gn.Circ, 0.8f), (Gn.Eye, 0.8f), (Gn.Elong, 1.5f), (Gn.Buoy, 0.3f), (Gn.Egg, 10f) },
    };
    System.Collections.IEnumerator Shots(string dir, bool gallery)
    {
        System.IO.Directory.CreateDirectory(dir);
        if (gallery)
        {
            var par = w.strains[w.live[0]];
            foreach (var set in GAL)
            {
                var g = (float[])par.g.Clone(); foreach (var (j, x) in set) g[j] = x;
                var ns = w.Inject(par.slot, g, 1e-6); if (ns == null) continue;
                for (int v = 0; v < World.V; v++) { double c = 0; for (int k = 0; k < ns.K; k++) c += w.cnt[World.Ix(ns.slot, v, k)]; for (int k = 0; k < ns.K; k++) w.cnt[World.Ix(ns.slot, v, k)] = Math.Round(c / ns.K); }
            }
        }
        System.Collections.IEnumerator Snap(string n)
        {
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            Debug.Log($"[EvoTank] shot {n} avs {avs.Count} B {B:G3} split {split} macro {macro} sel {sel != null} live {w.live.Count} ms {frameSim:0.0} | " + string.Join(" ", avs.GroupBy(a => (World.Grade(w.strains[a.slot]), a.k, (int)(a.L * 1e6))).OrderByDescending(gr => gr.Count()).Take(8).Select(gr => $"g{gr.Key.Item1}k{gr.Key.Item2}:{gr.Key.Item3}um×{gr.Count()}"))); yield return null;
        }
        var wait = new WaitForSecondsRealtime(3);
        yield return wait; yield return Snap("1-start"); noDots = true; yield return null; yield return Snap("1b-nodots"); noDots = false;
        int n0 = avs.Count; float b0 = Bt *= 2; yield return wait;   // 축소한 축척이 그대로 있는지, 개체가 늘어나는지
        Debug.Log($"[EvoTank] zoomhold {(Bt == b0 && Mathf.Abs(B / b0 - 1) < 0.05f ? "ok" : "FAIL")} Bt {Bt:G3} B {B:G3} avs {n0}->{avs.Count}");
        if (split) { Mode(false); yield return wait; yield return Snap("2-micro"); Mode(true); yield return wait; }
        var big = avs.OrderByDescending(a => a.L).FirstOrDefault();
        if (big != null) { sel = big; Bt = big.L * SELZ; tab = 1; }
        yield return wait; yield return Snap("3-select");
        sel = null; tab = 2; yield return null; yield return Snap("4-species");
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
        Controls(dt); Move(dt); Draw();
    }

    // 전체에서 가장 큰 생물·가장 작은 생물 → 모드 나눔, 자동 축척
    void Scan()
    {
        maxL = 0; float minL = 1;
        foreach (int s in w.live) { var st = w.strains[s]; if (st.total < 10) continue; float L = w.AdultL(st) * 1e-6f; maxL = Mathf.Max(maxL, L); minL = Mathf.Min(minL, L); }
        bool was = split; split = maxL > 1e-3f && minL < MICRO;
        if (split && !was) { macro = B > 2e-3f; w.Log("scale", "거시생물(1 mm 이상)이 나타나 미생물 관찰과 거시생물 관찰을 나눕니다."); }   // 축척은 그대로
        if (fit) { var b = Biggest(true); if (b.s < 0) b = Biggest(false); if (b.L > 0) { Bt = Mathf.Clamp(b.L * 6, 2e-5f, 3); fit = false; } }
    }

    // 지금 모드에서 가장 큰 것(계통, 단계, 길이 m). here = 보는 칸 안에서만
    (int s, int k, float L) Biggest(bool here)
    {
        (int, int, float) b = (-1, 0, 0);
        foreach (int s in w.live)
        {
            var st = w.strains[s];
            for (int k = 0; k < st.K; k++)
            {
                if (st.stage[k] < 1 || here && w.cnt[World.Ix(s, V, k)] <= 0) continue;
                float L = w.M(s, V, k).L * 1e-6f; if (InMode(L) && L > b.Item3) b = (s, k, L);
            }
        }
        return b;
    }

    // 보는 칸의 실제 밀도 × 농축 배율 → 상자 안 개체 수. 이미 있는 개체는 되도록 그대로 둔다
    void Resample()
    {
        int v = V; double boxML = (double)B * B * B * 1e6, sum = 0; int big = -1; float bigL = 0;
        cand.Clear();
        foreach (int s in w.live)
        {
            var st = w.strains[s];
            for (int k = 0; k < st.K; k++)
            {
                double c = w.cnt[World.Ix(s, v, k)]; if (c <= 0) continue;
                var m = w.M(s, v, k); float L = m.L * 1e-6f;
                if (!InMode(L) || L < B * 0.006f || L > B * 0.5f) continue;
                cand.Add(new Cand { s = s, k = k, L = L, e = c / World.VML * boxML, m = m }); sum += cand[^1].e;
                if (L > bigL) { bigL = L; big = cand.Count - 1; }
            }
        }
        double nOn = sum * conc; if (sum > 0 && (nOn > NMAX || nOn < NMIN)) conc = (float)((nOn > NMAX ? NMAX : NMIN) / sum);
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
            if ((c.s, c.k) == want) { sel = avs.LastOrDefault(a => a.slot == c.s && a.k == c.k); want = (-1, 0); }
        }
    }
    static long Key(int s, int k) => ((long)s << 8) | (uint)k;

    Av Spawn(in Cand c)
    {
        var st = w.strains[c.s];
        var a = new Av { id = st.id, slot = c.s, k = c.k, L = c.L, spd = c.m.speed * 1e-6f, sink = c.m.sink / 3600f, sessile = c.m.sessile, t0 = UnityEngine.Random.value * 100, rate = 1 };
        a.p = new Vector3(UnityEngine.Random.Range(-H, H), UnityEngine.Random.Range(-H, H), UnityEngine.Random.Range(-H, H));
        a.dir = UnityEngine.Random.onUnitSphere; a.rot = UnityEngine.Random.rotation;
        if (a.sessile) { a.p.y = -H; a.rot = Quaternion.LookRotation(Vector3.up, Vector3.forward) * Quaternion.Euler(0, 0, UnityEngine.Random.value * 360); }
        return a;
    }

    void Move(float dt)
    {
        float u = BOX / B;   // 화면 단위 / m
        foreach (var a in avs)
        {
            if (a.sessile) continue;
            if (a.spd > 0)
            {
                a.dir = (a.dir + UnityEngine.Random.insideUnitSphere * dt * 1.5f).normalized;
                a.p += a.dir * Mathf.Min(a.spd * u, BOX * 0.3f) * dt;
                a.rot = Quaternion.Slerp(a.rot, Quaternion.LookRotation(a.dir), 1 - Mathf.Exp(-dt * 5));
            }
            else a.rot *= Quaternion.Euler(dt * 9, dt * 6, 0);   // 헤엄 못 하는 것은 물살에 천천히 구른다
            a.p.y -= Mathf.Clamp(a.sink * u, -BOX * 0.2f, BOX * 0.2f) * dt;
            a.p = Wrap(a.p);
        }
        if (sel != null && !sel.sessile)   // 고른 개체를 가운데에 둔다(떠다니는 원점)
        {
            var o = sel.p; bool floorOn = fy == World.NY - 1;
            foreach (var a in avs) { if (a.sessile) { if (!floorOn) continue; a.p.x -= o.x; a.p.z -= o.z; } else a.p -= floorOn ? new Vector3(o.x, 0, o.z) : o; a.p = Wrap(a.p); }
        }
    }
    static Vector3 Wrap(Vector3 p) => new(Mathf.Repeat(p.x + H, BOX) - H, Mathf.Repeat(p.y + H, BOX) - H, Mathf.Repeat(p.z + H, BOX) - H);

    // ---------- 그리기 ----------
    void Draw()
    {
        bool ms = MacroStyle; int v = V; float sun = Mathf.Clamp01(w.I[v] * 1.6f), u = BOX / B;
        var water = Color.Lerp(new Color(0.01f, 0.03f, 0.06f), new Color(0.12f, 0.35f, 0.45f), sun);
        cam.backgroundColor = ms ? water : new Color(0.01f, 0.012f, 0.02f);
        Shader.SetGlobalVector("_FogCol", new Vector4(water.r, water.g, water.b, 0.03f)); Shader.SetGlobalFloat("_Sun", sun);
        var target = sel != null && fy == World.NY - 1 && !sel.sessile ? new Vector3(0, sel.p.y, 0) : Vector3.zero;
        cam.transform.position = target + Quaternion.Euler(pitch, yaw, 0) * new Vector3(0, 0, -dist); cam.transform.LookAt(target);
        float S = w.env[Ev.Salt], chl = 1.5f * (1 - w.I[v] / (w.I[v] + 0.15f));
        foreach (var a in avs)
        {
            var st = w.strains[a.slot]; if (st == null || st.id != a.id) continue;
            var m = w.M(a.slot, v, a.k); var g = st.g;
            float swell = S > g[Gn.Osmo] * 1.08f ? -1 : S < w.P.OSMO_FLOOR && g[Gn.Wall] < 0.5f && g[Gn.Vacuole] < 0.5f ? 1 : 0;
            var mesh = Bodies.Get(st, a.k, m, swell, g[Gn.Photo] > 0.05f ? chl : 0);
            var e = new Vector3(Mathf.Abs(a.p.x), Mathf.Abs(a.p.y), Mathf.Abs(a.p.z));
            mpb.SetFloat("_Fade", Mathf.Clamp01((H - Mathf.Max(e.x, Mathf.Max(a.sessile ? 0 : e.y, e.z))) / 0.8f + (a == sel ? 1 : 0)));
            mpb.SetFloat("_Sel", a == sel ? 1 : 0); mpb.SetFloat("_T0", a.t0); mpb.SetFloat("_Rate", a.rate);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(a.p, a.rot, Vector3.one * Mathf.Max(a.L, m.L * 1e-6f) * u), ms ? mMacro : mMicro, 0, null, 0, mpb);
        }
        // 너무 작아 몸을 못 그리는 것: 미생물 모드는 세균, 거시 모드는 미생물 안개
        double boxML = (double)B * B * B * 1e6, small = 0;
        if (ms) foreach (int s in w.live) { var st = w.strains[s]; for (int k = 0; k < st.K; k++) { double c = w.cnt[World.Ix(s, v, k)]; if (c > 0 && w.M(s, v, k).L * 1e-6f < B * 0.006f) small += c; } }
        double nDots = ms ? Math.Log10(1 + small / World.VML * boxML) * 400 : w.bac[v] * boxML * conc;
        mDots.SetFloat("_Count", (float)Math.Min(1, nDots / 3000)); mDots.SetFloat("_Box", BOX);
        mDots.SetFloat("_Size", ms ? 0.025f : Mathf.Max(0.02f, 0.6e-6f * u));
        mDots.SetColor("_Color", ms ? new Color(0.7f, 0.9f, 0.7f, 0.35f) : new Color(0.75f, 0.8f, 1f, 0.5f));
        if (!noDots) Graphics.DrawMesh(dots, Matrix4x4.identity, mDots, 0);
        Graphics.DrawMesh(box, Matrix4x4.identity, mLine, 0);
        if (fy == World.NY - 1) Graphics.DrawMesh(ms ? floorM : grid, Matrix4x4.identity, ms ? mMacro : mLine, 0);
    }

    // ---------- 조작 ----------
    void Controls(float dt)
    {
        var mp = Input.mousePosition; var gp = new Vector2(mp.x, Screen.height - mp.y) / UIScale;
        overUI = panel.Contains(gp) || top.Contains(gp) || info.Contains(gp);
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) { m0 = mp; dragging = !overUI; }
        if (dragging && (Input.GetMouseButton(0) || Input.GetMouseButton(1))) { var d = Input.mousePosition - m0; m0 = Input.mousePosition; yaw += d.x * 0.25f; pitch = Mathf.Clamp(pitch - d.y * 0.25f, -80, 80); }
        if (Input.GetMouseButtonUp(0) && dragging && (Input.mousePosition - downAt).sqrMagnitude < 36) Pick(mp);
        if (Input.GetMouseButtonDown(0)) downAt = mp;
        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)) dragging = false;
        float sc = Input.mouseScrollDelta.y; if (!overUI && sc != 0) { Bt = Mathf.Clamp(Bt * Mathf.Exp(-sc * 0.15f), 2e-5f, 3); fit = false; }
        if (Input.GetKeyDown(KeyCode.Space)) speed = speed == 0 ? 1 : 0;
        for (int i = 0; i < 4; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) speed = i;
        if (Input.GetKeyDown(KeyCode.PageUp)) Layer(-1);
        if (Input.GetKeyDown(KeyCode.PageDown)) Layer(1);
        if (Input.GetKeyDown(KeyCode.Tab) && split) Mode(!macro);
        if (Input.GetKeyDown(KeyCode.F)) Fit();
        if (Input.GetKeyDown(KeyCode.Escape)) sel = null;
    }
    Vector3 downAt;

    void Pick(Vector3 mp)
    {
        Av best = null; float bz = 1e9f, f = Screen.height / (2 * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2)), u = BOX / B;
        foreach (var a in avs)
        {
            var sp = cam.WorldToScreenPoint(a.p); if (sp.z <= 0) continue;
            float r = a.L * u * 0.5f * f / sp.z + 8;
            if (((Vector2)sp - (Vector2)mp).sqrMagnitude < r * r && sp.z < bz) { bz = sp.z; best = a; }
        }
        sel = best; if (sel != null) tab = 1;   // 고르기만 한다. 축척은 F로
    }

    void Layer(int d) { fy = Mathf.Clamp(fy + d, 0, World.NY - 1); sel = null; avs.Clear(); tRes = 0; }
    void Mode(bool mac) { macro = mac; sel = null; fit = true; avs.Clear(); tScan = tRes = 0; }

    // F: 고른 개체가 있으면 그것에 맞추고, 없으면 가장 큰 생물이 가장 많은 층으로 가서 하나를 고른다
    void Fit()
    {
        if (sel != null) { Bt = Mathf.Clamp(sel.L * SELZ, 2e-5f, 3); return; }
        var (s, k, _) = Biggest(false); if (s >= 0) Focus(s, k);
    }
    void Focus(int s, int k)
    {
        int by = fy, bc = fc; double bn = 0;
        for (int v = 0; v < World.V; v++) { double c = w.cnt[World.Ix(s, v, k)]; if (c > bn) { bn = c; by = v / World.LS; bc = v % World.LS; } }
        fy = by; fc = bc; avs.Clear(); sel = null; want = (s, k); tRes = 0;
        if (split) macro = w.M(s, fy * World.LS + fc, k).L * 1e-6f >= MICRO;
        Bt = Mathf.Clamp(w.M(s, fy * World.LS + fc, k).L * 1e-6f * SELZ, 2e-5f, 3);
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
    static Mesh BoxMesh()
    {
        var c = new List<Vector3>(); for (int i = 0; i < 8; i++) c.Add(new Vector3((i & 1) == 0 ? -H : H, (i & 2) == 0 ? -H : H, (i & 4) == 0 ? -H : H));
        int[] e = { 0, 1, 2, 3, 4, 5, 6, 7, 0, 2, 1, 3, 4, 6, 5, 7, 0, 4, 1, 5, 2, 6, 3, 7 };
        var m = new Mesh { vertices = c.ToArray(), colors = Enumerable.Repeat(new Color(1, 1, 1, 0.12f), 8).ToArray() }; m.SetIndices(e, MeshTopology.Lines, 0); return m;
    }
    static Mesh GridMesh()
    {
        var v = new List<Vector3>(); var idx = new List<int>();
        for (int i = 0; i <= 10; i++) { float x = -H + i; v.Add(new(x, -H, -H)); v.Add(new(x, -H, H)); v.Add(new(-H, -H, x)); v.Add(new(H, -H, x)); }
        for (int i = 0; i < v.Count; i++) idx.Add(i);
        var m = new Mesh { vertices = v.ToArray(), colors = Enumerable.Repeat(new Color(0.6f, 0.55f, 0.4f, 0.25f), v.Count).ToArray() }; m.SetIndices(idx, MeshTopology.Lines, 0); return m;
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
    Font font, fontB; GUIStyle sL, sB, sS, sBtn, sOn, sBox; Texture2D bg;
    Rect panel, top, info; int tab; Vector2 scroll;
    static readonly string[] TABS = { "환경", "생물", "종", "진화" };
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
    }

    void OnGUI()
    {
        Styles(); float s = UIScale; GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
        float W = Screen.width / s, Hh = Screen.height / s;
        top = new Rect(10, 10, W - 20, 40); panel = new Rect(W - 370, 58, 360, Hh - 68); info = new Rect(10, Hh - 132, 520, 122);
        // 머리
        GUI.Box(top, GUIContent.none, sBox);
        int nsp = w.species.Values.Count(x => x.total > 0), gmax = w.gradesSeen.Count > 0 ? w.gradesSeen.Max() : 0;
        GUI.Label(new Rect(22, 18, W - 420, 26), $"<b>진화 수조</b>   {w.Time(w.tick)}   ·   종 {nsp}   ·   개체 {w.totalCount:0.0e0}   ·   가장 발달한 체제: <b>{World.GradeName[gmax]}</b>", sL);
        for (int i = 0; i < 4; i++) if (GUI.Button(new Rect(W - 372 + i * 90, 15, 86, 30), SPN[i], speed == i ? sOn : sBtn)) speed = i;
        // 아래 왼쪽: 축척·층·모드
        GUI.Box(info, GUIContent.none, sBox);
        ScaleBar(new Rect(22, Hh - 124, 300, 30));
        float depth = (fy + 0.5f) * World.DZ;
        GUI.Label(new Rect(22, Hh - 92, 500, 22), $"상자 한 변 {Len(B)}  ·  수심 {depth:0.00} m (층 {fy + 1}/{World.NY})  ·  {(conc >= 1 ? $"실제보다 {Num(conc)}배 농축" : $"실제의 1/{Num(1 / conc)}만 표시")}", sS);
        if (GUI.Button(new Rect(22, Hh - 64, 60, 26), "▲ 위", sBtn)) Layer(-1);
        if (GUI.Button(new Rect(86, Hh - 64, 60, 26), "▼ 아래", sBtn)) Layer(1);
        if (GUI.Button(new Rect(150, Hh - 64, 150, 26), sel != null ? "고른 개체에 맞추기" : "가장 큰 생물에 맞추기", sBtn)) Fit();
        if (split)
        {
            if (GUI.Button(new Rect(304, Hh - 64, 106, 26), "미생물 관찰", !macro ? sOn : sBtn)) Mode(false);
            if (GUI.Button(new Rect(414, Hh - 64, 106, 26), "거시생물 관찰", macro ? sOn : sBtn)) Mode(true);
        }
        GUI.Label(new Rect(22, Hh - 34, 500, 22), "끌기: 돌려 보기 · 휠: 축척 · 클릭: 개체 고르기 · PgUp/PgDn: 층 · Space: 멈춤 · F: 맞추기", sS);
        // 오른쪽 판
        GUI.Box(panel, GUIContent.none, sBox);
        for (int i = 0; i < TABS.Length; i++) if (GUI.Button(new Rect(panel.x + 8 + i * 86, panel.y + 8, 82, 28), TABS[i], tab == i ? sOn : sBtn)) { tab = i; scroll = Vector2.zero; }
        GUILayout.BeginArea(new Rect(panel.x + 10, panel.y + 44, panel.width - 20, panel.height - 52));
        scroll = GUILayout.BeginScrollView(scroll);
        if (tab == 0) EnvTab(); else if (tab == 1) OrgTab(); else if (tab == 2) SpeciesTab(); else EventTab();
        GUILayout.EndScrollView(); GUILayout.EndArea();
    }

    static readonly (string name, float[] env)[] PRESETS = {
        ("원시 바다", new[] { 25f, 35, 60, 30, 2, 50 }), ("어류의 바다(추천)", new[] { 18f, 35, 70, 20, 21, 40 }), ("민물 연못", new[] { 20f, 0.5f, 70, 30, 21, 40 }),
        ("고염 석호", new[] { 28f, 150, 80, 50, 21, 40 }), ("차가운 바다", new[] { 6f, 35, 50, 20, 21, 50 }), ("빛 없는 심해", new[] { 4f, 35, 0, 0, 10, 30 }),
    };
    void EnvTab()
    {
        GUILayout.Label("슬라이더는 목표값이다. 바다는 정한 속도로 천천히 따라간다.", sS);
        for (int e = 0; e < Ev.N; e++)
        {
            var d = Ev.Def[e];
            GUILayout.Label($"{d.name}  <b>{w.env[e]:0.#}{d.unit}</b>  → 목표 {w.target[e]:0.#}{d.unit}", sL);
            float t = GUILayout.HorizontalSlider(w.target[e], d.lo, d.hi); if (t != w.target[e]) w.SetTarget(e, t);
        }
        GUILayout.Space(6); GUILayout.Label("변화 속도", sL);
        GUILayout.BeginHorizontal();
        foreach (var (n, r) in new[] { ("느리게", 0.0001f), ("보통", 0.0005f), ("빠르게", 0.003f), ("즉시", 1f) })
            if (GUILayout.Button(n, rateSel == r ? sOn : sBtn)) { rateSel = r; w.rate = r; }
        GUILayout.EndHorizontal();
        GUILayout.Space(6); GUILayout.Label($"돌연변이 속도 ×{mutX:0.00}  (높을수록 새 계통이 자주 생긴다)", sL);
        float mx = Mathf.Pow(2, GUILayout.HorizontalSlider(Mathf.Log(mutX, 2), -2, 2));
        if (Mathf.Abs(mx - mutX) > 1e-3f) { mutX = mx; var d0 = new Params(); w.P.MUT_NEW = d0.MUT_NEW * mx; w.P.MUT_U = d0.MUT_U * mx; w.P.MUT_FLOOR = d0.MUT_FLOOR * mx; }
        GUILayout.Space(6); GUILayout.Label("미리 정한 바다", sL);
        for (int i = 0; i < PRESETS.Length; i += 2)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + 2, PRESETS.Length); j++) if (GUILayout.Button(PRESETS[j].name, sBtn)) for (int e = 0; e < Ev.N; e++) w.SetTarget(e, PRESETS[j].env[e]);
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(10);
        if (GUILayout.Button("이 환경에서 새로 시작(공통 조상부터)", sBtn)) { w.Reset(w.seed + 1, (float[])w.target.Clone()); avs.Clear(); sel = null; fit = true; split = false; }
        GUILayout.Space(6);
        GUILayout.Label($"물기둥 {World.NY * World.DZ:0} m · 칸 {World.V}개 · 살아 있는 계통 {w.live.Count} · 계산 {frameSim:0} ms/프레임", sS);
    }

    void OrgTab()
    {
        var st = sel != null ? w.strains[sel.slot] : null;   // OnGUI 안에서는 상태를 바꾸지 않는다(Layout·Repaint 칸 수가 어긋남)
        if (st == null || st.id != sel.id) { GUILayout.Label("상자 안의 개체를 누르면 그 개체의 에너지 수지·형질이 나온다.", sL); return; }
        int v = V, q = World.Ix(sel.slot, v, sel.k); var m = w.M(sel.slot, v, sel.k); var sp = w.species.TryGetValue(st.sp, out var x) ? x : null;
        GUILayout.Label($"<b>{sp?.name}</b>", sB); GUILayout.Label(sp?.ko ?? "", sS);
        string stage = !st.dev ? "" : sel.k == 0 ? " · 알·유생" : sel.k == st.K - 1 ? " · 성체" : $" · 어린 개체 {sel.k}/{st.K - 1}";
        GUILayout.Label($"{World.GradeName[World.Grade(st)]}{stage}\n몸길이 {Len(m.L * 1e-6f)} · 세포 {Num(m.C)}개 · 헤엄 {Len(m.speed * 1e-6f)}/s\n임계 산소 {m.pcrit:0.0}% atm · 이 칸 {Num(w.cnt[q] / World.VML)}마리/mL · 세대 {st.gen:0}", sL);
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
        GUILayout.Label("살아 있는 종(많은 순). 누르면 그 종이 가장 많은 곳으로 간다.", sS);
        foreach (var sp in w.species.Values.Where(x => x.total > 0).OrderByDescending(x => x.total).Take(30))
        {
            var c = Color.HSVToRGB(Mathf.Repeat(sp.hue / 360, 1), 0.6f, 1);
            if (GUILayout.Button($"<color=#{ColorUtility.ToHtmlStringRGB(c)}>●</color> <b>{sp.name}</b>  {World.GradeName[sp.grade]}\n<size=11>{sp.ko} · {sp.total:0.0e0}마리</size>", new GUIStyle(sBtn) { alignment = TextAnchor.MiddleLeft, richText = true }))
            {
                int best = -1; double bt = 0; foreach (int s in w.live) if (w.strains[s].sp == sp.id && w.strains[s].total > bt) { bt = w.strains[s].total; best = s; }
                if (best >= 0) { var st = w.strains[best]; int bk = 0; for (int k = 1; k < st.K; k++) if (st.stage[k] > 0) bk = k; Focus(best, st.K > 1 ? bk : 0); }
            }
        }
    }

    void EventTab()
    {
        for (int i = w.events.Count - 1; i >= Math.Max(0, w.events.Count - 60); i--)
        {
            var e = w.events[i]; bool big = e.type == "grade" || e.type == "scale";
            GUILayout.Label($"<color=#8fa3b8>{w.Time(e.t)}</color>  {(big ? "<b>" + e.text + "</b>" : e.text)}", sL);
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
