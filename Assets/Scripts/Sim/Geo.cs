// 세계 지도: 32×32칸(칸당 200 km, 위도 2–60°N)마다 고도(m). 해수면 아래 칸은 바다.
// 시뮬레이션은 칸을 하나씩 계산하지 않고, 수심대·위도대가 같고 이어진 칸을 '해역'으로 묶어 해역마다 물기둥 하나로 계산한다.
using System;
using System.Collections.Generic;

namespace Evo
{
    public sealed class Geo
    {
        public const int G = 32;
        public const float CELL = 200e3f;   // m
        public readonly float[] h = new float[G * G];   // 고도(m, 해수면 0 기준)
        public float sea;                                  // 해수면(m): 빙하기에 내려간다
        public readonly int[] reg = new int[G * G];       // 칸 → 해역(육지 −1)
        public readonly int[] sreg = new int[G * G], lreg = new int[G * G];   // 땅 칸 → 그 위도대의 조간대(바다와 맞닿은 칸만)·육지 해역(바다 칸 −1)
        public int NR, NO;                                 // 해역 수, 그중 바다(0..NO−1). 그 뒤는 위도대마다 조간대·육지
        public float[] rDepth, rLat, rArea, rCoast;       // 해역 평균 수심(m)·위도·넓이(m²)·해안 칸 비율
        public int[] rCells;
        public byte[] rKind;                               // 0 바다 · 1 조간대(밀물에 잠기고 썰물에 드러나는 물가 띠) · 2 육지(흙 속 물막)
        public const int NLAND = 6;                        // 조간대·육지 해역 최대 수(위도대 3 × 2)
        public const float SHORE_W = 1000, SHORE_D = 1, LAND_D = 0.1f;   // 조간대 띠 폭(해안 칸 하나에 1 km)·물 깊이(조수 웅덩이·젖은 모래), 흙 속 물막 두께(m)
        public readonly List<(int a, int b, int e, float dist)> links = new();
        public readonly List<int> spots = new();           // 열점(해산·화산 섬 자리): 화산이 주로 여기서 되풀이된다   // 이웃 해역: 맞닿은 칸 변 수, 중심 거리(m)

        public static float Lat(int z) => 2 + 58 * (z + 0.5f) / G;
        public float Depth(int i) => sea - h[i];
        public int LandCells() { int n = 0; for (int i = 0; i < G * G; i++) if (h[i] >= sea) n++; return n; }
        public static string DName(float d) => d < 60 ? "연안" : d < 250 ? "대륙붕" : d < 2500 ? "대륙사면" : "대양";
        public static string LName(float lat) => lat < 23 ? "열대" : lat < 45 ? "온대" : "한대";
        public string Name(int c) => LName(rLat[c]) + " " + (rKind[c] == 1 ? "조간대" : rKind[c] == 2 ? "육지" : DName(rDepth[c]));
        public int Any(int i) => reg[i] >= 0 ? reg[i] : sreg[i] >= 0 ? sreg[i] : lreg[i];   // 지도 칸 → 화면에서 고를 해역(해안 땅 칸은 조간대)

