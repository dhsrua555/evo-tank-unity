// 2단계 — 그리기 도구. 모두 화면 단위(GUI 좌표, 위가 0) 사각형·원판·선을 GL로 바로 그린다(미리 만든 그림·모형 없음).
// 생물 색은 유전자(단백질 배분)에서 매끄럽게 나온다: 광합성 = 초록(산소 광합성) ~ 자주(산소 없는 광합성), 화학합성 = 주황, 흡수 = 회백, 포식 = 빨강.
using System;
using System.Collections.Generic;
using Life;
using UnityEngine;

namespace Tank
{
    public static class Draw
    {
        static Material mat;
        public static float ui = 1;              // GUI 배율
        static int mode = -1;                    // GL.QUADS / GL.LINES 중 열린 것

        public static void Begin()
        {
            if (mat == null) mat = new Material(Shader.Find("Life/Disc"));
            GL.PushMatrix(); mat.SetPass(0);
            GL.LoadPixelMatrix(0, Screen.width / ui, Screen.height / ui, 0);
            mode = -1;
        }
        public static void End() { if (mode >= 0) GL.End(); mode = -1; GL.PopMatrix(); }
        static void Mode(int m) { if (mode == m) return; if (mode >= 0) GL.End(); GL.Begin(m); mode = m; }

        public static void Rect(float x, float y, float w, float h, Color c)
        {
            Mode(GL.QUADS); GL.Color(c); GL.TexCoord2(0, 0);
            GL.Vertex3(x, y, 0); GL.Vertex3(x + w, y, 0); GL.Vertex3(x + w, y + h, 0); GL.Vertex3(x, y + h, 0);
        }
        public static void Disc(float x, float y, float r, Color c)
        {
            Mode(GL.QUADS); GL.Color(c);
            GL.TexCoord2(-1, -1); GL.Vertex3(x - r, y - r, 0); GL.TexCoord2(1, -1); GL.Vertex3(x + r, y - r, 0);
            GL.TexCoord2(1, 1); GL.Vertex3(x + r, y + r, 0); GL.TexCoord2(-1, 1); GL.Vertex3(x - r, y + r, 0);
        }
        public static void Line(float x0, float y0, float x1, float y1, Color c)
        {
            Mode(GL.LINES); GL.Color(c); GL.TexCoord2(0, 0); GL.Vertex3(x0, y0, 0); GL.Vertex3(x1, y1, 0);
        }
        public static void Ring(float x, float y, float r, Color c)
        {
            const int n = 24;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2 / n, a1 = (i + 1) * Mathf.PI * 2 / n;
                Line(x + r * Mathf.Cos(a0), y + r * Mathf.Sin(a0), x + r * Mathf.Cos(a1), y + r * Mathf.Sin(a1), c);
            }
        }

