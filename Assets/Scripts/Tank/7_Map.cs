// 7단계 — 물속 화면 위에 뜨는 판(반투명): 오른쪽 위 해역 지도(누르면 그 해역으로), 왼쪽 수심계(층마다 같은 높이 칸, 빛 밝기,
// 고른 종이 사는 층, 누르면 그 깊이로), 왼쪽 아래 축척 막대와 지금 보는 것, 마우스 밑 개체 이름.
using System;
using Life;
using UnityEngine;

namespace Tank
{
    public sealed partial class Tank
    {
        Rect mapR, gauge; Texture2D mapT; int mapFrame = -999;

        // 해역 지도: 고도·수심 색, 바다얼음, 지금 해역은 노랗게. 아래 줄 = 남쪽(위도 2°), 위 = 북쪽(60°)
        void MapBox()
        {
            Card(mapR); var g = run.pl.geo; int G = Evo.Geo.G;
            if (!mapT) mapT = new Texture2D(G, G) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            if (Time.frameCount - mapFrame > 30)
            {
                mapFrame = Time.frameCount;
                for (int i = 0; i < G * G; i++)
                {
                    float d = g.Depth(i);
                    var c = d <= 0 ? Color.Lerp(new Color(0.4f, 0.34f, 0.2f), new Color(0.78f, 0.7f, 0.52f), -d / 1500)
                                   : Color.Lerp(new Color(0.5f, 0.86f, 0.84f), new Color(0.03f, 0.08f, 0.26f), Mathf.Log(1 + d / 20) / Mathf.Log(251));
                    if (d > 0 && g.reg[i] >= 0 && snap.ice != null && g.reg[i] < snap.ice.Length) c = Color.Lerp(c, new Color(0.92f, 0.96f, 1), snap.ice[g.reg[i]] * 0.8f);
                    if (g.In(i, reg)) c = Color.Lerp(c, new Color(1, 0.9f, 0.2f), 0.8f);
                    mapT.SetPixel(i % G, i / G, c);
                }
                mapT.Apply();
            }
            var img = new Rect(mapR.x + 8, mapR.y + 8, mapR.height - 16, mapR.height - 16); GUI.DrawTexture(img, mapT);
            var p = run.pl; var s = snap; int v = vNow < 0 ? Voxel() : vNow; bool floor = v >= p.NF0, vent = v >= p.NV0 && !floor; int k = v >= p.NV0 ? Cfg.NY - 1 : v % Cfg.NY;
            GUI.Label(new Rect(img.xMax + 10, mapR.y + 6, mapR.xMax - img.xMax - 16, mapR.height - 12),
                $"<b>{g.Name(reg)}</b>\n북위 {g.rLat[reg]:0}° · 바닥 {Bottom:0} m\n지금 수심 {py:0.0} m · {(floor ? "바닥 퇴적면" : vent ? "열수공 곁" : LAYER[k])}\n수온 {s.vT[v]:0.0} °C · 빛 {s.vLight[v] * 100:0.##} %\n" +
                $"O₂ {s.vO2[v]:0.###} · Fe²⁺ {s.vFe2[v]:0.##} μM\nH₂S {s.vH2S[v]:0.##} μM · 바다눈 {s.vPOC[v]:0.##} μM C", sSR);
            if (GUI.Button(img, GUIContent.none, GUIStyle.none))
            {
                var m = UnityEngine.Event.current.mousePosition;
                int x = Mathf.Clamp((int)((m.x - img.x) / img.width * G), 0, G - 1), z = Mathf.Clamp(G - 1 - (int)((m.y - img.y) / img.height * G), 0, G - 1);
                int to = g.Any(z * G + x); if (to >= 0 && to != reg) { sel = -1; GoTo(to, Math.Min(py, g.rDepth[to] * 0.5)); }
            }
        }