        // 서쪽 대륙과 대륙붕, 동쪽 대양, 화산 섬 몇 개. 같은 시드면 같은 지도
        public void Generate(int seed)
        {
            var r = new Rng((ulong)seed * 7919 + 17);
            var noise = new float[4][]; int[] res = { 4, 8, 16, 32 };
            for (int o = 0; o < 4; o++) { noise[o] = new float[(res[o] + 1) * (res[o] + 1)]; for (int i = 0; i < noise[o].Length; i++) noise[o][i] = (float)r.U(); }
            float N(int x, int z)
            {
                float s = 0, a = 1, tot = 0;
                for (int o = 0; o < 4; o++)
                {
                    float fx = x * res[o] / (float)G, fz = z * res[o] / (float)G; int ix = (int)fx, iz = (int)fz; float tx = fx - ix, tz = fz - iz, n = res[o] + 1;
                    float v00 = noise[o][iz * (int)n + ix], v10 = noise[o][iz * (int)n + ix + 1], v01 = noise[o][(iz + 1) * (int)n + ix], v11 = noise[o][(iz + 1) * (int)n + ix + 1];
                    tx = tx * tx * (3 - 2 * tx); tz = tz * tz * (3 - 2 * tz);
                    s += a * ((v00 * (1 - tx) + v10 * tx) * (1 - tz) + (v01 * (1 - tx) + v11 * tx) * tz); tot += a; a *= 0.5f;
                }
                return s / tot;
            }
            // 대륙성(잡음 + 서쪽일수록 대륙)의 순위로 높이를 정한다: 육지 35%, 대륙붕 18%(그중 수심 60 m 위 연안 ⅓), 대륙사면 14%, 대양 33%.
            // 실제 지구(대륙붕 ~8%)보다 얕은 바다가 넓다 — 고생대 초처럼 대륙 위로 바다가 들어온 시대(얕은 바다가 생물 대부분의 무대)
            var cont = new float[G * G]; var ord = new int[G * G];
            for (int z = 0; z < G; z++) for (int x = 0; x < G; x++) { cont[z * G + x] = -(N(x, z) + 0.55f * (1 - x / (G - 1f))); ord[z * G + x] = z * G + x; }
            Array.Sort((float[])cont.Clone(), ord);
            for (int i = 0; i < G * G; i++)
            {
                float q = (i + 0.5f) / (G * G); int c = ord[i];
                h[c] = q < 0.35f ? 50 + 1500 * MathF.Pow((0.35f - q) / 0.35f, 1.5f)
                     : q < 0.53f ? -250 * MathF.Pow((q - 0.35f) / 0.18f, 1.3f)
                     : q < 0.67f ? -250 - 2250 * (q - 0.53f) / 0.14f
                     : -2500 - 2500 * MathF.Sqrt((q - 0.67f) / 0.33f);
            }
            spots.Clear();
            for (int k = 0; k < 3; k++)   // 해산·화산 섬
            {
                int cx = 14 + (int)(r.U() * 16), cz = 2 + (int)(r.U() * 28); spots.Add(cz * G + cx); float top = k == 0 ? 300 : -100 - 800 * (float)r.U();
                for (int z = 0; z < G; z++) for (int x = 0; x < G; x++)
                {
                    float d2 = (x - cx) * (x - cx) + (z - cz) * (z - cz), b = MathF.Exp(-d2 / 2.2f);
                    h[z * G + x] = Math.Max(h[z * G + x], -4500 + (top + 4500) * b);
                }
            }
        }

        static int DClass(float d) => d < 60 ? 0 : d < 250 ? 1 : d < 2500 ? 2 : 3;
        static int LClass(float lat) => lat < 23 ? 0 : lat < 45 ? 1 : 2;

