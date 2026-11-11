// 4단계 — 물속 탐방(예전 v0.1 화면과 같은 방식). 관찰자는 고른 해역 물기둥의 수심 py(m)에 있고, 둘레 상자(한 변 B m)를 화면 상자 BOX(10)로 키워 본다.
// 상자 안 개체 수 = 그 칸의 실제 밀도 × 상자 부피 × 농축 배율 F(보이는 수가 40–220이 되게 10배씩). 시야의 1/150–0.7 크기인 종만 몸을 그리고 더 작은 것은 점으로.
// 개체를 누르면 그 개체를 따라 헤엄치고(관찰자가 함께 움직인다) 카메라가 그 둘레를 돈다. 손을 떼고 3초면 천천히 옆모습으로 돈다.
// 조작: 끌기 = 둘러보기, 휠·두 손가락 = 시야 폭, WASD = 헤엄(E/Q 위아래, Shift 빠르게), PageUp/Down = 0.5 m, F = 고른 것에 맞춤, Esc = 놓기.
using System;
using System.Collections.Generic;
using Life;
using UnityEngine;

namespace Tank
{
    public sealed partial class Tank
    {
        const float BOX = 10, HB = BOX / 2;                         // 상자 한 변(화면 단위)과 그 반
        const float SELZ = 3;                                       // 고른 개체에 맞추면 상자 한 변 = 몸길이 × 3
        const int NMIN = 40, NMAX = 220;
        static readonly string[] LAYER = { "표층 0–20 m", "20–100 m", "100–500 m", "깊은 바다 500 m–" };
        int reg = -1; double py = 1.25, px, pz;                     // 관찰자: 해역, 수심(m), 수평 위치(m, 물빛·입자가 흐르게)
        float B = 3e-4f, Bt = 3e-4f;                                // 시야 폭(m)과 목표(부드럽게 따라간다)
        float yaw = 35, pitch = 18, dist = 9, camDist = 9;          // 카메라: 고른 개체(없으면 상자 가운데) 둘레
        double F = 1;                                               // 농축 배율
        int vNow = -1, wantSp = -1; float Bseen, lastRec = -9;
        float yF = -1e9f, yS = 1e9f;                                // 바닥·수면 높이(화면 단위, 관찰자 기준)
        Vector3 dotOff;                                             // 점 무늬 어긋남(상자 비율): 헤엄치면 흘러간다
        sealed class Av { public int sp; public Vector3 p, dir; public Quaternion q; public float fade, t0, rate, ph; public bool dying; }
        readonly List<Av> avs = new(); Av selAv;
        readonly System.Random rnd = new(7);
        int hoverSp = -1; Av hoverAv;
        double smallN;                                              // 점으로만 보이는 작은 생물(상자 안 실제 마리 수)

        float Bottom => (float)run.pl.geo.rDepth[reg];
        bool Micro => B < 2e-3f;

        // 지금 칸: 수심이 든 층(해저 아래 층은 없으니 위로), 바닥 1 m 안이면 퇴적면 칸, 100 m 안이고 이 해역에 열수공이 있으면 열수공 칸
        int Voxel()
        {
            var p = run.pl; int k = 0;
            while (k < Cfg.NY - 1 && py >= Cfg.ZE[k + 1]) k++;
            while (k > 0 && !p.live[reg * Cfg.NY + k]) k--;
            if (Bottom - py < 1 && p.HasFloor(reg)) return p.NF0 + reg;   // 바닥 1 m 안 = 퇴적면 칸
            if (Bottom - py < Cfg.VENT_THICK) for   // 열수공 칸 = 바닥 위 100 m 물(Focus는 그 가운데로 간다)
 (int i = 0; i < Cfg.NVENT; i++) if (p.ventReg[i] == reg && p.live[p.NV0 + i]) return p.NV0 + i;
            return reg * Cfg.NY + k;
        }