        // 수심계: 이 해역의 층마다 같은 높이 칸(층 두께가 20 m–수 km로 달라서), 빛(밝을수록), 지금 수심, 고른 종의 밀도. 누르면 그 깊이로
        void DepthGauge()
        {
            Card(gauge); var p = run.pl; var s = snap;
            var bar = new Rect(gauge.x + 8, gauge.y + 22, 12, gauge.height - 52);
            int n = 0; while (n < Cfg.NY && p.live[reg * Cfg.NY + n]) n++; n = Math.Max(1, n);
            float hh = bar.height / n; var fs = sel >= 0 ? s.sp[sel] : null; double fmax = 0;
            if (fs?.conc != null) for (int y = 0; y < n; y++) fmax = Math.Max(fmax, fs.conc[reg * Cfg.NY + y]);
            for (int y = 0; y < n; y++)
            {
                int v = reg * Cfg.NY + y; float l = Mathf.Clamp01(Mathf.Pow(s.vLight[v], 0.3f)), y0 = bar.y + hh * y;
                Fill(new Rect(bar.x, y0, bar.width, hh - 1), Color.Lerp(new Color(0.02f, 0.05f, 0.1f), new Color(0.35f, 0.7f, 0.85f), l));
                if (fmax > 0 && fs.conc[v] > 0) Fill(new Rect(bar.xMax + 2, y0, 2 + 14 * (float)Math.Sqrt(fs.conc[v] / fmax), hh - 1), Draw.Of(fs.t, fs.id));
            }
            for (int i = 0; i < Cfg.NVENT; i++) if (p.ventReg[i] == reg && p.live[p.NV0 + i]) Fill(new Rect(bar.x + 2, bar.yMax - 6, 8, 6), new Color(1, 0.55f, 0.2f));   // 열수공
            int yc = 0; while (yc < n - 1 && py >= Cfg.ZE[yc + 1]) yc++;
            double top = Cfg.ZE[yc], bot = Math.Min(Cfg.ZE[yc + 1], Bottom);
            float my = bar.y + hh * (yc + Mathf.Clamp01((float)((py - top) / Math.Max(1e-6, bot - top)))); Fill(new Rect(bar.x - 4, my - 1, bar.width + 8, 3), Color.white);
            GUI.Label(new Rect(gauge.x, gauge.y + 3, gauge.width, 18), "수심", sSC);
            GUI.Label(new Rect(gauge.x, gauge.yMax - 32, gauge.width, 14), $"{Bottom:0} m", sSC);
            if (GUI.Button(new Rect(gauge.x + 4, gauge.yMax - 14, 19, 13), "▲", sTiny)) Layer(-1);
            if (GUI.Button(new Rect(gauge.x + 24, gauge.yMax - 14, 19, 13), "▼", sTiny)) Layer(+1);
            if (GUI.Button(bar, GUIContent.none, GUIStyle.none))
            {
                float t = Mathf.Clamp01((UnityEngine.Event.current.mousePosition.y - bar.y) / bar.height) * n; int y = Mathf.Min((int)t, n - 1);
                double t0 = Cfg.ZE[y], t1 = Math.Min(Cfg.ZE[y + 1], Bottom);
                GoTo(reg, t0 + (t - y) * (t1 - t0));
            }
        }

        // 왼쪽 아래: 축척 막대(1-2-5, 화면 60–160 px) + 지금 보는 것
        void ScaleBar(Rect r)
        {
            float ppu = Screen.height / Draw.ui / (2 * camDist * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2)), pxPerM = ppu * BOX / B;
            float len = 1e-7f; for (int i = 0; i < 40 && len * pxPerM < 60; i++) len *= (i % 3 == 1) ? 2.5f : 2;
            GUI.DrawTexture(new Rect(r.x, r.y + 14, len * pxPerM, 3), Texture2D.whiteTexture);
            GUI.Label(new Rect(r.x + len * pxPerM + 8, r.y + 4, r.width - len * pxPerM - 8, 22), LenTxt(len) + (selAv != null ? $"     따라가는 중: {snap.sp[selAv.sp].name}" : ""), sL);
            int nv = 0; foreach (var a in avs) if (!a.dying) nv++;
            GUI.Label(new Rect(r.x, r.y + 28, r.width, 18), $"{(Micro ? "암시야 현미경" : "물속")} · 시야 {LenTxt(B)} · 보이는 {nv}마리 = 실제 밀도 × {Pow10(F)}", sS);
        }
        // 농축 배율(10의 거듭제곱): 1000 넘으면 10⁶처럼 짧게
        static string Pow10(double f)
        {
            int e = (int)Math.Round(Math.Log10(f)); if (e >= 0 && e < 4) return Math.Pow(10, e).ToString("0");
            string t = e < 0 ? "⁻" : ""; foreach (char c in Math.Abs(e).ToString()) t += "⁰¹²³⁴⁵⁶⁷⁸⁹"[c - '0'];
            return "10" + t;
        }
        static string LenTxt(double m) => m < 1e-3 ? $"{m * 1e6:0.#} μm" : m < 1e-2 ? $"{m * 1e3:0.##} mm" : m < 1 ? $"{m * 100:0.#} cm" : $"{m:0.##} m";

        void DiveGui()
        {
            MapBox(); DepthGauge();
            float x = gauge.xMax + 14, w = mapR.x - x - 14, yb = (showGraph ? graphR.y : H) - 8;   // 아래 둘째 줄까지: 도움말 · 축척 · 지금 보는 것
            GUI.Label(new Rect(x, yb - 88, w, 16), "끌기 둘러보기 · 휠 시야 · 누르기 따라가기", sS);
            GUI.Label(new Rect(x, yb - 71, w, 16), "WASD 헤엄(E/Q 위아래) · F 맞춤 · Esc 놓기", sS);
            ScaleBar(new Rect(x, yb - 50, w, 46));
            if (hoverSp >= 0 && inView)
            {
                var sp = snap.sp[hoverSp]; string t = $"{sp.name} · {Draw.RoleName[(int)sp.role]} · {Size(sp.body.size)}";
                var r = new Rect(mouse.x + 14, mouse.y + 10, sS.CalcSize(new GUIContent(t)).x + 12, 20);
                Fill(r, new Color(0, 0, 0, 0.6f)); GUI.Label(new Rect(r.x + 6, r.y + 2, r.width, 18), t, sS);
            }
        }
    }
}