        // 해역 나누기: (수심대, 위도대)가 같고 이어진 칸끼리. 작은 조각(3칸 미만)과 rmax 넘는 몫은 가장 많이 맞닿은 이웃에 합친다
        public void Regions(int rmax)
        {
            var lab = reg; Array.Fill(lab, -1); int n = 0; var q = new Queue<int>();
            for (int i = 0; i < G * G; i++)
            {
                if (Depth(i) <= 0 || lab[i] >= 0) continue;
                int key = DClass(Depth(i)) * 4 + LClass(Lat(i / G)); lab[i] = n; q.Enqueue(i);
                while (q.Count > 0)
                {
                    int c = q.Dequeue(), x = c % G, z = c / G;
                    foreach (int nb in Nb(x, z)) if (nb >= 0 && lab[nb] < 0 && Depth(nb) > 0 && DClass(Depth(nb)) * 4 + LClass(Lat(nb / G)) == key) { lab[nb] = n; q.Enqueue(nb); }
                }
                n++;
            }
            while (true)
            {
                var size = new int[n]; var dsum = new float[n]; var lsum = new float[n];
                for (int i = 0; i < G * G; i++) if (lab[i] >= 0) { size[lab[i]]++; dsum[lab[i]] += Depth(i); lsum[lab[i]] += Lat(i / G); }
                int alive = 0, small = -1; for (int a = 0; a < n; a++) if (size[a] > 0) { alive++; if (small < 0 || size[a] < size[small]) small = a; }
                if (small < 0 || size[small] >= 3 && alive <= rmax) break;
                var share = new Dictionary<int, int>();
                for (int i = 0; i < G * G; i++) if (lab[i] == small) foreach (int nb in Nb(i % G, i / G)) if (nb >= 0 && lab[nb] >= 0 && lab[nb] != small) share[lab[nb]] = share.GetValueOrDefault(lab[nb]) + 1;
                // 같은 수심대 이웃을 먼저(얕은 바다를 깊은 바다에 합치면 바닥 생물의 수심이 틀어진다), 그다음 같은 위도대, 맞닿은 변이 많은 쪽
                int Score(int a, int e) => -1000 * Math.Abs(DClass(dsum[a] / size[a]) - DClass(dsum[small] / size[small])) - 100 * Math.Abs(LClass(lsum[a] / size[a]) - LClass(lsum[small] / size[small])) + e;
                int to = -1, best = int.MinValue; foreach (var kv in share) { int sc = Score(kv.Key, kv.Value); if (sc > best) { best = sc; to = kv.Key; } }
                if (to < 0) { if (alive > 1 || size[small] < 3) for (int i = 0; i < G * G; i++) if (lab[i] == small) { lab[i] = -1; h[i] = sea + 1; } continue; }   // 외딴 물웅덩이는 땅으로
                for (int i = 0; i < G * G; i++) if (lab[i] == small) lab[i] = to;
            }
            var map = new Dictionary<int, int>(); for (int i = 0; i < G * G; i++) if (lab[i] >= 0) { if (!map.ContainsKey(lab[i])) map[lab[i]] = map.Count; lab[i] = map[lab[i]]; }
            // 땅: 위도대마다 육지(그 위도대의 땅 칸 전부) 하나와, 바다와 맞닿은 땅 칸이 있으면 조간대 하나를 바다 해역 뒤에 붙인다
            NO = map.Count; n = NO; Array.Fill(sreg, -1); Array.Fill(lreg, -1);
            bool Coastal(int i) { foreach (int nb in Nb(i % G, i / G)) if (nb >= 0 && lab[nb] >= 0) return true; return false; }
            var kinds = new List<byte>();
            for (int b = 0; b < 3; b++)
            {
                int sr = -1, lr = -1;
                for (int i = 0; i < G * G; i++)
                {
                    if (lab[i] >= 0 || LClass(Lat(i / G)) != b) continue;
                    if (lr < 0) { lr = n++; kinds.Add(2); }
                    lreg[i] = lr;
                    if (Coastal(i)) { if (sr < 0) { sr = n++; kinds.Add(1); } sreg[i] = sr; }
                }
            }
            NR = n; rDepth = new float[NR]; rLat = new float[NR]; rArea = new float[NR]; rCoast = new float[NR]; rCells = new int[NR]; rKind = new byte[NR];
            for (int a = NO; a < NR; a++) rKind[a] = kinds[a - NO];
            var cx = new float[NR]; var cz = new float[NR];
            for (int i = 0; i < G * G; i++)
            {
                int x = i % G, z = i / G;
                foreach (int a in new[] { lab[i], sreg[i], lreg[i] })
                {
                    if (a < 0) continue;
                    rCells[a]++; rLat[a] += Lat(z); cx[a] += x; cz[a] += z;
                    if (a < NO) { rDepth[a] += Depth(i); foreach (int nb in Nb(x, z)) if (nb >= 0 && lab[nb] < 0) { rCoast[a]++; break; } }
                }
            }
            for (int a = 0; a < NR; a++)
            {
                rLat[a] /= rCells[a]; cx[a] /= rCells[a]; cz[a] /= rCells[a];
                if (a < NO) { rDepth[a] /= rCells[a]; rCoast[a] /= rCells[a]; rArea[a] = rCells[a] * CELL * CELL; }
                else if (rKind[a] == 1) { rDepth[a] = SHORE_D; rArea[a] = rCells[a] * CELL * SHORE_W; }
                else { rDepth[a] = LAND_D; rArea[a] = rCells[a] * CELL * CELL; }
            }
            // 이웃: 바다–바다, 바다–조간대(해안선), 위도대를 넘는 조간대–조간대·육지–육지(맞닿은 칸 변 수), 조간대–육지(해안 칸 수)
            var e = new Dictionary<(int, int), int>();
            void Add(int a, int b) { if (a < 0 || b < 0 || a == b) return; var k = (Math.Min(a, b), Math.Max(a, b)); e[k] = e.GetValueOrDefault(k) + 1; }
            for (int i = 0; i < G * G; i++)
            {
                int x = i % G, z = i / G; Add(sreg[i], lreg[i]);
                foreach (int j in new[] { x + 1 < G ? i + 1 : -1, z + 1 < G ? i + G : -1 })
                {
                    if (j < 0) continue;
                    Add(lab[i], lab[j]); Add(sreg[i], sreg[j]); Add(lreg[i], lreg[j]);
                    if (lab[i] >= 0) Add(lab[i], sreg[j]); else Add(sreg[i], lab[j]);
                }
            }
            links.Clear();
            foreach (var kv in e)
            {
                var (a, b) = kv.Key; bool local = (rKind[a] == 1) != (rKind[b] == 1);   // 조간대와 바다·육지 사이는 물가에서 바로 맞닿는다
                float d = local ? CELL : MathF.Max(1, MathF.Sqrt((cx[a] - cx[b]) * (cx[a] - cx[b]) + (cz[a] - cz[b]) * (cz[a] - cz[b]))) * CELL; links.Add((a, b, kv.Value, d));
            }
        }

        static IEnumerable<int> Nb(int x, int z)
        {
            yield return x > 0 ? z * G + x - 1 : -1; yield return x < G - 1 ? z * G + x + 1 : -1;
            yield return z > 0 ? (z - 1) * G + x : -1; yield return z < G - 1 ? (z + 1) * G + x : -1;
        }
    }
}
