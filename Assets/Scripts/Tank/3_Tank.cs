// 3단계 — 수조 화면. 행성 단면: 가로 = 해역(바다는 적도 → 극, 그 뒤 조간대·육지), 세로 = 수심층(표층 · 20–100 m · 100–500 m · 심해 · 바닥/열수공).
// 칸 바탕색 = 빛과 물의 화학(산소 = 파랑, 철 = 녹색, 황화수소 = 자주). 살아 있는 종은 모두 한 마리 이상 보이고, 많을수록 여러 마리.
// 몸은 그 종 대표 계통의 세포 배치(Body.cells) 그대로. 휠 = 확대, 끌기 = 이동, 누르기 = 종 고르기.
using System;
using System.Collections.Generic;
using System.Linq;
using Life;
using UnityEngine;

namespace Tank
{
    public sealed partial class Tank : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { var go = new GameObject("EvoTank"); go.AddComponent<Camera>(); go.AddComponent<Tank>(); }

        Runner run; Snap snap, lastSnap;
        readonly List<DayStat> hist = new();
        readonly List<Life.Event> news = new();
        int[] col; int ncol; int nOcean, nShore;   // 해역 → 단면 열
        const int ROWS = 5;

        // 보이는 개체(그림일 뿐: 종 생물량에 맞춰 수를 맞춘다)
        sealed class Ind { public int sp, v; public float x, y, vx, vy, ang, spin, fade = 0; public bool dying; }
        readonly List<Ind> inds = new();
        System.Random rnd = new(1);

        // 화면 배치(GUI 단위)
        float W, H, topH = 46, panelW = 340, graphH = 140;
        Rect tank;
        float zoom = 1, camX, camY;            // 보는 곳(세계 좌표 중심)
        int sel = -1;

