// 설정 한 곳: 시뮬레이션의 모든 상수. 사용자가 바꾸는 값(Knobs)과 물리·생물 상수(Cfg)를 나눈다.
// 단위: 시간 일(day), 농도 μM(μmol/L), 생물량 μM C(물 1 L에 든 생물 탄소 μmol), 길이 μm(생물)·m(지형).
namespace Life
{
    // 사용자가 화면에서 바꾸는 손잡이
    public sealed class Knobs
    {
        public double mu = 1.0;          // 돌연변이율 배율(0.0001–10, 1 = 기본): 분열마다 생기는 돌연변이 공급과 정착 상한에 곱한다
        public double eventRate = 1.0;   // 자연 사건 빈도 배율(0 = 사건 없음)
        public double phosphate = 1.0;   // 풍화로 들어오는 인 배율
    }

    public static class Cfg
    {
        // 시간
        public const double DT = 1.0 / 3;            // 한 틱 = 8시간
        public const int TPD = 3;                     // 하루 틱 수

        // 공간: 해역 × 수심층. 층 경계(m)
        public static readonly double[] ZE = { 0, 20, 100, 500, 6000 };
        public const int NY = 4;
        // 섞임은 '두 칸 중 작은 쪽이 한 번 바뀌는 시간(일)'으로 정한다. 실제 바다(수백~수천 년)보다 훨씬 빠르게 압축했다(지질 시간 압축)
        public static readonly double[] TAU_Z = { 20, 100, 400 };   // 층 사이: 표층–20 m 아래, 그 아래, 심해
        public const double TAU_H = 150;                            // 이웃 해역 사이(같은 층)
        // 열수공 칸: 해저 바닥 칸에 붙은 작은 칸(분출 유체가 묽어지기 전의 굴뚝 주변). 바닥 칸과 TAU_VENT일에 한 번 섞인다
        public const int NVENT = 16;                                // 열수공 칸 자리(사건으로 새로 열릴 수 있음)
        public const double VENT_VOL = 2e12, VENT_THICK = 100, TAU_VENT = 20;   // L, m, 일

        // 원시 지구 시작값 (Fe·SO4·PO4: 시생누대 추정 범위 안, brief 3-6)
        public const double CO2_PAL0 = 30, O2_PAL0 = 1e-7, CH4_PPM0 = 100, H2_PPM0 = 100;
        public const double FE2_DEEP0 = 50, FE2_TOP0 = 20, SO4_0 = 50, PO4_0 = 0.2, NH4_0 = 2, DOC_0 = 0.02;   // 녹은 유기물은 처음에 거의 없다: 먹는 쪽은 만드는 쪽이 생겨야 산다
        public const double SUN = 0.78;               // 태양 밝기(지금 = 1)

        // 대기: 실제 우리 행성 크기(지구의 8 %) 대기를 ATM배로 줄여 느린 저장고가 생태 시간 안에 움직이게 한다(지질 시간 압축).
        public const double ATM = 1e-5;
        public const double O2_PAL_MOL = 2.9e18 * ATM, CO2_PAL_MOL = 3.9e15 * ATM, PPM_MOL = 1.4e13 * ATM;
        public const double PISTON = 3.0;             // 기체 교환 피스톤 속도(m/일)
        public const double O2_SAT = 250, CO2_SAT = 10, CH4_SAT = 0.0014, H2_SAT = 0.0008;   // 1 PAL(또는 1 ppm)에 녹는 양(μM)
        public const double H_ESCAPE = 2e-3;          // 대기 H2(CH4의 H 포함)가 우주로 빠지는 비율(/일)
        public const double CH4_OX = 0.02;            // 대기 CH4 광화학 산화(/일 × √O2_PAL)

        // 경계 유입(행성 전체, mol/일): 열수공·화산·풍화
        public const double VENT_H2 = 4e8, VENT_FE = 2e8, VENT_H2S = 5e7, VENT_CO2 = 1e8;
        public const double VOLC_CO2 = 1.5e9, VOLC_H2 = 3e7, VOLC_SO4 = 2e7;
        public const double WEATHER_P = 1.5e6, WEATHER_CO2 = 1.5e9;   // 풍화: 인 유입, CO2 흡수(땅 넓이·기온 비례)
        // 산화 풍화(1 PAL, 땅 29 %일 때): 땅에 드러난 옛 퇴적암의 유기 탄소·황철석이 산소에 녹아 나온다. 지구 값 × 행성 크기 0.08 / 365일
        public const double WEATHER_ORG = 1.3e9, WEATHER_PYR = 2.9e8;   // 유기 탄소 ~5.7e12 mol C/년(Zondervan 2023), 황철석 황 ~1.3e12 mol S/년(Burke 2018)
        public const double BURIAL = 0.003;           // 바닥에 닿은 사체가 하루에 묻히는 비율
        // 묻히는 유기물의 C:P(총 반응성 인): 산소 있는 바닥은 인을 철 산화물·인회석으로 붙잡아 낮고(≈60), 산소 없는 바닥은 인을 물로 되돌려 높다(≈200)
        // (Algeo & Ingall 2007; Van Cappellen & Ingall 1996). 정상 상태 유기 탄소 매몰 = 인 공급 × 이 비(Lenton & Daines 2018): 묻힘 속도가 아니라 이것과 인 공급이 산소를 정한다
        public const double CP_OX = 60, CP_ANOX = 200, CP_K = 20;   // CP_K: 바닥 물 O2(μM)가 이만큼이면 두 값의 중간
        public const double FE3_SINK = 1.0;           // 철 산화물 입자 침강(m/일)

