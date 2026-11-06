// 관찰 도구: 확대 카드(고른 개체를 크게, 끌어서 돌려 보고 부위 이름표), 표본 쟁반(살아 있는 종을 한 판에 나란히).
using System;
using System.Linq;
using UnityEngine;
using Evo;

public partial class EvoTank
{
    bool showCard = true, showTray, trayReal; float cYaw = 60, cPitch = 14, cDragT = -10; Vector2 cScroll;
    RenderTexture cardRT; Matrix4x4 cardVP; Vector3 cardEye; Mesh cardMesh; Rect card, tray;

    // 이 자리의 몸 메시(삼투로 부풂·쭈글, 빛에 따른 엽록체 양까지)
    Mesh Body(Strain st, int k, in Morph m)
    {
        var g = st.g; float S = w.env[Ev.Salt], I = Mix(w.I), chl = 1.5f * (1 - I / (I + 0.15f));
        float swell = S > g[Gn.Osmo] * 1.08f ? -1 : S < w.P.OSMO_FLOOR && g[Gn.Wall] < 0.5f && g[Gn.Vacuole] < 0.5f ? 1 : 0;
        return Bodies.Get(st, k, m, swell, g[Gn.Photo] > 0.05f ? chl : 0);
    }

    // LateUpdate에서: 확대 카드 그림
    void RenderCard()
    {
        if (sel == null || !showCard) { cardMesh = null; return; }
        var st = w.strains[sel.slot]; if (st == null || st.id != sel.id) return;
        if (!cardRT) { cardRT = new RenderTexture(512, 512, 24) { antiAliasing = 4 }; cardRT.Create(); }
        if (Time.time - cDragT > 2) cYaw += Time.deltaTime * 12;   // 손 떼면 천천히 돈다
        var m = w.M(sel.slot, V, sel.k); cardMesh = Body(st, sel.k, m);
        cardEye = Quaternion.Euler(cPitch, cYaw, 0) * new Vector3(0, 0, -2.3f);
        cardVP = Render1(cardRT, cardMesh, m.L * 1e-6f >= MICRO, cardEye, sel.t0, sel.rate, m.sessile);
    }

    // 확대 카드: 왼쪽 수심계 옆. 끌면 돌리고, 부위마다 이름표(뒤쪽 것은 흐리게)
    void Card(float Hh)
    {
        card = default;
        if (sel == null || !showCard || cardMesh == null) return;
        var st = w.strains[sel.slot]; if (st == null) return;
        float sz = Mathf.Clamp(Hh - 58 - 180 - 70, 200, 400);
        card = new Rect(62, 58, sz + 16, sz + 64); GUI.Box(card, GUIContent.none, sBox);
        var m = w.M(sel.slot, V, sel.k);
        GUI.Label(new Rect(card.x + 10, card.y + 6, sz - 30, 20), $"<b>{SpName(st.sp)}</b>", sT);
        GUI.Label(new Rect(card.x + 10, card.y + 24, sz, 18), $"{StageName(st, sel.k)} · 몸길이 {Len(m.L * 1e-6f)} · 약 {Num(sz * 0.8f * UIScale * 2.5e-4f / (m.L * 1e-6f))}배 확대", sTd);
        if (GUI.Button(new Rect(card.xMax - 26, card.y + 6, 20, 20), "×", sBtn)) showCard = false;
        var R = new Rect(card.x + 8, card.y + 46, sz, sz); GUI.DrawTexture(R, cardRT);
        var e = Event.current;
        if (e.type == EventType.MouseDrag && R.Contains(e.mousePosition)) { cYaw += e.delta.x * 0.6f; cPitch = Mathf.Clamp(cPitch + e.delta.y * 0.4f, -80, 80); cDragT = Time.time; e.Use(); }
        // 부위 이름표: 메시 좌표 → 카드 위 점. 가운데에서 바깥쪽으로 이름을 뺀다
        int n = 0; float dc = cardEye.magnitude;
        foreach (var (name, p) in Bodies.Parts(cardMesh))
        {
            if (n++ >= 10) break;
            var c = cardVP * new Vector4(p.x, p.y, p.z, 1); if (c.w <= 0) continue;
            var q = new Vector2(R.x + (c.x / c.w * 0.5f + 0.5f) * R.width, R.y + (0.5f - c.y / c.w * 0.5f) * R.height);
            bool back = c.w > dc + 0.01f;   // 몸 가운데보다 멀다 = 뒤쪽(c.w = 카메라에서 깊이)
            var dir = q - R.center; dir = dir.sqrMagnitude < 1 ? Vector2.right : dir.normalized;
            // 이름은 카드 가장자리 둘레(가운데에서 그 부위 쪽)로 빼서 몸을 가리지 않게
            var t = R.center + dir * R.width * 0.4f - new Vector2(dir.x < 0 ? 74 : 0, 8); t.x = Mathf.Clamp(t.x, R.x + 4, R.xMax - 78); t.y = Mathf.Clamp(t.y, R.y + 4, R.yMax - 18);
            var col = new Color(1, 1, 1, back ? 0.35f : 0.9f);
            Line(q, t + new Vector2(dir.x < 0 ? 74 : 0, 8), col); Fill(new Rect(q.x - 2, q.y - 2, 4, 4), col);
            var tr = new Rect(t.x, t.y, 74, 16); Fill(tr, new Color(0, 0, 0, back ? 0.25f : 0.55f));
            var o = GUI.color; GUI.color = col; GUI.Label(tr, name, sT); GUI.color = o;
        }
        GUI.Label(new Rect(card.x + 10, card.yMax - 16, sz, 14), "끌어서 돌리기 · M: 카드 닫기/열기", new GUIStyle(sTd) { fontSize = 10 });
    }

