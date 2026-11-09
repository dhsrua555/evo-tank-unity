// 유전체: 모든 유전자는 0–1 연속값. 형질은 매끄러운 지수·로그 사상으로만 정해져 한 걸음(0.02)이 형질을 크게 바꾸지 못한다.
// 미리 정한 몸 형태·생물군·문턱은 없다. 무엇을 하는 생물인지는 단백질 배분과 그 결과(탄소를 어디서 얻는지)로 나중에 판정한다.
using System;

namespace Life
{
    public static class G
    {
        // 단백질 배분 유전자(몫은 e^(κ·a)를 정규화)
        public const int RIB = 0, PHOTO = 1, FIX = 2, CCM = 3, CHEMO = 4, OSMO = 5, PHAGO = 6, RESP = 7, SOD = 8, MOT = 9, ADH = 10, WALL = 11, NFIX = 12, DRY = 13, NUT = 14;
        public const int NA = 15;
        // 연속 매개변수
        public const int EOX = 15, SPEC = 16, DNR = 17, TOPT = 18, TW = 19, SIZE = 20, ASPECT = 21, BRANCH = 22, ANGLE = 23, PROP = 24, DIFF = 25, R1 = 26, R2 = 27, R3 = 28, PREY = 29;
        public const int N = 30;
        public static readonly string[] Name = {
            "리보솜", "광계", "탄소 고정", "탄소 농축", "화학합성", "흡수 운반체", "포식 막", "산소 호흡", "항산화", "편모", "붙음", "세포벽", "질소 고정", "마름 견딤", "영양염 운반체",
            "광계 산화력", "고정 효소 특이성", "공여체 선호", "최적 온도", "온도 폭", "세포 크기", "세포 길쭉함", "가지치기", "분열 각", "떨어지는 몫", "분화", "수용체 1", "수용체 2", "수용체 3", "먹이 크기" };
    }

    // 유전체에서 바로 나오는 세포 형질(덩이 모양은 Body)
    public sealed class Traits
    {
        public readonly double[] phi = new double[G.NA];   // 단백질 몫(합 = 1 − PHI_Q)
        public double eox;      // 광계 산화력(V): 이보다 0.1 V 낮은 공여체를 쓸 수 있다
        public double S;        // 고정 효소 특이성(CO2/O2), 높을수록 느리다
        public double dnr;      // 화학합성 공여체 선호 축(H2 0.1 · CH4 0.35 · H2S 0.6 · Fe2+ 0.9)
        public double topt, tw; // 최적 온도·폭(°C)
        public double dCell;    // 세포 지름(μm)
        public double aspect;   // 세포 장단축비
        public double cellC;    // 세포 하나의 탄소(mol)
        public double nA;       // 덩이가 갈라지기 직전 세포 수(1 = 단세포)
        public double n0;       // 떨어져 나가는 번식체 세포 수
        public double diff;     // 분화 정도
        public int vbin;        // 바이러스 수용체 칸(5×5×5)
        public double lr;       // 먹이 크기 선호: log10(먹는 쪽 ÷ 먹이). 0 = 자기만 한 먹이(와편모류), 0.5 = 1/3(작은 편모류), 1.3 = 1/20(걸러 먹기). Hansen 1994

        public static Traits Of(float[] g)
        {
            var t = new Traits(); double sum = 0;
            for (int i = 0; i < G.NA; i++) { t.phi[i] = Math.Exp(Cfg.KAPPA * g[i]); sum += t.phi[i]; }
            for (int i = 0; i < G.NA; i++) t.phi[i] *= (1 - Cfg.PHI_Q) / sum;
            t.eox = -0.5 + 2.0 * g[G.EOX];
            t.S = 5 * Math.Pow(40, g[G.SPEC]);
            t.dnr = g[G.DNR];
            t.topt = 80 * g[G.TOPT];
            t.tw = 2 * Math.Pow(10, g[G.TW]);
            t.dCell = 0.5 * Math.Pow(200, g[G.SIZE]);                       // 0.5–100 μm
            t.aspect = Math.Pow(8, g[G.ASPECT]);
            double vol = Math.PI / 6 * t.dCell * t.dCell * t.dCell;
            t.cellC = 0.216e-12 * Math.Pow(vol, 0.939) / 12;                  // Menden-Deuer & Lessard 2000
            // 붙음: 분열한 딸세포가 붙어 남을 확률 p = φ/(φ+φ0) → 갈라지기 전 덩이 ≈ 1/(1−p) = 1 + φ/φ0, 거기에 지수.
            // 문턱이 없어 붙음 단백질을 조금만 늘려도 덩이가 조금 커진다. 한 걸음(φ ×1.17)에 세포 수 ×1.23 이하
            t.nA = Math.Clamp(Math.Pow(1 + t.phi[G.ADH] / Cfg.ADH0, Cfg.ADH_EXP), 1, Cfg.CELLS_MAX);
            t.n0 = Math.Max(1, t.nA * 0.5 * g[G.PROP]);
            t.diff = g[G.DIFF];
            t.lr = 1.3 * g[G.PREY];
            t.vbin = (int)(g[G.R1] * 4.999) * 25 + (int)(g[G.R2] * 4.999) * 5 + (int)(g[G.R3] * 4.999);
            Cell.Prep(t);
            return t;
        }