        void Awake()
        {
            Application.runInBackground = true;          // 창을 내려도 화면이 계속(시뮬레이션은 어차피 따로 돈다)
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;            // 화면은 30 fps면 충분: 남는 CPU는 시뮬레이션에
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            var cam = GetComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.03f, 0.04f, 0.07f); cam.cullingMask = 0;
            int seed = Arg("-seed", Environment.TickCount & 0xffff);
            run = new Runner(seed);
            Layout();
            camX = ncol / 2f; camY = ROWS / 2f;
            Fonts();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-shots") >= 0) StartCoroutine(Shots());
            Debug.Log($"[EvoTank] 시작: 시드 {seed}, 해역 {run.pl.NR}, 칸 {run.pl.V}");
        }

        void OnDestroy() => run?.Stop();
        void OnApplicationQuit() => run?.Stop();

        static int Arg(string name, int def)
        {
            var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length && int.TryParse(a[i + 1], out int x) ? x : def;
        }

        // 태블릿: 앱이 멈춘 동안 갔어야 할 날을 다시 열 때 따라잡는다(최대 3만 일)
        DateTime pausedAt; double pausedDay, pausedRate;
        void OnApplicationPause(bool p)
        {
            if (run == null) return;
            if (p) { pausedAt = DateTime.UtcNow; pausedDay = run.pl.Day; pausedRate = run.speed > 0 ? run.speed : Math.Max(1, snap?.rate ?? 1); return; }
            if (run.paused || pausedAt == default) return;
            double want = (DateTime.UtcNow - pausedAt).TotalSeconds * pausedRate, done = run.pl.Day - pausedDay;
            run.ffDays = Math.Clamp(want - done, 0, 30000);
        }

        // 열 순서: 바다(따뜻한 → 찬), 조간대, 육지
        void Layout()
        {
            var g = run.pl.geo; int nr = run.pl.NR;
            var order = Enumerable.Range(0, nr).OrderBy(r => g.rKind[r]).ThenBy(r => g.rLat[r]).ThenByDescending(r => g.rDepth[r]).ToArray();
            col = new int[nr]; for (int i = 0; i < nr; i++) col[order[i]] = i;
            ncol = nr; nOcean = order.Count(r => g.rKind[r] == 0); nShore = order.Count(r => g.rKind[r] == 1);
        }

        // 칸 → 세계 좌표 사각형(열, 행)
        void Cell(int v, out int c, out int row)
        {
            var p = run.pl;
            if (v >= p.NV0) { c = col[p.ventReg[v - p.NV0]]; row = 4; return; }
            c = col[v / Cfg.NY]; row = v % Cfg.NY;
        }

        Vector2 ToScreen(float wx, float wy)
        {
            float sx = tank.width / ncol * zoom, sy = tank.height / ROWS * zoom;
            return new Vector2(tank.x + tank.width / 2 + (wx - camX) * sx, tank.y + tank.height / 2 + (wy - camY) * sy);
        }
        Vector2 ToWorld(Vector2 s)
        {
            float sx = tank.width / ncol * zoom, sy = tank.height / ROWS * zoom;
            return new Vector2(camX + (s.x - tank.x - tank.width / 2) / sx, camY + (s.y - tank.y - tank.height / 2) / sy);
        }

        void Update()
        {
            Draw.ui = Application.isMobilePlatform ? Mathf.Clamp(Screen.dpi / 150f, 1, 3) : Mathf.Clamp(Screen.height / 1000f, 1, 2);
            W = Screen.width / Draw.ui; H = Screen.height / Draw.ui;
            tank = new Rect(0, topH, W - panelW, H - topH - (showGraph ? graphH : 0));
            while (run.days.TryDequeue(out var d)) hist.Add(d);
            while (run.news.TryDequeue(out var e)) { news.Add(e); if (news.Count > 200) news.RemoveAt(0); }
            snap = run.snap;
            if (snap != lastSnap || Time.unscaledTime - lastRec > 0.25f) { Reconcile(); lastSnap = snap; lastRec = Time.unscaledTime; }
            Move(Time.deltaTime);
            Input_();
        }

        // 종마다 보일 마리 수를 생물량(로그)으로 정하고 칸에 나눈다. 전체를 볼 때 모든 산 종은 최소 한 마리.
        // 확대하면 화면 넓이당 마리 수가 그대로가 되게 보이는 칸에 더 놓는다(확대해도 텅 비지 않게)
        float lastRec;
        void Reconcile()
        {
            if (snap == null) return;
            int MAX = Application.isMobilePlatform ? 160 : 260;
            float hx = ncol / 2f / zoom, hy = ROWS / 2f / zoom;
            Rect view = new Rect(camX - hx, camY - hy, 2 * hx, 2 * hy);
            double tot = 0; foreach (int i in snap.alive) tot += snap.sp[i].biomass;
            var cnt = new Dictionary<int, int>(); int sum = 0;
            foreach (int i in snap.alive)
            {
                var s = snap.sp[i]; if (s.where == null || s.where.Length == 0) continue;
                int c = (int)Math.Clamp(Math.Round(1 + 1.6 * Math.Log(1 + s.biomass / Math.Max(1e-30, 1e-4 * tot))), 1, 14);
                cnt[i] = c; sum += c;
            }
            float scale = sum > MAX ? (float)MAX / sum : 1, z2 = zoom * zoom;
            var want = new Dictionary<(int, int), int>();
            foreach (var kv in cnt)
            {
                var s = snap.sp[kv.Key]; float c = Math.Max(1, kv.Value * scale);
                int given = 0;
                for (int k = 0; k < s.where.Length; k++)
                {
                    int v = s.where[k]; float vf = VisFrac(v, view); if (vf <= 0) continue;
                    int n = Math.Min(40, (int)Math.Round(c * s.share[k] * z2 * vf));
                    if (n > 0) { want[(kv.Key, v)] = n; given += n; }
                }
                if (given == 0 && zoom < 1.01f) want[(kv.Key, s.where[0])] = 1;   // 전체 보기: 모든 종이 보이게
            }
            var have = new Dictionary<(int, int), int>();
            foreach (var d in inds) { if (d.dying) continue; var key = (d.sp, d.v); have.TryGetValue(key, out int h); have[key] = h + 1; }
            // 넘치는 것은 화면 밖 것부터 지운다
            for (int pass = 0; pass < 2; pass++)
                foreach (var d in inds)
                {
                    if (d.dying) continue; var key = (d.sp, d.v);
                    if (pass == 0 && view.Contains(new Vector2(d.x, d.y))) continue;
                    want.TryGetValue(key, out int w); int h = have[key];
                    if (h > w) { d.dying = true; have[key] = h - 1; }
                }
            foreach (var kv in want)
            {
                have.TryGetValue(kv.Key, out int h);
                for (int k = h; k < kv.Value; k++) inds.Add(Spawn(kv.Key.Item1, kv.Key.Item2, view));
            }
        }

        float VisFrac(int v, Rect view)
        {
            Cell(v, out int c, out int row);
            float x0 = Mathf.Max(c, view.xMin), x1 = Mathf.Min(c + 1, view.xMax), y0 = Mathf.Max(row, view.yMin), y1 = Mathf.Min(row + 1, view.yMax);
            return x1 > x0 && y1 > y0 ? (x1 - x0) * (y1 - y0) : 0;
        }

        Ind Spawn(int sp, int v, Rect view)
        {
            Cell(v, out int c, out int row);
            float x0 = Mathf.Max(c + 0.06f, view.xMin), x1 = Mathf.Min(c + 0.94f, view.xMax), y0 = Mathf.Max(row + 0.08f, view.yMin), y1 = Mathf.Min(row + 0.92f, view.yMax);
            if (x1 <= x0) { x0 = c + 0.06f; x1 = c + 0.94f; } if (y1 <= y0) { y0 = row + 0.08f; y1 = row + 0.92f; }
            return new Ind { sp = sp, v = v, x = x0 + (x1 - x0) * (float)rnd.NextDouble(), y = y0 + (y1 - y0) * (float)rnd.NextDouble(), ang = (float)(rnd.NextDouble() * Math.PI * 2), spin = (float)(rnd.NextDouble() - 0.5) * 0.4f };
        }

        void Move(float dt)
        {
            if (snap == null) return;
            for (int i = inds.Count - 1; i >= 0; i--)
            {
                var d = inds[i];
                d.fade = Mathf.Clamp01(d.fade + (d.dying ? -dt : dt) * 1.5f);
                if (d.dying && d.fade <= 0) { inds.RemoveAt(i); continue; }
                var s = d.sp < snap.sp.Length ? snap.sp[d.sp] : null; if (s == null) continue;
                float mot = (float)s.t.Sat(G.MOT, 0.01);
                // 떠돌기: 작으면 흔들림, 편모가 있으면 헤엄
                d.vx += ((float)rnd.NextDouble() - 0.5f) * dt * (0.02f + 0.2f * mot); d.vy += ((float)rnd.NextDouble() - 0.5f) * dt * (0.02f + 0.2f * mot);
                d.vx *= 1 - dt; d.vy *= 1 - dt;
                d.x += d.vx * dt / zoom; d.y += d.vy * dt / zoom; d.ang += d.spin * dt;
                Cell(d.v, out int c, out int row);
                if (d.x < c + 0.06f) { d.x = c + 0.06f; d.vx = Mathf.Abs(d.vx); } if (d.x > c + 0.94f) { d.x = c + 0.94f; d.vx = -Mathf.Abs(d.vx); }
                if (d.y < row + 0.08f) { d.y = row + 0.08f; d.vy = Mathf.Abs(d.vy); } if (d.y > row + 0.92f) { d.y = row + 0.92f; d.vy = -Mathf.Abs(d.vy); }
            }
        }

        // 휠·두 손가락 = 확대, 끌기 = 이동, 누르고 떼기 = 고르기
        Vector2 downAt; bool dragging; float pinch0;
        void Input_()
        {
            if (showTree) return;
            Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / Draw.ui;
            bool inTank = tank.Contains(m);
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0).position; var b = Input.GetTouch(1).position; float dd = (a - b).magnitude;
                if (pinch0 > 0) ZoomAt(new Vector2((a.x + b.x) / 2, Screen.height - (a.y + b.y) / 2) / Draw.ui, dd / pinch0);
                pinch0 = dd; dragging = true; return;
            }
            pinch0 = 0;
            float wheel = Input.mouseScrollDelta.y;
            if (inTank && wheel != 0) ZoomAt(m, Mathf.Pow(1.15f, wheel));
            if (Input.GetMouseButtonDown(0) && inTank) { downAt = m; dragging = false; }
            if (Input.GetMouseButton(0) && downAt != default)
            {
                var d = m - downAt;
                if (d.magnitude > 6) dragging = true;
                if (dragging)
                {
                    float sx = tank.width / ncol * zoom, sy = tank.height / ROWS * zoom;
                    camX -= (m.x - lastM.x) / sx; camY -= (m.y - lastM.y) / sy; Clamp();
                }
            }
            if (Input.GetMouseButtonUp(0) && downAt != default)
            {
                if (!dragging && inTank) Pick(m);
                downAt = default;
            }
            lastM = m;
        }
        Vector2 lastM;

        void ZoomAt(Vector2 s, float f)
        {
            var w0 = ToWorld(s); zoom = Mathf.Clamp(zoom * f, 1, 40); var w1 = ToWorld(s);
            camX += w0.x - w1.x; camY += w0.y - w1.y; Clamp();
        }
        void Clamp()
        {
            float hx = ncol / 2f / zoom, hy = ROWS / 2f / zoom;
            camX = Mathf.Clamp(camX, hx, ncol - hx); camY = Mathf.Clamp(camY, hy, ROWS - hy);
        }

        void Pick(Vector2 m)
        {
            float best = 1e9f; int who = -1;
            foreach (var d in inds)
            {
                if (d.dying || snap == null) continue; var s = snap.sp[d.sp];
                var p = ToScreen(d.x, d.y); float R = BodyR(s) + 6, dd = (p - m).magnitude;
                if (dd < R && dd < best) { best = dd; who = d.sp; }
            }
            sel = who;
        }

        // 그 종을 화면 가운데로(가장 많이 사는 칸)
        void Focus(int id)
        {
            sel = id; var s = snap?.sp[id]; if (s?.where == null || s.where.Length == 0) return;
            Cell(s.where[0], out int c, out int row);
            zoom = Mathf.Max(zoom, 3); camX = c + 0.5f; camY = row + 0.5f; Clamp();
        }

        // 몸의 화면 반지름: 크기(μm)의 0.45제곱 — 세균부터 큰 덩이까지 한 화면에. 확대하면 커진다
        float BodyR(SpView s) => (1.6f + 1.8f * Mathf.Pow((float)s.body.size, 0.45f)) * Mathf.Pow(zoom, 0.7f);

        void DrawTank()
        {
            var p = run.pl; var s = snap;
            Draw.Rect(tank.x, tank.y, tank.width, tank.height, new Color(0.05f, 0.05f, 0.06f));
            // 칸 바탕
            for (int r = 0; r < p.NR; r++)
                for (int row = 0; row < ROWS; row++)
                {
                    var a = ToScreen(col[r], row); var b = ToScreen(col[r] + 1, row + 1);
                    if (b.x < tank.x || a.x > tank.xMax || b.y < tank.y || a.y > tank.yMax) continue;
                    Color c;
                    int v = r * Cfg.NY + row;
                    byte kind = p.geo.rKind[r];
                    if (row < 4 && p.live[v])
                    {
                        float L = Mathf.Pow(Mathf.Clamp01(s.vLight[v]), 0.35f);
                        float o2 = Mathf.Clamp01(Mathf.Log10(1 + s.vO2[v]) / 2.5f), fe = Mathf.Clamp01(Mathf.Log10(1 + s.vFe2[v]) / 2.5f), hs = Mathf.Clamp01(Mathf.Log10(1 + s.vH2S[v]) / 2f);
                        c = kind == 2 ? new Color(0.36f, 0.27f, 0.18f) : new Color(0.03f + 0.10f * L, 0.07f + 0.25f * L, 0.14f + 0.35f * L);
                        c += new Color(-0.02f, 0.02f, 0.18f) * o2 + new Color(0.02f, 0.12f, 0.02f) * fe + new Color(0.12f, 0.0f, 0.10f) * hs;
                        if (row == 0 && s.ice != null && r < s.ice.Length) c = Color.Lerp(c, new Color(0.8f, 0.86f, 0.92f), s.ice[r] * 0.6f);
                    }
                    else c = row == 4 ? new Color(0.13f, 0.11f, 0.10f) : new Color(0.10f, 0.09f, 0.08f);   // 해저·바위
                    Draw.Rect(Mathf.Max(a.x, tank.x), Mathf.Max(a.y, tank.y), Mathf.Min(b.x, tank.xMax) - Mathf.Max(a.x, tank.x) - 1, Mathf.Min(b.y, tank.yMax) - Mathf.Max(a.y, tank.y) - 1, c);
                }
            // 열수공(바닥 줄)
            for (int i = 0; i < Cfg.NVENT; i++)
            {
                int v = p.NV0 + i; if (!p.live[v]) continue;
                float m = Mathf.Clamp01(s.ventMul[v]);
                var a = ToScreen(col[p.ventReg[i]] + 0.3f, 4.15f); var b = ToScreen(col[p.ventReg[i]] + 0.7f, 4.95f);
                Draw.Rect(a.x, a.y, b.x - a.x, b.y - a.y, new Color(0.25f + 0.6f * m, 0.12f + 0.25f * m, 0.06f, 0.9f));
            }
            // 생물
            foreach (var d in inds)
            {
                var sv = snap.sp[d.sp]; if (sv.t == null) continue;
                var q = ToScreen(d.x, d.y); float R = BodyR(sv);
                if (q.x < tank.x - R || q.x > tank.xMax + R || q.y < tank.y - R || q.y > tank.yMax + R) continue;
                float al = d.fade * (sel >= 0 && sel != d.sp ? 0.45f : 0.95f);
                Draw.Body(Draw.ShapeOf(sv.body, sv.t), q.x, q.y, R, d.ang, Draw.Of(sv.t, sv.id), al);
                if (d.sp == sel) Draw.Ring(q.x, q.y, R + 4, new Color(1, 1, 1, 0.8f * d.fade));
            }
        }
    }
}
