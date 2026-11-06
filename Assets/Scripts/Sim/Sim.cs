// 진화 수조 3D — 시뮬레이션 코어. Unity API를 쓰지 않는다(dotnet 시험 SimTest와 Unity가 같은 파일을 쓴다).
// 개체 하나하나가 아니라 '계통(strain) × 칸(voxel) × 크기 단계'의 개체 수를 확률적으로 계산한다:
// 출생 ~ 푸아송, 사망·성장·이동 ~ 이항, 포식 = 크기·속도·감각·방어가 정하는 만남(Gerritsen & Strickler 1977) + Holling II.
// 화면에 보이는 생물은 이 개체 수에서 뽑은 표본일 뿐이다.
// 세계: 연못·연안 한 구역 20 m × 20 m × 깊이 10 m (가로 2×2칸, 깊이 0.5 m × 20층), 1틱 = DT시간.
// 원생생물(세포 분열)부터 발생하는 다세포(알→성체 크기 단계)·동물·어류까지 같은 공식으로 계산한다.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Evo
{
    public sealed class GeneDef
    {
        public string k, name; public float lo, hi, init, sd, sc;
        public GeneDef(string k, string name, float lo, float hi, float init, float sd, float sc) { this.k = k; this.name = name; this.lo = lo; this.hi = hi; this.init = init; this.sd = sd; this.sc = sc; }
    }

    public sealed class EnvDef
    {
        public string k, name, unit; public float lo, hi, init;
        public EnvDef(string k, string name, string unit, float lo, float hi, float init) { this.k = k; this.name = name; this.unit = unit; this.lo = lo; this.hi = hi; this.init = init; }
    }

    public static class Gn
    {
        public const int Size = 0, Osmo = 1, Vacuole = 2, Wall = 3, Flagella = 4, Chemo = 5, Eye = 6, Photo = 7, Phago = 8,
            Aerobic = 9, Pigment = 10, Topt = 11, Tbreadth = 12, Mutrate = 13, Elong = 14, Spines = 15, Lorica = 16, Cilia = 17,
            Plastic = 18, Cells = 19, Form = 20, Diff = 21, Attach = 22, Buoy = 23, Gut = 24, Neuro = 25, Bilat = 26, Circ = 27,
            Skel = 28, Egg = 29, N = 30;
        // 키, 이름, 최소, 최대, 시작, 돌연변이 표준편차, 종 거리 척도(0 = 거리에 안 셈)
        public static readonly GeneDef[] Def = {
            new("size", "세포 크기", 1, 8, 3.3f, 0.12f, 0.7f),            // log₂ 지름(μm): 2–256 μm, 시작 10 μm
            new("osmo", "삼투 조절 한계", 3, 320, 40, 8, 40),             // g/L
            new("vacuole", "수축포", 0, 1, 0.1f, 0.05f, 0.25f),
            new("wall", "세포벽", 0, 1, 0, 0.05f, 0.25f),
            new("flagella", "편모", 0, 1, 0.3f, 0.05f, 0.25f),
            new("chemo", "화학 감각", 0, 1, 0.2f, 0.05f, 0.25f),
            new("eye", "광수용·눈", 0, 1, 0, 0.04f, 0.25f),
            new("photo", "광합성", 0, 1, 0, 0.05f, 0.25f),
            new("phago", "식세포 작용", 0, 1, 0.5f, 0.05f, 0.25f),
            new("aerobic", "호기 호흡", 0, 1, 0.3f, 0.05f, 0.25f),
            new("pigment", "광보호 색소", 0, 1, 0.1f, 0.05f, 0.25f),
            new("topt", "최적 온도", -2, 60, 25, 1.8f, 6),
            new("tbreadth", "온도 내성 폭", 3, 25, 8, 0.6f, 4),
            new("mutrate", "돌연변이율", 0.2f, 5, 1, 0.15f, 0),
            new("elong", "길쭉함", -1.5f, 3, 0.3f, 0.12f, 0.6f),          // log₂ 장단축비: 원반 0.35 – 바늘 8
            new("spines", "가시", 0, 1, 0, 0.05f, 0.25f),
            new("lorica", "껍데기(각)", 0, 1, 0, 0.05f, 0.25f),
            new("cilia", "섬모", 0, 1, 0, 0.05f, 0.25f),
            new("plastic", "형태 가소성", 0, 1, 0, 0.05f, 0.25f),          // 포식 신호가 있을 때만 가시·군체를 켬
            new("cells", "몸 세포 수", 0, 36, 0, 0.25f, 1.5f),            // log₂ 세포 수: 1 – 약 700억(수십 g 어류)
            new("form", "몸 모양", 0, 1, 0.5f, 0.06f, 0.3f),              // 0 실 · 0.5 판 · 1 덩어리(공)
            new("diff", "세포 분업", 0, 1, 0, 0.05f, 0.25f),              // 생식-체세포 분화: 0.5 넘고 16세포 넘으면 '발생'(알→성체)
            new("attach", "바닥 부착", 0, 1, 0, 0.05f, 0.25f),
            new("buoy", "부력(기포·부레)", 0, 1, 0, 0.05f, 0.25f),
            new("gut", "소화관·입", 0, 1, 0, 0.05f, 0.25f),              // 세포 밖 소화: 몸 크기 기준으로 큰 먹이
            new("neuro", "신경·근육", 0, 1, 0, 0.05f, 0.25f),
            new("bilat", "좌우대칭·머리", 0, 1, 0, 0.05f, 0.25f),
            new("circ", "순환·아가미", 0, 1, 0, 0.05f, 0.25f),            // 확산 한계를 넘는 산소 운반
            new("skel", "척삭·골격", 0, 1, 0, 0.05f, 0.25f),
            new("egg", "알 크기", 0, 24, 0, 0.3f, 2),                     // log₂ 알(번식체) 세포 수
        };
    }

    public static class Ev
    {
        public const int Temp = 0, Salt = 1, Light = 2, Uv = 3, O2 = 4, Nut = 5, N = 6;
        public static readonly EnvDef[] Def = {
            new("temp", "수온", "°C", -2, 60, 25), new("salt", "염분", "g/L", 0, 300, 35), new("light", "햇빛", "%", 0, 100, 60),
            new("uv", "자외선", "%", 0, 100, 30), new("o2", "대기 산소", "%", 0, 30, 2), new("nutrient", "영양염", "%", 0, 100, 50),
        };
    }

    // 상수. 출처 키는 docs/research-*.md
    public sealed class Params
    {
        public float DT = 2;              // 시간/틱
        public float BASAL = 0.004f;      // 기초 대사(부피 비례, 시간당): 원생생물 대사 ∝ 질량^1, 후생동물 ^0.75 (DeLong 2010)
        public float FIX = 0.0012f;       // 세포당 고정비(핵·유전체): 너무 작은 세포는 손해 (Raven 1998)
        public float FIXB = 0.15f;        // 세포 하나 짓는 비용 중 크기와 무관한 부분(10 μm 세포 = 1)
        public float E_ACT = 7543;        // 0.65 eV / k (Gillooly 2001)
        public float C_OSMO = 0.02f;      // 녹은 유기물 흡수(표면적 비례)
        public float C_PHOTO = 0.12f;     // 광합성(엽록체 부피 비례, package effect로 큰 세포는 효율↓)
        public float KUP = 0.1f;          // 질소·인 흡수 능력(표면적 비례): 광합성은 이것을 넘지 못함
        public float KN = 1;              // 영양염 반포화(μM)
        public float IK = 0.08f;          // 광포화(맑은 날 수면 빛 대비). 호수 Ik 50–200 μmol/m²/s ÷ 한낮 ~2000 (docs/research-water.md)
        public float AER_GAIN = 1.5f, O2_K = 1;
        public float O2_TOX = 0.0048f, UV_DMG = 0.02f, UV_MUT = 3;
        public float OSMO_FLOOR = 3, OSMO_INT = 0.000012f, OSMO_CAP = 0.000004f;
        public float W_IN = 0.0028f, W_OUT = 0.0012f, PUMP = 0.02f, PUMP_COST = 0.25f;
        public float CLR = 1e-4f;         // 세포 수준 청소율(mL/h, 10 μm·식세포 작용 1): 부피^0.77 (Hansen 1997)
        public float M0 = 4e5f;           // 원생생물 선(질량^1)과 후생동물 선(^0.75)이 만나는 질량(10 μm 세포 단위, 4e5 ≈ 0.2 mg). 1 g 어류: 최대 섭식 ~1%/h(Hartvig 2011 h=85), 유지비 ~0.1%/h(k=10), 자연사 ~0.8%/일(Lorenzen) — docs/research-sizeecology.md
        public float MAXING = 0.08f;      // 최대 섭식(10 μm 세포 질량당 시간당), 동물은 질량^0.75
        public float PRED_EFF = 0.4f;     // 포식 총성장효율 상한 (Straile 1997: 관측 20–30%)
        public float BAC_E = 3e-4f;       // 세균 한 마리 에너지(10 μm 세포 = 1, 부피비)
        public float DEATH = 0.004f;      // 모형 밖 포식자·노화 (시간당)
        public float VIR = 0.012f, VIR_K = 2e4f;   // 계통별 바이러스: 흔한 계통일수록 많이 죽는다(kill-the-winner, Thingstad 2000). 시간당 최대, 반포화 밀도(/mL)
        public float SEASON_T = 5, SEASON_L = 0.3f;   // 계절: 수온 ±°C, 햇빛 ±비율 (가을에 식으면 성층이 깨져 바닥까지 섞인다)
        public float TSR = 0.025f;        // 온도-크기 규칙 2.5%/°C (Atkinson 2003)
        public float MUT_U = 1e-3f;       // 출생 1회당 돌연변이 개체가 나올 확률(게임 압축). 세포 수가 많으면 생식세포 분열이 많아 늘어난다(Lynch 2010)
        public float MUT_NEW = 2;         // 한 틱에 새로 기록하는 돌연변이 계통 수(전체). 4로 올리면 계통 칸 경쟁이 심해져 오히려 덜 다양했다(20년 시험)
        public float MUT_ALPHA = 0.25f;   // 계통별 몫 ∝ (출생×돌연변이율)^α — 작으면 드문 계통(큰 생물)도 자주 얻는다. 0.12 이하는 큰 포식자가 먹이보다 빨리 진화해 생태계가 무너진다(20년 시험)
        public float MUT_LUMP = 100;      // 기록된 돌연변이 하나가 대표하는 같은 돌연변이 묶음 크기(게임 압축)
        public float MUT_F0 = 2e-3f;      // 새 돌연변이의 시작 빈도 상한(부모 대비). 실제 거대 집단의 표준 변이(돌연변이-선택 평형 ~u/s)를 흉내
        public float MUT_SD = 2;          // 돌연변이 효과 크기 배율(게임 압축: 실제보다 큰 걸음)
        public float MUT_BIG = 0.1f;      // 효과가 4배 큰 돌연변이 비율
        public float LOAD = 0.01f;        // 해로운 돌연변이로 죽는 자손 비율(돌연변이율 1일 때)
        public float SPEC_D = 1.6f;       // 종 분화 거리
        public float MUT_FLOOR = 1f / 120;   // 다세포(세포 4개 이상) 계통은 몫이 작아도 열흘에 한 번은 돌연변이를 얻는다. 게임 압축: 진화의 앞줄을 빠르게(단세포까지 주면 거대 포식 세포가 생태계를 무너뜨렸다)
        public float MERGE_D = 0.5f;      // 계통 칸이 모자라면 이 거리 안의 줄어드는 계통을 더 많은 쪽에 합친다(유전자 한 걸음 ≈ 0.4)
        public float KW = 0.25f, KCHL = 2.4e-5f, KUV = 1.2f;   // 물·엽록소·자외선 감쇠(m⁻¹)
        public float QN = 0.83e-3f;       // 10 μm 세포 1개/mL가 품은 질소(μM) (Menden-Deuer & Lessard 2000, C:N 6.6)
        public float KSED = 0.02f, NSED = 40;   // 바닥 퇴적물이 공급하는 영양염: 영양염 100% = 40 μM
        public float MIX_ML = 14f, MIX_TC = 0.01f, MIX_DEEP = 0.03f, MIX_H = 0.05f;   // 칸 사이 교환 비율(시간당) = Kz/DZ²: 혼합층 Kz ~1e-3, 수온약층 ~1e-6, 심층 ~2e-6 m²/s (research-water)
        public float KEX = 0.08f;         // 수면 산소 교환(피스톤 속도 ~1 m/day ÷ 0.5 m 층)
        public float KO2 = 4.8e-4f, KDEC = 0.03f / 24;       // 에너지 1(10 μm 세포 1개분, 탄소 ~6 pmol)/mL ↔ 용존 산소(대기 % 환산, 20°C 포화 280 μM)
    }

    public sealed class Strain
    {
        public int id, slot, sp, parent, K; public float[] g; public long born; public double gen, total, peak, kills;
        public float hue; public bool dev; public double trend, prevTot;
        // 유전형만으로 정해지는 모양 값
        public float esd0, aspect, fA, longAx, thinAx, rho;
        public float[] C = new float[World.KM];   // 단계별 세포 수(발생하지 않으면 단계 1개)
        public double[] stage = new double[World.KM];   // 단계별 개체 수 합(Totals)
    }

    public sealed class Species
    {
        public int id, parent; public float[] rep; public long born, ext = -1; public double total, peak, kills;
        public string name, ko; public float hue; public bool named; public int grade;
    }

    // 칸 하나에서 계통 하나·단계 하나의 실제 모양(가소성·온도 반영). 화면이 그릴 때도 쓴다.
    public struct Morph
    {
        public float perf, kt, esd, cellL, spines, C, M, D, L, Leff, thick, expo, pcrit, speed, sink, pe, gapeC, lnOptC, gapeB, Rs, Fc, org;
        public bool sessile;
    }

    public struct Rng
    {
        ulong s;
        public Rng(ulong seed) { s = seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL; Next(); }
        public ulong Next() { ulong z = (s += 0x9E3779B97F4A7C15UL); z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); }
        public double U() => (Next() >> 11) * (1.0 / 9007199254740992.0);
        public double Gauss() { double u = 1 - U(), v = U(); return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v); }
        public double Poisson(double l)
        {
            if (l <= 0) return 0;
            if (l < 30) { double L = Math.Exp(-l), p = 1; int k = 0; do { k++; p *= U(); } while (p > L); return k - 1; }
            return Math.Max(0, Math.Round(l + Math.Sqrt(l) * Gauss()));
        }
        public double Binom(double n, double p)
        {
            if (n <= 0 || p <= 0) return 0; if (p >= 1) return n;
            if (n < 25) { int k = 0; for (int i = 0; i < n; i++) if (U() < p) k++; return k; }
            double m = n * p;
            if (m < 30) return Math.Min(n, Poisson(m));
            if (n - m < 30) return n - Math.Min(n, Poisson(n - m));
            return Math.Clamp(Math.Round(m + Math.Sqrt(m * (1 - p)) * Gauss()), 0, n);
        }
    }

    public sealed class World
    {
        public const int NX = 2, NZ = 2, NY = 20, LS = NX * NZ, V = LS * NY, KM = 8;   // 가로 2×2, 깊이 20층, 크기 단계 최대 8(16·2배 간격도 시험: 동물 탐침 결과 같음, 계산만 늘어 8로)
        public const float DX = 10f, DZ = 0.5f, DEPTH = NY * DZ, WIDTH = NX * DX;     // m
        public const double VML = DX * DX * DZ * 1e6;                                 // 칸 부피(mL) = 50 m³
        public const int SMAX = 256, NI = 1000;
        public static readonly string[] GradeName = { "원생생물", "군체", "다세포 조류", "해면형", "자포형", "벌레형", "척삭동물형", "어류" };

        public Params P = new();
        public int seed; public long tick; public float[] env = new float[Ev.N], target = new float[Ev.N];
        public float rate = 0.0001f;      // 환경 변화 속도(범위 대비 틱당). 천천히 바꿔야 진화적 구조가 일어난다
        public bool parallel = true;

        public readonly double[] cnt = new double[SMAX * V * KM];   // 계통 × 칸 × 단계 개체 수(실제 개체 수)
        public readonly float[] mu = new float[SMAX * V * KM];      // 지난 틱 성장률(/h) — 주화성·화면용
        public readonly float[] cue = new float[SMAX * V];          // 포식 신호 0–1 (가소성)
        public readonly float[] gP = new float[SMAX * V * KM], gB = new float[SMAX * V * KM], kR = new float[SMAX * V * KM];   // 지난 틱 포식·세균 섭식 이득, 잡아먹힘 사망률(화면·시험용)
        public readonly double[] nut = new double[V], bac = new double[V], chl = new double[V], dox = new double[V], o2d = new double[V];
        public readonly float[] T = new float[NY], I = new float[V], U = new float[NY], mixV = new float[NY];
        public readonly Strain[] strains = new Strain[SMAX];
        public readonly List<int> live = new();
        public readonly Dictionary<int, Species> species = new();
        public readonly List<(long t, string type, string text)> events = new();
        public readonly HashSet<int> gradesSeen = new();
        public double totalCount, totalBio, lostN, births, deaths, kills;
        public readonly History hist = new();
        public readonly double[] prof = new double[6];   // 단계별 누적 ms(캐시·칸·이동·화학·돌연변이·합계)
        int nextId = 1, nextSp = 1;
        readonly double[] vBirth = new double[V], vDeath = new double[V], vKill = new double[V], dep = new double[V];
        readonly double[] depS = new double[SMAX * LS], genAcc = new double[SMAX * V], killAcc = new double[SMAX * V];
        readonly ThreadLocal<Scratch> scratch = new(() => new Scratch());
        readonly Morph[] mc = new Morph[SMAX * NY * KM];   // 틱마다 계통 × 층 × 단계 모양 캐시(가소성 없는 계통)

        // 모양: 가소성이 있으면 칸마다(포식 신호), 없으면 층 캐시
        public Morph M(int s, int v, int k) => strains[s].g[Gn.Plastic] > 0.02f ? MorphAt(strains[s], v, k, cue[s * V + v]) : mc[(s * NY + v / LS) * KM + k];
        readonly bool[] pres = new bool[SMAX * V * KM];    // 틱 시작 때 있었나(이웃 칸 확인용 — 병렬 칸 계산이 서로의 개체 수 변화를 읽지 않게)
        void Cache(int s)
        {
            var st = strains[s]; for (int y = 0; y < NY; y++) for (int k = 0; k < st.K; k++) mc[(s * NY + y) * KM + k] = MorphAt(st, y * LS, k, 0);
            for (int q = Ix(s, 0, 0), e = q + V * KM; q < e; q++) pres[q] = cnt[q] > 0;
        }

        sealed class Scratch
        {
            public int[] idx = new int[NI], stg = new int[NI], ord = new int[NI]; public float[] key = new float[NI]; public Morph[] m = new Morph[NI];
            public double[] gainP = new double[NI], gainB = new double[NI], kill = new double[NI], risk = new double[NI];
            public int[] pj = new int[NI]; public double[] pw = new double[NI];
            public double[] dD = new double[NI], dUp = new double[NI], dB = new double[NI], need = new double[NI], aff = new double[NI], ing = new double[NI], eff = new double[NI];
            public double[] B = new double[14]; public double[] row = new double[V * KM], rowB = new double[V];
        }

        public World(int seed = 1, float[] envInit = null) { Reset(seed, envInit); }

        public void Reset(int seed, float[] envInit = null)
        {
            this.seed = seed; tick = 0; nextId = 1; nextSp = 1;
            for (int e = 0; e < Ev.N; e++) env[e] = target[e] = envInit != null ? envInit[e] : Ev.Def[e].init;
            Array.Clear(cnt, 0, cnt.Length); Array.Clear(mu, 0, mu.Length); Array.Clear(cue, 0, cue.Length); Array.Clear(strains, 0, SMAX);
            live.Clear(); species.Clear(); events.Clear(); hist.Clear(); gradesSeen.Clear(); gradesSeen.Add(0);
            lostN = 0; Array.Clear(dep, 0, V);
            for (int v = 0; v < V; v++) { nut[v] = P.NSED * env[Ev.Nut] / 100 * 0.6; bac[v] = 1e6; chl[v] = 0; dox[v] = env[Ev.O2]; }
            var g0 = new float[Gn.N]; for (int i = 0; i < Gn.N; i++) g0[i] = Gn.Def[i].init;
            var sp = NewSpecies(g0, 0); sp.name = "Protomonas primigenia"; sp.ko = "원시 편모충 (공통 조상)"; sp.named = true;
            var s = NewStrain(g0, sp.id, 0, 0);
            var r = new Rng((ulong)seed);
            for (int v = 0; v < V; v++) if (v / LS < 12) cnt[Ix(s.slot, v, 0)] = Math.Round(VML * (400 + 200 * r.U()));   // 400–600 마리/mL
            Log("start", "원시 바다에 공통 조상 원생생물이 퍼져 있습니다 (mL당 약 500마리).");
            Layers(); Totals();
        }

        public static int Ix(int s, int v, int k) => (s * V + v) * KM + k;
        public void Log(string type, string text) { events.Add((tick, type, text)); if (events.Count > 400) events.RemoveAt(0); }

        Species NewSpecies(float[] g, int parent)
        {
            var sp = new Species { id = nextSp++, parent = parent, rep = (float[])g.Clone(), born = tick, hue = (nextSp * 137.508f) % 360 };
            species[sp.id] = sp; return sp;
        }

        Strain NewStrain(float[] g, int sp, int parent, double gen)
        {
            int slot = -1;
            for (int i = 0; i < SMAX; i++) if (strains[i] == null) { slot = i; break; }
            if (slot < 0) return null;
            var s = new Strain { id = nextId++, slot = slot, sp = sp, parent = parent, g = g, born = tick, gen = gen, hue = (nextId * 97.3f) % 360 };
            Shape(s); strains[slot] = s; live.Add(slot);
            return s;
        }

        // 유전형만으로 정해지는 모양: 같은 부피의 회전타원체(장단축비 a)의 겉넓이(Thomsen 근사)와 긴·짧은 축, 밀도, 발생 단계
        public static void Shape(Strain s)
        {
            var g = s.g;
            s.esd0 = MathF.Pow(2, g[Gn.Size]); s.aspect = MathF.Pow(2, g[Gn.Elong]);
            double a = Math.Pow(s.aspect, 2.0 / 3), b = Math.Pow(s.aspect, -1.0 / 3), p = 1.6075;
            s.fA = (float)Math.Pow((2 * Math.Pow(a * b, p) + Math.Pow(b * b, p)) / 3, 1 / p);
            s.longAx = (float)Math.Max(a, b); s.thinAx = (float)Math.Min(a, b);
            // 밀도(kg/m³): 세포질 ~1050, 규산·석회 각과 뼈는 무겁고, 기포·기름·부레는 가볍다 (Walsby 1994)
            s.rho = 1050 + 300 * g[Gn.Lorica] + 40 * g[Gn.Wall] + 60 * g[Gn.Skel] - 90 * g[Gn.Buoy];
            // 발생: 생식-체세포 분화(diff>0.5)가 있고 16세포 이상이면 한 세포(알)에서 자라는 개체 → 크기 단계(질량 10배마다)
            float cA = g[Gn.Cells] >= 4 && g[Gn.Diff] >= 0.5f ? MathF.Pow(2, g[Gn.Cells]) : MathF.Pow(2, MathF.Floor(g[Gn.Cells]));
            s.dev = g[Gn.Cells] >= 4 && g[Gn.Diff] >= 0.5f;
            if (!s.dev) { s.K = 1; s.C[0] = cA; return; }
            float c0 = MathF.Pow(2, MathF.Min(g[Gn.Egg], g[Gn.Cells] - 1));
            s.K = Math.Clamp((int)MathF.Ceiling(MathF.Log(cA / c0, 5)) + 1, 2, KM);
            for (int k = 0; k < s.K; k++) s.C[k] = c0 * MathF.Pow(cA / c0, k / (float)(s.K - 1));
        }

        public static int Grade(Strain s)
        {
            var g = s.g;
            if (g[Gn.Cells] < 1) return 0;
            if (!s.dev) return 1;
            bool animal = g[Gn.Gut] > 0.5f || g[Gn.Neuro] > 0.5f;
            if (!animal) return g[Gn.Photo] > 0.3f ? 2 : g[Gn.Attach] > 0.5f && g[Gn.Phago] > 0.3f ? 3 : 1;
            if (g[Gn.Bilat] < 0.5f) return g[Gn.Attach] > 0.5f && g[Gn.Gut] < 0.5f ? 3 : 4;
            if (g[Gn.Skel] < 0.4f) return 5;
            float adultL = s.esd0 * MathF.Pow(s.C[s.K - 1], 1 / 3f) * s.longAx;   // μm
            bool fish = g[Gn.Circ] > 0.5f && g[Gn.Eye] > 0.5f && g[Gn.Neuro] > 0.5f && g[Gn.Skel] > 0.6f && g[Gn.Attach] < 0.5f && g[Gn.Form] > 0.7f && adultL >= 1e4;
            return fish ? 7 : 6;
        }

        public static float ThermalPerf(float[] g, float t)
        {
            // 최적 온도 위쪽이 더 가파름(Huey & Kingsolver 1989). 폭이 넓으면 정점이 낮다
            float d = t - g[Gn.Topt], w = d > 0 ? g[Gn.Tbreadth] * 0.6f : g[Gn.Tbreadth];
            return MathF.Pow(8 / g[Gn.Tbreadth], 0.2f) * MathF.Exp(-(d / w) * (d / w));
        }

        public float Kt(float t) => Math.Clamp(MathF.Exp(P.E_ACT * (1 / 293.15f - 1 / (t + 273.15f))), 0.25f, 4);

        // 칸 v에서 계통 s·단계 k의 실제 모양. q = 포식 신호(가소성)
        public Morph MorphAt(Strain s, int v, int k, float q)
        {
            var g = s.g; Morph m;
            int y = v / LS; float t = T[y]; m.perf = ThermalPerf(g, t); m.kt = Kt(t);
            float plastic = g[Gn.Plastic], ind = 1 - plastic + plastic * q;      // 유도 방어: 신호 없으면 덜 만든다
            m.esd = s.esd0 * MathF.Cbrt(Math.Clamp(MathF.Exp(-P.TSR * (t - 15)), 0.5f, 1.6f));
            m.spines = g[Gn.Spines] * ind;
            // 포식 신호 → 군체 (Scenedesmus + Daphnia 카이로몬). 발생하는 생물은 단계별 세포 수가 정해져 있다
            m.C = s.dev ? s.C[k] : MathF.Floor(MathF.Pow(2, MathF.Floor(g[Gn.Cells]) + MathF.Round(2 * plastic * q)));
            float v10 = m.esd * m.esd * m.esd * 1e-3f;
            m.M = m.C * v10;
            m.D = 1 + 2 * g[Gn.Form];
            float cellL = m.cellL = m.esd * s.longAx;
            if (m.C <= 1) m.L = cellL;
            else if (s.dev && m.D > 2.4f) m.L = m.esd * MathF.Pow(m.C, 1 / 3f) * s.longAx;            // 덩어리 몸: 길쭉함이 몸 모양
            else m.L = m.esd * MathF.Pow(m.C, 1 / m.D) * (m.D < 1.5f ? 1 : 1.1f);
            m.Leff = (m.C <= 1 ? m.esd * MathF.Sqrt(s.longAx) : m.L) * (1 + 1.5f * m.spines);   // 가시는 포식자 입 크기 기준 몸을 키운다
            // 바깥에 닿은 세포 비율: 실·판은 전부, 공·덩어리는 겉만 (얇은 판은 커져도 확산 한계가 없다)
            m.expo = m.C <= 1 || m.D <= 2 ? 1 : 1 + (MathF.Min(1, 4.8f * MathF.Pow(m.C, -1 / 3f)) - 1) * (m.D - 2);
            // 산소: 확산 거리(반두께 h)가 길면 안쪽이 모자란다. 순환·아가미는 유효 확산 거리를 줄인다.
            // 순환·아가미는 유효 확산 거리를 줄인다. 활동(신경·근육)이 많으면 필요량↑ → 임계 산소 Pcrit (대기 % 환산)
            float h = m.C <= 1 || m.D <= 2 ? m.esd * s.thinAx / 2 * (m.C > 1 ? 1 + (m.D - 1) : 1)
                : m.esd * s.thinAx / 2 * (1 + (m.D - 2) * (MathF.Pow(m.C, 1 / 3f) - 1));
            m.thick = 2 * h;
            float he = h / (1 + 300 * g[Gn.Circ] * g[Gn.Circ]);
            // 확산만으로 사는 몸은 두께 ~1 mm에서 산소 ~1% atm(≈5% PAL)이 필요하다(Catling 2005). 순환·아가미가 있으면 아가미 확산이 문턱을 정한다:
            // 어류 Pcrit ~3% atm(LC50 1.5 mg/L). 신경·근육(활동)이 많으면 수요↑
            m.pcrit = (4e-8f * he * he + 2 * g[Gn.Circ]) * (1 + g[Gn.Neuro]);
            m.sessile = g[Gn.Attach] > 0.5f && (!s.dev || k > 0);                 // 발생하는 부착 생물은 유생(단계 0)만 떠다닌다
            m.org = Math.Clamp(g[Gn.Circ] + g[Gn.Neuro], 0, 1) * (s.dev ? 1 : 0);
            // 헤엄: 편모 추진은 크기와 거의 무관, 섬모는 표면적에 비례해 큰 세포에서 빨라진다 (Fenchel 1987).
            // 신경·근육이 있으면 몸길이/초로 헤엄(어류 1–3 BL/s), 척삭·골격·좌우대칭이 효율을 올림
            float drag = 1 + 0.1f * MathF.Abs(g[Gn.Elong]) + 0.8f * m.spines + 0.3f * g[Gn.Lorica];
            float sp = (150 * g[Gn.Flagella] + 250 * g[Gn.Cilia] * MathF.Pow(m.esd / 10, 0.8f)) / drag / (1 + 0.5f * g[Gn.Wall]);
            if (m.C > 1) sp *= MathF.Pow(m.C, -0.1f) * (1 + 0.5f * g[Gn.Diff]);
            float muscle = s.dev ? 2 * g[Gn.Neuro] * (0.3f + 0.7f * g[Gn.Skel]) * (0.4f + 0.6f * g[Gn.Bilat]) * m.L : 0;
            m.speed = m.sessile ? 0 : MathF.Max(sp, muscle);                      // μm/s
            // 침강(Stokes, m/h): v = Δρ g Vol / (6π μ r_drag Φ). 형태 저항 Φ: 길쭉함·가시·실 모양이 올린다 (Padisák 2003)
            float rhoW = 1000 + 0.76f * env[Ev.Salt], visc = 1.79e-3f * MathF.Exp(-0.0235f * t);
            float vol = m.C * MathF.PI / 6 * m.esd * m.esd * m.esd;             // μm³
            float rD;
            if (m.C <= 1) rD = m.esd / 2;
            else
            {
                float fil = 2f / 3 * m.L / (MathF.Log(MathF.Max(2, m.L / m.esd)) + 0.5f), sheet = 0.85f * m.L / 2, ball = MathF.Cbrt(vol * 6 / MathF.PI) / 2;
                rD = m.D < 2 ? fil + (sheet - fil) * (m.D - 1) : sheet + (ball - sheet) * (m.D - 2);
            }
            float phi = 1 + 0.25f * MathF.Abs(g[Gn.Elong]) + 1.2f * m.spines;
            float sink = (s.rho - rhoW) * 9.81f * vol * 1e-18f / (6 * MathF.PI * visc * rD * 1e-6f * phi), vS = sink;   // m/s
            for (int it = 0; it < 3; it++) sink = vS / (1 + 0.15f * MathF.Pow(MathF.Abs(sink) * 2 * rD * 1e-6f * rhoW / visc, 0.687f));   // 큰 몸(Re > 1)은 Stokes보다 느리게 가라앉는다 (Schiller–Naumann)
            sink *= 3600;
            m.sink = m.sessile ? 0 : Math.Clamp(sink, -50, 50);
            m.pe = g[Gn.Phago] * (1 - g[Gn.Wall]) * (1 - g[Gn.Wall]);           // 세포벽은 식세포 작용을 막는다
            // 세포 수준 섭식(식세포 작용·여과): 입 크기와 좋아하는 먹이 크기 (Hansen 1994: 와편모류 1:1, 편모충 3:1, 섬모충 8:1)
            m.gapeC = m.esd * s.longAx * (0.45f + 0.6f * m.pe);
            float engulf = Math.Clamp((m.pe - 0.7f) / 0.3f, 0, 1);
            m.lnOptC = 1.0986f + 0.98f * g[Gn.Cilia] - 1.0986f * engulf;
            // 세포 수준 청소율: 섭식류(편모·섬모가 만드는 물살). 군체·해면은 겉 세포가 모두 거른다
            m.Fc = m.pe < 0.02f ? 0 : P.CLR * m.pe * MathF.Pow(m.esd / 10, 2.3f) * (0.5f + sp / 150) * MathF.Pow(m.C, 0.85f) * m.expo;
            // 몸 수준 섭식(소화관): 입 = 몸길이의 ~1/3, 감지 거리 = 기계 감각(신경) + 눈(빛이 있어야)
            m.gapeB = s.dev ? m.L * 0.33f * g[Gn.Gut] : 0;
            float lf = I[v] / (I[v] + 0.05f);
            m.Rs = m.L * (0.3f + 0.4f * g[Gn.Neuro] + 0.8f * g[Gn.Eye] * g[Gn.Eye] * lf * g[Gn.Neuro]);   // 1 g 어류 탐색 부피 ~1 m³/h (Hartvig 2011)
            return m;
        }

        // ---------- 환경 ----------
        public void SetTarget(int e, float v) => target[e] = v;

        void MoveEnv()
        {
            for (int e = 0; e < Ev.N; e++)
            {
                float cur = env[e], tg = target[e]; if (cur == tg) continue;
                float st = rate >= 1 ? float.MaxValue : (Ev.Def[e].hi - Ev.Def[e].lo) * rate * P.DT;
                env[e] = MathF.Abs(tg - cur) <= st ? tg : cur + MathF.Sign(tg - cur) * st;
            }
        }

        // 층마다 수온(혼합층 0–3 m, 수온약층 3–5 m), 자외선, 섞임, 빛(엽록소 그늘)
        void Layers()
        {
            float sea = MathF.Sin(2 * MathF.PI * (tick * P.DT / 24f / 365f - 0.25f));   // 0일 = 봄, 1/4년 = 한여름
            float ts = env[Ev.Temp] + P.SEASON_T * sea, td = MathF.Min(ts, MathF.Max(4, env[Ev.Temp] - P.SEASON_T));   // 깊은 물 = 지난겨울 섞일 때의 수온 → 겨울엔 뒤집힘(산소 공급)
            float strat = Math.Clamp((ts - td) / 4, 0, 1);                       // 겉과 바닥 수온 차가 작으면(가을·겨울) 성층이 깨져 바닥까지 섞인다
            for (int y = 0; y < NY; y++)
            {
                float z = (y + 0.5f) * DZ;
                T[y] = z < 3 ? ts : z < 5 ? ts + (td - ts) * (z - 3) / 2 : td;
                U[y] = env[Ev.Uv] / 100 * MathF.Exp(-P.KUV * z);
                float zi = (y + 1) * DZ;                                          // y와 y+1 사이 경계 깊이
                mixV[y] = zi < 3 ? P.MIX_ML : zi <= 5 ? P.MIX_ML * (1 - strat) + P.MIX_TC * strat : P.MIX_ML * (1 - strat) + P.MIX_DEEP * strat;
            }
            float L0 = env[Ev.Light] / 100 * (1 + P.SEASON_L * sea);
            for (int c = 0; c < LS; c++)
            {
                double od = 0;
                for (int y = 0; y < NY; y++)
                {
                    int v = y * LS + c; double k = (P.KW + P.KCHL * chl[v]) * DZ;
                    I[v] = (float)(L0 * Math.Exp(-(od + k / 2)));
                    od += k;
                }
            }
        }

        // ---------- 한 틱 ----------
        public void Step()
        {
            tick++;
            MoveEnv(); Layers();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (parallel) Parallel.ForEach(live, Cache); else foreach (int s in live) Cache(s);
            prof[0] += sw.Elapsed.TotalMilliseconds; sw.Restart();
            if (parallel) Parallel.For(0, V, Voxel); else for (int v = 0; v < V; v++) Voxel(v);
            prof[1] += sw.Elapsed.TotalMilliseconds; sw.Restart();
            if (parallel) Parallel.ForEach(live, Move); else foreach (int s in live) Move(s);
            prof[2] += sw.Elapsed.TotalMilliseconds; sw.Restart();
            Chem();
            prof[3] += sw.Elapsed.TotalMilliseconds; sw.Restart();
            Mutants();
            prof[4] += sw.Elapsed.TotalMilliseconds; sw.Restart();
            Totals();
            prof[5] += sw.Elapsed.TotalMilliseconds;
            if (tick % 24 == 0) Daily();
        }

        // 칸 하나: 포식·섭식 → 에너지 수지 → 성장·출생·사망 (다른 칸에 쓰지 않으므로 병렬)
        void Voxel(int v)
        {
            var S = scratch.Value; var r = new Rng(((ulong)seed << 40) ^ ((ulong)tick << 12) ^ (ulong)v);
            int n = 0, y = v / LS; const int LK = LS * KM;
            float t = T[y], kt = Kt(t), DT = P.DT;
            foreach (int s in live)
            {
                var st = strains[s]; int b = Ix(s, v, 0);
                for (int k = 0; k < st.K && n < NI; k++)
                {
                    int q = b + k;
                    bool here = pres[q], nb = (y > 0 && pres[q - LK]) || (y < NY - 1 && pres[q + LK]);
                    if (!here && !nb) { mu[q] = 0; continue; }
                    S.idx[n] = s; S.stg[n] = k; S.m[n] = M(s, v, k);
                    S.gainP[n] = S.gainB[n] = S.kill[n] = S.risk[n] = S.eff[n] = 0; n++;
                }
            }
            vBirth[v] = vDeath[v] = vKill[v] = 0;
            if (n == 0) return;
            // 먹이 후보(이 칸에 있는 것)를 크기 순으로
            int np = 0;
            for (int i = 0; i < n; i++) if (cnt[Ix(S.idx[i], v, S.stg[i])] > 0) { S.ord[np] = i; S.key[np] = S.m[i].Leff; np++; }
            Array.Sort(S.key, S.ord, 0, np);
            float o2s = (float)(dox[v] / (dox[v] + P.O2_K));
            double bacEat = 0;
            for (int i = 0; i < n; i++)
            {
                ref var mi = ref S.m[i];
                if (mi.Fc <= 0 && mi.gapeB <= 0) continue;
                var gi = strains[S.idx[i]].g;
                double nI = cnt[Ix(S.idx[i], v, S.stg[i])];
                double imax = P.MAXING * mi.M * kt * Math.Pow(Math.Max(1, mi.M / P.M0), -0.25 * mi.org);
                // 세균(0.6 μm): 6 μm 안팎 세포가 가장 잘 먹고, 섬모(여과 섭식)는 큰 세포도 세균을 먹게 해 준다
                double lb = Math.Log(mi.esd / 6) / (2.2 + 2 * gi[Gn.Cilia]);
                double fb = mi.Fc * Math.Exp(-lb * lb) * bac[v] / (bac[v] + 2e5);
                double food = fb * bac[v] * P.BAC_E;
                double lo = Math.Min(mi.Fc > 0 ? mi.cellL / Math.Exp(mi.lnOptC + 2.5) : 1e30, mi.gapeB > 0 ? mi.L / 400 : 1e30);
                double hi = Math.Max(mi.Fc > 0 ? mi.gapeC : 0, mi.gapeB);
                int p0 = LowerBound(S.key, np, (float)lo), cnt2 = 0;
                double vi = mi.speed;
                for (int p = p0; p < np && S.key[p] <= hi; p++)
                {
                    int j = S.ord[p];   // 같은 계통도 먹는다(동족 포식) — 빼면 같은 크기의 돌연변이만 부모에게 잡아먹히는 허상이 생긴다
                    ref var mj = ref S.m[j];
                    if (mj.sessile && !mi.sessile && vi < 1 && mi.gapeB <= 0) continue;
                    double F = 0, pref = 0;
                    if (mi.Fc > 0 && mj.Leff <= mi.gapeC)
                    {
                        double lr = Math.Log(mi.cellL / mj.Leff) - mi.lnOptC, pc = Math.Exp(-lr * lr / 0.8);
                        if (pc > 1e-4) { F = mi.Fc; pref = pc; }
                    }
                    if (mi.gapeB > 0 && mj.Leff <= mi.gapeB)
                    {
                        double lr = Math.Log(mi.L / mj.Leff) - 1.5, pb = Math.Exp(-lr * lr / 1.2);   // 포식자:먹이 길이비 ~4.5 (질량비 ~100)
                        if (pb > 1e-4)
                        {
                            double R = (mi.Rs + (mi.L + mj.L) / 2) * 1e-4, vr = Math.Sqrt(vi * vi + mj.speed * mj.speed) * 0.36 + 0.36 * 20;
                            double Fb = Math.PI * R * R * vr * kt;                             // 만남 부피(mL/h) = π R² v_rel (Gerritsen & Strickler 1977)
                            if (Fb * pb > F * pref) { F = Fb; pref = pb; }
                        }
                    }
                    if (pref <= 0) continue;
                    var gj = strains[S.idx[j]].g;
                    double esc = 1 / (1 + 0.5 * mj.speed / (vi + 30)) * (1 - 0.5 * gj[Gn.Eye] * gj[Gn.Neuro]);   // 빠르고 보는 먹이는 잘 도망친다
                    double dens = cnt[Ix(S.idx[j], v, S.stg[j])] / VML;
                    double w = F * pref * esc * dens;
                    S.pj[cnt2] = j; S.pw[cnt2] = w; cnt2++;
                    food += w * mj.M;
                    if (nI > 0) S.risk[j] += nI / VML * F * pref;                     // 포식 신호(카이로몬)
                }
                double sat = 1 / (1 + food / Math.Max(imax, 1e-12));                // Holling II: 소화 시간
                double aer = 1 + P.AER_GAIN * gi[Gn.Aerobic] * o2s * Math.Min(1, dox[v] / Math.Max(1e-3, mi.pcrit)) + 0.4 * (1 - gi[Gn.Aerobic]) * (1 - o2s);
                double eff = P.PRED_EFF * Math.Min(1, aer / 2.5) * mi.perf; S.eff[i] = eff;
                double bEat = fb * bac[v] * sat;
                S.gainB[i] = bEat * P.BAC_E * eff;
                bacEat += bEat * nI / VML;
                for (int p = 0; p < cnt2; p++)
                {
                    int j = S.pj[p]; var gj = strains[S.idx[j]].g; ref var mj = ref S.m[j];
                    double eat = S.pw[p] * sat;
                    double digest = (1 - 0.7 * gj[Gn.Lorica]) * (1 - 0.3 * gj[Gn.Wall]) * (1 - 0.4 * gj[Gn.Skel] * (mi.gapeB > 0 ? 1 : 0));   // 각·벽·골판: 삼켜도 살아남음
                    S.gainP[i] += eat * digest * mj.M * eff;
                    if (nI > 0) S.kill[j] += nI * eat * digest / cnt[Ix(S.idx[j], v, S.stg[j])];
                }
            }
            if (bacEat > 0) { double e = Math.Min(bac[v], bacEat * DT); bac[v] -= e; nut[v] += e * 2.9e-7; }
            // 에너지 수지 → 성장·출생·사망
            double pool = nut[v], born = 0, died = 0, killed = 0, o2c = 0, o2p = 0, needW = 0;
            float uv = U[y];
            for (int i = 0; i < n; i++)
            {
                int s = S.idx[i], k = S.stg[i], q = Ix(s, v, k); var st = strains[s]; ref var m = ref S.m[i];
                double E = Budget(st, v, ref m, S.gainP[i], S.gainB[i], S.B);
                double cellB = m.esd * m.esd * m.esd * 1e-3 + P.FIXB, Mb = m.C * cellB;   // 이 단계 개체 하나를 짓는 비용
                float mm = (float)(E / Mb);
                // 저산소: 산소가 임계(Pcrit)의 2배보다 낮으면 근육·신경 조직이 버티지 못한다 (어류 LC50 ~1.5 mg/L, 포식자는 0.5 mg/L 아래에서 사라짐)
                double hd = m.org > 0 ? 0.05 * st.g[Gn.Neuro] * Math.Max(0, 1 - dox[v] / Math.Max(1e-3, 2 * m.pcrit)) : 0;
                mu[q] = (float)(mm - hd); gP[q] = (float)S.gainP[i]; gB[q] = (float)S.gainB[i]; kR[q] = (float)S.kill[i];
                if (k == 0) cue[s * V + v] = (float)(S.risk[i] / (S.risk[i] + 0.02));
                S.dD[i] = S.dUp[i] = S.dB[i] = S.need[i] = S.ing[i] = 0;
                double c = cnt[q]; if (c <= 0) continue;
                o2c += c * S.B[12] * (0.3 + 0.7 * st.g[Gn.Aerobic] * o2s); o2p += c * S.B[1] * 1.3;
                double uvm = 1 + P.UV_MUT * uv * (1 - 0.9 * st.g[Gn.Pigment]);
                double load = P.LOAD * st.g[Gn.Mutrate] * uvm;
                double sizeM = Math.Pow(Math.Max(1, m.M / P.M0), -0.25 * m.org);           // 큰 동물은 바깥 사망률이 낮다(질량^−0.25)
                double dens = c / VML, vir = P.VIR * dens / (dens + P.VIR_K) * Math.Min(kt, 1.5);
                double dr = P.DEATH * Math.Min(kt, Math.Sqrt(kt)) * sizeM + vir + (mm < 0 ? -2 * mm : 0) + S.kill[i] + hd;
                double d = r.Binom(c, 1 - Math.Exp(-dr * DT));
                killed += dr > 0 ? d * S.kill[i] / dr : 0;
                double nInd = Mb * P.QN / VML;                                       // 개체 하나가 품은 영양염(μM)
                double up = 0, bth = 0, c0b = st.C[0] * cellB * P.QN / VML;
                if (E > 0)
                {
                    if (!st.dev) bth = r.Poisson(c * mm * DT * (1 - load));         // 분열
                    else if (k < st.K - 1)
                    {
                        double gamma = mm / (st.C[k + 1] / st.C[k] - 1);              // 다음 단계로 넘어가는 속도. 단계 안 개체는 C_k로 세므로 번 만큼만 자라야 질량이 보존된다(ln 비로 하면 질량이 저절로 생겨 20년 뒤 폭주). 단계 5배 간격: 실제보다 (r−1)/ln r ≈ 2.5배 늦게 자란다
                        up = r.Binom(c - d, 1 - Math.Exp(-gamma * DT));
                    }
                    else bth = r.Poisson(c * E * 0.9 / (st.C[0] * cellB) * DT * (1 - load));   // 성체: 알·번식체
                }
                double need = bth * c0b + (st.dev && k < st.K - 1 ? c * Math.Max(0, E) * DT * P.QN / VML : 0);
                pool += d * nInd;
                S.dD[i] = d; S.dUp[i] = up; S.dB[i] = bth; S.need[i] = need;
                S.aff[i] = 10 * st.fA / m.esd * m.expo;                              // 표면적:부피가 큰 쪽이 영양염을 먼저 잡는다(R* 이론, Tilman 1982)
                // 먹이를 먹는 생물은 먹이의 질소로 몸을 짓는다(남는 것은 내놓음 = 미생물 고리). 먹이 질소는 이번 틱 죽은 먹이가 물에 돌려준 몫
                S.ing[i] = S.eff[i] > 0 ? c * (S.gainP[i] + S.gainB[i]) / S.eff[i] * DT * P.QN / VML : 0;
                died += d; killAcc[s * V + v] += d * (dr > 0 ? S.kill[i] / dr : 0);
            }
            double resv = 0; for (int i = 0; i < n; i++) resv += Math.Min(S.need[i], S.ing[i]);
            double fr = resv > pool * 0.9 ? pool * 0.9 / resv : 1, pool2 = pool * 0.9 - resv * fr;
            for (int i = 0; i < n; i++) needW += (S.need[i] - Math.Min(S.need[i], S.ing[i])) * S.aff[i];
            double share = needW > 0 ? pool2 / needW : 0, used = 0;
            for (int i = 0; i < n; i++)
            {
                if (S.dD[i] == 0 && S.dUp[i] == 0 && S.dB[i] == 0) continue;
                int s = S.idx[i], k = S.stg[i], q = Ix(s, v, k);
                double own = Math.Min(S.need[i], S.ing[i]), got = own * fr + (S.need[i] - own) * Math.Min(1, share * S.aff[i]);
                double f = S.need[i] > 0 ? got / S.need[i] : 1;                      // 영양염이 모자라면 못 짓는다
                if (f < 1) { S.dB[i] = r.Binom(S.dB[i], f); S.dUp[i] = r.Binom(S.dUp[i], f); }
                used += S.need[i] * Math.Min(1, f);
                born += S.dB[i]; genAcc[s * V + v] += S.dB[i];
                cnt[q] -= S.dD[i] + S.dUp[i];
                if (S.dUp[i] > 0) cnt[q + 1] += S.dUp[i];
                if (S.dB[i] > 0) cnt[Ix(s, v, 0)] += S.dB[i];
            }
            nut[v] = Math.Max(0, pool - used);
            o2d[v] = (o2p - o2c) * DT / VML;                                        // 용존 산소 변화(에너지/mL) — Chem()에서 환산
            vBirth[v] = born; vDeath[v] = died; vKill[v] = killed;
        }

        static int LowerBound(float[] a, int n, float x) { int lo = 0, hi = n; while (lo < hi) { int m = (lo + hi) >> 1; if (a[m] < x) lo = m + 1; else hi = m; } return lo; }

        // 개체 하나의 시간당 에너지 수지(10 μm 세포 1개분 = 1). B에 항목별로 채운다(화면 개체 정보에도 씀):
        // 0 녹은 유기물 1 광합성 2 세균 3 포식 | 4 기초 대사 5 형질 유지비 6 몸 구조·모양 7 삼투 8 삼투 손상 9 자외선 10 산소 독성·부족 11 온도 | 12 호흡 합계 13 순수지
        public double Budget(Strain st, int v, ref Morph m, double gainP, double gainB, double[] B)
        {
            var g = st.g; int y = v / LS; float t = T[y]; float kt = m.kt, perf = m.perf;
            double V10 = m.esd * m.esd * m.esd * 1e-3, A10 = m.esd * m.esd * 1e-2 * st.fA, C = m.C;
            double n = nut[v], nup = n / (n + P.KN), work = perf * kt;
            double o2 = dox[v], o2s = o2 / (o2 + P.O2_K), hyp = Math.Min(1, o2 / Math.Max(1e-3, m.pcrit));
            double aer = 1 + P.AER_GAIN * g[Gn.Aerobic] * o2s * hyp + 0.4 * (1 - g[Gn.Aerobic]) * (1 - o2s);
            double Il = I[v];
            double up = P.C_OSMO * A10 * nup * work * aer * m.expo;
            double pkg = 1 / (1 + 0.025 * m.esd * st.thinAx);                    // package effect: 두꺼운 세포는 빛 흡수 효율↓ (Finkel 2010)
            double phPot = P.C_PHOTO * g[Gn.Photo] * V10 * pkg * Il / (Il + P.IK) * work * (1 - 0.1 * g[Gn.Pigment]) * m.expo;
            double nCap = P.KUP * A10 * nup * m.expo * kt;                         // 질소·인 흡수 능력: 표면적:부피가 큰 세포가 빈영양에 강함
            double ph = Math.Min(phPot, nCap);
            B[0] = up * C; B[1] = ph * C; B[2] = gainB; B[3] = gainP;
            double colF = C > 1 ? Math.Min(1, (C - 1) / 15) : 0, lab = 1 - 0.5 * g[Gn.Diff] * colF;   // 분업: 체세포가 운동·감각을 맡음
            double sc = m.org > 0 ? Math.Pow(Math.Max(1, m.M / P.M0), -0.25 * m.org) : 1;                // 조직·순환이 있으면 대사 ∝ 질량^0.75 (DeLong 2010)
            const double K = 0.4;   // 웹판 계수(틱당)를 시간당으로
            B[4] = (P.BASAL * V10 + P.FIX) * kt * C * sc;
            B[5] = ((0.006 * g[Gn.Photo] + 0.002 * g[Gn.Phago] + 0.004 * g[Gn.Aerobic] + 0.002 * g[Gn.Vacuole] + 0.003 * g[Gn.Buoy]) * V10
                + lab * 0.002 * g[Gn.Flagella] * (m.esd / 8)
                + lab * (0.001 * g[Gn.Eye] + 0.004 * g[Gn.Eye] * g[Gn.Eye]) * Math.Pow(V10, 0.6)
                + (0.0015 * g[Gn.Chemo] + 0.003 * g[Gn.Pigment] + 0.005 * g[Gn.Wall] + 0.002 * lab * g[Gn.Cilia]) * A10) * K * kt * C * sc;
            // 몸 구조: 가시·각(표면적), 모양 유지(세포벽이 있으면 공짜에 가깝다), 군체 접착·분업, 부착 기관, 가소성,
            // 동물 기관(소화관·신경·순환·골격·좌우대칭): 기관 조직 유지비. 신경 조직은 비싸다(척추동물 뇌 = 대사의 수 %–20%)
            B[6] = ((0.003 * m.spines + 0.004 * g[Gn.Lorica] + 0.002 * g[Gn.Elong] * g[Gn.Elong] * (1 - 0.7 * g[Gn.Wall])) * A10
                + (C > 1 ? 0.0012 + 0.002 * g[Gn.Diff] : 0) + 0.002 * g[Gn.Attach] + 0.0008 * g[Gn.Plastic]
                + (st.dev ? (0.0015 * g[Gn.Gut] + 0.004 * g[Gn.Neuro] + 0.002 * g[Gn.Circ] + 0.002 * g[Gn.Skel] + 0.0008 * g[Gn.Bilat]) * V10 : 0)) * K * kt * C * sc;
            float S = env[Ev.Salt]; double internal_ = Math.Max(P.OSMO_FLOOR, Math.Min(S, g[Gn.Osmo]));
            double osm = (P.OSMO_INT * internal_ + P.OSMO_CAP * g[Gn.Osmo]) * V10, od = 0;
            if (S > g[Gn.Osmo] * 1.08f) od = P.W_OUT * (S - g[Gn.Osmo] * 1.08) * A10;      // 탈수. 8%까지는 순응으로 버팀
            else if (S < P.OSMO_FLOOR)
            {
                double influx = P.W_IN * (P.OSMO_FLOOR - S) * A10 * (1 - 0.9 * g[Gn.Wall]);  // 민물: 물이 들어와 부푼다. 세포벽 팽압이 막음
                double pump = Math.Min(influx, P.PUMP * g[Gn.Vacuole] * A10);
                osm += pump * P.PUMP_COST; od = influx - pump;
            }
            B[7] = osm * kt * C * m.expo; B[8] = od * C * m.expo;                // 다세포는 겉 세포만 바깥물과 맞닿는다
            double prot = 1 - 0.9 * g[Gn.Pigment];
            B[9] = P.UV_DMG * U[y] * prot * (1 - 0.5 * g[Gn.Lorica]) * A10 * m.expo * C;
            // 산소: 혐기성의 산소 독성 + 산소가 임계보다 낮을 때 신경·근육 조직의 산소 부족
            B[10] = (P.O2_TOX * (o2 / 21) * (1 - g[Gn.Aerobic]) * (1 - g[Gn.Aerobic]) * V10 + (1 - hyp) * g[Gn.Neuro] * 0.004 * V10 * kt) * C * sc;
            B[11] = ((perf < 0.3 ? 0.012 * (0.3 - perf) / 0.3 : 0) + (t > 62 ? 0.02 * (t - 62) : 0)) * V10 * C * sc;   // 진핵 한계 ~60°C. 대사처럼 몸 크기에 따라 준다
            double swim = 0.0004 * (m.esd / 10) * (m.speed / 100) * (m.speed / 100) * kt;   // 세포 헤엄 동력은 작다(Stokes)
            if (m.org > 0 && m.L > 0) { double bl = m.speed / m.L; swim = 0.3 * B[4] * bl * bl / 4; }   // 근육 유영: 2 BL/s면 기초 대사의 30%
            double cost = B[4] + B[5] + B[6] + B[7] + B[8] + B[9] + B[10] + B[11] + swim;
            B[12] = B[4] + B[5] + B[6] + swim;                                     // 산소를 쓰는 호흡(용존 산소 소비)
            double net = B[0] + B[1] + B[2] + B[3] - cost;
            B[13] = net;
            return net;
        }

        // 계통 하나의 이동: 침강·부력, 헤엄(주화성·주광성), 난류 섞임. 계통마다 독립이라 병렬
        void Move(int s)
        {
            var S = scratch.Value; var st = strains[s]; var r = new Rng(((ulong)seed << 40) ^ ((ulong)tick << 12) ^ (0x800 | (ulong)s));
            double[] dst = S.row; Array.Clear(dst, 0, V * KM);
            int b0 = Ix(s, 0, 0); const int LK = LS * KM; float DT = P.DT;
            float sense = 0.15f + 0.85f * MathF.Max(st.g[Gn.Eye], MathF.Max(st.g[Gn.Chemo], st.g[Gn.Neuro]));
            for (int v = 0; v < V; v++)
                for (int k = 0; k < st.K; k++)
                {
                    int q = (v * KM) + k; double c = cnt[b0 + q]; if (c <= 0) continue;
                    int y = v / LS, x = v % NX, z = (v / NX) % NZ;
                    var m = M(s, v, k);
                    if (m.sessile) { dst[y < NY - 1 ? q + LK : q] += c; continue; }   // 부착 생물은 바닥에 내려앉아 산다
                    double hov = Math.Max(0, Math.Abs(m.sink) - m.speed * 3600e-6);   // 헤엄치는 생물은 제 속도만큼 가라앉음·떠오름을 버틴다(헤엄 비용은 늘 냄)
                    double fs = Math.Sign(m.sink) * hov * DT / DZ;                  // 침강(+아래) 또는 떠오름(−)
                    double sw = Math.Min(0.9, m.speed * 3600e-6 * DT / DZ);
                    double here = mu[b0 + q], up = y > 0 ? mu[b0 + q - LK] : here, dn = y < NY - 1 ? mu[b0 + q + LK] : here;
                    double bias = Math.Tanh((dn - up) * 300) * sense;              // +면 아래가 낫다
                    if (Math.Max(up, dn) < here) bias *= 0.3;                      // 지금 자리가 제일 좋으면 덜 움직임
                    double mixd = Math.Max(0, 1 - sw);                             // 빨리 헤엄치는 동물은 물살에 덜 휩쓸린다
                    double pD = Math.Max(0, fs) + sw * Math.Max(0, bias), pU = Math.Max(0, -fs) + sw * Math.Max(0, -bias);
                    pD += y < NY - 1 ? Math.Min(0.4, mixV[y] * DT) * mixd : 0; pU += y > 0 ? Math.Min(0.4, mixV[y - 1] * DT) * mixd : 0;   // 난류에 휩쓸림(틱당 한 층까지)
                    double pH = P.MIX_H * DT, tot = pD + pU + 4 * pH;
                    if (tot > 0.95) { double f = 0.95 / tot; pD *= f; pU *= f; pH *= f; }
                    double nd = r.Binom(c, pD), nu = r.Binom(c - nd, pU / Math.Max(1e-9, 1 - pD));
                    double rest = c - nd - nu, hx = r.Binom(rest, Math.Min(1, 4 * pH / Math.Max(1e-9, 1 - pD - pU)));
                    rest -= hx;
                    if (y < NY - 1) dst[q + LK] += nd;
                    else depS[s * LS + v % LS] += nd * m.M;   // 바닥 밖 = 퇴적물에 묻힘(분해되며 산소를 씀). 계통별 줄에 써서 병렬 경쟁 없음   // 바닥 밖 = 퇴적물에 묻힘(분해되며 산소를 씀)
                    if (y > 0) dst[q - LK] += nu; else rest += nu;                 // 수면에 뜬 것은 그대로
                    double h1 = Math.Floor(hx / 4), h4 = hx - 3 * h1;
                    dst[(y * LS + z * NX + (x + 1) % NX) * KM + k] += h1;
                    dst[(y * LS + z * NX + (x + NX - 1) % NX) * KM + k] += h1;
                    dst[(y * LS + ((z + 1) % NZ) * NX + x) * KM + k] += h1;
                    dst[(y * LS + ((z + NZ - 1) % NZ) * NX + x) * KM + k] += h4;
                    dst[q] += rest;
                }
            Array.Copy(dst, 0, cnt, b0, V * KM);
        }

        // 영양염·세균·용존 산소: 섞임, 바닥 공급, 세균 성장, 수면 기체 교환. 엽록소(그늘)
        void Chem()
        {
            float DT = P.DT;
            double nsed = P.NSED * env[Ev.Nut] / 100;
            for (int c = 0; c < LS; c++) nut[(NY - 1) * LS + c] += P.KSED * DT * (nsed - nut[(NY - 1) * LS + c]);
            Mix(nut); Mix(bac); Mix(dox);
            foreach (int s in live) for (int c = 0; c < LS; c++) { double m = depS[s * LS + c]; if (m == 0) continue; dep[(NY - 1) * LS + c] += m; lostN += m * P.QN / VML; depS[s * LS + c] = 0; }
            float ts = env[Ev.Temp], sal = env[Ev.Salt];
            for (int v = 0; v < V; v++)
            {
                int y = v / LS; float t = T[y];
                double nn = nut[v], g = 0.08 * Kt(t) * nn / (nn + 0.5) * (1 - bac[v] / (2e6 + 4e5 * nn)) * Math.Min(1, 0.3 + dox[v] / 2);
                double db = bac[v] * (g - 0.01) * DT;
                bac[v] = Math.Max(1e3, bac[v] + db);
                nut[v] = Math.Max(0, nn - db * 2.9e-7);                            // 세균 한 마리 ≈ 4 fg N
                // 산소 용해도는 따뜻하고 짤수록 낮다 → 같은 호흡이 산소를 더 빨리 깎는다
                double sol = Math.Exp(-0.0205 * (t - 20)) * (1 - 0.0052 * sal / 1.0 * 0.1), kO2 = P.KO2 / Math.Max(0.2, sol);
                // 바닥 퇴적물: 가라앉은 유기물이 하루 ~3%씩 분해되며 산소를 쓰고 영양염을 돌려준다(KDEC)
                double dec = y == NY - 1 ? dep[v] * P.KDEC * DT : 0; dep[v] -= dec; nut[v] += dec * P.QN / VML; lostN -= dec * P.QN / VML;
                double resp = o2d[v] - Math.Max(0, db) * P.BAC_E * 2.3 - dec / VML;
                dox[v] = Math.Clamp(dox[v] + resp * kO2, 0, 21 + 100 * (y + 0.5) * DZ / 10 + 0.3 * env[Ev.O2]);
            }
            for (int c = 0; c < LS; c++) dox[c] += P.KEX * DT * (env[Ev.O2] - dox[c]);   // 수면: 대기와 평형
            Array.Clear(chl, 0, V);
            foreach (int s in live)
            {
                var st = strains[s]; if (st.g[Gn.Photo] < 0.01f) continue;
                double ph0 = st.g[Gn.Photo] * Math.Pow(st.esd0 / 10, 3);
                for (int v = 0; v < V; v++) for (int k = 0; k < st.K; k++) { double c = cnt[Ix(s, v, k)]; if (c > 0) chl[v] += c / VML * ph0 * st.C[k]; }
            }
        }

        void Mix(double[] a)
        {
            var tmp = scratch.Value.rowB; float mx = P.MIX_H; for (int y = 0; y < NY; y++) mx = MathF.Max(mx, mixV[y]);
            int nsub = (int)MathF.Ceiling(mx * P.DT / 0.4f); float DT = P.DT / nsub;   // 혼합층은 몇 시간 만에 섞인다: 안정하게 잘게 나눠 푼다
            for (int it = 0; it < nsub; it++)
            {
            Array.Copy(a, tmp, V);
            for (int v = 0; v < V; v++)
            {
                int y = v / LS; double d = 0;
                if (y > 0) d += mixV[y - 1] * (tmp[v - LS] - tmp[v]);
                if (y < NY - 1) d += mixV[y] * (tmp[v + LS] - tmp[v]);
                int x = v % NX, z = (v / NX) % NZ;
                d += P.MIX_H * (tmp[y * LS + z * NX + (x + 1) % NX] + tmp[y * LS + z * NX + (x + NX - 1) % NX] + tmp[y * LS + ((z + 1) % NZ) * NX + x] + tmp[y * LS + ((z + NZ - 1) % NZ) * NX + x] - 4 * tmp[v]);
                a[v] = tmp[v] + d * DT;
            }
            }
        }

        // 돌연변이 계통: 실제 집단(조 단위)은 매 시간 수많은 돌연변이를 만들고, 같은 돌연변이가 여러 번 독립으로 생긴다.
        // 한 틱에 새로 기록하는 돌연변이 계통은 모두 합쳐 MUT_NEW개(계통 칸이 한정되어 있으므로). 계통마다 몫 ∝ (출생 × 돌연변이율)^MUT_ALPHA:
        // 수가 많은 계통이 더 자주 얻되, 수가 적은 큰 생물도 자기 실제 공급(출생 × 돌연변이율)까지는 얻는다.
        // 기록된 돌연변이 하나는 비슷한 돌연변이 묶음을 대표해 시작 수 = min(부모 × MUT_F0, 출생 × 돌연변이율 × MUT_LUMP).
        readonly double[] mw = new double[SMAX];
        void Mutants()
        {
            var r = new Rng(((ulong)seed << 40) ^ ((ulong)tick << 12) ^ 0xFFF);
            int nl = live.Count; double sw = 0;
            for (int li = 0; li < nl; li++)
            {
                int s = live[li]; var st = strains[s]; double bsum = 0; int b = s * V;
                for (int v = 0; v < V; v++) bsum += genAcc[b + v];
                mw[li] = bsum * P.MUT_U * st.g[Gn.Mutrate] * Math.Pow(st.C[st.K - 1], 0.15);
                sw += Math.Pow(mw[li], P.MUT_ALPHA);
            }
            if (sw <= 0) return;
            var todo = new List<(int s, int id, double bU, double lam)>();
            for (int li = 0; li < nl; li++) if (mw[li] > 0) todo.Add((live[li], strains[live[li]].id, mw[li], Math.Min(mw[li], Math.Max(P.MUT_NEW * Math.Pow(mw[li], P.MUT_ALPHA) / sw, strains[live[li]].C[strains[live[li]].K - 1] >= 4 ? P.MUT_FLOOR : 0))));
            foreach (var (s, id, bU, lam) in todo)
            {
                var st = strains[s]; if (st == null || st.id != id) continue;   // 그새 지워진 계통
                int b = s * V;
                int k = (int)r.Poisson(lam);
                for (int i = 0; i < k; i++)
                {
                    double tot = 0; for (int q = Ix(s, 0, 0), e = q + V * KM; q < e; q++) tot += cnt[q];
                    double n0 = Math.Max(1, Math.Min(bU * P.MUT_LUMP, P.MUT_F0 * tot)), bsum = 0;
                    for (int v = 0; v < V; v++) bsum += genAcc[b + v];
                    var g = Mutate(st.g, ref r);
                    var ns = NewStrain(g, st.sp, st.id, st.gen);
                    if (ns == null) { if (!Prune(s)) return; ns = NewStrain(g, st.sp, st.id, st.gen); if (ns == null) return; }
                    Split(s, ns, n0, tot, bsum, ref r);
                }
            }
        }

        // 부모 s의 분포(칸·단계)에서 n0마리를 새 계통 ns로 떼어 준다. 단계 수가 바뀌면 세포 수가 가장 가까운 단계로
        void Split(int s, Strain ns, double n0, double tot, double bsum, ref Rng r)
        {
            var st = strains[s]; int b = s * V; var map = new int[KM];
            for (int a = 0; a < st.K; a++) { int bi = 0; double bd = 1e30; for (int c = 0; c < ns.K; c++) { double dd = Math.Abs(Math.Log(ns.C[c] / st.C[a])); if (dd < bd) { bd = dd; bi = c; } } map[a] = bi; }
            double f = n0 / Math.Max(1, tot);
            if (n0 <= 1 && bsum > 0)
            {
                double x = r.U() * bsum; int v = 0; for (; v < V - 1; v++) { x -= genAcc[b + v]; if (x <= 0) break; }
                if (cnt[Ix(s, v, 0)] >= 1) { cnt[Ix(s, v, 0)] -= 1; cnt[Ix(ns.slot, v, map[0])] += 1; }
            }
            else
                for (int v = 0; v < V; v++) for (int a = 0; a < st.K; a++)
                {
                    int q = Ix(s, v, a); double c = cnt[q]; if (c <= 0) continue;
                    double m = r.Binom(c, f); cnt[q] -= m; cnt[Ix(ns.slot, v, map[a])] += m;
                }
        }

        // 시험용: 계통 s에서 유전형 g인 계통을 비율 frac만큼 떼어 만든다(침입 적합도 탐침)
        public Strain Inject(int s, float[] g, double frac)
        {
            var st = strains[s]; var ns = NewStrain(g, st.sp, st.id, st.gen); if (ns == null) return null;
            var r = new Rng((ulong)(tick * 7919 + ns.id)); Split(s, ns, Math.Max(2, frac * st.total), st.total, 0, ref r); ns.prevTot = 0; return ns;
        }
        public void PruneTo(int free) { while (SMAX - live.Count < free && Prune(-1)) { } }

        // 자리가 없으면 먼저 거의 같은 계통끼리 합친다(같은 유전형 묶음으로 보는 근사, 개체는 그대로).
        // 그래야 돌연변이가 많아도 드물고 다른 계통(진화의 앞줄인 큰 생물)이 계통 칸 부족으로 지워지지 않는다.
        // 합칠 짝이 없으면 줄어드는 작은 계통을 지운다. ponytail: 한 번에 16쌍, 계통 수² 거리 계산(256이면 ~2 ms)
        bool Prune(int keep)
        {
            var cand = new List<(double d, int s, int n)>();
            foreach (int s in live)
            {
                var st = strains[s]; if (s == keep || tick - st.born < 72 || st.trend > 0) continue;   // 퍼지는 새 돌연변이는 건드리지 않는다
                double dm = P.MERGE_D; int nn = -1;
                foreach (int o in live) { var so = strains[o]; if (o == s || so.total < st.total) continue; double d = Dist(st.g, so.g); if (d < dm) { dm = d; nn = o; } }
                if (nn >= 0) cand.Add((dm, s, nn));
            }
            cand.Sort(); var gone = new HashSet<int>();
            foreach (var (_, s, n) in cand) { if (gone.Count >= 16 || gone.Contains(s) || gone.Contains(n)) continue; Merge(s, n); gone.Add(s); }
            if (gone.Count > 0) return true;
            int best = -1; double bt = double.MaxValue;
            double big = 0; foreach (int s in live) big = Math.Max(big, strains[s].total);
            foreach (int s in live)   // 줄어드는 작은 계통부터(새 돌연변이는 6일 보호)
            {
                var st = strains[s]; if (s == keep || st.total > 0.05 * big) continue;
                double sc = tick - st.born < 72 ? 1e9 : st.trend;
                if (sc < bt) { bt = sc; best = s; }
            }
            if (best < 0) foreach (int s in live) if (s != keep && strains[s].total < bt) { bt = strains[s].total; best = s; }
            if (best < 0) return false;
            Remove(best); return true;
        }

        void Merge(int s, int n)   // 계통 s의 개체를 n으로 옮기고 s를 지운다. 단계는 세포 수가 가장 가까운 단계로
        {
            Strain a = strains[s], b = strains[n];
            for (int k = 0; k < a.K; k++)
            {
                int bk = 0; double bd = 1e30; for (int c = 0; c < b.K; c++) { double dd = Math.Abs(Math.Log(b.C[c] / a.C[k])); if (dd < bd) { bd = dd; bk = c; } }
                for (int v = 0; v < V; v++) cnt[Ix(n, v, bk)] += cnt[Ix(s, v, k)];
            }
            for (int v = 0; v < V; v++) { genAcc[n * V + v] += genAcc[s * V + v]; killAcc[n * V + v] += killAcc[s * V + v]; }
            b.total += a.total; Remove(s);
        }

        void Remove(int s)
        {
            Array.Clear(cnt, Ix(s, 0, 0), V * KM); Array.Clear(mu, Ix(s, 0, 0), V * KM); Array.Clear(cue, s * V, V);
            Array.Clear(genAcc, s * V, V); Array.Clear(killAcc, s * V, V);
            strains[s] = null; live.Remove(s);
        }

        public float[] Mutate(float[] g, ref Rng r)
        {
            var c = (float[])g.Clone(); int n = 1 + (int)r.Poisson(0.3);   // 대부분 유전자 하나(0.5로 올리면 해로운 다중 돌연변이만 늘었다)
            for (int i = 0; i < n; i++)
            {
                int j = (int)(r.U() * Gn.N); var G = Gn.Def[j];
                double d = r.Gauss() * G.sd * P.MUT_SD; if (r.U() < P.MUT_BIG) d *= 4;         // 드물게 효과 큰 돌연변이(대부분 해롭다: Fisher)
                if ((j == Gn.Cells || j == Gn.Egg) && r.U() < 0.15) d = r.U() < 0.5 ? 1 : -1;   // 세포 수 두 배·절반(분열 뒤 안 떨어지기)
                c[j] = j == Gn.Mutrate ? Math.Clamp(c[j] * (float)Math.Exp(d), G.lo, G.hi) : Math.Clamp(c[j] + (float)d, G.lo, G.hi);
            }
            return c;
        }

        public static double Dist(float[] a, float[] b)
        {
            double s = 0; for (int i = 0; i < Gn.N; i++) { float sc = Gn.Def[i].sc; if (sc > 0) { double d = (a[i] - b[i]) / sc; s += d * d; } }
            return Math.Sqrt(s);
        }

        void Totals()
        {
            totalCount = 0; totalBio = 0; births = deaths = kills = 0;
            for (int v = 0; v < V; v++) { births += vBirth[v]; deaths += vDeath[v]; kills += vKill[v]; }
            foreach (var sp in species.Values) sp.total = 0;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                int s = live[i]; var st = strains[s]; double tot = 0, gb = 0, kl = 0, bio = 0; int b = s * V;
                for (int k = 0; k < st.K; k++) st.stage[k] = 0;
                for (int v = 0; v < V; v++)
                {
                    gb += genAcc[b + v]; kl += killAcc[b + v]; genAcc[b + v] = 0; killAcc[b + v] = 0;
                    for (int k = 0; k < st.K; k++) { double c = cnt[Ix(s, v, k)]; st.stage[k] += c; }
                }
                for (int k = 0; k < st.K; k++) { tot += st.stage[k]; bio += st.stage[k] * st.C[k]; }
                if (tot <= 0) { Remove(s); continue; }
                st.trend = st.prevTot > 0 ? 0.98 * st.trend + 0.02 * Math.Log(tot / st.prevTot) : 0; st.prevTot = tot;
                st.gen += gb / tot; st.total = tot; st.kills += kl; if (tot > st.peak) st.peak = tot;
                totalCount += tot; totalBio += bio * Math.Pow(st.esd0 / 10, 3);
                if (species.TryGetValue(st.sp, out var sp)) sp.total += tot;
            }
            foreach (var sp in species.Values)
            {
                if (sp.total > sp.peak) sp.peak = sp.total;
                if (sp.total <= 0 && sp.ext < 0) { sp.ext = tick; if (sp.named) Log("ext", $"{sp.name} 멸종 ({Time(tick - sp.born)} 생존)"); }
            }
        }

        // 하루마다: 종 분화·새 단계(등급) 판정, 기록
        void Daily()
        {
            double thr = Math.Max(1e4, totalCount * 2e-4);
            for (int li = 0; li < live.Count; li++)
            {
                var st = strains[live[li]];
                int gr = Grade(st);
                if (!gradesSeen.Contains(gr) && st.total >= Math.Min(thr, 50)) { gradesSeen.Add(gr); Log("grade", $"처음으로 {GradeName[gr]} 등장"); }
                if (st.total < thr && !(gr >= 3 && st.total >= 50)) continue;      // 큰 생물은 수가 적어도 종으로 인정
                var sp = species[st.sp];
                if (Dist(st.g, sp.rep) <= P.SPEC_D) continue;
                var ns = NewSpecies(st.g, sp.id); ns.grade = gr; Names.Name(ns, species.Values);
                ns.named = true; st.sp = ns.id;
                foreach (int o in live) { var so = strains[o]; if (so.sp == sp.id && Dist(so.g, ns.rep) < Dist(so.g, sp.rep)) so.sp = ns.id; }
                Log("sp", $"신종: {ns.name} — {ns.ko} ({Names.Of(sp)}에서 갈라짐)");
            }
            hist.Add(this);
        }

        public string Time(long ticks)   // 틱 → 사람이 읽는 기간
        {
            long h = (long)(ticks * P.DT), d = h / 24; if (d < 1) return h + "시간"; if (d < 365) return d + "일";
            return (d / 365) + "년 " + (d % 365) + "일";
        }

        public int Vox(float x, float y, float z)   // m 좌표(x,z 0–20, y 깊이 0–10) → 칸
        {
            int ix = Math.Clamp((int)(x / DX), 0, NX - 1), iz = Math.Clamp((int)(z / DX), 0, NZ - 1), iy = Math.Clamp((int)(y / DZ), 0, NY - 1);
            return iy * LS + iz * NX + ix;
        }

        public float[] MeanGenesBy(Func<Strain, double> weight)
        {
            var m = new double[Gn.N]; double w = 0;
            foreach (int s in live) { var st = strains[s]; double ww = weight(st); for (int i = 0; i < Gn.N; i++) m[i] += st.g[i] * ww; w += ww; }
            var r = new float[Gn.N]; for (int i = 0; i < Gn.N; i++) r[i] = w > 0 ? (float)(m[i] / w) : 0; return r;
        }
        public float[] MeanGenes() => MeanGenesBy(s => s.total);
        public double Biomass(Strain st) { double b = 0; for (int k = 0; k < st.K; k++) b += st.stage[k] * st.C[k]; return b * Math.Pow(st.esd0 / 10, 3); }
        public float AdultL(Strain st) { var m = MorphAt(st, Vox(10, 1, 10), st.K - 1, 0); return m.L; }
    }

    public sealed class History
    {
        public readonly List<long> t = new(); public readonly List<float[]> env = new(), mean = new();
        public readonly List<double> total = new(), bio = new(), maxL = new(); public readonly List<Dictionary<int, double>> sp = new();
        public void Clear() { t.Clear(); env.Clear(); mean.Clear(); total.Clear(); bio.Clear(); maxL.Clear(); sp.Clear(); }
        public void Add(World w)
        {
            double ml = 0; foreach (int s in w.live) { var st = w.strains[s]; if (st.total >= 10) ml = Math.Max(ml, w.AdultL(st)); }
            t.Add(w.tick); env.Add((float[])w.env.Clone()); mean.Add(w.MeanGenes()); total.Add(w.totalCount); bio.Add(w.totalBio); maxL.Add(ml);
            var d = new Dictionary<int, double>(); foreach (var s in w.species.Values) if (s.total > 0) d[s.id] = s.total; sp.Add(d);
            if (t.Count > 4000) { t.RemoveAt(0); env.RemoveAt(0); mean.RemoveAt(0); total.RemoveAt(0); bio.RemoveAt(0); maxL.RemoveAt(0); sp.RemoveAt(0); }
        }
    }

    // 종 이름: 가장 두드러진 특징 → 라틴어 속명 앞부분 + 종소명, 한국어 특징
    public static class Names
    {
        static readonly (string pre, string ep, string ko, Func<float[], float> f)[] F = {
            ("Halo", "salina", "호염성", g => (g[Gn.Osmo] - 90) / 50),
            ("Thermo", "thermophila", "호열성", g => (g[Gn.Topt] - 38) / 7),
            ("Psychro", "glacialis", "호냉성", g => (12 - g[Gn.Topt]) / 7),
            ("Chloro", "viridis", "광합성", g => (g[Gn.Photo] - 0.3f) / 0.2f),
            ("Ophthalmo", "oculata", "눈", g => (g[Gn.Eye] - 0.4f) / 0.2f),
            ("Theco", "tecta", "세포벽", g => (g[Gn.Wall] - 0.35f) / 0.2f),
            ("Lesto", "vorax", "포식성", g => (g[Gn.Phago] * (1 - g[Gn.Wall]) * (1 - g[Gn.Wall]) - 0.65f) / 0.12f),
            ("Mega", "gigas", "대형", g => (g[Gn.Size] - 4.6f) / 0.4f),
            ("Nano", "minuta", "소형", g => (2.5f - g[Gn.Size]) / 0.3f),
            ("Tacho", "velox", "쾌속", g => (g[Gn.Flagella] - 0.6f) / 0.15f),
            ("Rhodo", "rubra", "붉은 색소", g => (g[Gn.Pigment] - 0.45f) / 0.2f),
            ("Aero", "aerobia", "호기성", g => (g[Gn.Aerobic] - 0.7f) / 0.12f),
            ("Anaero", "anoxica", "혐기성", g => (0.12f - g[Gn.Aerobic]) / 0.08f),
            ("Hydro", "dulcis", "수축포", g => (g[Gn.Vacuole] - 0.5f) / 0.15f),
            ("Acantho", "spinosa", "가시", g => (g[Gn.Spines] - 0.35f) / 0.2f),
            ("Tintinno", "loricata", "껍데기", g => (g[Gn.Lorica] - 0.35f) / 0.2f),
            ("Cilio", "ciliata", "섬모", g => (g[Gn.Cilia] - 0.35f) / 0.2f),
            ("Proteo", "mutabilis", "형태 가소성", g => (g[Gn.Plastic] - 0.4f) / 0.2f),
            ("Rhabdo", "elongata", "길쭉함", g => (g[Gn.Elong] - 1.2f) / 0.5f),
            ("Disco", "plana", "납작함", g => (-g[Gn.Elong] - 0.5f) / 0.3f),
            ("Rhizo", "affixa", "바닥 부착", g => (g[Gn.Attach] - 0.55f) / 0.15f),
            ("Physo", "natans", "부력", g => (g[Gn.Buoy] - 0.4f) / 0.2f),
            ("Nemato", "filamentosa", "실 모양", g => g[Gn.Cells] < 1 ? -1 : (0.33f - g[Gn.Form]) / 0.15f + (g[Gn.Cells] - 2) / 2),
            ("Phyllo", "foliacea", "판 모양", g => g[Gn.Cells] < 1 ? -1 : (0.17f - MathF.Abs(g[Gn.Form] - 0.5f)) / 0.1f + (g[Gn.Cells] - 2) / 2),
            ("Volvo", "globosa", "공 모양", g => g[Gn.Cells] < 1 ? -1 : (g[Gn.Form] - 0.67f) / 0.15f + (g[Gn.Cells] - 2) / 2),
            ("Soma", "differentiata", "세포 분업", g => g[Gn.Cells] < 2 ? -1 : (g[Gn.Diff] - 0.4f) / 0.15f),
            ("Gastro", "vorans", "소화관", g => (g[Gn.Gut] - 0.45f) / 0.15f),
            ("Neuro", "agilis", "신경·근육", g => (g[Gn.Neuro] - 0.45f) / 0.15f),
            ("Cephalo", "bilateralis", "좌우대칭", g => (g[Gn.Bilat] - 0.45f) / 0.15f),
            ("Branchio", "respirans", "아가미", g => (g[Gn.Circ] - 0.45f) / 0.15f),
            ("Chordo", "dorsalis", "척삭", g => (g[Gn.Skel] - 0.4f) / 0.15f),
        };
        static readonly string[] PLAIN = { "primigenia", "communis", "vulgaris", "simplex", "antiqua", "nova", "modesta", "pelagica", "errans", "parva" };

        static string Suffix(float[] g)
        {
            if (g[Gn.Skel] > 0.6f && g[Gn.Neuro] > 0.5f && g[Gn.Circ] > 0.5f) return "ichthys";
            if (g[Gn.Neuro] > 0.5f && g[Gn.Bilat] > 0.5f) return "helmis";
            if (g[Gn.Gut] > 0.5f) return "cnida";
            if (g[Gn.Cells] >= 1) return g[Gn.Attach] > 0.5f && g[Gn.Phago] > 0.3f && g[Gn.Photo] < 0.3f ? "spongia" : g[Gn.Form] < 0.33f ? "nema" : g[Gn.Form] < 0.67f ? "phyllum" : "sphaera";
            if (g[Gn.Photo] > 0.4f) return g[Gn.Wall] > 0.4f ? "phyton" : g[Gn.Flagella] > 0.25f ? "monas" : "coccus";
            if (g[Gn.Cilia] > 0.4f) return "stoma";
            if (g[Gn.Phago] * (1 - g[Gn.Wall]) * (1 - g[Gn.Wall]) > 0.6f) return "zoon";
            if (g[Gn.Wall] > 0.4f) return "cystis";
            return g[Gn.Flagella] > 0.25f ? "monas" : "coccus";
        }

        public static List<(string pre, string ep, string ko, float v)> Describe(float[] g)
        {
            var l = new List<(string, string, string, float)>();
            foreach (var f in F) { float v = f.f(g); if (v > 0) l.Add((f.pre, f.ep, f.ko, v)); }
            l.Sort((a, b) => b.Item4.CompareTo(a.Item4)); return l;
        }

        public static void Name(Species sp, IEnumerable<Species> all)
        {
            var d = Describe(sp.rep);
            string genus = (d.Count > 0 ? d[0].pre : "Proto") + Suffix(sp.rep);
            string ep = d.Count > 1 ? d[1].ep : PLAIN[sp.id % PLAIN.Length];
            var used = new HashSet<string>(); foreach (var s in all) if (s.name != null) used.Add(s.name);
            string name = genus + " " + ep; int k = 2;
            while (used.Contains(name)) name = genus + " " + ep + " " + Roman(k++);
            sp.name = name;
            sp.ko = d.Count > 0 ? string.Join(" · ", d.GetRange(0, Math.Min(3, d.Count)).ConvertAll(x => x.ko)) : "조상형";
        }

        static string Roman(int k) => k switch { 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", 10 => "X", _ => k.ToString() };
        public static string Of(Species s) => s.name ?? ("계통 " + s.id);
    }
}