        public double pack, uH2, uHS, uFe, uW, sav, sqS;   // 생리에서 형질만으로 정해지는 값(Cell.Prep): 색소 묶음, 공여체별 광계 쓰임, 겉넓이 몫, √(20/S)
        public double elong, kd;                           // 길쭉함 배율(같은 부피 공보다 확산·끌림 반지름이 큼), 홀로 있을 때 확산이 대 주는 흡수(/μM/일, 세포 탄소 1 mol당)
        public readonly double[] wD = new double[4];         // 화학합성 공여체별 선호 무게
        public double Sat(int gene, double k) => phi[gene] / (phi[gene] + k);
        public double MeanCells => Math.Sqrt(nA * n0);
    }

    public static class Genome
    {
        // 원시 세포(첫 공통 조상 무렵): 열수공 곁에서 H2 + CO2로 사는 1 μm 무산소 독립영양 세포
        // (Wood–Ljungdahl 경로, Weiss 2016 · Moody 2024. 이 모형에선 산물을 메탄·황화수소로 단순화).
        // 배분 유전자는 문헌 근거를 네 단계로 나눠 정한다(괄호 = 단백질 몫):
        //   주 기능        리보솜 · 탄소 고정 · H2 화학합성
        //   곁 기능(~1 %)  녹은 유기물 · 암모늄 · 인산 운반체 — 어느 세포나 영양을 들여와야 한다
        //   논쟁(~0.2 %)   질소 고정(Weiss 2016 있음 ↔ Berkemer & McGlynn 2020·Moody 2024 근거 없음; 동위원소로는 3.2 Ga, Stüeken 2015),
//                  항산화(약한 산소에 적응했다 Ślesak 2012 ↔ 말단 산화효소가 없는 무산소 세포 Moody 2024), 세포벽, 마름 견딤
        //   없음(NONE)     광합성(최초 증거 3.4 Ga, Tice & Lowe 2004), 산소 호흡(산소 광합성 뒤, Soo 2017), 탄소 농축(시아노박테리아 카복시좀),
        //                  포식(진핵 식세포 작용), 편모(세균 편모와 고세균 archaellum은 기원이 다름, Jarrell & Albers 2012)
        // '없음'도 0은 아니다: 돌연변이가 넓혀 갈 출발점(다른 일을 하던 단백질을 빌려 쓰는 셈)만 남긴다.
        const float NONE = 0.2f;   // '없음' 유전자 값: 몫 ~0.04 %
        public static float[] Primordial()
        {
            var g = new float[G.N];
            g[G.RIB] = 1.0f; g[G.FIX] = 0.85f; g[G.CHEMO] = 0.95f;
            g[G.OSMO] = 0.6f; g[G.NUT] = 0.6f;
            g[G.NFIX] = 0.4f; g[G.WALL] = 0.4f; g[G.DRY] = 0.4f; g[G.SOD] = 0.4f;
            g[G.PHOTO] = NONE; g[G.RESP] = NONE; g[G.CCM] = NONE; g[G.PHAGO] = NONE; g[G.MOT] = NONE;
            g[G.ADH] = 0.3f;   // 붙음: 첫 공통 조상엔 근거 없음, 미생물 매트는 3.48 Ga부터(Djokic 2017). 딸세포가 가끔 붙어 남는 정도(평균 1.4세포)
            // 최적 온도 33 °C = 이 모형 처음 열수공 곁 물 온도: 초호열 아님(Catchpole & Forterre 2019), 중온~약한 호열(Boussau 2008), 시생대 바다 26–35 °C(Blake 2010)
            g[G.EOX] = 0.45f; g[G.SPEC] = 0.2f; g[G.DNR] = 0.1f; g[G.TOPT] = 0.41f; g[G.TW] = 0.6f;
            // 크기 1 μm·조금 길쭉: 첫 공통 조상 크기를 직접 잰 연구는 없다. 유전체 2.5 Mb·단백질 ~2,600개로 지금 원핵생물과 비슷해(Moody 2024) 그 크기를 쓴다
            g[G.SIZE] = 0.13f; g[G.ASPECT] = 0.2f; g[G.BRANCH] = 0.9f; g[G.ANGLE] = 0.2f; g[G.PROP] = 0.5f; g[G.DIFF] = 0;
            g[G.R1] = 0.5f; g[G.R2] = 0.5f; g[G.R3] = 0.5f; g[G.PREY] = 0.37f;   // 포식이 생기면 쓸 먹이 크기: 자기의 1/3(작은 편모류 흔한 값, Hansen 1994)
            return g;
        }

        // 돌연변이: 유전자 하나를 정규분포 한 걸음만큼(경계에서 반사)
        public static float[] Mutate(float[] p, ref Evo.Rng r, out int gene)
        {
            var g = (float[])p.Clone();
            gene = (int)(r.U() * G.N);
            bool shape = gene == G.SIZE || gene == G.ASPECT || gene == G.BRANCH || gene == G.ANGLE || gene == G.PROP || gene == G.ADH;
            double v = g[gene] + (shape ? Cfg.STEP_SHAPE : Cfg.STEP) * r.Gauss();
            if (v < 0) v = -v; if (v > 1) v = 2 - v;
            g[gene] = (float)Math.Clamp(v, 0, 1);
            return g;
        }

        public static double Dist(float[] a, float[] b)
        {
            double s = 0; for (int i = 0; i < G.N; i++) { double d = a[i] - b[i]; s += d * d; }
            return Math.Sqrt(s);
        }
    }
}