        // 무생물 반응(/μM/일)
        public const double KSP_FES = 10;            // 황화철 용해도곱(μM², Fe2+ × 전체 황화물): 매키나와이트 log K* −3.5(Rickard 2006), pH 7.5
        public const double K_FE_OX = 0.5, K_HS_OX = 0.1, K_CH4_OX = 0.0005, K_H2_OX = 0.001;
        public const double HYDROLYSIS = 0.05;        // 사체 → 녹은 유기물(/일)
        public const double K_UV = 0.5;               // 자외선이 녹은 유기물을 깨는 속도(/일, 수면 빛 1, 오존 없을 때)
        public static readonly double[] POC_SIZE = { 1, 10, 100 };    // 사체 크기 칸(μm): 죽은 세포 크기 그대로(덩이·큰 몸은 큰 칸)
        public static readonly double[] POC_SINK = { 1, 10, 100 };    // 침강(m/일)

        // 세포
        public const double PHI_Q = 0.45;             // 늘 필요한 살림 단백질 몫(나머지 0.55를 유전자가 나눈다)
        public const double KAPPA = 8;                // 배분 유전자 → 몫: e^(κ·a). 한 걸음(0.02)에 ×1.17
        public const double K_RIB = 8.0;              // 리보솜 몫 1당 최대 성장률(/일)
        public const double A_BIO = 2.5;              // 세포 탄소 1 mol 짓는 ATP
        public const double A_FIX = 8.0;              // CO2 1 mol 고정하는 ATP 당량(ATP 3 + 환원력)
        public const double K_PHOTO = 120;             // 광계 몫 1당 ATP(/일, 빛 포화)
        public const double K_LIGHT = 0.15;           // 빛 반포화(수면 빛 대비)
        public const double A_CM = 0.05;              // 세포 안 색소 흡광 계수(/μm, 빛 띠 평균): 큰 세포는 색소끼리 가려 빛을 덜 쓴다(묶음 효과, Morel & Bricaud 1981)
        public const double K_FIX = 40;               // 고정 효소 몫 1당 최대 고정(/일, S=20 기준)
        public const double K_CO2 = 30;               // 고정 효소 CO2 반포화(μM)
        public const double K_CHEMO = 200;            // 화학합성 효소 몫 1당 전자 공여체 산화(/일)
        public const double K_OSMO = 30;              // 흡수 운반체 몫 1당 녹은 유기물 흡수(/일)
        public const double K_DOC = 10;
        public const double D_NUT = 8.64e-5;          // 녹은 영양염·유기물 확산 계수(m²/일) = 1e-9 m²/s(NH4⁺ 2e-9, PO4 0.7e-9, 작은 유기물 0.5–1e-9 사이 값)
        public const double K_NFIX = 4, A_NFIX = 8;   // 질소 고정: 몫 1당 N(/일), N 하나에 ATP
        // 이형세포 외피(당지질 층)가 들여보내는 바깥 O2 몫: 들어온 O2는 호흡이 태워 속을 거의 무산소로 둔다(Walsby 1985). Anabaena는 공기 포화(≈250 μM)에서도 잘 고정한다(Olson 2016 인용) —
        // 0.01이면 250 μM에서 속 2.5 μM(억제 반), 원생누대 바다 수십 μM에서는 거의 안 억제. ponytail: 정확한 투과 배수는 문헌에서 못 찾아 정성 맞춤
        public const double HET_O2 = 0.01;
        public const double M0 = 0.3;                 // 유지 ATP(/일, 20 °C)
        public const double MORT0 = 0.02;             // 바탕 사망(/일)
        public const double K_AER = 0.1;              // 산소 호흡 반포화 O2(μM): 해양 군집 실측 10–200 nM(Tiano 2014), 대장균 120 nM(Stolper 2010)
        // 산소 호흡 용량: 호흡 단백질 몫 1당 태울 수 있는 유기 C(/일). 대장균 ε_r 390 mM ATP/OD/h(Basan 2015) ≈ 600 ATP/C/일 ÷ 5 ATP/C = 120 /일.
        // 이 모형 화학합성 효소(K_CHEMO 200 H2 = 전자 400/일 → 유기 C 100/일)와도 맞는다. 리보솜 속도 비로 줄인 6.8은 산소 있는 물에서도 발효가 남아(65–87 %) 실측(nM O2까지 산소 호흡, Tiano 2014)과 어긋남
        public const double K_RESP = 120;
        public const double D_O2 = 1.73e-4;           // 몸 속 O2 확산 계수(m²/일) = 2e-9 m²/s(물 25 °C 2.1e-9, 조직은 그 절반쯤 — Payne 2009 PNAS 106:24이 쓴 값과 같은 차수)
        public const double Y_AER = 5, Y_FERM = 0.7;  // 유기물 C 하나에서 얻는 ATP: 산소 호흡, 발효(H2 쌓이면 줄어듦)
        public const double ROS = 0.6, ROS_K = 60;    // 산소 독성(/일), 반포화 O2(μM)
        public const double OWN_O2 = 80;              // 산소 광합성 세포 안 O2(μM 당량, 고정 1/일당)
        public const double HS_TOX = 0.3, HS_K = 30;  // 황화수소 독성(산소 호흡에 기대는 세포)
        public const double DRY_MORT = 3;             // 마름 사망(/일, 완전히 마른 곳)