        // 몸: 배치(μm)를 화면 반지름 R에 맞춰 돌려 그린다. 세포가 많으면 앞 세포들만(이어진 덩이), 너무 작으면 점 하나
        public static void Body(Shape s, float x, float y, float R, float ang, Color c, float alpha)
        {
            if (R < 2.5f) { Disc(x, y, Mathf.Max(1.2f, R), new Color(c.r, c.g, c.b, alpha)); return; }
            if (s.n == 1)
            {
                // 단세포: 길쭉하면 막대(원판을 줄지어), 아니면 공. 막(어두운 테)과 속
                float rr = Mathf.Max(1.2f, R / Mathf.Sqrt(s.aspect)), hl = R - rr, ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                int m = hl > 0.5f ? Mathf.Clamp((int)(hl / (rr * 0.4f)), 1, 24) : 0;
                for (int pass = 0; pass < 2; pass++)
                    for (int i = -m; i <= m; i++)
                    {
                        float t = m == 0 ? 0 : hl * i / m;
                        Disc(x + ca * t, y + sa * t, pass == 0 ? rr : rr * 0.78f, pass == 0 ? new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, alpha) : new Color(c.r, c.g, c.b, alpha));
                    }
                return;
            }
            float k = R / s.ext, cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
            int n = Mathf.Min(s.n, Mathf.Max(8, (int)(R * R * 0.6f)));
            for (int i = 0; i < n; i++)
            {
                float px = s.x[i] * k, py = s.y[i] * k, rr = Mathf.Max(0.8f, s.r[i] * k);
                float shade = 0.75f + 0.25f * s.z[i];
                Color cc = s.inner[i] ? Color.Lerp(c, new Color(0.95f, 0.85f, 0.4f), s.diff) : c;
                float qx = x + px * cs - py * sn, qy = y + px * sn + py * cs;
                if (rr > 3) Disc(qx, qy, rr, new Color(cc.r * shade * 0.55f, cc.g * shade * 0.55f, cc.b * shade * 0.55f, alpha));
                Disc(qx, qy, rr > 3 ? rr * 0.8f : rr, new Color(cc.r * shade, cc.g * shade, cc.b * shade, alpha));
            }
        }

        // 몸 배치의 2차원 투영(중심 맞춤, 크기 1로)
        public sealed class Shape { public int n; public float[] x, y, z, r; public bool[] inner; public float ext, diff, aspect = 1; }
        static readonly Dictionary<Body, Shape> shapes = new();
        public static Shape ShapeOf(Body b, Traits t)
        {
            if (shapes.TryGetValue(b, out var s)) return s;
            if (shapes.Count > 4000) shapes.Clear();
            var c = b.cells; int n = c.Length; s = new Shape { n = n, x = new float[n], y = new float[n], z = new float[n], r = new float[n], inner = new bool[n], diff = (float)t.diff, aspect = (float)t.aspect };
            double cx = 0, cy = 0, cz = 0; foreach (var q in c) { cx += q.x; cy += q.y; cz += q.z; }
            cx /= n; cy /= n; cz /= n; float ext = 0, zx = 1e-6f;
            for (int i = 0; i < n; i++) { s.x[i] = (float)(c[i].x - cx); s.y[i] = (float)(c[i].y - cy); s.z[i] = (float)(c[i].z - cz); s.r[i] = c[i].r; s.inner[i] = c[i].outer < 0.5f; ext = Mathf.Max(ext, Mathf.Sqrt(s.x[i] * s.x[i] + s.y[i] * s.y[i]) + c[i].r); zx = Mathf.Max(zx, Mathf.Abs(s.z[i])); }
            for (int i = 0; i < n; i++) s.z[i] /= zx;
            s.ext = Mathf.Max(1e-3f, ext);
            // 길쭉한 세포는 원판 여러 개로 늘어놓지 않고 반지름만: 길쭉함은 배치 간격이 보여 준다
            shapes[b] = s; return s;
        }

        // 생물 색(유전자에서): 몫의 가중 평균. 종 번호로 아주 조금만 흔들어 가까운 종끼리 구별
        public static Color Of(Traits t, int id)
        {
            var p = t.phi;
            double uW = 1 / (1 + Math.Exp(-(t.eox - 0.92) / 0.15));
            Color ph = Color.Lerp(new Color(0.62f, 0.32f, 0.72f), new Color(0.25f, 0.85f, 0.45f), (float)uW);
            float wPh = (float)p[G.PHOTO], wCh = (float)p[G.CHEMO], wOs = (float)p[G.OSMO], wPg = (float)p[G.PHAGO] * 1.5f;
            float W = wPh + wCh + wOs + wPg + 1e-6f;
            Color c = (ph * wPh + new Color(0.98f, 0.6f, 0.2f) * wCh + new Color(0.82f, 0.82f, 0.78f) * wOs + new Color(0.95f, 0.28f, 0.3f) * wPg) / W;
            float j = ((id * 2654435761u) % 1000) / 1000f - 0.5f;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return Color.HSVToRGB(Mathf.Repeat(h + 0.04f * j, 1), Mathf.Clamp01(s * 1.15f), Mathf.Clamp01(0.55f + 0.5f * v));
        }

        public static readonly string[] RoleName = { "광합성", "화학합성", "포식", "청소", "분해", "혼합" };
        public static string Mass(double mol)
        {
            if (mol <= 0) return "0";
            double g = mol * 12, t = g / 1e6; // g C, t C
            if (t >= 1e8) return $"{t / 1e8:0.#}억 t";
            if (t >= 1e4) return $"{t / 1e4:0.#}만 t";
            if (t >= 1) return $"{t:0.#} t";
            if (g >= 1e3) return $"{g / 1e3:0.#} kg";
            return $"{g:0.#} g";
        }
    }
}
