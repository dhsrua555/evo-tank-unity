// 8단계 — 판들(물속 화면 위에 반투명으로 뜬다): 윗줄(상태·속도·돌연변이율·사건), 오른쪽 종 목록(살아 있는 종만, 큰 갈래로 접힘, 찾기, 많은 순/나온 순),
// 고른 종 정보(몸·하는 일·형질·조상), 아래 그래프(O₂·CO₂·기온·종 수), 계통수 창(멸종한 조상까지).
using System;
using System.Collections.Generic;
using System.Linq;
using Life;
using UnityEngine;

namespace Tank
{
    public sealed partial class Tank
    {
        Font font, fontB;
        GUIStyle sL, sB, sS, sBtn, sOn, sField, sR, sSR, sWrap, sSC, sTiny;
        const float INFO_H = 380;
        bool showGraph = true, showTree, showEv, sortNew;
        string search = "", muText = "1";
        Vector2 listScroll; readonly HashSet<int> open = new();
        float treeScroll, listWant = 9999; bool treeAll;

        void Fonts()
        {
            font = Resources.Load<Font>("Fonts/Pretendard-Regular"); fontB = Resources.Load<Font>("Fonts/Pretendard-SemiBold");
        }
        void Styles()
        {
            if (sL != null) return;
            var fg = new Color(0.88f, 0.9f, 0.92f);
            sL = new GUIStyle(GUI.skin.label) { font = font, fontSize = 13, normal = { textColor = fg }, wordWrap = false, clipping = TextClipping.Clip };
            sB = new GUIStyle(sL) { font = fontB, fontSize = 14 };
            sS = new GUIStyle(sL) { fontSize = 11, normal = { textColor = new Color(0.65f, 0.68f, 0.72f) } };
            sBtn = new GUIStyle(GUI.skin.button) { font = font, fontSize = 12 };
            sOn = new GUIStyle(sBtn) { font = fontB, normal = { textColor = new Color(1, 0.85f, 0.4f), background = sBtn.active.background } };
            sField = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 13 };
            sR = new GUIStyle(sL) { richText = true }; sSR = new GUIStyle(sS) { richText = true, normal = { textColor = fg } }; sWrap = new GUIStyle(sS) { wordWrap = true };
            sSC = new GUIStyle(sS) { alignment = TextAnchor.UpperCenter }; sTiny = new GUIStyle(sBtn) { fontSize = 9, padding = new RectOffset(0, 0, 0, 0) };
        }

