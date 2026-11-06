// 화면 판(IMGUI): 머리 줄 · 오른쪽 판(환경·개체·종·기록) · 아래 왼쪽 축척 · 큰 창(계통수·진화 경로).
// 사용자판은 필요한 것만 펴 두고 나머지는 접는다. 개발자판(EVOTANK_DEV 빌드 또는 -dev)은 수치 조정·계산 시간·원래 유전자 값까지 보인다.
// 단추·판은 Unity 기본 모양 대신 둥근 반투명 판과 직접 그린 아이콘(거리장으로 만든 텍스처)을 쓴다.
using System;
using System.Collections.Generic;
using System.Linq;
using Evo;
using UnityEngine;

public partial class EvoTank
{
#if EVOTANK_DEV
    static readonly bool DEV = true;
#else
    static readonly bool DEV = Array.IndexOf(Environment.GetCommandLineArgs(), "-dev") >= 0;
#endif
    Font font, fontB; GUIStyle sL, sB, sS, sBtn, sOn, sBox, sT, sTd, sTab, sTabOn, sChip, sChipOn, sIco, sCard, sH, sFold, sRow, sRowOn; Texture2D bg;
    Rect panel, top, info; int tab; Vector2 scroll, winScroll;
    int win;                                     // 큰 창: 0 없음 · 1 계통수 · 2 진화 경로
    bool help, showCycle, showWorld, showAdv, showBud = true;
    int evFilter;                                // 기록 거르기: 0 전체 · 1 지질 · 2 종 분화 · 3 멸종 · 4 체제·큰 사건
    readonly HashSet<int> fold = new();          // 접어 둔 계통(종 id)
    Action later;                                // 배치가 바뀌는 조작은 다음 Layout 앞에서(IMGUI 칸 수가 어긋나지 않게)
    void Later(Action a) => later += a;

    static readonly string[] TABS = { "환경", "개체", "종", "기록" };
    static readonly Color TXT = new(0.86f, 0.9f, 0.94f), DIM = new(0.56f, 0.64f, 0.72f), ACC = new(0.37f, 0.83f, 1f);
    float UIScale => Mathf.Max(0.75f, Mathf.Min(Screen.height / 900f, Screen.width / 1600f));

