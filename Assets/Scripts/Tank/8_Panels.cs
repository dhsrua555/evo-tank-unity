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
        GUIStyle sL, sB, sS, sBtn, sOn, sField, sR, sSR, sWrap, sSC, sBC, sTiny, sWrapL;
        const float INFO_H = 380, INFO_S = 236;   // 정보 판: 자세히(단백질 배분·조상) / 기본(접힘) — 기본이 작아야 좁은 화면에서 종 목록이 눌리지 않는다
        bool showGraph = true, showTree, showEv, infoMore;
        string search = "", muText = "1";
        Vector2 listScroll;
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
            sR = new GUIStyle(sL) { richText = true }; sSR = new GUIStyle(sS) { richText = true, normal = { textColor = fg } }; sWrap = new GUIStyle(sS) { wordWrap = true }; sWrapL = new GUIStyle(sWrap) { normal = { textColor = new Color(0.8f, 0.83f, 0.86f) } };
            sSC = new GUIStyle(sS) { alignment = TextAnchor.UpperCenter }; sBC = new GUIStyle(sB) { alignment = TextAnchor.UpperCenter }; sTiny = new GUIStyle(sBtn) { fontSize = 9, padding = new RectOffset(0, 0, 0, 0) };
        }

        void OnGUI()
        {
            if (snap == null) return;
            Styles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(Draw.ui, Draw.ui, 1));
            var ev0 = UnityEngine.Event.current;
            if (showEv && (ev0.isMouse || ev0.type == EventType.ScrollWheel) && evR.Contains(ev0.mousePosition)) { EventMenu(); return; }   // 메뉴가 맨 위에 뜨므로 그 안의 누름은 메뉴만 받는다(밑의 지도·목록이 가로채지 않게)
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
            if (showEv) EventMenu();
            if (Time.unscaledTime < noteUntil) GUI.Label(new Rect((W - panelW) / 2 - 200, topH + 8, 400, 20), note, sSC);   // 물속 화면 가운데 위
        }

        // ── 사건 메뉴: 일으킬 사건과 그 효과, 지금 남은 효과, 인 공급 손잡이, 새 지구 ──
        Rect evR;
        void EventMenu()
        {
            var r = evR; var K = Planet.KINDS;
            if (UnityEngine.Event.current.type == EventType.Repaint) { Fill(r, new Color(0.03f, 0.05f, 0.09f, 0.97f)); Fill(new Rect(r.x, r.y, r.width, 1), EDGE); Fill(new Rect(r.x, r.yMax - 1, r.width, 1), EDGE); }
            float x = r.x + 8, y = r.y + 6, w = r.width - 16;
            GUI.Label(new Rect(x, y, w - 60, 20), "자연 사건 · 누르면 크기 10으로 일으킴(저절로도 일어남)", sB);
            if (GUI.Button(new Rect(r.xMax - 58, y - 2, 50, 24), "닫기", sBtn)) showEv = false;
            y += 28;
            for (int k = 0; k < K.Length; k++)
            {
                if (GUI.Button(new Rect(x, y + 2, 100, 28), K[k].name, sBtn)) { int kk = k; run.cmds.Enqueue(p => p.Trigger(kk, 10)); showEv = false; }
                GUI.Label(new Rect(x + 106, y, w - 106, 40), K[k].fx, sWrapL);
                y += 42;
            }
            var fx = snap.fx;
            GUI.Label(new Rect(x, y, w, 34), "지금 남은 효과: " + (fx == null || fx.Length == 0 ? "없음" : string.Join(" · ", fx)), sWrapL); y += 36;
            double ph = run.pl.knobs.phosphate;
            GUI.Label(new Rect(x, y, 130, 20), $"인 공급(풍화) ×{ph:0.0#}", sL);
            float lp = GUI.HorizontalSlider(new Rect(x + 134, y + 5, 130, 12), (float)Math.Log10(ph), -1, 1);
            if (Math.Abs(lp - Math.Log10(ph)) > 1e-3) { double p2 = Math.Round(Math.Pow(10, lp), 2); run.cmds.Enqueue(p => p.knobs.phosphate = p2); run.pl.knobs.phosphate = p2; }
            GUI.Label(new Rect(x + 272, y, w - 272, 20), "바다에 쌓인 인이 많아 아주 느림(×10이어도 100만 일+)", sS); y += 22;
            GUI.Label(new Rect(x, y, w, 30), $"지금 광합성을 막는 것: {snap.lim} — 인·질소면 바다 뒤집힘(사건)이 깊은 물의 영양분을 올려 광합성을 늘림", sWrapL); y += 34;
            GUI.Label(new Rect(x, y, w, 46), "산소가 7 % PAL쯤에 머무는 까닭: 깊은 바다의 황화수소·철이 내려오는 산소를 먹어 치운다(원생누대 '지루한 10억 년'처럼). 광합성만 늘리면 호흡·분해가 그만큼 태운다. 산소는 묻힌 유기물·황철석이 남긴 만큼만 쌓이고, 깊은 바다까지 산화해야 올라서 수십만 일 넘게 걸린다.", sWrap); y += 50;
            // 새 지구: 한 번 누르면 묻고, 4초 안에 다시 누르면 지금 지구를 지우고 처음부터
            bool ask = Time.unscaledTime - askNew < 4;
            if (GUI.Button(new Rect(x, y, 130, 26), ask ? "정말? 한 번 더" : "새 지구로 시작", ask ? sOn : sBtn)) { if (ask) NewEarth(); else askNew = Time.unscaledTime; }
        }

        // ── 윗줄 ──
        static readonly double[] SPEEDS = { -1, 1, 10, 100, 0 };
        static readonly string[] SPN = { "멈춤", "1일/초", "10", "100", "최대" };
        float askNew = -9;
        void TopBar()
        {
            var s = snap; double yr = s.day / 365;
            string t = $"{s.day:0}일 ({(yr < 10 ? yr.ToString("0.0") : yr.ToString("0"))}년) · 산소 {Sci(s.O2)} PAL · CO₂ {s.CO2:0.0} PAL · 메탄 {s.CH4:0} ppm · 기온 {s.T:0.0} °C · 종 {s.nSp} · {s.rate:0}일/초";
            float x = 10, y = 24, tight = Mathf.Clamp01((1000 - W) / 150);   // 좁은 화면(태블릿 ≈ 930): 막대를 줄여 오른쪽 단추와 안 겹치게
            for (int i = 0; i < SPEEDS.Length; i++)
            {
                bool on = SPEEDS[i] < 0 ? run.paused : !run.paused && run.speed == SPEEDS[i];
                if (GUI.Button(new Rect(x, y, i == 0 ? 44 : 50, 19), SPN[i], on ? sOn : sBtn)) { if (SPEEDS[i] < 0) run.paused = true; else { run.paused = false; run.speed = SPEEDS[i]; } }
                x += (i == 0 ? 44 : 50) + 2;
            }
            x += 14;
            GUI.Label(new Rect(x, y, 66, 20), "돌연변이율", sL); x += 66;
            double mu = run.pl.knobs.mu; float lg = GUI.HorizontalSlider(new Rect(x, y + 5, 110 - 50 * tight, 12), (float)Math.Log10(mu), -4, 1); x += 116 - 50 * tight;
            if (Math.Abs(lg - Math.Log10(mu)) > 1e-4) { mu = Math.Pow(10, lg); SetMu(mu); muText = mu.ToString("0.####"); }
            string nt = GUI.TextField(new Rect(x, y, 56, 19), muText, sField); x += 62;
            if (nt != muText) { muText = nt; if (double.TryParse(nt, out double m) && m > 0) SetMu(Math.Min(10, m)); }
            x += 10;
            GUI.Label(new Rect(x, y, 52, 20), "사건 빈도", sL); x += 56;
            float er = GUI.HorizontalSlider(new Rect(x, y + 5, 80 - 30 * tight, 12), (float)run.pl.knobs.eventRate, 0, 5); x += 84 - 30 * tight;
            if (Math.Abs(er - run.pl.knobs.eventRate) > 1e-3) { double e2 = Math.Round(er, 1); run.cmds.Enqueue(p => p.knobs.eventRate = e2); }
            GUI.Label(new Rect(x, y, 34, 20), $"×{run.pl.knobs.eventRate:0.0}", sS); x += 36;
            if (GUI.Button(new Rect(x, y, 64, 19), "사건 ▼", showEv ? sOn : sBtn)) showEv = !showEv;
            float ew = Mathf.Min(460, W - 8); evR = new Rect(Mathf.Clamp(x + 64 - ew, 4, W - ew - 4), topH + 2, ew, 34 + Planet.KINDS.Length * 42 + 180);
            x += 70;
            // 오른쪽 끝에 붙이되 사건 단추를 덮지 않는다. 그래도 넘치면(세로 화면·휴대폰) 윗줄 오른쪽으로 올리고 상태 글자를 그만큼 줄인다
            bool up = Mathf.Max(x, W - 170) + 164 > W - 4;
            float xr = up ? W - 174 : Mathf.Max(x, W - 170), yb = up ? 2 : y;
            if (GUI.Button(new Rect(xr, yb, 80, 19), "계통수", showTree ? sOn : sBtn)) showTree = !showTree;
            if (GUI.Button(new Rect(xr + 84, yb, 80, 19), "그래프", showGraph ? sOn : sBtn)) showGraph = !showGraph;
            GUI.Label(new Rect(10, 3, (up ? xr - 6 : W - 10) - 10, 20), t, sB);
        }
        // 반투명 판: GL(Draw)로 그리는 것과 GUI로 그리는 것 둘 다
        static readonly Color GLASS = new(0.03f, 0.06f, 0.1f, 0.65f), EDGE = new(1, 1, 1, 0.12f);
        static void Glass(Rect r) { Draw.Rect(r.x, r.y, r.width, r.height, GLASS); Draw.Line(r.x, r.y, r.xMax, r.y, EDGE); Draw.Line(r.x, r.yMax, r.xMax, r.yMax, EDGE); Draw.Line(r.x, r.y, r.x, r.yMax, EDGE); Draw.Line(r.xMax, r.y, r.xMax, r.yMax, EDGE); }
        static void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
        static void Card(Rect r) { Fill(r, GLASS); Fill(new Rect(r.x, r.y, r.width, 1), EDGE); Fill(new Rect(r.x, r.yMax - 1, r.width, 1), EDGE); Fill(new Rect(r.x, r.y, 1, r.height), EDGE); Fill(new Rect(r.xMax - 1, r.y, 1, r.height), EDGE); }
        void SetMu(double m) { run.cmds.Enqueue(p => p.knobs.mu = m); run.pl.knobs.mu = m; }
        static string Size(double um) => um < 1000 ? $"{um:0.#} μm" : $"{um / 1000:0.#} mm";
        static string Sci(double x) => x <= 0 ? "0" : x >= 0.01 ? x.ToString("0.###") : x.ToString("0.0e0");

        // ── 종 목록: 하는 일·몸 꼴로 거르고(갈래로 묶지 않음 — 갈래 하나에 종이 1–3개라 묶어도 줄지 않았다), 줄마다 무엇인지(일·꼴·크기)를 보인다 ──
        static readonly string[] FORMS = { "단세포", "작은 덩이", "실", "판", "덩어리" }, ROLES = { "광합성", "화학", "포식", "청소", "분해", "혼합" }, SORTS = { "많은 순", "큰 순", "나온 순" };
        static int FormOf(SpView s) { double n = s.t.MeanCells; return n < 2 ? 0 : n < 8 ? 1 : s.body.dim < 1.5 ? 2 : s.body.dim < 2.3 ? 3 : 4; }   // LifeTest Forms와 같은 경계
        int fRole = -1, fForm = -1, sortBy; readonly List<int> shown = new();
        void List(Rect r)
        {
            var s = snap; float y = r.y + 6, ch = Application.isMobilePlatform ? 24 : 20, rh = Application.isMobilePlatform ? 26 : 21;
            shown.Clear();
            foreach (int i in s.alive)
            {
                var sp = s.sp[i]; if (sp.body == null || sp.t == null) continue;
                if (fRole >= 0 && (int)sp.role != fRole || fForm >= 0 && FormOf(sp) != fForm) continue;
                if (search.Length > 0 && sp.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 && s.clades[sp.clade].name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                shown.Add(i);
            }
            Func<int, double> key = sortBy == 0 ? i => -s.sp[i].biomass : sortBy == 1 ? i => -s.sp[i].body.size : i => -s.sp[i].born;
            shown.Sort((a, b) => key(a).CompareTo(key(b)));
            GUI.Label(new Rect(r.x + 10, y, r.width - 20, 20), shown.Count == s.alive.Length ? $"살아 있는 종 {s.alive.Length}" : $"살아 있는 종 {s.alive.Length} 중 {shown.Count}", sB);
            y += 22;
            search = GUI.TextField(new Rect(r.x + 10, y, r.width - 20, ch), search, sField);
            if (search.Length == 0) GUI.Label(new Rect(r.x + 14, y + 1, r.width - 30, 20), Application.isMobilePlatform ? "이름 찾기" : "이름 찾기 · 키 N = 다음 종, Shift+N = 이전", sS);
            y += ch + 4;
            y = Chips(r, y, ch, ROLES, ref fRole, true); y = Chips(r, y, ch, FORMS, ref fForm, true); y = Chips(r, y, ch, SORTS, ref sortBy, false) + 2;
            float inner = shown.Count * rh; listWant = y - r.y + inner + 8;
            DragScroll(1, new Rect(r.x, y, r.width, r.yMax - y), ref listScroll.y, inner - (r.yMax - y));
            listScroll = GUI.BeginScrollView(new Rect(r.x, y, r.width, r.yMax - y), listScroll, new Rect(0, 0, r.width - 16, inner));
            listView = r.yMax - y;
            float top = listScroll.y - rh, bot = listScroll.y + (r.yMax - y);
            for (int k = 0; k < shown.Count; k++)
            {
                float yy = k * rh; if (yy < top || yy > bot) continue;
                var sp = s.sp[shown[k]];
                if (sp.id == sel) { GUI.color = new Color(1, 1, 1, 0.12f); GUI.DrawTexture(new Rect(0, yy, r.width, rh), Texture2D.whiteTexture); GUI.color = Color.white; }
                Swatch(new Rect(8, yy + rh / 2 - 5, 10, 10), sp);
                if (GUI.Button(new Rect(22, yy, r.width - 40, rh - 1), $"{sp.name}  <size=11><color=#9aa3ad>{ROLES[(int)sp.role]} · {FORMS[FormOf(sp)]} {Size(sp.body.size)} · {Draw.Mass(sp.biomass)}</color></size>", sR)) Focus(sp.id);
            }
            GUI.EndScrollView();
        }
        float listView;
        // 칩 한 줄: 누르면 그것만, 다시 누르면 '전체'(all = true일 때 맨 앞에 '전체' 칩)
        float Chips(Rect r, float y, float h, string[] names, ref int v, bool all)
        {
            int n = names.Length + (all ? 1 : 0); float w = (r.width - 20) / n;
            for (int k = 0; k < n; k++)
            {
                int val = all ? k - 1 : k; string t = all && k == 0 ? "전체" : names[all ? k - 1 : k];
                if (GUI.Button(new Rect(r.x + 10 + k * w, y, w - 2, h), t, v == val ? sOn : sBtn)) { v = val; listScroll.y = 0; }
            }
            return y + h + 3;
        }
        // 다음·이전 종(지금 거른 목록 차례대로): 그 종이 사는 곳으로 가서 하나를 따라가고, 목록도 그 줄로 넘긴다
        void Step(int d)
        {
            if (shown.Count == 0) return;
            int k = shown.IndexOf(sel); k = k < 0 ? (d > 0 ? 0 : shown.Count - 1) : (k + d + shown.Count) % shown.Count;
            Focus(shown[k]);
            float rh = Application.isMobilePlatform ? 26 : 21; listScroll.y = Mathf.Max(0, k * rh - listView / 2);
        }
        // 손가락(마우스)으로 끌어 넘기기: IMGUI 스크롤 뷰는 터치 끌기를 모른다. 판 안에서 누른 채 8 단위 넘게 움직이면 넘기기로 보고 단추 누름을 취소한다
        Vector2 dragAt; float dragFrom; int dragKey = -1; bool dragMoved;
        void DragScroll(int key, Rect area, ref float scroll, float max)
        {
            var e = UnityEngine.Event.current;
            if (e.type == EventType.MouseDown && area.Contains(e.mousePosition)) { dragKey = key; dragAt = e.mousePosition; dragFrom = scroll; dragMoved = false; }
            else if (e.type == EventType.MouseDrag && dragKey == key)
            {
                float dy = e.mousePosition.y - dragAt.y;
                if (!dragMoved && Mathf.Abs(dy) > 8) { dragMoved = true; GUIUtility.hotControl = 0; }   // 누르던 단추를 놓게 한다(손을 떼도 안 눌림)
                if (dragMoved) { scroll = Mathf.Clamp(dragFrom - dy, 0, Mathf.Max(0, max)); e.Use(); }
            }
            else if (e.type == EventType.MouseUp && dragKey == key) { dragKey = -1; if (dragMoved) e.Use(); }
        }
        void Swatch(Rect r, SpView s) { if (s.t == null) return; GUI.color = Draw.Of(s.t, s.id); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }

        // ── 고른 종 ──
        Rect bodyBox;
        void InfoDraw(Rect r)
        {
            Glass(r);
            var s = snap.sp[sel]; if (s.t == null) return;
            bodyBox = new Rect(r.xMax - 106, r.y + 34, 96, 96);   // 위 오른쪽 구석은 닫기 단추 자리
            Draw.Rect(bodyBox.x, bodyBox.y, bodyBox.width, bodyBox.height, new Color(0.04f, 0.06f, 0.1f));
            Draw.Body(Draw.ShapeOf(s.body, s.t), bodyBox.center.x, bodyBox.center.y, 42, Time.time * 0.1f, Draw.Of(s.t, s.id), 1);
            // 수입 막대
            float bx = r.x + 10, by = r.y + 164, bw = r.width - 20; double sum = s.income.Sum();
            Color[] ic = { new Color(0.3f, 0.8f, 0.4f), new Color(0.98f, 0.6f, 0.2f), new Color(0.95f, 0.28f, 0.3f), new Color(0.75f, 0.6f, 0.4f), new Color(0.8f, 0.8f, 0.78f) };
            float xx = bx;
            for (int q = 0; q < 5 && sum > 0; q++) { float w = (float)(s.income[q] / sum) * bw; Draw.Rect(xx, by, w, 8, ic[q]); xx += w; }
            // 단백질 배분 막대(많은 것 7개)
            var top = Enumerable.Range(0, G.NA).OrderBy(i => -s.t.phi[i]).Take(7).ToArray();
            for (int k = 0; k < top.Length && infoMore; k++) Draw.Rect(bx + 90, r.y + 238 + k * 15, (float)(s.t.phi[top[k]] / s.t.phi[top[0]]) * (bw - 150), 9, new Color(0.5f, 0.65f, 0.85f, 0.8f));
        }
        void Info(Rect r)
        {
            var s = snap.sp[sel]; if (s.t == null) return;
            if (GUI.Button(new Rect(r.xMax - 92, r.y + 4, 26, 26), "◀", sBtn)) { Step(-1); return; }   // 거른 목록의 이전·다음 종
            if (GUI.Button(new Rect(r.xMax - 64, r.y + 4, 26, 26), "▶", sBtn)) { Step(1); return; }
            if (GUI.Button(new Rect(r.xMax - 36, r.y + 4, 26, 26), "×", sBtn)) { sel = -1; return; }
            float x = r.x + 10, y = r.y + 8, w = r.width - 126;
            GUI.Label(new Rect(x, y, w, 20), s.name, sB); y += 20;
            GUI.Label(new Rect(x, y, w, 18), $"{snap.clades[s.clade].name} · {(s.Alive ? $"{s.born}일에 나옴" : $"{s.born}–{s.died}일 (멸종)")}", sS); y += 18;
            GUI.Label(new Rect(x, y, w, 18), $"하는 일: {Draw.RoleName[(int)s.role]}", sL); y += 18;
            GUI.Label(new Rect(x, y, w, 18), $"생물량 {Draw.Mass(s.biomass)} · 계통 {s.strains}", sL); y += 18;
            double mc = s.t.MeanCells;
            GUI.Label(new Rect(x, y, w, 18), mc < 1.5 ? $"단세포 {s.t.dCell:0.#} μm" : $"{FORMS[FormOf(s)]} · 세포 {mc:0}개 · {Size(s.body.size)}", sL); y += 18;
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
            if (GUI.Button(new Rect(r.xMax - 74, r.y + 214, 64, 18), infoMore ? "접기 ▲" : "자세히 ▼", sBtn)) infoMore = !infoMore;
            if (!infoMore) return;
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
        const float TreeRh = 18;   // 한 줄에 종 하나(이름까지), 넘치면 휠로 내린다
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
            GUI.Label(new Rect(R.x + 10, R.y + 4, R.width - 180, 20), R.width < 560 ? $"계통수 · 종 {snap.sp.Length} · 살아 있음 {snap.alive.Length}" : $"계통수 · 종 {snap.sp.Length}개(살아 있음 {snap.alive.Length}) · 회색 † = 멸종 · 누르면 그 종 정보", sB);   // 좁으면 짧게
            if (GUI.Button(new Rect(R.xMax - 172, R.y + 3, 104, 24), "멸종 곁가지도", treeAll ? sOn : sBtn)) { treeAll = !treeAll; treeScroll = 0; }
            if (GUI.Button(new Rect(R.xMax - 64, R.y + 3, 58, 24), "닫기 ×", sBtn)) showTree = false;
            DragScroll(2, new Rect(R.x, R.y + 28, R.width, R.height - 28), ref treeScroll, treeN * rh - R.height + 40);
            var ev = UnityEngine.Event.current;
            if (ev.type == EventType.ScrollWheel && R.Contains(ev.mousePosition)) { treeScroll = Mathf.Clamp(treeScroll + ev.delta.y * 20, 0, Mathf.Max(0, treeN * rh - R.height + 40)); ev.Use(); }
            if (ev.type == EventType.MouseUp && R.Contains(ev.mousePosition) && ev.mousePosition.y > R.y + 28)
            {
                int row = (int)((ev.mousePosition.y - y0) / rh);
                for (int i = 0; i < snap.sp.Length; i++) if (treeRow[i] == row) { sel = i; break; }
                ev.Use();
            }
            for (int i = 0; i < snap.sp.Length; i++)
            {
                var s = snap.sp[i]; if (treeRow[i] < 0) continue; float y = y0 + treeRow[i] * rh;
                if (y < R.y + 24 || y > R.yMax - 14) continue;
                GUI.color = s.Alive || i == sel ? Color.white : new Color(0.92f, 0.92f, 0.95f);   // 멸종 이름도 어두운 판에서 읽히게(sS 회색 × 0.92)
                GUI.Label(new Rect(x0 + w * (s.Alive ? 1 : s.died / now) + 4, y - 2, 170, rh + 4), s.Alive ? s.name : s.name + " †", s.Alive || i == sel ? sL : sS);
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