        void OnGUI()
        {
            if (snap == null) return;
            Styles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(Draw.ui, Draw.ui, 1));
            if (UnityEngine.Event.current.type == EventType.Repaint)
            {
                Draw.Begin();
                if (showTree) TreeDraw();
                Draw.Rect(0, 0, W, topH, GLASS); Draw.Line(0, topH, W, topH, EDGE);
                Glass(listR);
                if (showGraph) GraphDraw(graphR);
                if (sel >= 0) InfoDraw(infoR);
                Draw.End();
            }
            if (!showTree) DiveGui();
            TopBar();
            List(listR);
            if (sel >= 0) Info(infoR);
            if (showGraph) GraphText(graphR);
            if (showTree) TreeGui();
            if (!showTree) Ticker();
            if (run.ffDays > 0) GUI.Label(new Rect(W / 2 - 150, H / 2 - 15, 300, 30), $"멈춘 동안 못 간 날 따라잡는 중 · {run.ffDays:0}일 남음", sB);
        }

        // ── 윗줄 ──
        static readonly double[] SPEEDS = { -1, 1, 10, 100, 0 };
        static readonly string[] SPN = { "멈춤", "1일/초", "10", "100", "최대" };
        void TopBar()
        {
            var s = snap; double yr = s.day / 365;
            string t = $"{s.day:0}일 ({(yr < 10 ? yr.ToString("0.0") : yr.ToString("0"))}년) · 산소 {Sci(s.O2)} PAL · CO₂ {s.CO2:0.0} PAL · 메탄 {s.CH4:0} ppm · 기온 {s.T:0.0} °C · 종 {s.nSp} · {s.rate:0}일/초";
            GUI.Label(new Rect(10, 3, W - 20, 20), t, sB);
            float x = 10, y = 24;
            for (int i = 0; i < SPEEDS.Length; i++)
            {
                bool on = SPEEDS[i] < 0 ? run.paused : !run.paused && run.speed == SPEEDS[i];
                if (GUI.Button(new Rect(x, y, i == 0 ? 44 : 50, 19), SPN[i], on ? sOn : sBtn)) { if (SPEEDS[i] < 0) run.paused = true; else { run.paused = false; run.speed = SPEEDS[i]; } }
                x += (i == 0 ? 44 : 50) + 2;
            }
            x += 14;
            GUI.Label(new Rect(x, y, 66, 20), "돌연변이율", sL); x += 66;
            double mu = run.pl.knobs.mu; float lg = GUI.HorizontalSlider(new Rect(x, y + 5, 110, 12), (float)Math.Log10(mu), -4, 0); x += 116;
            if (Math.Abs(lg - Math.Log10(mu)) > 1e-4) { mu = Math.Pow(10, lg); SetMu(mu); muText = mu.ToString("0.####"); }
            string nt = GUI.TextField(new Rect(x, y, 56, 19), muText, sField); x += 62;
            if (nt != muText) { muText = nt; if (double.TryParse(nt, out double m) && m > 0) SetMu(Math.Min(1, m)); }
            x += 10;
            GUI.Label(new Rect(x, y, 52, 20), "사건 빈도", sL); x += 56;
            float er = GUI.HorizontalSlider(new Rect(x, y + 5, 80, 12), (float)run.pl.knobs.eventRate, 0, 5); x += 84;
            if (Math.Abs(er - run.pl.knobs.eventRate) > 1e-3) { double e2 = Math.Round(er, 1); run.cmds.Enqueue(p => p.knobs.eventRate = e2); }
            GUI.Label(new Rect(x, y, 34, 20), $"×{run.pl.knobs.eventRate:0.0}", sS); x += 36;
            if (GUI.Button(new Rect(x, y, 64, 19), "사건 ▾", showEv ? sOn : sBtn)) showEv = !showEv; x += 70;
            if (showEv)
            {
                string[] ev = { "화산 대분출", "소행성 충돌", "빙하기", "열수공 이동" };
                for (int k = 0; k < 4; k++)
                    if (GUI.Button(new Rect(x - 70, topH + 4 + k * 22, 100, 20), ev[k], sBtn)) { int kk = k; run.cmds.Enqueue(p => p.Trigger(kk, 10)); showEv = false; }
            }
            if (GUI.Button(new Rect(W - 170, y, 80, 19), "계통수", showTree ? sOn : sBtn)) showTree = !showTree;
            if (GUI.Button(new Rect(W - 86, y, 80, 19), "그래프", showGraph ? sOn : sBtn)) showGraph = !showGraph;
        }
        // 반투명 판: GL(Draw)로 그리는 것과 GUI로 그리는 것 둘 다
        static readonly Color GLASS = new(0.03f, 0.06f, 0.1f, 0.65f), EDGE = new(1, 1, 1, 0.12f);
        static void Glass(Rect r) { Draw.Rect(r.x, r.y, r.width, r.height, GLASS); Draw.Line(r.x, r.y, r.xMax, r.y, EDGE); Draw.Line(r.x, r.yMax, r.xMax, r.yMax, EDGE); Draw.Line(r.x, r.y, r.x, r.yMax, EDGE); Draw.Line(r.xMax, r.y, r.xMax, r.yMax, EDGE); }
        static void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
        static void Card(Rect r) { Fill(r, GLASS); Fill(new Rect(r.x, r.y, r.width, 1), EDGE); Fill(new Rect(r.x, r.yMax - 1, r.width, 1), EDGE); Fill(new Rect(r.x, r.y, 1, r.height), EDGE); Fill(new Rect(r.xMax - 1, r.y, 1, r.height), EDGE); }
        void SetMu(double m) { run.cmds.Enqueue(p => p.knobs.mu = m); run.pl.knobs.mu = m; }
        static string Size(double um) => um < 1000 ? $"{um:0.#} μm" : $"{um / 1000:0.#} mm";
        static string Sci(double x) => x <= 0 ? "0" : x >= 0.01 ? x.ToString("0.###") : x.ToString("0.0e0");

        // ── 종 목록 ──
        void List(Rect r)
        {
            var s = snap; float y = r.y + 6;
            var cl = s.alive.GroupBy(i => s.sp[i].clade).ToList();
            GUI.Label(new Rect(r.x + 10, y, r.width - 20, 20), $"살아 있는 종 {s.alive.Length} · 큰 갈래 {cl.Count}", sB); y += 22;
            search = GUI.TextField(new Rect(r.x + 10, y, r.width - 130, 20), search, sField);
            if (search.Length == 0) GUI.Label(new Rect(r.x + 14, y + 1, 100, 20), "이름 찾기", sS);
            if (GUI.Button(new Rect(r.xMax - 114, y, 50, 20), "많은 순", !sortNew ? sOn : sBtn)) sortNew = false;
            if (GUI.Button(new Rect(r.xMax - 60, y, 50, 20), "나온 순", sortNew ? sOn : sBtn)) sortNew = true;
            y += 26;
            var rows = new List<(int kind, int id)>();   // 0 = 갈래 머리, 1 = 종
            Func<int, double> key = i => sortNew ? s.sp[i].born : -s.sp[i].biomass;
            if (search.Length > 0)
                foreach (int i in s.alive.Where(i => s.sp[i].name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || s.clades[s.sp[i].clade].name.Contains(search)).OrderBy(key)) rows.Add((1, i));
            else
                foreach (var g in cl.OrderBy(g => sortNew ? s.clades[g.Key].born : -g.Sum(i => s.sp[i].biomass)))
                {
                    rows.Add((0, g.Key));
                    if (open.Contains(g.Key)) foreach (int i in g.OrderBy(key)) rows.Add((1, i));
                }
            float rh = 21, inner = rows.Count * rh; listWant = y - r.y + inner + 8;
            listScroll = GUI.BeginScrollView(new Rect(r.x, y, r.width, r.yMax - y), listScroll, new Rect(0, 0, r.width - 16, inner));
            float top = listScroll.y - rh, bot = listScroll.y + (r.yMax - y);
            for (int k = 0; k < rows.Count; k++)
            {
                float yy = k * rh; if (yy < top || yy > bot) continue;
                var (kind, id) = rows[k];
                if (kind == 0)
                {
                    var members = cl.First(g => g.Key == id); int big = members.OrderBy(i => -s.sp[i].biomass).First();
                    Swatch(new Rect(8, yy + 5, 10, 10), s.sp[big]);
                    string mark = open.Contains(id) ? "▾" : "▸";
                    if (GUI.Button(new Rect(22, yy, r.width - 40, rh - 1), $"{mark} {s.clades[id].name}  ·  {members.Count()}종  ·  {Draw.Mass(members.Sum(i => s.sp[i].biomass))}", sL)) { if (!open.Remove(id)) open.Add(id); }
                }
                else
                {
                    var sp = s.sp[id];
                    if (id == sel) { GUI.color = new Color(1, 1, 1, 0.12f); GUI.DrawTexture(new Rect(0, yy, r.width, rh), Texture2D.whiteTexture); GUI.color = Color.white; }
                    Swatch(new Rect(26, yy + 5, 10, 10), sp);
                    if (GUI.Button(new Rect(40, yy, r.width - 58, rh - 1), $"{sp.name}  <size=11><color=#9aa3ad>{Draw.RoleName[(int)sp.role]} · {Draw.Mass(sp.biomass)}</color></size>", sR)) Focus(id);
                }
            }
            GUI.EndScrollView();
        }
        void Swatch(Rect r, SpView s) { if (s.t == null) return; GUI.color = Draw.Of(s.t, s.id); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }

        // ── 고른 종 ──
        Rect bodyBox;
        void InfoDraw(Rect r)
        {
            Glass(r);
            var s = snap.sp[sel]; if (s.t == null) return;
            bodyBox = new Rect(r.xMax - 124, r.y + 10, 114, 114);
            Draw.Rect(bodyBox.x, bodyBox.y, bodyBox.width, bodyBox.height, new Color(0.04f, 0.06f, 0.1f));
            Draw.Body(Draw.ShapeOf(s.body, s.t), bodyBox.center.x, bodyBox.center.y, 50, Time.time * 0.1f, Draw.Of(s.t, s.id), 1);
            // 수입 막대
            float bx = r.x + 10, by = r.y + 164, bw = r.width - 20; double sum = s.income.Sum();
            Color[] ic = { new Color(0.3f, 0.8f, 0.4f), new Color(0.98f, 0.6f, 0.2f), new Color(0.95f, 0.28f, 0.3f), new Color(0.75f, 0.6f, 0.4f), new Color(0.8f, 0.8f, 0.78f) };
            float xx = bx;
            for (int q = 0; q < 5 && sum > 0; q++) { float w = (float)(s.income[q] / sum) * bw; Draw.Rect(xx, by, w, 8, ic[q]); xx += w; }
            // 단백질 배분 막대(많은 것 7개)
            var top = Enumerable.Range(0, G.NA).OrderBy(i => -s.t.phi[i]).Take(7).ToArray();
            for (int k = 0; k < top.Length; k++) Draw.Rect(bx + 90, r.y + 238 + k * 15, (float)(s.t.phi[top[k]] / s.t.phi[top[0]]) * (bw - 150), 9, new Color(0.5f, 0.65f, 0.85f, 0.8f));
        }
        void Info(Rect r)
        {
            var s = snap.sp[sel]; if (s.t == null) return;
            if (GUI.Button(new Rect(r.xMax - 22, r.y + 2, 20, 18), "×", sS)) { sel = -1; return; }
            float x = r.x + 10, y = r.y + 8, w = r.width - 150;
            GUI.Label(new Rect(x, y, w, 20), s.name, sB); y += 20;
            GUI.Label(new Rect(x, y, w, 18), $"{snap.clades[s.clade].name} · {(s.Alive ? $"{s.born}일에 나옴" : $"{s.born}–{s.died}일 (멸종)")}", sS); y += 18;
            GUI.Label(new Rect(x, y, w, 18), $"하는 일: {Draw.RoleName[(int)s.role]}", sL); y += 18;
            GUI.Label(new Rect(x, y, w, 18), $"생물량 {Draw.Mass(s.biomass)} · 계통 {s.strains}", sL); y += 18;
            double mc = s.t.MeanCells;
            GUI.Label(new Rect(x, y, w, 18), mc < 1.5 ? $"단세포 {s.t.dCell:0.#} μm" : $"덩이 세포 {mc:0}개 · {s.body.size:0} μm", sL); y += 18;
            GUI.Label(new Rect(x, y, w, 18), $"최적 {s.t.topt:0} °C (±{s.t.tw:0})", sS); y += 16;
            GUI.Label(new Rect(x, y, w, 18), $"광계 {s.t.eox:+0.00;-0.00} V{(s.t.eox > 0.85 ? " · 물을 쪼갬(산소)" : "")}", sS);
            GUI.Label(new Rect(bodyBox.x, bodyBox.yMax + 1, bodyBox.width, 16), "실제 크기 " + Size(s.body.size), sS);
            y = r.y + 146;
            GUI.Label(new Rect(x, y, r.width - 20, 16), "탄소를 어디서: <color=#4dcc66>빛</color> · <color=#fa9933>화학</color> · <color=#f2474d>산 먹이</color> · <color=#bf9966>사체</color> · <color=#ccccc7>녹은 유기물</color>", sSR);
            y = r.y + 178;
            double es = s.energy.Sum();
            if (es > 0) GUI.Label(new Rect(x, y, r.width - 20, 16), $"에너지를 어디서: 빛 {s.energy[0] / es:P0} · 화학 {s.energy[1] / es:P0} · 유기물 태우기 {s.energy[2] / es:P0}", sS);
            y += 18;
            GUI.Label(new Rect(x, y, r.width - 20, 16), $"산소 호흡 {s.t.Sat(G.RESP, 0.02):P0} · 항산화 {s.t.Sat(G.SOD, 0.005):P0} · 세포벽 {s.t.Sat(G.WALL, 0.02):P0} · 편모 {s.t.Sat(G.MOT, 0.01):P0}", sS); y += 18;
            GUI.Label(new Rect(x, r.y + 216, r.width - 20, 16), "단백질 배분", sS);
            var top = Enumerable.Range(0, G.NA).OrderBy(i => -s.t.phi[i]).Take(7).ToArray();
            for (int k = 0; k < top.Length; k++) GUI.Label(new Rect(x, r.y + 234 + k * 15, 90, 16), G.Name[top[k]], sS);
            // 조상(멸종한 것 포함, 계통수 창에서 더)
            var anc = new List<string>(); int a = s.parent;
            while (a >= 0 && anc.Count < 5) { anc.Add(snap.sp[a].name + (snap.sp[a].Alive ? "" : "†")); a = snap.sp[a].parent; }
            GUI.Label(new Rect(x, r.yMax - 36, r.width - 20, 32), anc.Count > 0 ? "조상 ← " + string.Join(" ← ", anc) : "첫 조상", sWrap);
        }

        // ── 그래프 ──
        void GraphDraw(Rect r)
        {
            Glass(r);
            int n = hist.Count; if (n < 2) return;
            float gx = r.x + 60, gw = r.width - 70, gy = r.y + 8, gh = r.height - 24;
            int step = Math.Max(1, n / (int)gw); int maxSp = 1; foreach (var d in hist) maxSp = Math.Max(maxSp, d.nSp);
            Func<DayStat, float>[] f = {
                d => Mathf.InverseLerp(-8, 0, Mathf.Log10(Mathf.Max(1e-9f, d.O2))),
                d => Mathf.InverseLerp(-1, 2.5f, Mathf.Log10(Mathf.Max(0.01f, d.CO2))),
                d => Mathf.InverseLerp(-30, 50, d.T),
                d => (float)d.nSp / maxSp };
            for (int k = 0; k < f.Length; k++)
            {
                float px = 0, py = 0;
                for (int i = 0; i < n; i += step)
                {
                    float x = gx + gw * i / (n - 1), y = gy + gh * (1 - Mathf.Clamp01(f[k](hist[i])));
                    if (i > 0) Draw.Line(px, py, x, y, GCOL[k]); px = x; py = y;
                }
            }
            // 큰 사건 표시
            foreach (var e in news) if (e.size >= 5) { float x = gx + gw * Mathf.Clamp01((float)e.day / hist[n - 1].day); Draw.Line(x, gy + gh, x, gy + gh + 6, new Color(1, 0.5f, 0.3f, 0.8f)); }
        }
        static readonly Color[] GCOL = { new(0.4f, 0.7f, 1f), new(0.9f, 0.6f, 0.3f), new(1f, 0.4f, 0.4f), new(0.6f, 0.9f, 0.5f) };
        void GraphText(Rect r)
        {
            string[] nm = { "O₂ (로그 1e-8–1)", "CO₂ (로그 0.1–300)", "기온 (−30–50 °C)", "종 수" };
            for (int k = 0; k < 4; k++) { GUI.color = GCOL[k]; GUI.Label(new Rect(r.x + 6, r.y + 6 + k * 16, 60, 16), nm[k].Split(' ')[0], sS); }
            GUI.color = Color.white;
            if (hist.Count > 0) GUI.Label(new Rect(r.x + 60, r.yMax - 21, 420, 18), $"0일 → {hist[^1].day}일 (빨간 눈금 = 큰 사건)", sS);
        }

        // ── 계통수: 가로 = 시간, 세로 = 종(부모 아래에 자식, 나온 순). 멸종한 종은 회색 ──
        int[] treeRow; Snap treeSnap; int treeN; bool treeAllSeen;
        void TreeLayout()
        {
            if (treeSnap == snap && treeAllSeen == treeAll) return; treeSnap = snap; treeAllSeen = treeAll;
            int n = snap.sp.Length; treeRow = new int[n]; treeN = 0;
            var keep = new bool[n];   // 기본: 살아 있는 종과 그 조상만(멸종한 곁가지는 '멸종 곁가지도'로)
            for (int i = 0; i < n; i++) if (treeAll || snap.sp[i].Alive) for (int a = i; a >= 0 && a < n && !keep[a]; a = snap.sp[a].parent) keep[a] = true;
            for (int i = 0; i < n; i++) treeRow[i] = -1;
            var kids = new List<int>[n]; for (int i = 0; i < n; i++) kids[i] = new List<int>();
            var roots = new List<int>();
            for (int i = 0; i < n; i++) { if (!keep[i]) continue; var s = snap.sp[i]; if (s.parent >= 0 && s.parent < n) kids[s.parent].Add(i); else roots.Add(i); }
            var st = new Stack<int>(); for (int i = roots.Count - 1; i >= 0; i--) st.Push(roots[i]);
            while (st.Count > 0) { int i = st.Pop(); treeRow[i] = treeN++; for (int k = kids[i].Count - 1; k >= 0; k--) st.Push(kids[i][k]); }
        }
        Rect TreeRect => new Rect(gauge.xMax + 10, topH + 10, mapR.x - gauge.xMax - 20, (showGraph ? graphR.y : H) - topH - 20);
        const float TreeRh = 15;   // 한 줄에 종 하나(이름까지), 넘치면 휠로 내린다
        void TreeDraw()
        {
            TreeLayout(); var R = TreeRect;
            Draw.Rect(R.x, R.y, R.width, R.height, new Color(0.03f, 0.05f, 0.08f, 0.88f));
            float now = (float)Math.Max(1, snap.day), x0 = R.x + 10, w = R.width - 160, y0 = R.y + 28 - treeScroll, rh = TreeRh;
            for (int i = 0; i < snap.sp.Length; i++)
            {
                var s = snap.sp[i]; if (treeRow[i] < 0) continue; float y = y0 + treeRow[i] * rh + rh / 2;
                float xa = x0 + w * s.born / now, xb = x0 + w * (s.Alive ? 1 : s.died / now);
                if (s.parent >= 0 && treeRow[s.parent] >= 0) { float yp = y0 + treeRow[s.parent] * rh + rh / 2, ya = Mathf.Max(Mathf.Min(y, yp), R.y + 24), yb = Mathf.Min(Mathf.Max(y, yp), R.yMax - 2); if (yb > ya) Draw.Line(xa, ya, xa, yb, new Color(1, 1, 1, 0.18f)); }
                if (y < R.y + 24 || y > R.yMax) continue;
                Color c = s.Alive && s.t != null ? Draw.Of(s.t, s.id) : new Color(0.45f, 0.45f, 0.48f);
                Draw.Rect(xa, y - 3, Mathf.Max(1, xb - xa), 6, c);
                if (i == sel) Draw.Rect(x0 - 6, y - 1, 4, 2, Color.white);
            }
        }
        void TreeGui()
        {
            TreeLayout();
            var R = TreeRect; float rh = TreeRh, y0 = R.y + 28 - treeScroll, now = (float)Math.Max(1, snap.day), x0 = R.x + 10, w = R.width - 160;
            GUI.Label(new Rect(R.x + 10, R.y + 4, R.width - 180, 20), $"계통수 · 종 {snap.sp.Length}개(살아 있음 {snap.alive.Length}) · 회색 † = 멸종 · 누르면 그 종 정보", sB);
            if (GUI.Button(new Rect(R.xMax - 140, R.y + 4, 104, 20), "멸종 곁가지도", treeAll ? sOn : sBtn)) { treeAll = !treeAll; treeScroll = 0; }
            if (GUI.Button(new Rect(R.xMax - 30, R.y + 4, 24, 20), "×", sBtn)) showTree = false;
            var ev = UnityEngine.Event.current;
            if (ev.type == EventType.ScrollWheel && R.Contains(ev.mousePosition)) { treeScroll = Mathf.Clamp(treeScroll + ev.delta.y * 20, 0, Mathf.Max(0, treeN * rh - R.height + 40)); ev.Use(); }
            if (ev.type == EventType.MouseDown && R.Contains(ev.mousePosition) && ev.mousePosition.y > R.y + 24)
            {
                int row = (int)((ev.mousePosition.y - y0) / rh);
                for (int i = 0; i < snap.sp.Length; i++) if (treeRow[i] == row) { sel = i; break; }
                ev.Use();
            }
            for (int i = 0; i < snap.sp.Length; i++)
            {
                var s = snap.sp[i]; if (treeRow[i] < 0) continue; float y = y0 + treeRow[i] * rh;
                if (y < R.y + 24 || y > R.yMax - 14) continue;
                GUI.color = s.Alive ? Color.white : new Color(0.7f, 0.7f, 0.72f);
                GUI.Label(new Rect(x0 + w * (s.Alive ? 1 : s.died / now) + 4, y - 1, 150, 16), s.Alive ? s.name : s.name + " †", i == sel ? sL : sS);
            }
            GUI.color = Color.white;
        }

        // ── 사건 알림 ──
        void Ticker()
        {
            float x0 = gauge.xMax + 14, wMax = mapR.x - x0 - 14, y = topH + 8;
            for (int i = news.Count - 1, k = 0; i >= 0 && k < 4; i--, k++)
            {
                var e = news[i]; float age = (float)(snap.day - e.day); if (age > 400) break;
                float al = 1 - 0.5f * Mathf.Clamp01(age / 400);   // 오래된 알림도 반은 진하게
                string t = $"{e.day}일 {e.kind}: {e.text}";
                float tw = Mathf.Min(wMax, sS.CalcSize(new GUIContent(t)).x + 12), x = x0 + (wMax - tw) / 2;
                Fill(new Rect(x, y, tw, 18), GLASS);
                GUI.color = new Color(1, 0.85f, 0.7f, al);
                GUI.Label(new Rect(x + 6, y + 1, tw - 8, 18), t, sS); y += 20;
            }
            GUI.color = Color.white;
        }
    }
}
