// 유전체: 모든 유전자는 0–1 연속값. 형질은 매끄러운 지수·로그 사상으로만 정해져 한 걸음(0.02)이 형질을 크게 바꾸지 못한다.
// 미리 정한 몸 형태·생물군·문턱은 없다. 무엇을 하는 생물인지는 단백질 배분과 그 결과(탄소를 어디서 얻는지)로 나중에 판정한다.
using System;

namespace Life
{
    public static class G
    {
        // 단백질 배분 유전자(몫은 e^(κ·a)를 정규화)
        public const int RIB = 0, PHOTO = 1, FIX = 2, CCM = 3, CHEMO = 4, OSMO = 5, PHAGO = 6, RESP = 7, SOD = 8, MOT = 9, ADH = 10, WALL = 11, NFIX = 12, DRY = 13;
        public const int NA = 14;
        // 연속 매개변수
        public const int EOX = 14, SPEC = 15, DNR = 16, TOPT = 17, TW = 18, SIZE = 19, ASPECT = 20, BRANCH = 21, ANGLE = 22, PROP = 23, DIFF = 24, R1 = 25, R2 = 26, R3 = 27;
        public const int N = 28;
        public static readonly string[] Name = {
            "리보솜", "광계", "탄소 고정", "탄소 농축", "화학합성", "흡수 운반체", "포식 막", "산소 호흡", "항산화", "편모", "붙음", "세포벽", "질소 고정", "마름 견딤",
            "광계 산화력", "고정 효소 특이성", "공여체 선호", "최적 온도", "온도 폭", "세포 크기", "세포 길쭉함", "가지치기", "분열 각", "떨어지는 몫", "분화", "수용체 1", "수용체 2", "수용체 3" };
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
            // 붙음: 덩이가 갈라지기 전 세포 수 = (φ/φ0)^지수. 한 걸음(φ ×1.17)에 세포 수 ×1.23만 변한다(갑자기 커지지 않음)
            t.nA = Math.Clamp(Math.Pow(Math.Max(1, t.phi[G.ADH] / Cfg.ADH0), Cfg.ADH_EXP), 1, Cfg.CELLS_MAX);
            t.n0 = Math.Max(1, t.nA * 0.5 * g[G.PROP]);
            t.diff = g[G.DIFF];
            t.vbin = (int)(g[G.R1] * 4.999) * 25 + (int)(g[G.R2] * 4.999) * 5 + (int)(g[G.R3] * 4.999);
            return t;
        }

        public double Sat(int gene, double k) => phi[gene] / (phi[gene] + k);
        public double MeanCells => Math.Sqrt(nA * n0);
    }

    public static class Genome
    {
        // 원시 세포: 열수공의 H2 + CO2로 사는 1 μm 화학합성 세포(메탄 생성균과 비슷한 대사). 그 밖의 기능은 아주 조금씩만 갖고 있다
        public static float[] Primordial()
        {
            var g = new float[G.N];
            for (int i = 0; i < G.NA; i++) g[i] = 0.6f;    // 쓰지 않는 기능도 조금씩(각 ~1 %): 첫 공통 조상은 여러 대사 유전자를 갖고 있었다
            g[G.ADH] = 0.3f; g[G.WALL] = 0.4f; g[G.NFIX] = 0.4f; g[G.DRY] = 0.4f;
            g[G.RIB] = 1.0f; g[G.FIX] = 0.85f; g[G.CHEMO] = 0.95f;
            g[G.EOX] = 0.375f; g[G.SPEC] = 0.2f; g[G.DNR] = 0.1f; g[G.TOPT] = 0.35f; g[G.TW] = 0.6f;
            g[G.SIZE] = 0.13f; g[G.ASPECT] = 0.2f; g[G.BRANCH] = 0.9f; g[G.ANGLE] = 0.2f; g[G.PROP] = 0.5f; g[G.DIFF] = 0;
            g[G.R1] = 0.5f; g[G.R2] = 0.5f; g[G.R3] = 0.5f;
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
