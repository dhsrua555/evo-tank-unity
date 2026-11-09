// 행성 상태: 지도(Geo의 해역) × 수심층 = 칸(voxel). 칸마다 물질 풀(μM)과 계통별 생물량(μM C).
// 처음 상태 = 원시 지구: 거의 바다뿐, 산소 없음, CO2 많음, 바다에 Fe2+, 열수공 몇 곳에만 원시 세포가 조금.
using System;
using System.Collections.Generic;
using Evo;

namespace Life
{
    // 물질 풀 번호
    public static class P
    {
        public const int CO2 = 0, O2 = 1, H2 = 2, CH4 = 3, FE2 = 4, FE3 = 5, H2S = 6, SO4 = 7, NH4 = 8, PO4 = 9, DOC = 10, POC0 = 11, POC1 = 12, POC2 = 13;
        public const int N = 14;
        public static readonly string[] Name = { "CO₂", "O₂", "H₂", "CH₄", "Fe²⁺", "Fe³⁺", "H₂S", "SO₄²⁻", "NH₄⁺", "PO₄³⁻", "녹은 유기물", "작은 사체", "중간 사체", "큰 사체" };
    }

    public sealed class Strain
    {
        public int id, parent, slot, species = -1, born, gene = -1;   // gene: 부모와 다른 유전자(창시 돌연변이)
        public float[] g; public Traits t; public Body b;
        public double total;                                          // 행성 전체 생물량(mol C)
        public double prev, rate = double.NaN;                        // 지난 정리 때 생물량, 생물권보다 빨리 느는 속도(/일, 지수 평균)
        public double cFix, cPrey, cDet, cDoc, cChemo;                // 오늘 탄소·에너지 수입(mol C): 고정, 산 먹이, 사체, 녹은 유기물 · 화학합성 ATP
        public double o2Made, aerobic, catab;                         // 오늘 만든 O2, 산소 호흡으로 태운 C / 전체 태운 C
        public double jump, sel;                                      // 부모와의 몸 모양 거리(점진성 지표), 정착 때 선택 계수
        public double att, vul, lsz, lfeed, allo;                     // 포식 값(형질·몸으로 정해져 칸·틱마다 같다): 공격률, 먹이 약함, log10 크기·먹는 크기, 크기 배율
    }

    public sealed partial class Planet
    {
        [NonSerialized] public readonly Geo geo = new();   // 저장하지 않음: 시드로 다시 만든다
        public readonly Knobs knobs = new();
        public int NR, V;                       // 해역 수, 칸 수(NR × NY)
        public bool[] live;                     // 칸이 있나(해저 아래 층은 없음)
        public double[] vol, thick, zTop, area; // 칸 부피(L)·두께(m)·윗면 수심(m), 해역 넓이(m²)
        public byte[] kind;                     // 해역 종류 0 바다 · 1 조간대 · 2 육지
        public double[][] pool;                 // [풀][칸] μM
        public double[] temp, light, dry;       // 칸 수온(°C)·빛(수면 적도 = 1)·마름(0–1)
        public bool[] vent;                     // 열수공이 있는 칸
        public double[] ventMul;                // 열수공 세기 배율(사건이 바꾼다)
        public int NV0;                         // 해역 칸 수(NR × NY). 그 뒤 NVENT개가 열수공 칸
        public int[] ventHost, ventReg;         // 열수공 칸 i → 붙은 바닥 칸, 해역
        int nVent0;

        // 대기(mol)
        public double atmO2, atmCO2, atmCH4, atmH2;
        public double O2PAL => atmO2 / Cfg.O2_PAL_MOL;
        public double CO2PAL => atmCO2 / Cfg.CO2_PAL_MOL;
        public double CH4ppm => atmCH4 / Cfg.PPM_MOL;
        public double H2ppm => atmH2 / Cfg.PPM_MOL;

        // 생물: 계통 칸 × 칸
        public readonly Strain[] strains = new Strain[Cfg.SMAX];
        public double[] B;                      // [slot·V + v] μM C
        public int nextId;
        public long tick; public double Day => tick * Cfg.DT;
        public Rng rng;
        public readonly int seed;

        public Planet(int seed)
        {
            this.seed = seed; rng = new Rng((ulong)seed * 2654435761UL + 1);
            geo.Generate(seed); geo.Drown(0.03f); geo.Regions(24);
            Build();
            Init();
        }