        // 다른 해역·수심으로: 시야의 개체는 새로 뽑는다
        void GoTo(int r, double depth)
        {
            reg = r; avs.Clear(); selAv = null; vNow = -1;
            py = Math.Clamp(depth, 0.02, Math.Max(0.02, Bottom - 0.02));
            Edges();
        }
        void DiveTo(int r, double depth, SpView s)
        {
            GoTo(r, depth);
            if (s?.body != null) { Bt = B = Mathf.Clamp((float)(s.body.size * 1e-6) * SELZ, 5e-6f, 3); wantSp = s.id; }
        }

        void Edges() { float u = BOX / B; yF = -(Bottom - (float)py) * u; yS = (float)py * u; }

        // 관찰자를 dm(m, y = 위)만큼 옮긴다. 시야보다 멀리 가면 개체를 새로 뽑는다
        void MoveBy(Vector3 dm)
        {
            double ny = Math.Clamp(py - dm.y, 0.02, Math.Max(0.02, Bottom - 0.02)); dm.y = (float)(py - ny); py = ny;
            px += dm.x; pz += dm.z;
            if (dm.sqrMagnitude > B * B) { avs.Clear(); selAv = null; vNow = -1; return; }
            foreach (var a in avs) a.p = Wrap(a.p - dm);
            dotOff += dm / B;
        }

        // 상자 밖으로 나간 것은 반대편에서 들어온다(둘레가 이어진 물). 바닥·수면에서는 멈춘다
        Vector3 Wrap(Vector3 p)
        {
            float hb = B / 2;
            p.x = Mathf.Repeat(p.x + hb, B) - hb; p.z = Mathf.Repeat(p.z + hb, B) - hb;
            if (p.y < -hb || p.y > hb) p.y = Mathf.Repeat(p.y + hb, B) - hb;
            float lo = -(Bottom - (float)py), hi = (float)py;
            return new Vector3(p.x, Mathf.Clamp(p.y, lo, hi), p.z);
        }

        // 개체/m³: μM C → mol C/m³ ÷ (세포 탄소 × 덩이 세포 수)
        static double Dens(SpView s, int v) => s.conc == null || s.t == null ? 0 : s.conc[v] * 1e-3 / Math.Max(1e-30, s.t.cellC * s.t.MeanCells);

        void DiveUpdate(float dt, bool fresh)
        {
            if (reg < 0) StartSpot();
            bool typing = GUIUtility.keyboardControl != 0;
            if (!typing && Input.GetKeyDown(KeyCode.Escape)) { selAv = null; sel = -1; }
            if (!typing && Input.GetKeyDown(KeyCode.F)) Fit();
            if (!typing && Input.GetKeyDown(KeyCode.N)) Step(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);   // 다음·이전 종
            // 둘러보기·시야
            if (Input.touchCount == 2) TwoFingers();
            else if (Drag(out var d)) { yaw += d.x * 0.25f; pitch = Mathf.Clamp(pitch - d.y * 0.25f, -80, 80); }
            if (inView && Input.mouseScrollDelta.y != 0) Bt = Mathf.Clamp(Bt * Mathf.Exp(-Input.mouseScrollDelta.y * 0.15f), 5e-6f, 3);
            B = Mathf.Exp(Mathf.Lerp(Mathf.Log(B), Mathf.Log(Bt), 1 - Mathf.Exp(-dt * 4)));
            if (!typing) SwimKeys(dt);
            if (selAv != null && (selAv.dying || snap.sp[selAv.sp].died >= 0)) selAv = null;
            Edges();
            int v = Voxel();
            if (v != vNow || Mathf.Abs(Mathf.Log(B / Mathf.Max(1e-9f, Bseen))) > 0.05f || fresh && Time.unscaledTime - lastRec > 0.5f) { vNow = v; Bseen = B; lastRec = Time.unscaledTime; long t = Perf.T(); Reconcile(v); Perf.Add(1, t); }
            long t0 = Perf.T(); MoveAvs(dt); Perf.Add(2, t0);
            if (selAv != null) MoveBy(selAv.p);   // 고른 개체와 함께 헤엄친다(떠다니는 원점)
            CamUpdate(dt);
            t0 = Perf.T(); DrawAll(v); Perf.Add(3, t0);
            t0 = Perf.T(); HoverPick(); Perf.Add(4, t0);
        }

