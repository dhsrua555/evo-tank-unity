// 계통수 · 혈통 따라가기 · 혈통 타임라인 · 몸 초상(RenderTexture). EvoTank의 나머지 반쪽.
// 혈통 = 고른 계통 + 그 후손 전체. 시뮬레이션에는 개체가 없으므로 화면 개체는 혈통의 '대표 표본'이다.
// 따라가는 계통이 사라지면(멸종·합쳐짐·정리) 가장 많은 후손으로, 후손이 그 계통보다 2배 넘게 많아져도 그 후손으로 넘어간다.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Evo;

public partial class EvoTank
{
    int follow = -1;                  // 따라가는 계통 id
    Rec gone;                         // 끊긴 혈통(카드)
    string toast; float toastT;
    Vector3? birthAt;                 // 후손으로 넘어갈 때 새 대표가 나타날 자리
    float divAcc, divLast = -99; bool divTold;
    readonly Dictionary<int, int> idSlot = new();
    readonly Dictionary<int, List<int>> kids = new();

    void Follow(int id) { follow = id; w.pin = id; gone = null; scrub = -1; }
    void Unfollow() { follow = -1; w.pin = gone != null ? gone.id : -1; }
    Strain FollowSt => follow >= 0 && idSlot.TryGetValue(follow, out int s) && w.strains[s]?.id == follow ? w.strains[s] : null;

    void Index()
    {
        idSlot.Clear(); foreach (int s in w.live) idSlot[w.strains[s].id] = s;
        kids.Clear(); foreach (var r in w.recs.Values) { if (!kids.TryGetValue(r.parent, out var l)) kids[r.parent] = l = new(); l.Add(r.id); }
    }

    // id의 후손(자신 제외) 가운데 살아 있고 가장 많은 계통
    int BestDesc(int id)
    {
        int best = -1; double bt = 0; var st = new Stack<int>(); st.Push(id);
        while (st.Count > 0)
        {
            if (!kids.TryGetValue(st.Pop(), out var l)) continue;
            foreach (int c in l) { if (idSlot.TryGetValue(c, out int s) && w.strains[s].total > bt) { bt = w.strains[s].total; best = c; } st.Push(c); }
        }
        return best;
    }

    // Scan(1초)마다: 따라가는 계통이 사라졌으면 이어 갈 계통을 찾고, 화면 대표를 맞춘다
    void FollowStep()
    {
        Index();
        if (follow < 0) return;
        int t = follow; string why = null;
        for (int i = 0; i < 64 && t >= 0 && !idSlot.ContainsKey(t); i++)
        {
            if (!w.recs.TryGetValue(t, out var r)) { t = -1; break; }
            if (r.why == 2 && r.into >= 0) { t = r.into; why = "가까운 계통에 합쳐짐"; continue; }
            why = (r.why == 3 ? "계통 수 상한으로 정리됨" : r.eaten ? "잡아먹혀 사라짐" : "굶주려 사라짐") + " → 가장 많은 후손으로";
            t = BestDesc(t);
        }
        if (t < 0) { gone = w.recs.TryGetValue(follow, out var g) ? g : null; follow = -1; w.pin = gone?.id ?? -1; sel = null; return; }
        if (why == null) { int d = BestDesc(t); if (d >= 0 && w.strains[idSlot[d]].total > 2 * w.strains[idSlot[t]].total) { t = d; why = "후손이 더 많아짐"; } }
        if (t != follow) Switch(follow, t, why);
        int slot = idSlot[t]; var st = w.strains[slot];
        if (sel != null && sel.slot == slot && sel.id == st.id) return;
        int bk = -1; double bc = 0; for (int k = 0; k < st.K; k++) { double c = Dens(slot, k); if (c > bc) { bc = c; bk = k; } }
        if (bk < 0) { int ak = 0; for (int k = 1; k < st.K; k++) if (st.stage[k] > st.stage[ak]) ak = k; Focus(slot, ak, false); return; }   // 이 칸에 없으면 그 계통이 많은 곳으로
        float L = w.M(slot, V, bk).L * 1e-6f; if (L < B * 0.006f || L > B * 0.5f) Bt = Mathf.Clamp(L * SELZ, 2e-5f, 3);
        want = (slot, bk); tRes = 0;
    }