        // 칸 기하
        void Build()
        {
            NR = geo.NR; NV0 = NR * Cfg.NY; V = NV0 + Cfg.NVENT;
            ventHost = new int[Cfg.NVENT]; ventReg = new int[Cfg.NVENT]; Array.Fill(ventHost, -1);
            live = new bool[V]; vol = new double[V]; thick = new double[V]; zTop = new double[V]; area = new double[NR]; kind = new byte[NR];
            temp = new double[V]; light = new double[V]; dry = new double[V]; vent = new bool[V]; ventMul = new double[V];
            pool = new double[P.N][]; for (int p = 0; p < P.N; p++) pool[p] = new double[V];
            B = new double[Cfg.SMAX * V]; AllocBio();
            for (int r = 0; r < NR; r++)
            {
                area[r] = geo.rArea[r]; kind[r] = geo.rKind[r];
                for (int k = 0; k < Cfg.NY; k++)
                {
                    int v = r * Cfg.NY + k;
                    double top = Cfg.ZE[k], bot = Math.Min(Cfg.ZE[k + 1], geo.rDepth[r]);
                    if (kind[r] != 0) { if (k > 0) continue; top = 0; bot = geo.rDepth[r]; }
                    if (bot <= top) continue;
                    live[v] = true; zTop[v] = top; thick[v] = bot - top; vol[v] = area[r] * thick[v] * 1000;
                    dry[v] = kind[r] == 1 ? 0.3 : kind[r] == 2 ? 0.85 : 0;
                }
            }
            // 열수공: 화산 열점이 있는 해역들의 가장 깊은 칸 + 깊은 바다 몇 곳
            var spotRegs = new HashSet<int>(); foreach (int c in geo.spots) { int r = geo.reg[c]; if (r >= 0) spotRegs.Add(r); }
            for (int r = 0; r < geo.NO; r++) if (geo.rDepth[r] > 2500 && rng.U() < 0.3) spotRegs.Add(r);
            // 얕은 열수(화산섬 둘레·뜨거운 샘): 얕은 바다 해역 두 곳(없으면 조간대)
            var shallow = new List<int>(); for (int r = 0; r < NR; r++) if (kind[r] != 2 && geo.rDepth[r] < 300) shallow.Add(r);
            for (int i = 0; i < 2 && shallow.Count > 0; i++) { int j = (int)(rng.U() * shallow.Count); spotRegs.Add(shallow[j]); shallow.RemoveAt(j); }
            foreach (int r in spotRegs) OpenVent(r);
            nVent0 = Math.Max(1, spotRegs.Count);
        }

        // 열수공 칸 열기: 비어 있는 자리에 만든다(처음엔 빈 물. 질량은 섞임으로만 들어온다)
        public int OpenVent(int r)
        {
            int host = Bottom(r); if (host < 0) return -1;
            for (int i = 0; i < Cfg.NVENT; i++)
            {
                int v = NV0 + i; if (live[v]) continue;
                live[v] = true; vent[v] = true; ventMul[v] = 1; ventHost[i] = host; ventReg[i] = r;
                thick[v] = Cfg.VENT_THICK; zTop[v] = Math.Max(0, zTop[host] + thick[host] - Cfg.VENT_THICK); vol[v] = Cfg.VENT_VOL;
                links = null; return v;
            }
            return -1;
        }

        public int Bottom(int r) { for (int k = Cfg.NY - 1; k >= 0; k--) if (live[r * Cfg.NY + k]) return r * Cfg.NY + k; return -1; }
        public bool IsBottom(int v) => v >= NV0 || v % Cfg.NY == Cfg.NY - 1 || !live[v + 1];

        void Init()
        {
            atmCO2 = Cfg.CO2_PAL0 * Cfg.CO2_PAL_MOL; atmO2 = Cfg.O2_PAL0 * Cfg.O2_PAL_MOL;
            atmCH4 = Cfg.CH4_PPM0 * Cfg.PPM_MOL; atmH2 = Cfg.H2_PPM0 * Cfg.PPM_MOL;
            for (int v = 0; v < V; v++)
            {
                if (!live[v]) continue;
                bool deep = zTop[v] >= 100;
                pool[P.CO2][v] = Cfg.CO2_SAT * Cfg.CO2_PAL0; pool[P.O2][v] = 0;
                pool[P.H2][v] = vent[v] ? 200 : 0.05; pool[P.CH4][v] = 0.2;
                pool[P.FE2][v] = vent[v] ? 200 : deep ? Cfg.FE2_DEEP0 : Cfg.FE2_TOP0; pool[P.FE3][v] = 0;
                pool[P.H2S][v] = vent[v] ? 20 : deep ? 0.5 : 0; pool[P.SO4][v] = Cfg.SO4_0;
                pool[P.NH4][v] = Cfg.NH4_0; pool[P.PO4][v] = Cfg.PO4_0; pool[P.DOC][v] = Cfg.DOC_0;
            }
            Climate();
            // 원시 세포: 열수공 칸에만 아주 조금(1 μm 세포 약 10/mL)
            var g = Genome.Primordial();
            var s = NewStrain(g, -1, -1);
            foreach (int v in Vents()) B[s.slot * V + v] = 10 * 1e3 * s.t.cellC * 1e6;   // 10/mL → μM C
            ledger0 = Ledger();
        }

        public IEnumerable<int> Vents() { for (int v = 0; v < V; v++) if (vent[v]) yield return v; }

        public Strain NewStrain(float[] g, int parent, int gene, Traits t = null, Body b = null)
        {
            int slot = -1; for (int i = 0; i < Cfg.SMAX; i++) if (strains[i] == null) { slot = i; break; }
            if (slot < 0) return null;
            t ??= Traits.Of(g);
            var ps = parent >= 0 ? strains[parent] : null;
            var s = new Strain { id = nextId++, parent = ps?.id ?? -1, slot = slot, born = (int)Day, gene = gene, species = ps?.species ?? -1 };
            SetGenome(s, g, t, b);
            strains[slot] = s; alive.Add(s);
            return s;
        }

        // 계통의 유전체를 바꾸고 거기서 나오는 값(형질·몸·포식 값)을 다시 낸다. 표류로 바뀐 계통은 포식 표(06 PredCache)도 다시 잰다
        public void SetGenome(Strain s, float[] g, Traits t = null, Body b = null)
        {
            s.g = g; s.t = t ?? Traits.Of(g); s.b = b ?? Body.Of(g, s.t);
            s.att = Attack(s.t, s.b); s.vul = Vul(s.t); s.lsz = Math.Log10(s.b.size); s.lfeed = Math.Log10(s.b.feed); s.allo = Allo(s.b);
            if (kOwn != null && s.slot >= 0) kOwn[s.slot] = null;
        }
    }
}