    static void Line(Vector2 a, Vector2 b, Color c)
    {
        var mx = GUI.matrix; var d = b - a;   // RotateAroundPivot은 UI 배율이 있으면 축이 어긋나서 직접 돌린다
        GUI.matrix = mx * Matrix4x4.TRS(a, Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg), Vector3.one) * Matrix4x4.Translate(-a);
        Fill(new Rect(a.x, a.y - 0.5f, d.magnitude, 1), c); GUI.matrix = mx;
    }

    string StageName(Strain st, int k) => st.K <= 1 ? Taxa.Name(st) : k == 0 ? "어린 단계" : k == st.K - 1 ? "어른" : $"자라는 중({k + 1}/{st.K})";

    // 표본 쟁반: 살아 있는 종마다 가장 많은 계통의 어른 모습. '같은 크기'(칸을 채움) ↔ '실제 비율'(가장 큰 것 기준)
    void Tray(float W, float Hh)
    {
        tray = default; if (!showTray) return;
        var reps = w.live.Select(s => w.strains[s]).Where(st => st.total >= 1).GroupBy(st => st.sp)
            .Select(gp => gp.OrderByDescending(st => st.total).First()).OrderByDescending(st => w.AdultL(st)).Take(40).ToList();
        int cols = Mathf.Clamp(reps.Count, 3, Mathf.Max(3, Mathf.FloorToInt((W - 520) / 124))); int rows = Mathf.CeilToInt(reps.Count / (float)cols);
        tray = new Rect(70, 58, cols * 124 + 20, Mathf.Min(Hh - 200, rows * 150 + 64)); GUI.Box(tray, GUIContent.none, sBox);
        GUI.Label(new Rect(tray.x + 12, tray.y + 8, 300, 22), $"<b>표본 쟁반</b> · 살아 있는 종 {reps.Count}", sL);
        if (GUI.Button(new Rect(tray.xMax - 236, tray.y + 8, 92, 24), "같은 크기", trayReal ? sBtn : sOn)) trayReal = false;
        if (GUI.Button(new Rect(tray.xMax - 140, tray.y + 8, 92, 24), "실제 비율", trayReal ? sOn : sBtn)) trayReal = true;
        if (GUI.Button(new Rect(tray.xMax - 42, tray.y + 8, 32, 24), "×", sBtn)) showTray = false;
        float Lmax = reps.Count > 0 ? w.AdultL(reps[0]) : 1;
        cScroll = GUI.BeginScrollView(new Rect(tray.x + 8, tray.y + 40, tray.width - 12, tray.height - 48), cScroll, new Rect(0, 0, cols * 124, rows * 150));
        for (int i = 0; i < reps.Count; i++)
        {
            var st = reps[i]; var r = new Rect(i % cols * 124, i / cols * 150, 118, 144); float L = w.AdultL(st);
            Fill(r, follow == st.id ? new Color(0.5f, 0.9f, 1, 0.25f) : new Color(0, 0, 0, 0.3f));
            var tex = st.rec != null ? Thumb(st.rec) : null; float f = trayReal ? Mathf.Max(0.04f, L / Lmax) : 1, ts = 96 * f;
            var tr = new Rect(r.x + 11 + (96 - ts) / 2, r.y + 6 + (96 - ts) / 2, ts, ts);
            if (tex) GUI.DrawTexture(tr, tex); else Fill(tr, new Color(1, 1, 1, 0.05f));
            GUI.Label(new Rect(r.x + 4, r.y + 104, 112, 18), SpName(st.sp), sT);
            GUI.Label(new Rect(r.x + 4, r.y + 122, 112, 18), $"{Len(L * 1e-6f)} · {Taxa.Name(st)}", sTd);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none) && w.species.TryGetValue(st.sp, out var sp)) { showTray = false; GoSpecies(sp); }
        }
        GUI.EndScrollView();
    }
}