    void Switch(int old, int nu, string why)
    {
        var st = w.strains[idSlot[nu]]; string diff = w.recs.TryGetValue(old, out var ro) ? Diff(ro.g, st.g) : "";
        toast = $"<b>혈통이 이어집니다</b> — {why}\n{SpName(st.sp)}{(diff.Length > 0 ? " · " + diff : "")}"; toastT = 7;
        w.Log("line", $"혈통: {SpName(st.sp)}(계통 {nu})로 이어짐 — {why}{(diff.Length > 0 ? " · " + diff : "")}");
        Debug.Log($"[EvoTank] follow {old}->{nu} {why}");
        if (sel != null) birthAt = sel.p;
        follow = nu; w.pin = nu; sel = null; scrub = -1;
    }

    string SpName(int sp) => w.species.TryGetValue(sp, out var s) ? Names.Of(s) : "계통";

    // 두 유전자의 차이: 새로 생긴·잃은 형질 + 가장 많이 바뀐 유전자 셋(범위 대비 %)
    static string Diff(float[] a, float[] b)
    {
        var na = Names.Describe(a).Select(x => x.ko).ToHashSet(); var nb = Names.Describe(b).Select(x => x.ko).ToList();
        var l = nb.Where(x => !na.Contains(x)).Take(2).Select(x => "새 형질 " + x).Concat(na.Where(x => !nb.Contains(x)).Take(1).Select(x => "잃음 " + x)).ToList();
        var ch = new List<(float d, string s)>();
        for (int i = 0; i < Gn.N; i++) { var d = Gn.Def[i]; float x = (b[i] - a[i]) / (d.hi - d.lo); if (Mathf.Abs(x) > 0.01f) ch.Add((Mathf.Abs(x), $"{d.name} {(x > 0 ? "+" : "−")}{Mathf.Abs(x) * 100:0}%")); }
        l.AddRange(ch.OrderByDescending(x => x.d).Take(3).Select(x => x.s));
        return string.Join(" · ", l);
    }

    static string Cause(Rec r) => r.why == 2 ? "가까운 계통에 합쳐짐" : r.why == 3 ? "다른 계통에 밀려 정리됨(계통 수 상한)" : r.eaten ? "잡아먹힘" : "굶주림·환경(성장률이 음수)";

    // 끊긴 혈통에서 가장 가까운 살아 있는 친척: 조상을 하나씩 올라가며 가장 많은 후손
    void Relative()
    {
        Index(); int a = gone.parent;
        while (a != 0 && w.recs.TryGetValue(a, out var r)) { int d = BestDesc(a); if (d >= 0) { Follow(d); toast = "<b>가장 가까운 친척을 따라갑니다</b>"; toastT = 4; return; } a = r.parent; }
        toast = "살아 있는 친척이 없습니다."; toastT = 4;
    }

    // 고른 대표가 분열하는 모습(단세포): 실제 성장률로 분열 횟수를 쌓고, 5초에 한 번까지만 보여 준다.
    // 몸이 길어지며 가운데가 1.5초 동안 잘록해진 뒤(셰이더 _Pinch) 긴 축을 따라 둘로 나뉜다(가로 이분열)
    void Divide(float dt)
    {
        if (sel == null || follow < 0 || speed == 0) return;
        var st = w.strains[sel.slot]; if (st == null || st.dev) return;
        if (sel.pinch >= 1)
        {
            var side = sel.rot * Vector3.forward * sel.L * (BOX / B) * 0.36f;
            var sib = new Av { id = sel.id, slot = sel.slot, k = sel.k, look = sel.look, L = sel.L, spd = sel.spd, sink = sel.sink, sessile = sel.sessile, t0 = sel.t0 + 1.7f, rate = UnityEngine.Random.Range(0.8f, 1.25f), vs = sel.vs, ph = sel.ph + 3, p = sel.p - side, dir = sel.dir, rot = sel.rot, grow = 0.55f };   // 크기 0.3+0.7·smooth(0.55) ≈ 0.7: 잘록해진 반쪽과 비슷
            sel.p += side; sel.grow = 0.55f; sel.pinch = -1; avs.Add(sib);
            if (!divTold) { divTold = true; toast = "<b>분열</b> — 앞쪽 딸세포를 계속 따라갑니다"; toastT = 4; }
            return;
        }
        if (sel.pinch >= 0) return;
        float mu = w.mu[World.Ix(sel.slot, V, sel.k)]; if (mu <= 0) return;
        divAcc = Mathf.Min(1, divAcc + dt * TPS[speed] * w.P.DT * mu / 0.693f);
        if (divAcc < 1 || Time.time - divLast < 5) return;
        divAcc = 0; divLast = Time.time; sel.pinch = 0;
    }

