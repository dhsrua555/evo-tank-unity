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
    Material mMicro, mMacro, mDots, mFloor; Mesh dots, floorM;
    MaterialPropertyBlock mpb; readonly double[] bud = new double[14];

    int speed = 1; double acc; float frameSim;
    // 무거운 틱은 가운데(칸·이동·화학)를 뒤 스레드에서 돌린다(job). 앞·뒤는 화면 스레드에서
    System.Threading.Tasks.Task job;
    float midMs;     // 지난 틱 가운데에 걸린 시간
    bool syncSim;    // 점검 촬영: 세계를 직접 고치므로 늘 이 프레임 안에서
    bool allJobs;    // 점검: 모든 틱을 뒤 스레드로
    int jobs;        // 뒤 스레드로 보낸 틱 수(점검용)
    void SimMid()
    {
        var t = System.Diagnostics.Stopwatch.StartNew();
        w.StepMid();
        midMs = (float)t.Elapsed.TotalMilliseconds;
    }
    // 뒤 스레드 틱을 끝낸다. 세계를 바꾸기 전(사건·다시 시작)에도 부른다
    void SimFinish()
    {
        if (job == null) return;
        var j = job; job = null;
        j.Wait();
        w.StepPost(); acc -= 1;
    }
    void OnApplicationQuit() { try { job?.Wait(); } catch (Exception) { } }
    static readonly float[] TPS = { 0, 12, 120, 1e9f };
    float mutX = 1, rateSel = 0.0001f;

    float B = 3e-4f, Bt = 3e-4f; bool fit = true;    // 상자 한 변(m)과 목표값. fit = 다음 Scan에서 한 번 가장 큰 생물에 맞춤
    float conc = 1;
    float yaw = 35, pitch = 18, dist = 9, camDist = 9; Vector3 m0; bool dragging, overUI;
    (int slot, int k) want = (-1, 0);                  // 종 목록에서 고른 것: 다음 표본에서 하나 고른다

    sealed class Av { public int id, slot, k; public Look look; public Vector3 p, dir, vs = Vector3.one; public float L, spd, sink, t0, rate, grow = 1, pinch = -1, ph; public bool sessile, crawl; public Quaternion rot; }   // vs 개체마다 조금 다른 몸 비율, pinch 분열 진행(0–1)
    readonly List<Av> avs = new(); Av sel; float tRes, tScan;
    struct Cand { public int s, k; public float L; public double e; public Morph m; }
    readonly List<Cand> cand = new();

    int V => vNear;                       // 관찰자에게 가장 가까운 칸(모양·에너지 수지용). 밀도는 Dens로 섞는다
    bool MacroStyle => B > 2e-3f;

    void Start()
    {
        Application.targetFrameRate = 60;
        w = new World(Environment.TickCount & 0xffff) { o2Bio = true, autoQ = true, activity = 0.4f };   // 앱: 산소는 생물이, 사건은 자연이
        w.po.MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 2);   // 화면·렌더 스레드 몫 둘을 남긴다
        Bodies.Sp = id => w.species.TryGetValue(id, out var s) ? s : null;   // 몸 그리기가 종 표지를 찾는 길
        cam = new GameObject("Camera").AddComponent<Camera>();
        cam.fieldOfView = 50; cam.nearClipPlane = 0.05f; cam.farClipPlane = 300; cam.clearFlags = CameraClearFlags.SolidColor;
        mMicro = new Material(Shader.Find("Evo/Micro")); mMacro = new Material(Shader.Find("Evo/Macro")); mFloor = new Material(Shader.Find("Evo/Floor"));
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
        // 4단계 몸 설계(부품 조합): 갯지렁이 · 절지동물 · 요각류 · 삼엽충 · 고둥 · 조개 · 화살벌레 · 불가사리 · 성게 · 산호 · 칠성장어 · 갑주어 · 판피어 · 상어 · 경골어류
        Wm((Gn.Elong, 2f), (Gn.Seg, 4f), (Gn.Burrow, 0.8f)), Wm((Gn.Elong, 2f), (Gn.Seg, 4f), (Gn.Limb, 0.5f)), Wm((Gn.Cells, 18f), (Gn.Elong, 1.2f), (Gn.Seg, 4f), (Gn.Limb, 0.7f), (Gn.Exo, 0.6f), (Gn.Circ, 0.5f), (Gn.Eye, 0.4f), (Gn.Burrow, 0.6f)),
        Wm((Gn.Cells, 14f), (Gn.Elong, 1f), (Gn.Seg, 3f), (Gn.Limb, 0.7f), (Gn.Exo, 0.5f), (Gn.Eye, 0.3f), (Gn.PreyR, 2.5f)), Wm((Gn.Cells, 18f), (Gn.Elong, 1f), (Gn.Seg, 4f), (Gn.Limb, 0.6f), (Gn.Exo, 0.6f), (Gn.Mineral, 0.6f), (Gn.Circ, 0.5f), (Gn.Eye, 0.5f), (Gn.Burrow, 0.6f)),
        Wm((Gn.Elong, 0.5f), (Gn.Exo, 0.6f), (Gn.Mineral, 0.7f), (Gn.Jaw, 0.5f), (Gn.Herb, 0.6f), (Gn.Circ, 0.5f)), Wm((Gn.Neuro, 0.3f), (Gn.Elong, 0.3f), (Gn.Exo, 0.6f), (Gn.Mineral, 0.7f), (Gn.Burrow, 0.7f), (Gn.Cilia, 0.7f), (Gn.Circ, 0.5f)),
        Wm((Gn.Elong, 2f), (Gn.Jaw, 0.6f), (Gn.Transp, 0.7f), (Gn.Ambush, 0.7f), (Gn.Eye, 0.3f), (Gn.Pigment, 0f)),
        Wm((Gn.Bilat, 0f), (Gn.Neuro, 0.4f), (Gn.Exo, 0.6f), (Gn.Mineral, 0.7f), (Gn.Limb, 0.5f)), Wm((Gn.Bilat, 0f), (Gn.Neuro, 0.4f), (Gn.Exo, 0.6f), (Gn.Mineral, 0.7f)),
        Wm((Gn.Bilat, 0f), (Gn.Neuro, 0.3f), (Gn.Gut, 0.6f), (Gn.Attach, 0.7f), (Gn.Mineral, 0.6f), (Gn.Cells, 12f)),
        Fi(), Fi((Gn.Mineral, 0.6f)), Fi((Gn.Jaw, 0.7f), (Gn.Exo, 0.5f), (Gn.Mineral, 0.6f)), Fi((Gn.Jaw, 0.7f), (Gn.Limb, 0.5f)), Fi((Gn.Jaw, 0.7f), (Gn.Mineral, 0.6f), (Gn.Limb, 0.5f)),
        // 5단계: 이끼(마름 견디기 + 광합성 다세포)
        new[] { (Gn.Cells, 12f), (Gn.Diff, 0.8f), (Gn.Form, 0.5f), (Gn.Photo, 0.8f), (Gn.Attach, 0.7f), (Gn.Wall, 0.6f), (Gn.Phago, 0f), (Gn.Dry, 0.9f) },
    };
    static (int g, float v)[] Wm(params (int, float)[] x) => new[] { (Gn.Cells, 16f), (Gn.Diff, 0.9f), (Gn.Form, 1f), (Gn.Gut, 0.7f), (Gn.Neuro, 0.6f), (Gn.Bilat, 0.6f), (Gn.Photo, 0f), (Gn.Phago, 0.4f), (Gn.Aerobic, 1f) }.Concat(x).ToArray();
    static (int g, float v)[] Fi(params (int, float)[] x) => new[] { (Gn.Cells, 28f), (Gn.Diff, 1f), (Gn.Form, 1f), (Gn.Gut, 0.9f), (Gn.Neuro, 0.8f), (Gn.Bilat, 0.9f), (Gn.Skel, 0.8f), (Gn.Circ, 0.8f), (Gn.Eye, 0.8f), (Gn.Elong, 1.5f), (Gn.Photo, 0f), (Gn.Phago, 0.3f), (Gn.Aerobic, 1f), (Gn.Buoy, 0.3f), (Gn.Egg, 10f), (Gn.Jaw, 0f) }.Concat(x).ToArray();
    System.Collections.IEnumerator Shots(string dir, bool gallery)
    {
        System.IO.Directory.CreateDirectory(dir); var galS = new List<Strain>(); syncSim = true; SimFinish();   // 점검은 세계를 직접 고치므로 틱을 늘 이 프레임 안에서
        if (gallery)
        {
            w.PruneTo(GAL.Length + 4);
            var par = w.strains[w.live[0]];
            foreach (var set in GAL)
            {
                var g = (float[])par.g.Clone(); foreach (var (j, x) in set) g[j] = x;
                var ns = w.Inject(par.slot, g, 1e-6); if (ns == null) continue; galS.Add(ns);
                for (int v = 0; v < World.V; v++) { double c = 0; for (int k = 0; k < ns.K; k++) c += w.cnt[World.Ix(ns.slot, v, k)]; for (int k = 0; k < ns.K; k++) w.cnt[World.Ix(ns.slot, v, k)] = c > 0 ? Math.Max(1, Math.Round(c / ns.K)) : 0; }
            }
        }
        System.Collections.IEnumerator Snap(string n)
        {
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            Debug.Log($"[EvoTank] shot {n} avs {avs.Count} B {B:G3} depth {py:0.00} conc {conc:G3} sel {sel != null} live {w.live.Count} ms {frameSim:0.0} | " + string.Join(" ", avs.GroupBy(a => ((int)a.look, a.k, (int)(a.L * 1e6))).OrderByDescending(gr => gr.Count()).Take(8).Select(gr => $"g{gr.Key.Item1}k{gr.Key.Item2}:{gr.Key.Item3}um×{gr.Count()}"))); yield return null;
        }
        var wait = new WaitForSecondsRealtime(3);
        yield return wait; yield return Snap("1-start"); noDots = true; yield return null; yield return Snap("1b-nodots"); noDots = false;
        if (gallery)   // 몸 설계마다 확대 카드 그림을 따로 저장(단세포 card-<설계>.png, 다세포 card-L<몸 설계>.png = 어른 단계)
        {
            Unfollow(); var tex = new Texture2D(512, 512, TextureFormat.RGB24, false);
            foreach (var st in galS.Concat(w.live.Select(x => w.strains[x])).Where(x => w.strains[x.slot] == x).GroupBy(x => Taxa.Of(x) == Look.Cell ? (int)Bodies.PlanOf(x) * 2 + (x.aspect < 1.5f ? 0 : 1) : 100 + (int)Taxa.Of(x) * 2 + (Taxa.Of(x) == Look.Echino && x.g[Gn.Limb] < 0.3f ? 1 : 0)).Select(gp => gp.First()).ToList())
            {
                var lk = Taxa.Of(st); int k = lk == Look.Cell ? 0 : st.K - 1;
                sel = new Av { id = st.id, slot = st.slot, k = k, look = lk, dir = Vector3.forward, rot = Quaternion.identity, L = w.M(st.slot, V, k).L * 1e-6f, rate = 1 };
                cYaw = 60; cPitch = 14; cDragT = Time.time; yield return null; yield return null;
                RenderTexture.active = cardRT; tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); RenderTexture.active = null;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, lk == Look.Cell ? $"card-{(int)Bodies.PlanOf(st)}{Bodies.PlanOf(st)}{(st.aspect < 1.5f ? "" : "-long")}.png" : $"card-L{(int)lk:00}{lk}{(lk == Look.Echino && st.g[Gn.Limb] < 0.3f ? "-urchin" : lk == Look.Seaweed && st.g[Gn.Dry] >= 0.8f ? "-moss" : "")}.png"), tex.EncodeToPNG());
                Debug.Log($"[EvoTank] plan {(lk == Look.Cell ? Bodies.PlanOf(st).ToString() : lk.ToString())} {StageName(st, k)} verts {cardMesh?.vertexCount} parts {string.Join(",", Bodies.Parts(cardMesh).Select(x => x.name))}");
            }
            sel = null;
            // 자매 종이 서로 달라 보이는지: 몸 설계마다 부모 종(왼쪽) + 자식 종 넷을 나란히 그린다 → sibs.png
            var rts = new RenderTexture(256, 256, 24) { antiAliasing = 4 }; rts.Create();
            var reps = galS.GroupBy(x => Taxa.Of(x) == Look.Cell ? (int)Bodies.PlanOf(x) : 100 + (int)Taxa.Of(x)).Select(gr => gr.First()).Take(8).ToList();
            var sheet = new Texture2D(256 * 5, 256 * reps.Count, TextureFormat.RGB24, false);
            for (int row = 0; row < reps.Count; row++)
            {
                var st = reps[row]; var par = w.species[st.sp];
                for (int j = 0; j < 5; j++)
                {
                    var sp = par;
                    if (j > 0) { sp = new Species { id = 90000 + row * 10 + j, parent = par.id }; Species.Inherit(sp, par, w.seed); w.species[sp.id] = sp; }
                    var ph = new Strain { id = 800000 + row * 10 + j, sp = sp.id, g = st.g, hue = st.hue }; World.Shape(ph);
                    int k = ph.K - 1; var m = w.MorphAt(ph, V, k, 0); bool mac = m.L * 1e-6f >= MICRO;
                    Render1(rts, Bodies.Get(ph, k, m, 0, 0), mac, Quaternion.Euler(16, 60, 0) * new Vector3(0, 0, -2.3f), ph.id, 1, m.sessile);
                    RenderTexture.active = rts; sheet.ReadPixels(new Rect(0, 0, 256, 256), 256 * j, 256 * (reps.Count - 1 - row)); RenderTexture.active = null;
                    if (j > 0) w.species.Remove(sp.id);
                }
            }
            sheet.Apply(); System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "sibs.png"), sheet.EncodeToPNG()); rts.Release();
            Debug.Log($"[EvoTank] sibs rows {reps.Count}: {string.Join(", ", reps.Select(x => Taxa.Of(x) == Look.Cell ? Bodies.PlanOf(x).ToString() : Taxa.Of(x).ToString()))}");
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
        if (sel != null) Debug.Log($"[EvoTank] card {(cardMesh != null ? "ok" : "FAIL")} look {Taxa.Of(w.strains[sel.slot])} k {sel.k} bounds {cardMesh?.bounds.size} parts {string.Join(",", Bodies.Parts(cardMesh).Select(x => x.name))}");
        // 혈통 점검: 후손 있는 계통을 없애면 그 후손으로, 후손 없는 계통을 없애면 '끊김' 카드(또는 그새 생긴 후손)로
        void Kill(int id) { int s = idSlot[id]; for (int v = 0; v < World.V; v++) for (int k = 0; k < World.KM; k++) w.cnt[World.Ix(s, v, k)] = 0; }
        Index(); int f0 = w.live.Select(x => w.strains[x].id).FirstOrDefault(id => BestDesc(id) >= 0);
        if (f0 > 0) { Follow(f0); Kill(f0); yield return wait; Debug.Log($"[EvoTank] linetest desc {(follow > 0 && follow != f0 && Chain(follow).Any(r => r.id == f0) ? "ok" : "FAIL")} {f0}->{follow}"); win = 2; yield return null; yield return null; yield return Snap("3b-line"); win = 0; }
        Index(); int f1 = w.live.Select(x => w.strains[x].id).FirstOrDefault(id => BestDesc(id) < 0);
        if (f1 > 0) { Follow(f1); Kill(f1); yield return wait; Debug.Log($"[EvoTank] linetest gone {(gone?.id == f1 || follow > 0 && Chain(follow).Any(r => r.id == f1) ? "ok" : "FAIL")} {f1} follow {follow} gone {gone?.id} why {gone?.why}"); yield return Snap("3c-gone"); }
        sel = null; tab = 2; yield return null; yield return Snap("4-species");
        showTray = true; yield return wait; yield return Snap("4b-tray"); trayReal = true; yield return null; yield return Snap("4c-tray-real"); showTray = trayReal = false;
        double d0 = py; MoveBy(new Vector3(0, -1f, 0)); yield return wait;   // 헤엄 점검: 1 m 내려가면 수심이 그만큼 바뀐다
        Debug.Log($"[EvoTank] swim depth {d0:0.00}->{py:0.00} floor {Floor:0.00} {(Math.Abs(py - Math.Min(d0 + 1, Floor)) < 1e-3 ? "ok" : "FAIL")} avs {avs.Count}"); yield return Snap("2-swim");
        float bk = Bt; Bt = 1; yield return wait; yield return Snap("2b-wide"); Bt = bk; MoveBy(new Vector3(0, 1f, 0)); yield return wait;   // 제자리로
        double pk = py; float bk2 = Bt; MoveBy(new Vector3(0, (float)(py - 0.08), 0)); Bt = 0.4f; float pp = pitch; pitch = -30; yield return wait; yield return Snap("6-surface"); pitch = pp;   // 물속 느낌: 수면 가까이·깊은 곳
        MoveBy(new Vector3(0, -6f, 0)); yield return wait; yield return Snap("6b-deep"); MoveBy(new Vector3(0, (float)(py - pk), 0)); Bt = bk2; yield return wait;
        int r0 = reg, rd = 0; for (int c = 1; c < w.NR; c++) if (w.geo.rDepth[c] > w.geo.rDepth[rd]) rd = c;   // 해역 옮기기: 가장 깊은 바다 300 m
        GoTo(rd, 300); yield return wait; Debug.Log($"[EvoTank] region {r0}->{reg} {Geo.DName(w.geo.rDepth[reg])} depth {py:0} floor {Floor:0} vNear y{vNear / World.LS} {(reg == rd && Math.Abs(py - 300) < 1e-3 ? "ok" : "FAIL")}"); yield return Snap("7-ocean");
        // 땅: 조간대 웅덩이, 육지 흙 속 물막. 해역 번호는 다시 나누면 바뀌므로 갈 때마다 새로 찾는다
        int FirstOf(int kind)
        {
            for (int c = w.geo.NO; c < w.NR; c++) if (w.geo.rCells[c] > 0 && w.geo.rKind[c] == kind) return c;
            return -1;
        }
        int rs = FirstOf(1);
        if (rs >= 0) { GoTo(rs, 0.5); yield return wait; Debug.Log($"[EvoTank] shore {w.geo.Name(reg)} floor {Floor:0.0} obs {obsCell} {(w.geo.rKind[reg] == 1 && w.geo.In(obsCell, reg) ? "ok" : "FAIL")}"); yield return Snap("7b-shore"); }
        int rl = FirstOf(2);
        if (rl >= 0) { GoTo(rl, 0.05); yield return wait; Debug.Log($"[EvoTank] land {w.geo.Name(reg)} floor {Floor:0.00} obs {obsCell} {(w.geo.rKind[reg] == 2 && w.geo.In(obsCell, reg) ? "ok" : "FAIL")}"); yield return Snap("7c-land"); }
        GoTo(r0, 1.25); yield return wait;
        // 자연현상 점검: 지금 해역에 거대 화산, 빙하기 → 빙상·해수면·해역 다시 나누기, 계통수 사건 줄, 고급 패널
        tab = 0; qSel = 0; qSize = 3; float t0 = w.env[Ev.Temp]; FireQ(obsCell); yield return wait;
        Debug.Log($"[EvoTank] quake volcano {(w.quakes.Count > 0 && w.aer > 1 ? "ok" : "FAIL")} aer {w.aer:0.00} T {t0:0.0}->{w.env[Ev.Temp]:0.0} text {w.quakes[^1].text}"); yield return Snap("8-volcano");
        int rg0 = w.regridN; w.Fire(Qk.IceAge, 3, -1, World.IceAmp(3), 0, 40); speed = 3; yield return new WaitForSecondsRealtime(12);
        Debug.Log($"[EvoTank] quake ice ice {w.ice:0.00} sea {w.geo.sea:0} regrid {rg0}->{w.regridN} reg {reg}/{w.NR} obs {obsCell}->{w.geo.Any(obsCell)} {(w.regridN > rg0 && reg < w.NR && w.geo.Any(obsCell) == reg ? "ok" : "FAIL")} T {w.env[Ev.Temp]:0.0}"); yield return Snap("8b-ice");
        speed = 0; win = 1; yield return null; yield return null; yield return Snap("8c-tree");
        win = 2; yield return null; yield return null; yield return Snap("8e-path"); win = 0;
        tab = 0; showCycle = showWorld = showAdv = true; yield return null; yield return null; yield return Snap("8d-advanced"); showCycle = showWorld = showAdv = false;
        tab = 3; yield return null; yield return Snap("8f-log");
        speed = 3; tab = 3; yield return new WaitForSecondsRealtime(gallery ? 8 : 30); yield return Snap("5-fast");
        // 뒤 스레드 틱 점검: 모든 틱을 뒤 스레드로 보내도 세계가 나아가고 화면이 멈추지 않는지
        SimFinish(); syncSim = false; allJobs = true; speed = 2;
        int j0 = jobs, fr0 = Time.frameCount; long k0 = w.tick;
        yield return new WaitForSecondsRealtime(5);
        SimFinish(); allJobs = false; syncSim = true;
        int nj = jobs - j0, nf = Time.frameCount - fr0; long nk = w.tick - k0;
        Debug.Log($"[EvoTank] thread jobs {nj} ticks {nk} frames {nf} {(nj > 10 && nk >= nj && nf > 100 ? "ok" : "FAIL")}");
        Application.Quit();
    }

    // ---------- 매 프레임 ----------
    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (job != null && job.IsCompleted) SimFinish();
        if (speed > 0)
        {
            acc += dt * TPS[speed];
            var sw = System.Diagnostics.Stopwatch.StartNew(); float budget = speed == 3 ? 30 : 22;
            while (job == null && acc >= 1 && sw.ElapsedMilliseconds < budget)
            {
                w.StepPre();
                // 가벼운 틱: 보통 빠르기는 6 ms 밑, 빠르게(2·3)는 한 프레임에 두 틱 이상 들어갈 때. 예전처럼 이 프레임 안에서 여러 번
                bool light = midMs < (speed == 1 ? 6 : budget / 2);
                if (syncSim || light && !allJobs)
                {
                    SimMid(); w.StepPost(); acc -= 1;
                }
                else   // 무거운 틱은 뒤 스레드에서: 화면은 멈추지 않고 다음 프레임들에 끝을 확인한다
                {
                    job = System.Threading.Tasks.Task.Run(SimMid); jobs++;
                }
            }
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
                double c = Dens(s, k); if (!(c > 0) || double.IsInfinity(c)) continue;   // NaN·무한대는 건너뛴다(개체 수 계산이 넘쳐 메모리를 다 먹지 않게)
                var m = w.M(s, v, k); float L = m.L * 1e-6f;
                if (L < B * 0.006f || L > B * 0.5f || (m.sessile || Crawls(st, k, m)) && yF < -H) continue;   // 붙어 살거나 바닥을 기는 것은 바닥이 보일 때만
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

    // 바닥을 기는 몸: 시뮬의 저서(굴 파기) + 껍데기·골판으로 무거운 연체·극피동물. 유생은 헤엄친다
    static bool Crawls(Strain st, int k, in Morph m) => !m.sessile && (m.benthic || (k > 0 || st.K == 1) && Taxa.Of(st) is Look.Snail or Look.Clam or Look.Echino);

    Av Spawn(in Cand c)
    {
        var st = w.strains[c.s];
        var a = new Av { id = st.id, slot = c.s, k = c.k, L = c.L, spd = c.m.speed * 1e-6f, sink = c.m.sink / 3600f, sessile = c.m.sessile, t0 = UnityEngine.Random.value * 100, look = Taxa.Of(st),
            rate = UnityEngine.Random.Range(0.8f, 1.25f), vs = new Vector3(UnityEngine.Random.Range(0.9f, 1.1f), UnityEngine.Random.Range(0.9f, 1.1f), UnityEngine.Random.Range(0.9f, 1.1f)) };   // 같은 계통도 개체마다 박동 빠르기·몸 비율이 조금씩 다르다
        a.p = UnityEngine.Random.insideUnitSphere * H; a.p.y = Mathf.Clamp(a.p.y, yF, yS);
        a.dir = UnityEngine.Random.onUnitSphere; a.rot = UnityEngine.Random.rotation;
        if (a.sessile) { a.p.y = yF; a.rot = Quaternion.LookRotation(Vector3.up, Vector3.forward) * Quaternion.Euler(0, 0, UnityEngine.Random.value * 360); }
        else if (a.crawl = Crawls(st, c.k, c.m)) { a.p.y = yF; a.dir = Quaternion.Euler(0, UnityEngine.Random.value * 360, 0) * Vector3.forward; a.rot = Quaternion.LookRotation(a.dir); }
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
            if (a.crawl)   // 바닥을 긴다(굴 파는 벌레·고둥·조개·극피동물)
            {
                var dd = UnityEngine.Random.insideUnitSphere * dt; dd.y = 0; a.dir = (a.dir + dd).normalized;
                a.p += a.dir * Mathf.Min(a.spd * u, BOX * 0.3f) * dt; a.p.y = yF; a.p = Wrap(a.p);
                a.rot = Quaternion.Slerp(a.rot, Quaternion.LookRotation(a.dir), 1 - Mathf.Exp(-dt * 3)); continue;
            }
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
                a.dir = (a.dir + UnityEngine.Random.insideUnitSphere * dt * (a.look >= Look.Lancelet ? 0.5f : 1.5f)).normalized;
                float th = a.look == Look.Jelly ? 0.3f + 2.2f * Mathf.Max(0, Mathf.Cos(3 * (T * a.rate + a.t0))) : 1;   // 해파리: 갓이 오므라들 때 나아간다(셰이더 맥동과 같은 위상)
                a.p += a.dir * Mathf.Min(a.spd * u, BOX * 0.3f) * th * dt;
                a.rot = Quaternion.Slerp(a.rot, Quaternion.LookRotation(a.dir), 1 - Mathf.Exp(-dt * 5));
            }
            else a.rot *= Quaternion.Euler(dt * 9, dt * 6, 0);   // 헤엄 못 하는 것은 물살에 천천히 구른다
            a.p.y -= Mathf.Clamp(a.sink * u, -BOX * 0.2f, BOX * 0.2f) * dt;
            a.p = Wrap(a.p);
        }
        if (sel != null && !sel.sessile) MoveBy(new Vector3(sel.p.x, sel.crawl ? 0 : sel.p.y, sel.p.z) / u);   // 고른 개체와 함께 헤엄친다(떠다니는 원점)
    }

    // 물고기 떼: 같은 계통끼리 방향 맞추기·모이기·부딪히지 않기(boids, Reynolds 1987)
    void School(float dt, float u)
    {
        foreach (var a in avs)
        {
            if (a.look < Look.Lancelet || a.spd <= 0 || a.sessile) continue;
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
        int kd = w.geo.rKind[reg]; if (kd > 0) water = Color.Lerp(water, new Color(0.3f, 0.27f, 0.17f) * (0.3f + 0.7f * sun), kd == 2 ? 0.65f : 0.3f);   // 조간대 웅덩이·흙 속 물막: 흙탕 빛
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
            // 판 한 장의 실제 크기 T(m)는 축척 B에 가까운 2의 거듭제곱: 확대·축소하면 판이 관찰자 중심으로 함께 커지고 작아진다
            // (예전: 판 크기가 화면에 고정이라 축척이 바뀔 때마다 무늬 위치 px·u가 크게 튀었다)
            double T = Math.Pow(2, Math.Round(Math.Log(B, 2))); float S = (float)(T * u), sc = S / BOX;
            float ox = -(float)Frac(px / T) * S, oz = -(float)Frac(pz / T) * S;
            // 무늬 네 겹: 칸 크기 B/64·2^(j−f) … 무게 sin²(π(j+1−f)/4)(합 2) — 축척이 2배 바뀌면 겹 하나가 다음 겹 자리로 이어진다
            double lb = Math.Log(B, 2); int k0 = (int)Math.Floor(lb); float fr = (float)(lb - k0); Vector4 L4 = default, W4 = default, OX = default, OZ = default;
            for (int j = 0; j < 4; j++) { double Lj = Math.Pow(2, k0 - 6 + j); L4[j] = (float)Lj; W4[j] = 0.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * (j + 1 - fr) / 4), 2); OX[j] = (float)Frac(px / (Lj * 64)); OZ[j] = (float)Frac(pz / (Lj * 64)); }
            mFloor.SetVector("_L", L4); mFloor.SetVector("_W", W4); mFloor.SetVector("_OX", OX); mFloor.SetVector("_OZ", OZ);
            mFloor.SetColor("_Base", kd == 2 ? new Color(0.36f, 0.27f, 0.17f) : new Color(0.62f, 0.55f, 0.42f)); mFloor.SetFloat("_Kind", kd == 2 ? 1 : 0); mFloor.SetFloat("_FR", 2 * S);
            for (int i = -2; i <= 2; i++) for (int j = -2; j <= 2; j++)
                Graphics.DrawMesh(floorM, Matrix4x4.Translate(new Vector3(ox + i * S, yF, oz + j * S)) * Matrix4x4.Scale(new Vector3(sc, sc, sc)) * Matrix4x4.Translate(new Vector3(0, H, 0)), mFloor, 0);
        }
        LookEnd(ms, u, sun);
    }

    // ---------- 조작 ----------
    void Controls(float dt)
    {
        var mp = Input.mousePosition; var gp = new Vector2(mp.x, Screen.height - mp.y) / UIScale;
        overUI = win != 0 || panel.Contains(gp) || top.Contains(gp) || info.Contains(gp) || gauge.Contains(gp) || mapR.Contains(gp) || card.Contains(gp) || tray.Contains(gp) || help && gp.y > Screen.height / UIScale - 200 && gp.x < 570;
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) { m0 = mp; dragging = !overUI; }
        if (dragging && (Input.GetMouseButton(0) || Input.GetMouseButton(1))) { var d = Input.mousePosition - m0; m0 = Input.mousePosition; if (d.sqrMagnitude > 0) lastDrag = Time.time; yaw += d.x * 0.25f; pitch = Mathf.Clamp(pitch - d.y * 0.25f, -80, 80); }
        if (Input.GetMouseButtonUp(0) && dragging && (Input.mousePosition - downAt).sqrMagnitude < 36) Pick(mp);
        if (Input.GetMouseButtonDown(0)) downAt = mp;
        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)) dragging = false;
        float sc = Input.mouseScrollDelta.y; if (!overUI && sc != 0) { Bt = Mathf.Clamp(Bt * Mathf.Exp(-sc * 0.15f), 2e-5f, 3); fit = false; }
        if (Input.GetKeyDown(KeyCode.Space)) speed = speed == 0 ? 1 : 0;
        for (int i = 0; i < 4; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) speed = i;
        if (Input.GetKeyDown(KeyCode.F)) Fit();
        if (Input.GetKeyDown(KeyCode.Escape)) { if (win != 0) win = 0; else { sel = null; gone = null; Unfollow(); } }
        if (Input.GetKeyDown(KeyCode.T)) win = win == 2 ? 0 : 2;
        if (Input.GetKeyDown(KeyCode.G)) win = win == 1 ? 0 : 1;
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
    static Mesh FloorMesh()   // 평평한 판: 무늬·기복은 Floor 셰이더가 실제 m 좌표로 그린다(판 크기가 바뀌어도 튀지 않게)
    {
        var m = new Mesh { vertices = new[] { new Vector3(-H, -H, -H), new Vector3(H, -H, -H), new Vector3(H, -H, H), new Vector3(-H, -H, H) }, triangles = new[] { 0, 2, 1, 0, 3, 2 } };
        m.RecalculateNormals(); return m;
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