        // 처음: 가장 많은 종이 사는 칸(없으면 가장 얕은 바다 1.25 m)
        void StartSpot()
        {
            var s = snap; int big = -1;
            foreach (int i in s.alive) if (big < 0 || s.sp[i].biomass > s.sp[big].biomass) big = i;
            if (big >= 0 && s.sp[big].where?.Length > 0) { Focus(big); return; }
            var g = run.pl.geo; int r0 = 0; for (int c = 1; c < g.NO; c++) if (g.rDepth[c] < g.rDepth[r0]) r0 = c;
            reg = r0; GoTo(r0, 1.25);
        }

        void SwimKeys(float dt)
        {
            var mv = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) mv.z += 1; if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) mv.z -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) mv.x += 1; if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) mv.x -= 1;
            if (Input.GetKey(KeyCode.E)) mv.y += 1; if (Input.GetKey(KeyCode.Q)) mv.y -= 1;
            if (Input.GetKeyDown(KeyCode.PageUp)) MoveBy(new Vector3(0, 0.5f, 0));
            if (Input.GetKeyDown(KeyCode.PageDown)) MoveBy(new Vector3(0, -0.5f, 0));
            if (mv == Vector3.zero) return;
            selAv = null;   // 헤엄치면 따라가기를 멈춘다
            float sp = B * 0.5f * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 4 : 1);
            MoveBy((Quaternion.Euler(pitch, yaw, 0) * new Vector3(mv.x, 0, mv.z) + Vector3.up * mv.y) * sp * dt);
        }

        // F: 고른 개체에 맞추고, 없으면 가장 많은 종으로
        void Fit()
        {
            if (selAv != null) { Bt = Mathf.Clamp(Len(snap.sp[selAv.sp]) * SELZ, 5e-6f, 3); return; }
            int big = -1; foreach (int i in snap.alive) if (big < 0 || snap.sp[i].biomass > snap.sp[big].biomass) big = i;
            if (big >= 0) Focus(big);
        }
        static float Len(SpView s) => (float)(s.body.size * 1e-6);

        // 종마다 보일 수 = 실제 밀도 × 상자 부피 × F. 넘치거나 모자라면 F를 10배씩
        void Reconcile(int v)
        {
            var s = snap; double vol = (double)B * B * B, E = 0; smallN = 0;
            var want = new Dictionary<int, double>();
            foreach (int i in s.alive)
            {
                var sp = s.sp[i]; if (sp.body == null) continue; float L = Len(sp); double e = Dens(sp, v) * vol;
                if (L < B / 150) { smallN += e; continue; }
                if (L > 0.7f * B || e <= 0) continue;
                want[i] = e; E += e;
            }
            if (E > 0 && (E * F < NMIN || E * F > NMAX)) F = Math.Pow(10, Math.Round(Math.Log10(110 / E)));
            var have = new Dictionary<int, int>();
            foreach (var a in avs) if (!a.dying) { have.TryGetValue(a.sp, out int h); have[a.sp] = h + 1; }
            foreach (var a in avs)
            {
                if (a.dying || a == selAv) continue; want.TryGetValue(a.sp, out double w); int n = Want(w); int h = have[a.sp];
                if (h > n) { a.dying = true; have[a.sp] = h - 1; }
            }
            foreach (var kv in want) { have.TryGetValue(kv.Key, out int h); for (int k = h; k < Want(kv.Value); k++) avs.Add(Spawn(kv.Key)); }
            if (wantSp >= 0 && want.ContainsKey(wantSp) && Want(want[wantSp]) == 0) avs.Add(Spawn(wantSp));   // 고른 종이 드물어도 따라갈 하나는 둔다
            if (wantSp >= 0)   // 종 목록에서 고른 종: 가운데에 가장 가까운 하나를 따라간다
            {
                Av best = null; foreach (var a in avs) if (a.sp == wantSp && !a.dying && (best == null || a.p.sqrMagnitude < best.p.sqrMagnitude)) best = a;
                if (best != null) { selAv = best; sel = wantSp; } wantSp = -1;
            }
        }
        int Want(double e) { double n = e * F; return n <= 0.05 ? 0 : Math.Clamp((int)Math.Round(n), 1, 80); }   // 드문 종도 칸에 있으면 하나는 보이게

        Av Spawn(int sp)
        {
            float R() => ((float)rnd.NextDouble() - 0.5f) * B;
            var d = UnityEngine.Random.onUnitSphere;
            return new Av { sp = sp, p = Wrap(new Vector3(R(), R(), R())), dir = d, q = Quaternion.LookRotation(d), t0 = (float)rnd.NextDouble() * 10, rate = 0.7f + 0.6f * (float)rnd.NextDouble() };
        }

        // 움직임(그림일 뿐): 편모 몫만큼 몸길이의 15배/초까지 헤엄(작은 헤엄이는 나선, Jennings 1901), 20 μm 밑은 브라운 운동(D = kT/6πηr),
        // 못 헤엄치는 것은 가라앉는다(스토크스, 밀도 차 50 kg/m³)
        void MoveAvs(float dt)
        {
            for (int i = avs.Count - 1; i >= 0; i--)
            {
                var a = avs[i]; var s = snap.sp[a.sp];
                a.fade = Mathf.Clamp01(a.fade + (a.dying ? -dt : dt) * 1.5f);
                if (a.dying && a.fade <= 0 || s.body == null) { if (a == selAv) selAv = null; avs.RemoveAt(i); continue; }
                if (run.paused) continue;   // 멈춤: 생물도 그 자리에 선다(나타남·사라짐만 그대로)
                float L = Len(s), mot = (float)s.t.Sat(G.MOT, 0.01);
                float spd = mot > 0.3f ? Mathf.Min(mot * 15 * L, 3e-3f) : 0, sink = (1 - mot) * Mathf.Min(1.09e5f * L * L / 4, 1e-3f);
                if (L < 20e-6f) a.p += UnityEngine.Random.insideUnitSphere * Mathf.Sqrt(6 * 2.2e-19f / (L * 0.5f) * dt);
                if (spd > 0)
                {
                    a.dir = (a.dir + UnityEngine.Random.insideUnitSphere * dt * 0.8f).normalized;
                    a.ph += dt * a.rate * 5;
                    a.q = Quaternion.LookRotation(a.dir) * Quaternion.Euler(0, 0, a.ph * Mathf.Rad2Deg) * Quaternion.Euler(20, 0, 0);
                    a.p += a.q * Vector3.forward * spd * dt;
                }
                else a.q *= Quaternion.Euler(dt * 9, dt * 6, 0);   // 물살에 천천히 구른다
                a.p.y -= sink * dt;
                a.p = Wrap(a.p);
            }
        }

        // 카메라: 고른 개체(없으면 가운데) 둘레. 손을 떼고 3초면 헤엄 축의 옆모습으로 천천히 돈다. 늘 물속(수면 위·바닥 밑으로 안 나감)
        void CamUpdate(float dt)
        {
            if (selAv != null && !dragging && Time.time - lastDrag > 3)
            {
                var f = selAv.dir;
                if (Mathf.Abs(f.y) < 0.9f)
                {
                    float h = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, a1 = h + 90, a2 = h - 90, k = 1 - Mathf.Exp(-dt * 0.6f);
                    yaw = Mathf.LerpAngle(yaw, Mathf.Abs(Mathf.DeltaAngle(yaw, a1)) < Mathf.Abs(Mathf.DeltaAngle(yaw, a2)) ? a1 : a2, k);
                    pitch = Mathf.Lerp(pitch, 10, k);
                }
            }
            var cp = Quaternion.Euler(pitch, yaw, 0) * new Vector3(0, 0, -dist); cp.y = Mathf.Clamp(cp.y, yF + 0.05f, yS - 0.05f);
            cam.transform.position = cp; cam.transform.LookAt(Vector3.zero); camDist = cp.magnitude;
            cam.nearClipPlane = 0.01f; cam.farClipPlane = 400;
        }

        void DrawAll(int v)
        {
            LookBegin(v);
            float u = BOX / B; var mat = Micro ? mMicro : mMacro;
            foreach (var a in avs)
            {
                var s = snap.sp[a.sp]; if (s.body == null) continue;
                var pu = a.p * u;   // 관찰자 둘레 구 가장자리에서 물빛으로 녹는다. 하나를 고르면 나머지는 흐리게
                mpb.SetFloat("_Fade", a.fade * (a == selAv ? 1 : Mathf.Clamp01((HB - pu.magnitude) / 1.2f) * (selAv != null ? 0.45f : 1)));
                mpb.SetFloat("_Sel", a == selAv ? 1 : 0); mpb.SetFloat("_T0", a.t0); mpb.SetFloat("_Rate", a.rate); mpb.SetFloat("_Pinch", 0);
                var mesh = BodyMesh(s); var mx = Matrix4x4.TRS(pu, a.q, Vector3.one * (1e-6f * u));
                Graphics.DrawMesh(mesh, mx, mat, 0, null, 0, mpb); LookBody(mesh, mx, mpb);
            }
            Particles(v, smallN);
            LookEnd();
        }

        // 마우스 밑 개체(누르면 고르기 = 따라가기)
        void HoverPick()
        {
            hoverSp = -1; hoverAv = null; float best = 1e9f, u = BOX / B, f = Screen.height / (2 * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2));
            if (inView)
                foreach (var a in avs)
                {
                    if (a.dying) continue;
                    var q = cam.WorldToScreenPoint(a.p * u); if (q.z <= 0) continue;
                    float R = Len(snap.sp[a.sp]) * u * 0.5f * f / q.z / Draw.ui + (Application.isMobilePlatform ? 24 : 8);
                    float dd = (new Vector2(q.x, Screen.height - q.y) / Draw.ui - mouse).magnitude;
                    if (dd < R && q.z < best) { best = q.z; hoverAv = a; hoverSp = a.sp; }
                }
            if (Clicked()) { selAv = hoverAv; sel = hoverSp; }
        }

        // ── 입력: 끌기(누른 곳이 3D 화면이면), 누르기(덜 움직이고 뗌), 두 손가락 ──
        Vector3 downAt, lastM; bool dragging; float lastDrag = -10;
        bool Drag(out Vector2 d)
        {
            d = default; var m = Input.mousePosition;
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) { downAt = lastM = m; dragging = inView; }
            if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)) dragging = false;
            if (!dragging || !(Input.GetMouseButton(0) || Input.GetMouseButton(1))) return false;
            d = m - lastM; lastM = m; if (d.sqrMagnitude > 0) lastDrag = Time.time;
            return d.sqrMagnitude > 0;
        }
        bool Clicked()
        {
            float tap = Application.isMobilePlatform ? 20 : 6;
            return inView && Input.GetMouseButtonUp(0) && (Input.mousePosition - downAt).sqrMagnitude < tap * tap;
        }
        // 휴대폰 두 손가락: 벌리고 오므리기 = 시야 폭, 함께 끌기 = 헤엄
        void TwoFingers()
        {
            dragging = false; if (!inView) return;
            Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
            if (a.phase == TouchPhase.Began || b.phase == TouchPhase.Began) return;
            float d0 = (a.position - a.deltaPosition - (b.position - b.deltaPosition)).magnitude, d1 = (a.position - b.position).magnitude;
            if (d0 > 1 && d1 > 1) Bt = Mathf.Clamp(Bt * d0 / d1, 5e-6f, 3);
            Vector2 pan = (a.deltaPosition + b.deltaPosition) / 2 / Screen.height;
            if (pan.sqrMagnitude == 0) return;
            selAv = null; MoveBy(Quaternion.Euler(0, yaw, 0) * new Vector3(-pan.x, -pan.y, 0) * B);
        }

        // 한 층 위·아래 층 가운데로(가장 얕은 층은 5 m)
        void Layer(int dk)
        {
            int k = 0; while (k < Cfg.NY - 1 && py >= Cfg.ZE[k + 1]) k++;
            k = Math.Clamp(k + dk, 0, Cfg.NY - 1);
            double top = Cfg.ZE[k], bot = Math.Min(Cfg.ZE[k + 1], Bottom);
            if (top >= Bottom) return;
            GoTo(reg, k == 0 ? Math.Min(5, bot / 2) : (top + bot) / 2);
        }
    }
}