    // ---------- 계통수(옛 웹판처럼: 가로 = 시간, 막대 = 종, 세로선 = 조상에서 갈라짐) ----------
    bool treeAll = true;
    static void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = o; }

    void Tree()
    {
        var ch = new Dictionary<int, List<Species>>();
        foreach (var s in w.species.Values) { int a = s.parent != s.id && w.species.ContainsKey(s.parent) ? s.parent : 0; if (!ch.TryGetValue(a, out var l)) ch[a] = l = new(); l.Add(s); }
        var liveSub = new HashSet<int>();
        bool Mark(Species s) { bool any = s.total > 0; if (ch.TryGetValue(s.id, out var l)) foreach (var c in l) any |= Mark(c); if (any) liveSub.Add(s.id); return any; }
        if (ch.TryGetValue(0, out var roots)) foreach (var r in roots) Mark(r);
        var order = new List<Species>();
        void Walk(int id, int d) { if (d > 400 || !ch.TryGetValue(id, out var l)) return; foreach (var s in l.OrderBy(x => x.born)) { if (!treeAll && !liveSub.Contains(s.id)) continue; order.Add(s); Walk(s.id, d + 1); } }
        Walk(0, 0);
        var path = new HashSet<int>(); int psp = FollowSt?.sp ?? gone?.sp ?? -1;
        for (int i = 0; i < 1000 && w.species.TryGetValue(psp, out var ps); i++) { path.Add(ps.id); if (ps.parent == ps.id) break; psp = ps.parent; }

        GUILayout.BeginHorizontal();
        GUILayout.Label("<b>계통수</b>  <size=11>굵은 색 = 살아 있음 · † = 멸종 · 흰 테두리 = 따라가는 혈통</size>", sL);
        if (GUILayout.Button(treeAll ? "멸종 가지 숨기기" : "멸종 가지 보기", sBtn, GUILayout.Width(112))) treeAll = !treeAll;
        GUILayout.EndHorizontal();
        const float row = 16;
        var R = GUILayoutUtility.GetRect(300, order.Count * row + 38);   // 위 14 px = 사건 이름 줄
        float now = Mathf.Max(1, w.tick), lw = Mathf.Min(140, R.width * 0.45f), iw = R.width - lw - 14;
        float X(long t) => R.x + 4 + t / now * iw;
        var Y = new Dictionary<int, float>(); for (int i = 0; i < order.Count; i++) Y[order[i].id] = R.y + 22 + i * row;
        var lineC = new Color(0.45f, 0.55f, 0.65f, 0.6f);
        var qs = new GUIStyle(sTd) { fontSize = 10 }; float lastX = -99;
        foreach (var q in w.quakes)   // 지구 사건: 세로 줄 — 그 뒤에 어떤 갈래가 생기고 사라졌는지
        {
            if (q.kind == Qk.Volcano && q.size < 2 || q.kind == Qk.Impact && q.size < 1) continue;   // 작은 것은 생략
            float x = X(q.tick); var col = q.kind == Qk.Volcano ? new Color(1, 0.3f, 0.15f, 0.55f) : q.kind == Qk.Impact ? new Color(0.8f, 0.45f, 1, 0.6f) : q.kind == Qk.IceAge ? new Color(0.7f, 0.9f, 1, 0.5f) : new Color(1, 0.65f, 0.25f, 0.45f);
            if (q.kind == Qk.IceAge || q.kind == Qk.Warm || q.kind == Qk.Uplift) Fill(new Rect(x, R.y, Mathf.Max(1, X(q.tick + (long)(q.days * 12)) - x), R.height), col * new Color(1, 1, 1, 0.25f));
            Fill(new Rect(x, R.y, 1, R.height), col);
            if (x - lastX < 30) continue; lastX = x;   // 몰려 있으면 글은 첫 것만
            GUI.Label(new Rect(x + 2, R.y, 40, 14), q.kind switch { Qk.Volcano => "화산", Qk.Impact => "운석", Qk.IceAge => "빙기", Qk.Warm => q.amp >= 0 ? "온난" : "냉각", _ => q.amp >= 0 ? "융기" : "침강" }, qs);
        }
        foreach (var s in order)
        {
            float y = Y[s.id]; bool alive = s.total > 0, on = path.Contains(s.id); long end = alive ? w.tick : s.ext;
            if (Y.TryGetValue(s.parent, out float py) && s.parent != s.id) Fill(new Rect(X(s.born), Mathf.Min(py, y), on ? 2 : 1, Mathf.Abs(y - py)), on ? Color.white : lineC);
            var c = alive ? Color.HSVToRGB(Mathf.Repeat(s.hue / 360, 1), 0.6f, 1) : new Color(0.42f, 0.47f, 0.52f);
            float th = alive ? 4 : 1.5f, x0 = X(s.born), wd = Mathf.Max(2, X(end) - x0);
            if (on) Fill(new Rect(x0 - 1.5f, y - th / 2 - 1.5f, wd + 3, th + 3), Color.white);
            Fill(new Rect(x0, y - th / 2, wd, th), c);
            if (GUI.Button(new Rect(R.x + iw + 12, y - 8, lw, 16), (on ? "<b>" : "") + s.name + (alive ? "" : " †") + (on ? "</b>" : ""), alive ? sT : sTd) && alive) GoSpecies(s);
        }
        GUI.Label(new Rect(R.x + 4, R.yMax - 16, 120, 16), "0", sTd);
        GUI.Label(new Rect(R.x + iw - 116, R.yMax - 16, 120, 16), w.Time(w.tick), new GUIStyle(sTd) { alignment = TextAnchor.UpperRight });
    }

    // 종을 고르면: 그 종에서 가장 많은 계통을 따라가며 그곳으로 간다
    void GoSpecies(Species sp)
    {
        int best = -1; double bt = 0; foreach (int s in w.live) if (w.strains[s].sp == sp.id && w.strains[s].total > bt) { bt = w.strains[s].total; best = s; }
        if (best < 0) return;
        var st = w.strains[best]; int bk = 0; for (int k = 1; k < st.K; k++) if (st.stage[k] > 0) bk = k;
        Follow(st.id); Focus(best, st.K > 1 ? bk : 0, true);
    }

    // ---------- 혈통 타임라인: 공통 조상 → 지금, 몸 모양 줄 + 되감기 ----------
    int scrub = -1; bool playing, showLine = true; float playT;
    Rec bigRec; RenderTexture bigRT;

    List<Rec> Chain(int id)
    {
        var l = new List<Rec>();
        while (l.Count < 5000 && w.recs.TryGetValue(id, out var r)) { l.Add(r); if (r.parent == 0) break; id = r.parent; }
        l.Reverse(); return l;
    }

    void PlayStep(float dt)
    {
        if (!playing) return;
        if ((playT += dt) < 0.3f) return; playT = 0;
        int n = Chain(follow >= 0 ? follow : gone?.id ?? -1).Count;
        if (++scrub >= n - 1) { scrub = -1; playing = false; }
    }

    void LineStrip(float Hh)
    {
        bigRec = null;
        int id = follow >= 0 ? follow : gone?.id ?? -1; if (id < 0 || !showLine) return;
        var ch = Chain(id); if (ch.Count == 0) return;
        float x0 = info.xMax + 10, x1 = panel.x - 10; if (x1 - x0 < 300) return;
        var R = new Rect(x0, Hh - 162, x1 - x0, 152); line = R; GUI.Box(R, GUIContent.none, sBox);
        int cur = scrub < 0 ? ch.Count - 1 : Mathf.Min(scrub, ch.Count - 1); var rc = ch[cur];
        bigRec = rc; if (bigRT) GUI.DrawTexture(new Rect(R.x + 8, R.y + 8, 104, 104), bigRT);
        GUI.Label(new Rect(R.x + 8, R.y + 114, 104, 18), $"조상 {cur + 1}/{ch.Count}", new GUIStyle(sTd) { alignment = TextAnchor.UpperCenter });
        string life = rc.died < 0 ? $"{w.Time(rc.born)}에 생김 · 살아 있음" : $"{w.Time(rc.born)}–{w.Time(rc.died)} · {Cause(rc)}";
        string dif = cur > 0 ? Diff(ch[cur - 1].g, rc.g) : "공통 조상";
        GUI.Label(new Rect(R.x + 120, R.y + 6, R.width - 128, 58), $"<b>{SpName(rc.sp)}</b>  <size=11>계통 {rc.id}{(cur == ch.Count - 1 && follow >= 0 ? " · 지금 따라가는 계통" : "")}</size>\n<size=11>{life}\n{dif}</size>", sL);
        int n = Mathf.Max(2, Mathf.FloorToInt((R.width - 128) / 48)); float ty = R.y + 66;
        for (int j = 0; j < Mathf.Min(n, ch.Count); j++)
        {
            int i = ch.Count <= n ? j : Mathf.RoundToInt(j * (ch.Count - 1f) / (n - 1)); var tr = new Rect(R.x + 120 + j * 48, ty, 44, 44);
            if (i == cur) Fill(new Rect(tr.x - 2, tr.y - 2, 48, 48), new Color(0.5f, 0.9f, 1f));
            var tex = Thumb(ch[i]); if (tex) GUI.DrawTexture(tr, tex); else Fill(tr, new Color(0, 0, 0, 0.3f));
            if (GUI.Button(tr, GUIContent.none, GUIStyle.none)) { scrub = i; playing = false; }
        }
        float by = R.y + 118;
        if (GUI.Button(new Rect(R.x + 120, by, 40, 26), playing ? "❚❚" : "▶", sBtn)) { playing = !playing; playT = 0; if (playing && cur >= ch.Count - 1) scrub = 0; }
        if (GUI.Button(new Rect(R.x + 164, by, 50, 26), "지금", scrub < 0 ? sOn : sBtn)) { scrub = -1; playing = false; }
        float sv = GUI.HorizontalSlider(new Rect(R.x + 222, by + 8, R.width - 232, 14), cur, 0, ch.Count - 1);
        if (Mathf.RoundToInt(sv) != cur) { scrub = Mathf.RoundToInt(sv); playing = false; }
    }
    Rect line;

    void Cards(float W)
    {
        if (toastT > 0 && toast != null)
        {
            var r = new Rect(W / 2 - 300, 58, 600, 52); var c = GUI.color; GUI.color = new Color(1, 1, 1, Mathf.Clamp01(toastT));
            GUI.Box(r, GUIContent.none, sBox); GUI.Label(new Rect(r.x + 12, r.y + 6, r.width - 24, r.height - 8), toast, sL); GUI.color = c;
        }
        if (gone == null || follow >= 0) return;
        var g = new Rect(W / 2 - 260, toastT > 0 ? 116 : 58, 520, 92); GUI.Box(g, GUIContent.none, sBox);
        GUI.Label(new Rect(g.x + 12, g.y + 8, g.width - 24, 46), $"<b>혈통이 끊겼습니다</b> — {SpName(gone.sp)} (계통 {gone.id})\n<size=12>{w.Time(gone.died)} · 원인: {Cause(gone)} · {w.Time(gone.died - gone.born)} 동안 이어짐</size>", sL);
        if (GUI.Button(new Rect(g.x + 12, g.y + 58, 220, 26), "가장 가까운 친척 따라가기", sBtn)) Relative();
        if (GUI.Button(new Rect(g.x + 238, g.y + 58, 80, 26), "닫기", sBtn)) { gone = null; w.pin = -1; }
    }

    // ---------- 몸 초상: 혈통 기록의 유전자로 몸을 다시 만들어 RenderTexture에 그린다(사라진 조상도) ----------
    readonly Dictionary<int, RenderTexture> thumbs = new(); readonly HashSet<int> needThumb = new();
    CommandBuffer cb; MaterialPropertyBlock mpbP;

    Texture Thumb(Rec r) { if (thumbs.TryGetValue(r.id, out var t)) return t; needThumb.Add(r.id); return null; }

    void RenderPortraits()
    {
        cb ??= new CommandBuffer { name = "portrait" }; mpbP ??= new MaterialPropertyBlock();
        foreach (int id in needThumb)
        {
            if (!w.recs.TryGetValue(id, out var r)) continue;
            if (thumbs.Count > 96) { foreach (var x in thumbs.Values) x.Release(); thumbs.Clear(); }
            var rt = new RenderTexture(96, 96, 24) { antiAliasing = 4 }; rt.Create(); Portrait(rt, r, 35); thumbs[id] = rt;
        }
        needThumb.Clear();
        if (bigRec != null) { if (!bigRT) { bigRT = new RenderTexture(208, 208, 24) { antiAliasing = 4 }; bigRT.Create(); } Portrait(bigRT, bigRec, Time.time * 25); }
    }

    void Portrait(RenderTexture rt, Rec r, float yaw)
    {
        var ph = new Strain { id = r.id, g = r.g, hue = r.hue }; World.Shape(ph);
        int k = ph.K - 1; var m = w.MorphAt(ph, V, k, 0); bool mac = m.L * 1e-6f >= MICRO;
        Render1(rt, Bodies.Get(ph, k, m, 0, 0), mac, Quaternion.Euler(16, yaw, 0) * new Vector3(0, 0, -2.3f), r.id, 1, m.sessile);
    }

    // 메시 하나를 rt에 그린다(몸길이 1이 화면의 80%쯤). 돌려주는 값 = 메시 좌표 → 화면 행렬(이름표용)
    Matrix4x4 Render1(RenderTexture rt, Mesh mesh, bool mac, Vector3 eye, float t0, float rate, bool up = false)   // up: 부착 생물 메시(+Z = 위)를 세워서
    {
        cb ??= new CommandBuffer { name = "portrait" }; mpbP ??= new MaterialPropertyBlock();
        var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.TRS(eye, Quaternion.LookRotation(-eye), Vector3.one).inverse;
        var proj = Matrix4x4.Perspective(30, 1, 0.05f, 50);
        mpbP.SetFloat("_Fade", 1); mpbP.SetFloat("_Sel", 0); mpbP.SetFloat("_T0", t0); mpbP.SetFloat("_Rate", rate);
        cb.Clear(); cb.SetRenderTarget(rt); cb.ClearRenderTarget(true, true, mac ? new Color(0.08f, 0.2f, 0.27f) : new Color(0.01f, 0.012f, 0.02f));
        cb.SetViewProjectionMatrices(view, proj);
        cb.SetGlobalVector("_WorldSpaceCameraPos", eye);
        var bb = mesh.bounds; float s = 0.56f / Mathf.Max(1e-4f, Mathf.Max(bb.extents.x, Mathf.Max(bb.extents.y, bb.extents.z)));
        var model = (up ? Matrix4x4.Rotate(Quaternion.Euler(-90, 0, 0)) : Matrix4x4.identity) * Matrix4x4.Scale(Vector3.one * s) * Matrix4x4.Translate(-bb.center);   // 편모·가시·축족까지 칸에 꽉 차게
        cb.DrawMesh(mesh, model, mac ? mMacro : mMicro, 0, 0, mpbP);
        Graphics.ExecuteCommandBuffer(cb);
        return proj * view * model;
    }
}