    // ---------- 모양: 둥근 판·아이콘 텍스처 ----------
    static Texture2D Tex(int n) => new(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
    // 둥근 사각형(반지름 r px): 안 fill, 바깥 1 px 테두리 edge. under > 0이면 밑줄만(탭)
    static Texture2D Round(int r, Color fill, Color edge, int under = 0)
    {
        int n = r * 2 + 6; var t = Tex(n); var px = new Color[n * n]; float h = n / 2f;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            Color c;
            if (under > 0) c = y < under ? edge : fill;
            else
            {
                float qx = Mathf.Abs(x + 0.5f - h) - (h - r), qy = Mathf.Abs(y + 0.5f - h) - (h - r);
                float d = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r + 1;   // 둥근 사각형까지 거리(안 −)
                float e = Mathf.Clamp01(1 - Mathf.Abs(d + 0.5f));
                c = Color.Lerp(fill, new Color(edge.r, edge.g, edge.b, Mathf.Max(fill.a, edge.a)), edge.a > 0 ? e : 0); c.a *= Mathf.Clamp01(0.5f - d);
            }
            px[y * n + x] = c;
        }
        t.SetPixels(px); t.Apply(); return t;
    }
    // 아이콘: 거리장 f(p) (p = −1…1, 안 −)을 흰 글리프로. 그릴 때 GUI.color로 물들인다
    static Texture2D Glyph(Func<Vector2, float> f)
    {
        const int n = 64; var t = Tex(n); var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { var p = new Vector2((x + 0.5f) / n * 2 - 1, 1 - (y + 0.5f) / n * 2); px[(n - 1 - y) * n + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - f(p) * n / 2)); }
        t.SetPixels(px); t.Apply(); return t;
    }
    static float Seg(Vector2 p, Vector2 a, Vector2 b, float r) { var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude); return (p - a - ab * t).magnitude - r; }
    static float Tri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float Cr(Vector2 u, Vector2 v, Vector2 q) => (v.x - u.x) * (q.y - u.y) - (v.y - u.y) * (q.x - u.x);
        bool inside = Cr(a, b, p) <= 0 && Cr(b, c, p) <= 0 && Cr(c, a, p) <= 0 || Cr(a, b, p) >= 0 && Cr(b, c, p) >= 0 && Cr(c, a, p) >= 0;
        float d = Mathf.Min(Seg(p, a, b, 0), Mathf.Min(Seg(p, b, c, 0), Seg(p, c, a, 0))); return inside ? -d : d;
    }
    static float Circ(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
    Texture2D iPause, iPlay, iFast, iMax, iClose, iRight, iDown, iTree, iPath, iFit, iCard, iTray, iHelp;
    void Icons()
    {
        iPause = Glyph(p => Mathf.Min(Seg(p, new(-0.32f, -0.5f), new(-0.32f, 0.5f), 0.16f), Seg(p, new(0.32f, -0.5f), new(0.32f, 0.5f), 0.16f)));
        iPlay = Glyph(p => Tri(p, new(-0.4f, -0.6f), new(-0.4f, 0.6f), new(0.6f, 0)));
        iFast = Glyph(p => Mathf.Min(Tri(p, new(-0.75f, -0.5f), new(-0.75f, 0.5f), new(0.05f, 0)), Tri(p, new(0.0f, -0.5f), new(0.0f, 0.5f), new(0.8f, 0))));
        iMax = Glyph(p => Mathf.Min(Mathf.Min(Tri(p, new(-0.8f, -0.45f), new(-0.8f, 0.45f), new(-0.1f, 0)), Tri(p, new(-0.15f, -0.45f), new(-0.15f, 0.45f), new(0.55f, 0))), Seg(p, new(0.7f, -0.45f), new(0.7f, 0.45f), 0.1f)));
        iClose = Glyph(p => Mathf.Min(Seg(p, new(-0.5f, -0.5f), new(0.5f, 0.5f), 0.11f), Seg(p, new(-0.5f, 0.5f), new(0.5f, -0.5f), 0.11f)));
        iRight = Glyph(p => Tri(p, new(-0.3f, -0.5f), new(-0.3f, 0.5f), new(0.45f, 0)));
        iDown = Glyph(p => Tri(p, new(-0.5f, 0.3f), new(0.5f, 0.3f), new(0, -0.45f)));
        iTree = Glyph(p => Mathf.Min(Mathf.Min(Seg(p, new(-0.7f, 0), new(-0.2f, 0), 0.09f), Seg(p, new(-0.2f, -0.55f), new(-0.2f, 0.55f), 0.09f)),
                                 Mathf.Min(Mathf.Min(Seg(p, new(-0.2f, 0.55f), new(0.7f, 0.55f), 0.09f), Seg(p, new(-0.2f, -0.55f), new(0.25f, -0.55f), 0.09f)), Mathf.Min(Seg(p, new(0.25f, -0.55f), new(0.25f, -0.2f), 0.09f), Seg(p, new(0.25f, -0.2f), new(0.7f, -0.2f), 0.09f)))));
        iPath = Glyph(p => Mathf.Min(Mathf.Min(Circ(p, new(-0.6f, -0.45f), 0.18f), Circ(p, new(0, 0.2f), 0.18f)), Mathf.Min(Circ(p, new(0.6f, -0.1f), 0.18f), Mathf.Min(Seg(p, new(-0.6f, -0.45f), new(0, 0.2f), 0.07f), Seg(p, new(0, 0.2f), new(0.6f, -0.1f), 0.07f)))));
        iFit = Glyph(p => Mathf.Min(Mathf.Abs(Circ(p, Vector2.zero, 0.5f)) - 0.08f, Mathf.Min(Circ(p, Vector2.zero, 0.14f), Mathf.Min(Seg(p, new(0, 0.62f), new(0, 0.9f), 0.07f), Seg(p, new(0, -0.62f), new(0, -0.9f), 0.07f)))));
        iCard = Glyph(p => { var q = new Vector2(Mathf.Abs(p.x) - 0.45f, Mathf.Abs(p.y) - 0.6f); float box = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - 0.12f; return Mathf.Min(Mathf.Abs(box) - 0.07f, Circ(p, new(0, 0.12f), 0.2f)); });
        iTray = Glyph(p => { float d = 9; foreach (var c in new[] { new Vector2(-0.38f, -0.38f), new(0.38f, -0.38f), new(-0.38f, 0.38f), new(0.38f, 0.38f) }) { var q = new Vector2(Mathf.Abs(p.x - c.x) - 0.2f, Mathf.Abs(p.y - c.y) - 0.2f); d = Mathf.Min(d, new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - 0.06f); } return d; });
        iHelp = Glyph(p => Mathf.Min(Mathf.Abs(Circ(p, Vector2.zero, 0.78f)) - 0.07f, Mathf.Min(Circ(p, new(0, -0.45f), 0.09f), Mathf.Min(Mathf.Abs(Circ(p, new(0, 0.2f), 0.25f)) - 0.07f + (p.y < 0.1f && p.x < 0.05f ? 1 : 0), Seg(p, new(0, -0.05f), new(0, -0.22f), 0.07f)))));
    }

    void Fonts()
    {
        font = Resources.Load<Font>("Fonts/Pretendard-Regular"); fontB = Resources.Load<Font>("Fonts/Pretendard-SemiBold");
        if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "Noto Sans CJK KR", "Malgun Gothic", "Arial" }, 14);
        if (!fontB) fontB = font;
        bg = Round(12, new Color(0.03f, 0.045f, 0.065f, 0.86f), new Color(1, 1, 1, 0.07f)); Icons();
    }
    void Styles()
    {
        if (sL != null) return;
        GUIStyle S(Font f, int size, Color c) => new() { font = f, fontSize = size, wordWrap = true, richText = true, normal = { textColor = c } };
        GUIStyle Btn(Texture2D n, Texture2D h, Texture2D a, Color tc, int r) => new()
        {
            font = font, fontSize = 13, alignment = TextAnchor.MiddleCenter, richText = true, border = new RectOffset(r + 3, r + 3, r + 3, r + 3),
            padding = new RectOffset(10, 10, 5, 5), margin = new RectOffset(2, 2, 2, 2),
            normal = { background = n, textColor = tc }, hover = { background = h, textColor = Color.white }, active = { background = a, textColor = Color.white },
        };
        sL = S(font, 14, TXT); sB = S(fontB, 16, Color.white); sS = S(font, 12, DIM);
        var bN = Round(7, new Color(1, 1, 1, 0.055f), new Color(1, 1, 1, 0.09f)); var bH = Round(7, new Color(1, 1, 1, 0.11f), new Color(1, 1, 1, 0.18f)); var bA = Round(7, new Color(1, 1, 1, 0.17f), new Color(1, 1, 1, 0.22f));
        var oN = Round(7, new Color(ACC.r, ACC.g, ACC.b, 0.17f), new Color(ACC.r, ACC.g, ACC.b, 0.7f)); var oH = Round(7, new Color(ACC.r, ACC.g, ACC.b, 0.26f), new Color(ACC.r, ACC.g, ACC.b, 0.85f));
        sBtn = Btn(bN, bH, bA, TXT, 7); sOn = Btn(oN, oH, oH, new Color(0.7f, 0.93f, 1f), 7); sOn.font = fontB;
        var clear = Round(7, Color.clear, Color.clear);
        sTab = Btn(clear, Round(2, Color.clear, new Color(1, 1, 1, 0.25f), 2), clear, DIM, 2); sTab.fontSize = 14; sTab.border = new RectOffset(2, 2, 3, 3);
        sTabOn = Btn(Round(2, Color.clear, ACC, 3), Round(2, Color.clear, ACC, 3), Round(2, Color.clear, ACC, 3), Color.white, 2); sTabOn.font = fontB; sTabOn.fontSize = 14; sTabOn.border = sTab.border;
        var cN = Round(11, new Color(1, 1, 1, 0.05f), new Color(1, 1, 1, 0.12f)); var cH = Round(11, new Color(1, 1, 1, 0.1f), new Color(1, 1, 1, 0.2f)); var cO = Round(11, new Color(ACC.r, ACC.g, ACC.b, 0.2f), new Color(ACC.r, ACC.g, ACC.b, 0.75f));
        // 칩: 높이 24에 맞는 반지름(9-slice 테두리가 높이보다 크면 위아래 테두리가 겹쳐 글자 가운데 회색 띠가 생긴다)
        var kN = Round(8, new Color(1, 1, 1, 0.05f), new Color(1, 1, 1, 0.12f)); var kH = Round(8, new Color(1, 1, 1, 0.1f), new Color(1, 1, 1, 0.2f)); var kO = Round(8, new Color(ACC.r, ACC.g, ACC.b, 0.2f), new Color(ACC.r, ACC.g, ACC.b, 0.75f));
        sChip = Btn(kN, kH, kH, DIM, 8); sChip.fontSize = 12; sChip.padding = new RectOffset(10, 10, 4, 4); sChip.fixedHeight = 24;
        sChipOn = Btn(kO, kO, kO, Color.white, 8); sChipOn.fontSize = 12; sChipOn.padding = sChip.padding; sChipOn.fixedHeight = 24;
        sIco = Btn(clear, bH, bA, TXT, 7); sIco.padding = new RectOffset(6, 6, 6, 6);
        sBox = new GUIStyle { normal = { background = bg }, border = new RectOffset(15, 15, 15, 15) };
        sCard = new GUIStyle { normal = { background = Round(8, new Color(1, 1, 1, 0.035f), new Color(1, 1, 1, 0.06f)) }, border = new RectOffset(11, 11, 11, 11), padding = new RectOffset(10, 10, 8, 8), margin = new RectOffset(0, 0, 3, 5) };
        sH = S(fontB, 13, new Color(0.74f, 0.82f, 0.9f)); sH.margin = new RectOffset(0, 0, 6, 2);
        sFold = Btn(clear, Round(6, new Color(1, 1, 1, 0.05f), Color.clear), clear, new Color(0.74f, 0.82f, 0.9f), 6); sFold.alignment = TextAnchor.MiddleLeft; sFold.font = fontB; sFold.padding = new RectOffset(24, 6, 5, 5);
        sRow = Btn(clear, Round(6, new Color(1, 1, 1, 0.06f), Color.clear), Round(6, new Color(1, 1, 1, 0.1f), Color.clear), TXT, 6); sRow.alignment = TextAnchor.MiddleLeft; sRow.padding = new RectOffset(6, 6, 3, 3); sRow.margin = new RectOffset(0, 0, 0, 0);
        sRowOn = new GUIStyle(sRow) { normal = { background = Round(6, new Color(ACC.r, ACC.g, ACC.b, 0.14f), Color.clear), textColor = Color.white } };
        // 기본 스크롤 막대·슬라이더도 같은 모양으로(얇은 둥근 막대, 강조색 손잡이)
        var sk = GUI.skin; var bar = new Texture2D(16, 12, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 12; y++) for (int x = 0; x < 16; x++) bar.SetPixel(x, y, y >= 5 && y <= 6 ? new Color(1, 1, 1, 0.16f) : Color.clear); bar.Apply();
        sk.horizontalSlider.normal.background = bar; sk.horizontalSlider.border = new RectOffset(3, 3, 0, 0); sk.horizontalSlider.fixedHeight = 12; sk.horizontalSlider.margin = new RectOffset(4, 4, 6, 6);
        var knob = Glyph(p => Circ(p, Vector2.zero, 0.8f)); var knobH = Glyph(p => Circ(p, Vector2.zero, 0.95f));
        foreach (var st in new[] { sk.horizontalSliderThumb.normal, sk.horizontalSliderThumb.hover, sk.horizontalSliderThumb.active }) st.background = st == sk.horizontalSliderThumb.normal ? knob : knobH;
        sk.horizontalSliderThumb.fixedWidth = 14; sk.horizontalSliderThumb.fixedHeight = 14; sk.horizontalSliderThumb.border = new RectOffset(0, 0, 0, 0);
        sk.verticalScrollbar.normal.background = Round(3, new Color(1, 1, 1, 0.03f), Color.clear); sk.verticalScrollbar.border = new RectOffset(4, 4, 4, 4); sk.verticalScrollbar.fixedWidth = 7; sk.verticalScrollbar.margin = new RectOffset(4, 0, 0, 0);
        foreach (var st in new[] { sk.verticalScrollbarThumb.normal, sk.verticalScrollbarThumb.hover, sk.verticalScrollbarThumb.active }) st.background = Round(3, new Color(1, 1, 1, 0.22f), Color.clear);
        sk.verticalScrollbarThumb.border = new RectOffset(4, 4, 4, 4); sk.verticalScrollbarThumb.fixedWidth = 7;
        sk.settings.cursorColor = ACC; sk.settings.selectionColor = new Color(ACC.r, ACC.g, ACC.b, 0.35f);
        sT = S(font, 11, TXT); sT.wordWrap = false; sT.clipping = TextClipping.Clip; sTd = new GUIStyle(sT) { normal = { textColor = new Color(0.5f, 0.56f, 0.62f) } };
    }
    static void Ico(Rect r, Texture2D t, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, t); GUI.color = o; }
    bool IconBtn(Rect r, Texture2D t, bool on, string tip = null)
    {
        bool hit = GUI.Button(r, GUIContent.none, on ? sOn : sIco); float p = Mathf.Round(r.height * 0.24f);
        Ico(new Rect(r.x + (r.width - (r.height - 2 * p)) / 2, r.y + p, r.height - 2 * p, r.height - 2 * p), t, on ? new Color(0.7f, 0.93f, 1) : TXT);
        if (tip != null && r.Contains(Event.current.mousePosition)) tipText = tip;
        return hit;
    }
    string tipText;
    // 접는 머리: ▸/▾ + 제목. 눌리면 open을 뒤집는다(다음 Layout에서)
    bool Fold(string title, bool open, Action toggle)
    {
        var r = GUILayoutUtility.GetRect(new GUIContent(title), sFold, GUILayout.ExpandWidth(true));
        if (GUI.Button(r, title, sFold)) Later(toggle);
        Ico(new Rect(r.x + 6, r.y + (r.height - 12) / 2, 12, 12), open ? iDown : iRight, DIM); return open;
    }

    // ---------- 전체 배치 ----------
    void OnGUI()
    {
        Styles();
        if (Event.current.type == EventType.Layout && later != null) { var a = later; later = null; a(); }
        float s = UIScale; GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1)); tipText = null;
        float W = Screen.width / s, Hh = Screen.height / s;
        top = new Rect(10, 10, W - 20, 42); panel = new Rect(W - 370, 246, 360, Hh - 256); info = new Rect(10, Hh - 58, 560, 48);
        TopBar(W);
        // 아래 왼쪽: 축척·수심 + 작은 단추
        GUI.Box(info, GUIContent.none, sBox);
        ScaleBar(new Rect(22, Hh - 50, 220, 30));
        GUI.Label(new Rect(250, Hh - 46, 160, 40), $"<size=11>시야 {Len(B)}\n수심 {py:0.00} m{(DEV ? $" · 농축 ×{Num(conc)}" : "")}</size>", sS);
        float bx = 410;
        if (IconBtn(new Rect(bx, Hh - 50, 34, 32), iFit, false, sel != null ? "고른 개체에 맞추기 (F)" : "가장 큰 생물에 맞추기 (F)")) Fit();
        if (IconBtn(new Rect(bx + 36, Hh - 50, 34, 32), iCard, showCard, "확대 카드 (M)")) showCard = !showCard;
        if (IconBtn(new Rect(bx + 72, Hh - 50, 34, 32), iTray, showTray, "표본 쟁반 (P)")) showTray = !showTray;
        if (IconBtn(new Rect(bx + 108, Hh - 50, 34, 32), iHelp, help, "조작법")) help = !help;
        if (help)
        {
            var hr = new Rect(10, Hh - 200, 560, 136); GUI.Box(hr, GUIContent.none, sBox);
            GUI.Label(new Rect(hr.x + 16, hr.y + 12, hr.width - 32, hr.height - 20),
                "<b>조작</b>\n끌기: 둘러보기 · WASD: 헤엄 · E/Q: 위·아래 · Shift: 빠르게 · 휠: 관찰 축척(μm ↔ m)\n클릭: 개체 고르기 · F: 맞추기 · Space: 멈춤 · 1–4: 속도\nG: 계통수 · T: 진화 경로 · M: 확대 카드 · P: 표본 쟁반 · Esc: 창 닫기/고르기 풀기", sL);
        }
        // 오른쪽 판
        GUI.Box(panel, GUIContent.none, sBox);
        for (int i = 0; i < TABS.Length; i++) { int k = i; if (GUI.Button(new Rect(panel.x + 12 + i * 84, panel.y + 8, 80, 32), TABS[i], tab == i ? sTabOn : sTab)) Later(() => { tab = k; scroll = Vector2.zero; }); }
        Fill(new Rect(panel.x + 12, panel.y + 40, panel.width - 24, 1), new Color(1, 1, 1, 0.07f));
        GUILayout.BeginArea(new Rect(panel.x + 12, panel.y + 48, panel.width - 20, panel.height - 56));
        scroll = GUILayout.BeginScrollView(scroll, GUIStyle.none, GUI.skin.verticalScrollbar);
        if (tab == 0) EnvTab(); else if (tab == 1) OrgTab(); else if (tab == 2) SpeciesTab(); else EventTab();
        GUILayout.EndScrollView(); GUILayout.EndArea();
        DepthGauge(Hh); MapBox(new Rect(W - 370, 58, 360, 180));
        if (showTray) Tray(W, Hh); else Card(Hh);
        if (win != 0) Window(W, Hh);
        if (win == 0) Cards(W);   // 큰 창을 연 동안엔 알림 카드가 창 제목을 덮지 않게 쉰다
        if (tipText != null) { var mp = Event.current.mousePosition; var c = new GUIContent(tipText); var sz = sS.CalcSize(c); var tr = new Rect(Mathf.Min(mp.x + 14, W - sz.x - 30), mp.y - 34, sz.x + 20, 26); GUI.Box(tr, GUIContent.none, sBox); GUI.Label(new Rect(tr.x + 10, tr.y + 5, sz.x + 4, 20), tipText, sS); }
    }

    static readonly string[] SPN = { "멈춤", "1일/초", "10일/초", "최대" };
    void TopBar(float W)
    {
        GUI.Box(top, GUIContent.none, sBox);
        int nsp = w.species.Values.Count(x => x.total > 0);
        GUI.Label(new Rect(24, 19, 120, 26), "진화 수조", sB);
        string Stat(string k, string v) => $"<color=#8fa3b8>{k}</color> <b>{v}</b>";
        GUI.Label(new Rect(120, 20, W - 760, 24), string.Join("     ", new[] { Stat("경과", w.Time(w.tick)), Stat("종", $"{nsp}"), Stat("개체", w.totalCount.ToString("0.0e0")), Stat("체제", $"{w.looksSeen.Count}"), Stat("육지", $"{w.geo.LandCells() * 100f / (Geo.G * Geo.G):0}%") })
            + (DEV ? $"     <color=#ffb070>DEV</color> <size=11>계산 {frameSim:0} ms · 계통 {w.live.Count}</size>" : ""), sL);
        float x = W - 380 - 236;
        if (GUI.Button(new Rect(x, 16, 112, 30), "     계통수", win == 1 ? sOn : sBtn)) Later(() => win = win == 1 ? 0 : 1);
        Ico(new Rect(x + 14, 23, 16, 16), iTree, win == 1 ? ACC : TXT);
        if (GUI.Button(new Rect(x + 118, 16, 112, 30), "     진화 경로", win == 2 ? sOn : sBtn)) Later(() => win = win == 2 ? 0 : 2);
        Ico(new Rect(x + 132, 23, 16, 16), iPath, win == 2 ? ACC : TXT);
        // 속도: 아이콘 마디 단추
        var sp = new Rect(W - 372, 15, 352, 32); GUI.Box(sp, GUIContent.none, sCard);
        var ic = new[] { iPause, iPlay, iFast, iMax };
        for (int i = 0; i < 4; i++)
        {
            var r = new Rect(sp.x + 3 + i * 87, sp.y + 2, 85, 28); bool on = speed == i;
            if (GUI.Button(r, GUIContent.none, on ? sOn : sIco)) speed = i;
            Ico(new Rect(r.x + 10, r.y + 7, 14, 14), ic[i], on ? ACC : TXT); GUI.Label(new Rect(r.x + 28, r.y + 5, 56, 20), $"<size=12>{SPN[i]}</size>", on ? sL : sS);
        }
    }

    // ---------- 큰 창 ----------
    void Window(float W, float Hh)
    {
        Fill(new Rect(0, 0, W, Hh), new Color(0, 0, 0, 0.45f));
        var R = new Rect(40, 62, W - 80, Hh - 100); GUI.Box(R, GUIContent.none, sBox);
        GUI.Label(new Rect(R.x + 22, R.y + 12, R.width - 100, 24), win == 1 ? "계통수" : "진화 경로", sB);   // 제목 한 줄 + 설명 한 줄(길어도 아래 내용과 겹치지 않게)
        GUI.Label(new Rect(R.x + 22, R.y + 36, R.width - 100, 18), win == 1 ? "가로 = 시간 · 막대 = 종(굵은 색 = 현존, † = 절멸) · 세로선 = 분기 · 흰 테두리 = 추적 중인 계통"
            : "추적 중인 계통의 공통 조상 → 현재. 조상 계통마다 형태·형질 변화", new GUIStyle(sTd) { fontSize = 12, wordWrap = false, clipping = TextClipping.Clip });
        if (IconBtn(new Rect(R.xMax - 50, R.y + 10, 36, 34), iClose, false, "닫기 (Esc)")) Later(() => win = 0);
        var inner = new Rect(R.x + 18, R.y + 64, R.width - 36, R.height - 78);
        if (win == 1)
        {
            GUILayout.BeginArea(inner); winScroll = GUILayout.BeginScrollView(winScroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            Tree(true); GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        else PathWin(inner);
    }

    // ---------- 환경 탭 ----------
    static readonly (string name, float[] env)[] PRESETS = {
        ("원시 바다", new[] { 25f, 35, 60, 30, 2, 50 }), ("어류의 바다(추천)", new[] { 18f, 35, 70, 20, 21, 40 }), ("민물 연못", new[] { 20f, 0.5f, 70, 30, 21, 40 }),
        ("고염 석호", new[] { 28f, 150, 80, 50, 21, 40 }), ("차가운 바다", new[] { 6f, 35, 50, 20, 21, 50 }), ("빛 없는 심해", new[] { 4f, 35, 0, 0, 10, 30 }),
    };
    int qSel, qSize = 2; float qAmp = 2, qRad = 2, qDays = 100; bool placing;
    static readonly string[] QBTN = { "화산", "융기·침강", "온난화·냉각", "빙하기", "운석" };
    static bool Located(int k) => k == (int)Qk.Volcano || k == (int)Qk.Impact || k == (int)Qk.Uplift;
    void FireQ(int cell)
    {
        var k = (Qk)qSel; float amp = k == Qk.IceAge ? World.IceAmp(qSize) : qAmp; SimFinish();
        var q = w.Fire(k, qSize, cell, amp, qRad, k == Qk.Volcano ? 60 : qDays); placing = false;
        toast = "<b>" + q.text + "</b>"; toastT = 6;
    }
    // 값 칸: 이름(작게) 위, 값(크게) 아래
    void Stats(params (string k, string v)[] kv)
    {
        GUILayout.BeginHorizontal();
        foreach (var (k, v) in kv) GUILayout.Label($"<size=11><color=#8fa3b8>{k}</color></size>\n<b>{v}</b>", sL, GUILayout.Width(102));
        GUILayout.EndHorizontal();
    }
    void EnvTab()
    {
        float yrs = w.tick * w.P.DT / 24f * World.GEO_X;
        GUILayout.BeginVertical(sCard);
        GUILayout.Label("지구 상태", sH);
        Stats(("표층 수온(적도)", $"{w.env[Ev.Temp] + 3:0.0} °C"), ("CO₂", $"{w.co2:0} ppm"), ("대기 O₂", $"{w.env[Ev.O2]:0.0} %"));
        Stats(("해수면", $"{w.geo.sea:+0;-0;0} m"), ("육지 면적", $"{w.geo.LandCells() * 100f / (Geo.G * Geo.G):0} %"), ("빙상", $"{w.ice * 100:0} %"));
        if (w.aer > 0.05f || w.dark > 0.02f) GUILayout.Label($"<color=#ffb070>화산재 냉각 −{w.aer:0.0} °C · 충돌 먼지 차광 {w.dark * 100:0} %</color>", sS);
        GUILayout.Label($"<size=11>지질 시간 압축: 생태 1일 = 지질 1000년 (누적 {(yrs >= 1e6 ? $"{yrs / 1e6:0.0}백만" : $"{yrs / 1e4:0.#}만")} 년). 섭식·진화는 실시간, 탄소 순환·빙상·조산 운동은 압축.</size>", sS);
        GUILayout.EndVertical();

        GUILayout.BeginVertical(sCard);
        GUILayout.Label("지질 사건 일으키기", sH);
        GUILayout.BeginHorizontal();
        for (int k = 0; k < QBTN.Length; k++)
        {
            if (k == 3) { GUILayout.FlexibleSpace(); GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }   // 패널이 좁아 두 줄로
            int kk = k; if (GUILayout.Button(QBTN[k], qSel == k ? sChipOn : sChip)) { qSel = kk; placing = false; }
        }
        GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
        var kind = (Qk)qSel;
        if (kind == Qk.Volcano || kind == Qk.Impact || kind == Qk.IceAge)
        {
            string note = kind == Qk.Volcano ? World.VolNote[qSize] : kind == Qk.Impact ? World.ImpNote[qSize] : $"−{World.IceAmp(qSize):0} °C";
            GUILayout.Label($"규모 <b>{World.SizeName[qSize]}</b> — {note}", sS);
            qSize = Mathf.RoundToInt(GUILayout.HorizontalSlider(qSize, 0, 4));
        }
        if (kind == Qk.Uplift)
        {
            GUILayout.Label($"{(qAmp >= 0 ? "융기" : "침강")} {Mathf.Abs(qAmp):0.0} mm/년 · 반경 {qRad * 200:0} km", sS);
            qAmp = Mathf.Round(GUILayout.HorizontalSlider(qAmp, -5, 5) * 2) / 2; qRad = Mathf.Round(GUILayout.HorizontalSlider(qRad, 1, 6));
        }
        if (kind == Qk.Warm) { GUILayout.Label($"{(qAmp >= 0 ? "온난화 +" : "냉각 −")}{Mathf.Abs(qAmp):0.0} °C", sS); qAmp = Mathf.Round(GUILayout.HorizontalSlider(qAmp, -8, 8) * 2) / 2; }
        if (kind == Qk.Uplift || kind == Qk.Warm || kind == Qk.IceAge)
        {
            GUILayout.Label($"기간 {qDays:0}일 (지질 {qDays * World.GEO_X / 1e4:0.#}만 년)", sS);
            qDays = Mathf.Round(GUILayout.HorizontalSlider(qDays, 10, 600) / 10) * 10;
        }
        GUILayout.Space(4);
        if (Located(qSel)) { if (GUILayout.Button(placing ? "지도에서 위치를 누르세요 (다시 누르면 취소)" : "위치 고르기 → 지도를 누르면 발생", placing ? sOn : sBtn)) placing = !placing; }
        else if (GUILayout.Button("일으키기", sBtn)) FireQ(-1);
        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(w.autoQ ? "자연 발생 켜짐" : "자연 발생 꺼짐", w.autoQ ? sChipOn : sChip, GUILayout.Width(118))) w.autoQ = !w.autoQ;
        GUILayout.Label($"<size=11>{(w.activity < 0.34f ? "조용한 행성" : w.activity < 0.67f ? "보통 행성" : "격동하는 행성")} · 소규모 분화 약 {1 / (0.1 * Math.Pow(10, 2 * w.activity - 1.5)):0}일에 한 번</size>", sS);
        GUILayout.EndHorizontal();
        w.activity = GUILayout.HorizontalSlider(w.activity, 0, 1);
        double day = w.tick * w.P.DT / 24.0;
        foreach (var q in w.quakes) { double left = q.Day0 + q.days - day; if ((q.kind != Qk.Volcano || q.size == 4) && q.kind != Qk.Impact && left > 0 && day >= q.Day0) GUILayout.Label($"<color=#ffb070>진행 중</color> {q.text} — {left:0}일 남음", sS); }
        GUILayout.EndVertical();

        if (Fold("생지화학 순환 (이 해역)", showCycle, () => showCycle = !showCycle))
        {
            GUILayout.BeginVertical(sCard);
            Stats(("남세균", $"{Num(Mix(w.cya))}/mL"), ("종속영양 세균", $"{Num(Mix(w.bac))}/mL"), ("무기 영양염", $"{Mix(w.nut):0.00} μM"));
            Stats(("POM(해설)", $"{Mix(w.pom):0.00} μM N"), ("DOM", $"{Mix(w.dom):0.00} μM N"), ("용존 산소", $"{Mix(w.dox):0.0} % atm"));
            GUILayout.Label($"<size=11>퇴적물 매몰 유기물 누계 {Num(w.buried)} · 미생물 고리: 광합성 삼출·사체 → DOM → 세균 → 원생생물</size>", sS);
            GUILayout.EndVertical();
        }
        if (Fold("세계 설정", showWorld, () => showWorld = !showWorld))
        {
            GUILayout.BeginVertical(sCard);
            GUILayout.Label("환경 목표값 프리셋(바다는 정한 속도로 따라간다)", sS);
            for (int i = 0; i < PRESETS.Length; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + 2, PRESETS.Length); j++) if (GUILayout.Button(PRESETS[j].name, sBtn)) for (int e = 0; e < Ev.N; e++) w.SetTarget(e, PRESETS[j].env[e]);
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(4);
            if (GUILayout.Button("새 행성에서 다시 시작 (원시 바다 · 공통 조상)", sBtn)) Later(() => { SimFinish(); w.Reset(w.seed + 1, (float[])w.target.Clone()); Bodies.Clear(); avs.Clear(); sel = null; fit = true; reg = -1; obsCell = -1; fold.Clear(); });
            GUILayout.EndVertical();
        }
        if (DEV && Fold("개발: 수치 조정", showAdv, () => showAdv = !showAdv)) { GUILayout.BeginVertical(sCard); Advanced(); GUILayout.EndVertical(); }
        if (DEV) GUILayout.Label($"<size=11>해역 {w.NR} · 칸 {Enumerable.Range(0, World.V).Count(v => w.vol[v] > 0)} · 계통 {w.live.Count} · 메시 {Bodies.Count} · {frameSim:0} ms/프레임 · 돌연변이 정착 {w.mutKeep}/{w.mutTry} · 수평 이동 {w.hgtKeep}/{w.hgtTry}</size>", sS);
    }

    void Advanced()
    {
        GUILayout.Label("슬라이더 = 목표값. '수온'은 사건 없는 기후의 바탕(CO₂·빙상·사건 몫은 지구가 더한다).", sS);
        for (int e = 0; e < Ev.N; e++)
        {
            var d = Ev.Def[e];
            if (e == Ev.O2)
            {
                GUILayout.BeginHorizontal(); GUILayout.Label($"{d.name}  <b>{w.env[e]:0.#}{d.unit}</b>", sL);
                if (GUILayout.Button(w.o2Bio ? "생물이 정함" : "숫자로 고정", w.o2Bio ? sChipOn : sChip, GUILayout.Width(100))) { w.o2Bio = !w.o2Bio; w.target[e] = w.env[e]; }
                GUILayout.EndHorizontal();
                if (w.o2Bio) { GUILayout.Label("<size=11>유기물 매몰(광합성의 남은 몫)만큼 천천히 오르내린다</size>", sS); continue; }
            }
            else GUILayout.Label($"{d.name}  <b>{(e == Ev.Temp ? w.tBase : w.env[e]):0.#}{d.unit}</b>  → 목표 {w.target[e]:0.#}{d.unit}", sL);
            float t = GUILayout.HorizontalSlider(w.target[e], d.lo, d.hi); if (t != w.target[e]) w.SetTarget(e, t);
        }
        GUILayout.Label($"CO₂  <b>{w.co2:0} ppm</b> (기준 1000, 2배마다 +3 °C)", sL);
        float lc = GUILayout.HorizontalSlider(Mathf.Log10((float)w.co2), 2, 4); if (Mathf.Abs(lc - Mathf.Log10((float)w.co2)) > 1e-3f) w.co2 = Math.Pow(10, lc);
        GUILayout.Space(4); GUILayout.Label("변화 속도", sL);
        GUILayout.BeginHorizontal();
        foreach (var (n, r) in new[] { ("느리게", 0.0001f), ("보통", 0.0005f), ("빠르게", 0.003f), ("즉시", 1f) })
            if (GUILayout.Button(n, rateSel == r ? sChipOn : sChip)) { rateSel = r; w.rate = r; }
        GUILayout.EndHorizontal();
        GUILayout.Space(4); GUILayout.Label($"돌연변이율 ×{mutX:0.00}", sL);
        float mx = Mathf.Pow(2, GUILayout.HorizontalSlider(Mathf.Log(mutX, 2), -2, 2));
        if (Mathf.Abs(mx - mutX) > 1e-3f) { mutX = mx; var d0 = new Params(); w.P.MUT_NEW = d0.MUT_NEW * mx; w.P.MUT_U = d0.MUT_U * mx; w.P.MUT_FLOOR = d0.MUT_FLOOR * mx; }
        GUILayout.Space(4); GUILayout.Label($"<b>이 해역만</b> ({w.geo.Name(reg)})  수온 {w.rdT[reg]:+0.0;-0.0;0} °C · 영양염 ×{w.rnM[reg]:0.0}", sL);
        float rT = Mathf.Round(GUILayout.HorizontalSlider(w.rdT[reg], -10, 10) * 2) / 2, rN = Mathf.Round(GUILayout.HorizontalSlider(w.rnM[reg], 0, 5) * 10) / 10;
        if (rT != w.rdT[reg] || rN != w.rnM[reg]) w.SetRegionTweak(reg, rT, rN);
    }

    // ---------- 개체 탭 ----------
    void OrgTab()
    {
        var st = sel != null ? w.strains[sel.slot] : null;   // OnGUI 안에서는 상태를 바꾸지 않는다(Layout·Repaint 칸 수가 어긋남)
        if (st == null || st.id != sel.id) { GUILayout.Label("화면의 개체를 누르면 그 개체의 분류·에너지 수지·형질이 나온다.", sS); return; }
        int v = V, q = World.Ix(sel.slot, v, sel.k); var m = w.M(sel.slot, v, sel.k); var sp = w.species.TryGetValue(st.sp, out var x) ? x : null;
        GUILayout.BeginVertical(sCard);
        GUILayout.Label($"<b>{sp?.name}</b>", sB); GUILayout.Label(sp?.ko ?? "", sS);
        string stage = !st.dev ? "" : sel.k == 0 ? " · 알·유생" : sel.k == st.K - 1 ? " · 성체" : $" · 유체 {sel.k}/{st.K - 1}";
        GUILayout.Label($"{Taxa.Name(st)}{stage}", sL);
        Stats(("체장", Len(m.L * 1e-6f)), ("세포 수", Num(m.C)), ("유영 속도", $"{Len(m.speed * 1e-6f)}/s"));
        Stats(("밀도", $"{Num(w.cnt[q] / w.vol[v])}/mL"), ("임계 산소", $"{m.pcrit:0.0} %"), ("세대", $"{st.gen:0}"));
        if (follow >= 0) GUILayout.Label("<color=#7fe0ff>계통 추적 중</color> — 화면의 개체는 이 계통의 대표 표본(시뮬레이션은 개체군을 계산). 계통이 사라지면 가장 큰 후손 계통으로 넘어간다.", sS);
        GUILayout.EndVertical();
        w.Budget(st, v, ref m, w.gP[q], w.gB[q], bud);
        if (Fold("에너지 수지 (시간당 유입 / 유출)", showBud, () => showBud = !showBud))
        {
            GUILayout.BeginVertical(sCard);
            string[] nm = { "삼투영양(DOM)", "광합성", "세균 포식", "식세포 포식", "기초 대사", "형질 유지비", "구조 유지비", "삼투 조절", "삼투 스트레스", "UV 손상", "산소 스트레스", "열 스트레스" };
            double big = 1e-12; for (int i = 0; i < 12; i++) big = Math.Max(big, Math.Abs(bud[i]));
            for (int i = 0; i < 12; i++)
            {
                if (Math.Abs(bud[i]) < big * 0.01) continue;
                var r = GUILayoutUtility.GetRect(280, 18); GUI.Label(new Rect(r.x, r.y, 110, 18), nm[i], sS);
                Fill(new Rect(r.x + 112, r.y + 5, r.width - 120, 8), new Color(1, 1, 1, 0.05f));
                Fill(new Rect(r.x + 112, r.y + 5, (float)(Math.Abs(bud[i]) / big) * (r.width - 120), 8), i < 4 ? new Color(0.4f, 0.9f, 0.6f) : new Color(1f, 0.5f, 0.45f));
            }
            GUILayout.Label($"순 성장률 <b>{(bud[13] >= 0 ? "+" : "")}{bud[13] / Math.Max(1e-12, m.C * (m.esd * m.esd * m.esd * 1e-3 + w.P.FIXB)) * 2400:0.0} %/일</b>", sL);
            GUILayout.EndVertical();
        }
        GUILayout.BeginVertical(sCard);
        GUILayout.Label("진단 형질", sH);
        GUILayout.Label(string.Join(" · ", Names.Describe(st.g).Take(6).Select(d => d.ko)), sL);
        GUILayout.EndVertical();
        if (DEV)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Gn.N; i++) { var d = Gn.Def[i]; if (Mathf.Abs(st.g[i] - d.init) > 0.05f * (d.hi - d.lo)) sb.Append($"{d.name} {st.g[i]:0.##}  "); }
            GUILayout.Label("<b>유전자 (개발)</b>\n" + sb, sS);
        }
    }

    // ---------- 종 탭: 계통 순서로 고정, 갈래마다 접기 ----------
    void SpeciesTab()
    {
        var ch = new Dictionary<int, List<Species>>();
        foreach (var s in w.species.Values) { int a = s.parent != s.id && w.species.ContainsKey(s.parent) ? s.parent : 0; if (!ch.TryGetValue(a, out var l)) ch[a] = l = new(); l.Add(s); }
        foreach (var l in ch.Values) l.Sort((p, q) => p.id.CompareTo(q.id));   // 태어난 차례: 수가 바뀌어도 자리가 그대로
        var liveSub = new Dictionary<int, int>();                              // 종 id → 그 갈래의 현존 종 수
        int Mark(Species s) { int n = s.total > 0 ? 1 : 0; if (ch.TryGetValue(s.id, out var l)) foreach (var c in l) n += Mark(c); if (n > 0) liveSub[s.id] = n; return n; }
        int nLive = 0; if (ch.TryGetValue(0, out var roots)) foreach (var r in roots) nLive += Mark(r);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"현존 종 <b>{nLive}</b> <size=11>· 계통 순 (분기 순서)</size>", sL);
        if (GUILayout.Button("모두 펴기", sChip)) Later(() => fold.Clear());
        if (GUILayout.Button("모두 접기", sChip)) Later(() => { foreach (var kv in ch) if (kv.Key != 0) fold.Add(kv.Key); });
        GUILayout.EndHorizontal();
        GUILayout.Label("<size=11>▸ 눌러 갈래를 접고 편다. 이름을 누르면 그 종의 가장 큰 계통을 추적한다. 회색 † = 절멸한 조상 종.</size>", sS);
        int psp = FollowSt?.sp ?? -1;
        void Row(Species s, int depth)
        {
            if (!liveSub.TryGetValue(s.id, out int nSub)) return;
            var kids = ch.TryGetValue(s.id, out var l) ? l.Where(c => liveSub.ContainsKey(c.id)).ToList() : new List<Species>();
            bool alive = s.total > 0, open = !fold.Contains(s.id);
            var r = GUILayoutUtility.GetRect(200, 34, GUILayout.ExpandWidth(true)); float ind = Mathf.Min(depth, 9) * 11;
            if (kids.Count > 0)
            {
                var tr = new Rect(r.x + ind, r.y + 8, 18, 18); int id = s.id;
                if (GUI.Button(tr, GUIContent.none, sIco)) Later(() => { if (!fold.Remove(id)) fold.Add(id); });
                Ico(new Rect(tr.x + 2, tr.y + 2, 14, 14), open ? iDown : iRight, new Color(TXT.r, TXT.g, TXT.b, 0.85f));
            }
            var br = new Rect(r.x + ind + 20, r.y, r.width - ind - 20, r.height);
            var c = alive ? Color.HSVToRGB(Mathf.Repeat(s.hue / 360, 1), 0.6f, 1) : new Color(0.42f, 0.47f, 0.52f);
            string right = alive ? $"{s.total:0.0e0}" : "";
            string fold2 = !open && kids.Count > 0 ? $" <color=#8fa3b8>+{nSub - (alive ? 1 : 0)}종</color>" : "";
            string txt = $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>●</color> {(alive ? "<b>" + s.name + "</b>" : "<color=#8fa3b8>" + s.name + " †</color>")}{fold2}\n<size=11><color=#8fa3b8>{(alive ? Taxa.Name(s.look) + " · " + s.ko : "조상 종")}</color></size>";
            if (GUI.Button(br, txt, s.id == psp ? sRowOn : sRow) && alive) GoSpecies(s);
            if (alive) GUI.Label(new Rect(br.xMax - 70, br.y + 3, 64, 16), $"<size=11>{right}</size>", new GUIStyle(sS) { alignment = TextAnchor.UpperRight });
            if (open) foreach (var k in kids) Row(k, depth + 1);
        }
        if (roots != null) foreach (var r in roots) Row(r, 0);
    }

    // ---------- 기록 탭 ----------
    static readonly string[] EVF = { "전체", "지질", "종 분화", "절멸", "주요 사건" };
    void EventTab()
    {
        GUILayout.BeginHorizontal();
        for (int i = 0; i < EVF.Length; i++) { int k = i; if (GUILayout.Button(EVF[i], evFilter == i ? sChipOn : sChip)) Later(() => evFilter = k); }
        GUILayout.EndHorizontal();
        bool Pass(string t) => evFilter switch { 1 => t == "earth", 2 => t == "sp", 3 => t == "ext", 4 => t == "grade" || t == "scale" || t == "start", _ => true };
        int n = 0;
        for (int i = w.events.Count - 1; i >= 0 && n < 80; i--)
        {
            var e = w.events[i]; if (!Pass(e.type)) continue; n++;
            bool big = e.type == "grade" || e.type == "scale";
            string body = e.type == "earth" ? "<color=#ffb070>" + e.text + "</color>" : e.type == "ext" ? "<color=#a8b4c0>" + e.text + "</color>" : big ? "<b>" + e.text + "</b>" : e.text;
            GUILayout.Label($"<size=11><color=#8fa3b8>{w.Time(e.t)}</color></size>\n{body}", sL);
        }
        if (n == 0) GUILayout.Label("아직 해당 기록이 없다.", sS);
    }
}