        // 먹고 먹히기
        public const double ATTACK = 6;               // 포식 막 몫 1당 공격률(/μM C/일)
        public const double HANDLE = 0.5;             // 먹이 처리 시간(일)
        // 청소율(만남)은 최대 섭취보다 크기로 훨씬 덜 준다: Kiørboe & Hirst 2014 모음(PANGAEA 819856)을 다시 맞추면 원생생물 탄소당 최대 청소율 ∝ 질량^−0.07±0.06
        // (= 지름^−0.20, n 61, 주로 Hansen 1997 자료), 원생생물~어류 871개 전체는 질량^−0.04로 14자리 내내 300–500 ml/mg C/h. 최대 섭취는 원생생물 질량^−0.22±0.04(= 지름^−0.67)로 FEED_EXP와 같다
        public const double CLR_EXP = 0.20;
        public const double FEED_REF = 5, FEED_EXP = 0.69;   // 몸 탄소당 최대 섭취 ∝ (먹는 크기/5 μm)^−0.69 = 부피^−0.23 (Hansen 1997, 2–2000 μm). 5 μm 밑은 그대로
        public const double PREY_SD = 0.6, PREY_MAX = 1.2;   // 먹이 크기 폭 0.6 decade, 먹는 크기의 1.2배까지(선호 크기 비는 유전자 G.PREY)
        public const double EGEST = 0.3;              // 먹은 것 중 똥·찌꺼기
        public const double VIRUS = 0.25, VIRUS_K = 0.3;   // 이긴 자 죽이기: 같은 수용체 칸 숙주 밀도에 비례

        // 다세포
        public const double ADH0 = 0.003, ADH_EXP = 1.3;   // 덩이 세포 수 = (붙음 몫 / ADH0)^ADH_EXP
        public const int CELLS_MAX = 4096;            // 덩이 세포 수 상한

        // 진화
        public const double STEP = 0.04;              // 돌연변이 한 걸음: 대사·배분 유전자(범위의 4 %, 몫 ×1.38)
        public const double STEP_SHAPE = 0.02;        // 몸 모양 유전자(크기·길쭉함·가지·각·떨어지는 몫·붙음)는 더 작게: 모양이 갑자기 바뀌지 않게
        public const int EVAL = 160;                   // 틱마다 평가하는 돌연변이 후보 수
        public const double NCAP = 100;               // 돌연변이 공급에 쓰는 계통·칸 개체 수 상한(클론 간섭 근사)
        public const double NFOUND = 100;             // 정착 확률 식의 창시 개체 수
        public const double RARE = 1e-12;             // 계통이 생물권 전체의 이 몫 밑으로 떨어지면 멸종
        public const double FOUND = 0.3;             // 새 계통 시작 생물량 = 부모의 이 비율(지역 집단에서 번진 몫)
        public const int SMAX = 320;                  // 동시에 추적하는 계통 수
        public const double BMIN = 1e-14;             // 이보다 적으면(μM C, 칸 평균) 그 칸에서 사라짐
        public const double D_SP = 0.12;              // 종: 종 창시 유전체에서 이만큼 멀어지면 새 종
        public const double D_CLADE = 0.5;           // 큰 갈래: 갈래 창시 종에서 이만큼 멀어지면 새 갈래
    }
}
