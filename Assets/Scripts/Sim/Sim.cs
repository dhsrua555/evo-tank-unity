// 진화 수조 3D — 시뮬레이션 코어. Unity API를 쓰지 않는다(dotnet 시험 SimTest와 Unity가 같은 파일을 쓴다).
// 개체 하나하나가 아니라 '계통(strain) × 칸(voxel) × 크기 단계'의 개체 수를 확률적으로 계산한다:
// 출생 ~ 푸아송, 사망·성장·이동 ~ 이항, 포식 = 크기·속도·감각·방어가 정하는 만남(Gerritsen & Strickler 1977) + Holling II.
// 화면에 보이는 생물은 이 개체 수에서 뽑은 표본일 뿐이다.
// 세계: 지도(Geo, 6400 km 사방)의 바다를 해역(수심대·위도대가 같은 칸 묶음)으로 나누고, 해역마다 물기둥을 층으로 나눈 칸. 1틱 = DT시간.
// 원생생물(세포 분열)부터 발생하는 다세포(알→성체 크기 단계)·동물·어류까지 같은 공식으로 계산한다.
using System;
using System.Collections.Generic;
using System.Linq;
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
            Skel = 28, Egg = 29,
            Seg = 30, Limb = 31, Exo = 32, Mineral = 33, Jaw = 34, Herb = 35, Detr = 36, Ambush = 37, Burrow = 38, Toxin = 39, Transp = 40, School = 41, PreyR = 42, Dry = 43, N = 44;
        // 몸 부품(발생하는 다세포에서만 뜻이 있다): 단세포·군체에서는 돌연변이도 거리 계산도 하지 않는다
        public static bool BodyOnly(int j) => j >= Seg && j <= Jaw || j == Burrow || j == School;
        public static bool BodyReady(float[] g) => g[Cells] >= 4 && g[Diff] >= 0.4f;
        // 키, 이름, 최소, 최대, 시작, 돌연변이 표준편차, 종 거리 척도(0 = 거리에 안 셈)
        public static readonly GeneDef[] Def = {
            new("size", "세포 크기", 1, 8, 2.6f, 0.12f, 0.7f),            // log₂ 지름(μm): 2–256 μm, 시작 6 μm(세균을 가장 잘 먹는 크기, 첫 진핵생물 = 식세포 편모충)
            new("osmo", "삼투 조절 한계", 3, 320, 40, 8, 40),             // g/L
            new("vacuole", "수축포", 0, 1, 0.1f, 0.05f, 0.25f),
            new("wall", "세포벽", 0, 1, 0, 0.05f, 0.25f),
            new("flagella", "편모", 0, 1, 0.3f, 0.05f, 0.25f),
            new("chemo", "화학 감각", 0, 1, 0.2f, 0.05f, 0.25f),
            new("eye", "광수용·눈", 0, 1, 0, 0.04f, 0.25f),
            new("photo", "광합성", 0, 1, 0, 0.05f, 0.25f),
            new("phago", "식세포 작용", 0, 1, 0.7f, 0.05f, 0.25f),
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
            // 4단계: 몸 부품·식성·사냥·방어. 이득과 비용은 물리로만(MorphT·Attack·Digest·Budget)
            new("seg", "체절", 0, 6, 0, 0.25f, 1.5f),                       // log₂ 마디 수 1–64: 다리 수, 연동 운동(땅 파기)
            new("limb", "부속지(다리·지느러미)", 0, 1, 0, 0.05f, 0.25f),   // 지렛대(골격·외골격)가 있어야 힘을 쓴다: 헤엄·기기·긁기
            new("exo", "외골격·갑옷", 0, 1, 0, 0.05f, 0.25f),             // 덜 소화됨·근육 지렛대 / 탈피(자라기 느림)·피부 호흡 막힘·클수록 무거움
            new("mineral", "광물화(껍데기·뼈)", 0, 1, 0, 0.05f, 0.25f),    // 외골격 → 껍데기, 척삭 → 뼈. 무겁고 찬물에서 비싸다
            new("jaw", "턱", 0, 1, 0, 0.05f, 0.25f),                       // 입이 커지고 갑옷을 깨뜨린다(골격이 있어야)
            new("herb", "식물 소화", 0, 1, 0, 0.05f, 0.25f),               // 세포벽·광합성 먹이 소화 / 긴 장 = 덜 먹음
            new("detr", "부스러기 먹기", 0, 1, 0.5f, 0.05f, 0.25f),        // 바다눈·퇴적물을 얼마나 삼키나(밥통을 채우는 몫)
            new("ambush", "매복", 0, 1, 0, 0.05f, 0.25f),                  // 가만히 기다림: 만남↓·들킴↓·헤엄 비용↓ (Kiørboe 2011)
            new("burrow", "땅 파기", 0, 1, 0, 0.05f, 0.25f),               // 바닥 속에 숨어 퇴적물을 먹는다
            new("toxin", "독", 0, 1, 0, 0.05f, 0.25f),                     // 포식자가 뱉는다
            new("transp", "투명함", 0, 1, 0, 0.05f, 0.25f),                // 눈으로 찾는 포식자에게 덜 보임(색소·껍데기·큰 몸이면 소용없다)
            new("school", "떼짓기", 0, 1, 0, 0.05f, 0.25f),                // 눈·신경이 있어야. 무리가 빽빽하면 한 마리가 잡힐 확률↓
            new("preyr", "먹이 크기비", 0.5f, 4, 1.5f, 0.15f, 0.8f),        // ln(몸길이 ÷ 좋아하는 먹이 길이), 몸 수준 섭식. 1.5 ≈ 4.5배
            // 5단계: 땅
            new("dry", "마름 견디기", 0, 1, 0, 0.05f, 0.25f),               // 겉막·점액·휴면(포자·낭종)으로 물 밖에서 버팀. 물속에선 쓸모없는 비용
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
        public float FIX = 4e-5f;         // 세포당 고정비(핵·유전체 ≈ 5 μm³ 몫의 기초 대사): 2 μm 아래 세포만 손해 (Raven 1998; 가장 작은 진핵생물 ~1 μm)
        public float FIXB = 0.01f;        // 세포 하나 짓는 비용 중 크기와 무관한 부분(10 μm 세포 = 1)
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
        public float MAXING = 0.16f;      // 최대 섭식(10 μm 세포 질량당 시간당), 동물은 질량^0.75. 원생생물 Imax ~0.2/h·성장 효율 ~⅓ → 최대 성장 1–2/일 (Hansen 1997)
        public float PRED_EFF = 0.6f;     // 동화 효율(소화 비용 뺀 몫, mizer α = 0.6). 기초 대사는 따로 빼므로 총성장효율은 관측처럼 20–30%가 된다 (Straile 1997)
        public float BAC_E = 3e-4f;       // 세균 한 마리 에너지(10 μm 세포 = 1, 부피비)
        public float DEATH = 0.002f;      // 모형 밖 포식자·노화 (시간당, 하루 ~5%: 플랑크톤 모형의 선형 사망 0.02–0.1/일)
        public float VIR = 0.012f, VIR_K = 2e4f;   // 계통별 바이러스: 흔한 계통일수록 많이 죽는다(kill-the-winner, Thingstad 2000). 시간당 최대, 반포화 밀도(/mL)
        public float VSP = 1, VIR_CAP = 20;   // 1이면 바이러스 숙주 밀도 = 같은 종의 겉 세포 밀도, 사망 ∝ 밀도(포화 없음, VIR × VIR_CAP까지)
        public float SW = 2;              // 포식 전환 지수 α. 포식자가 흔한 먹이 종에 몰린다(몫 ∝ 만남^α, 총 섭식은 그대로; Vallina 2014, Prowe 2012). 1 = 끔
        public float MIXO = 0.5f;         // 혼합영양 trade-off — 광합성 몫 × (1 − MIXO·식세포), 식세포 몫 × (1 − MIXO·광합성) (Ward & Follows 2016)
        public float EXU = 0.15f;         // 식물성 플랑크톤 삼출 — 광합성 산물 중 DOM으로 내보내는 몫(PER 10–20 %, Baines & Pace 1991; DON 방출은 질소 흡수의 25–40 %, Bronk 1994). 세균 → 종속영양 편모충(미생물 고리, Azam 1983)
        public float HGT = 0.3f;          // 단세포의 새 변이 중 수평 유전자 이동 몫(군체는 0.3배, 발생 다세포는 0). 이웃 계통의 생리 유전자 하나를 그대로 얻는다
        public float COLD = 1, UFLOW = 0.05f;   // 군체 세포끼리 확산 경계층이 겹쳐 묽은 영양염 친화도가 준다(Pahlow 1997). 0 = 끔, 1 = 정전 용량 근사 그대로. UFLOW: 붙어 사는 생물이 받는 바닥 물살(m/s, 연안 수 cm/s)
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
        public float MUT_DEL = 0.2f;      // 침입 적합도가 부모보다 낮은 돌연변이도 이 확률로 기록(돌연변이-선택 평형의 표준 변이: 환경이 바뀌면 쓸모가 생긴다)
        public float MUT_FLOOR = 1f / 120;   // 다세포(세포 4개 이상) 계통은 몫이 작아도 열흘에 한 번은 돌연변이를 얻는다. 게임 압축: 진화의 앞줄을 빠르게(단세포까지 주면 거대 포식 세포가 생태계를 무너뜨렸다)
        public float MUT_EDGE = 0.15f;    // 바다에서 태어난 돌연변이 중 이웃 조간대·육지 물막으로 쓸려 가 그곳에서 겨루는 몫
        public float MERGE_D = 0.5f;      // 계통 칸이 모자라면 이 거리 안의 줄어드는 계통을 더 많은 쪽에 합친다(유전자 한 걸음 ≈ 0.4)
        public float KW = 0.04f, KT = 0.3f, KCHL = 2.4e-5f, KUV = 0.2f;   // 맑은 바닷물·연안 탁도(× e^(−수심/50 m))·엽록소·자외선 감쇠(m⁻¹)
        public float QN = 0.83e-3f;       // 10 μm 세포 1개/mL가 품은 질소(μM) (Menden-Deuer & Lessard 2000, C:N 6.6)
        public float KSED = 0.01f, NSED = 40;   // 바닥 퇴적물 영양염 공급(피스톤 속도 m/h): 영양염 100% = 40 μM
        public float RIV = 0.17f;         // 강이 해안에 싣고 오는 영양염(μM·m/h, 영양염 50%·해안 100%일 때 ≈ 4 mmol/m²/일)
        public float KZ_ML = 1e-2f, KZ_SHELF = 1e-4f, KZ_DEEP = 3e-5f;   // 세로 섞임 Kz(m²/s): 혼합층, 혼합층 아래 대륙붕(조석), 대양 내부 (Munk 1966)
        public float UPW = 0.05f;         // 연안 용승(m/h, 해안 100%일 때 ≈ 1 m/일): 수심 300 m 위로 깊은 물을 끌어올린다
        public float KH_H = 2e4f;         // 해역 사이 수평 교환(m²/s): 소용돌이 확산 + 해류 몫. 1000 km를 몇 년에 섞는다
        public float KEX = 0.12f;         // 수면 산소 교환 피스톤 속도(m/h ≈ 3 m/일)
        public float KO2 = 4.8e-4f, KDEC = 0.03f / 24;       // 에너지 1(10 μm 세포 1개분, 탄소 ~6 pmol)/mL ↔ 용존 산소(대기 % 환산, 20°C 포화 280 μM)
        // 물질 순환: 죽은 몸·똥 → 유기물 입자(POM, 바다눈) → 세균이 녹임 → 녹은 유기물(DOM) → 세균이 먹고 숨 쉬며 영양염으로(미생물 고리, Azam 1983)
        public float KDOM_B = 0.2f, KDOM_O = 5;   // DOM 반포화(μM N): 세균은 낮은 농도에서도 잘 먹고, 원생생물 흡수(삼투 영양)는 서툴다
        public float BAC_Y = 0.3f;        // 세균 성장 효율(del Giorgio & Cole 1998: 바다 0.1–0.4)
        public float BAC_M = 0.01f;       // 세균 사망(바이러스 용해, 시간당, 세균 1e6/mL일 때) → DOM(바이러스 고리, Wilhelm & Suttle 1999)
        public float BAC_REF = 1e6f, CYA_REF = 3e5f;   // 바이러스는 숙주가 많을수록 잘 퍼진다(kill-the-winner): 사망률 ∝ 밀도/기준 밀도
        public float KH = 0.1f / 24;      // POM → DOM 분해(시간당, 하루 ~10%)
        public float WS = 20f / 24;       // POM 침강(m/h): 부스러기·바다눈 평균 하루 ~20 m → 분해 깊이 수백 m (Martin 1987)
        public float F_EGEST = 0.3f;      // 먹고 남긴 질소 중 똥(POM)으로 나가는 몫(나머지는 배설 = 영양염)
        public float F_LYSIS = 0.3f;      // 포식 말고 죽은 몸 중 바로 녹는 몫(DOM), 나머지는 사체 입자(POM)
        public float DET_Q = 0.5f;        // 부스러기 먹이 질(살아 있는 먹이 대비 소화되는 비율)
        public float BURY = 0.002f / 24;  // 퇴적물 속 유기물이 묻혀 순환에서 빠지는 속도(시간당). 묻힌 탄소만큼 길게 보면 산소가 남는다(Berner 2004)
        public float CYA_G = 0.05f, CYA_M = 0.006f, CYA_IK = 0.05f;   // 남세균(1 μm 광합성 세균): 최대 성장(시간당, 하루 ~1회 분열), 바이러스 사망, 광포화
        public float JUMP = 1e-8f;        // 단세포의 먼 거리 이동(시간당, 표층에서 아무 해역으로)
        public float DESIC = 0.2f;        // 마름(물 밖에 드러난 표면적당, 시간당): 마름 견디기 0인 원생생물은 땅에서 하루 몸의 몇 배를 잃는다(몇 시간이면 죽음), 조간대는 ⅓
        public float TIDE = 0.35f;        // 조간대가 물 밖에 드러나는 몫(썰물·햇볕). 육지는 1
        public float LANDX = 0.02f;       // 육지 물막과 이웃 사이 교환(바닷물 교환 대비): 물보라·빗물·기어 감
        public float NLAND = 0.3f;        // 흙 속 물막 영양염(바다 퇴적물 대비, × 풍화 배율): 바위가 풍화되며 나오는 인
        public float SED_Q = 0.3f;        // 퇴적물 유기물의 먹이 질(대부분 분해 안 되는 찌꺼기)
        public float SZM = -0.25f;        // 바깥 사망률 ∝ 질량^−¼ (Peterson & Wroblewski 1984, McGurk 1986)
        // 지구 시스템(Earth.cs). 지질 과정은 압축: 생태 1일 = 지질 1000년
        public float TAU_C = 300;         // CO₂가 풍화로 평형을 찾는 시간(일 = 지질 30만 년; 규산염 풍화 온도조절기, Walker 1981)
        public float TAU_O2 = 500;        // 대기 산소가 매몰 평형을 찾는 시간(일)
        public float TAU_ICE = 10;        // 빙상이 자라고 녹는 시간(일 = 지질 1만 년)
        public float TAU_AER = 300;       // 화산 황산 에어로졸이 걷히는 시간(생태 일, 실제 1–2년)
        public float PRIMO = 0.05f;           // 원시 바다: 처음 드러난 땅 몫(0 = 끄고 다 자란 대륙으로 시작). 조산대가 땅을 올려 대륙이 자란다
        public float CRUST_T = 1500;      // 원시 바다 → 다 자란 대륙까지 걸리는 날(격동 0.5 기준, 지질 300만 년으로 압축. 실제 지각 성장은 수십억 년, Dhuime 2012)
        public float TAU_GEO = 3000;      // 솟은 섬·땅이 깎이고 가라앉는 시간(일 = 지질 300만 년). 없으면 화산 섬이 쌓이기만 해 바다가 메워진다
        public float ICE_ALB = 4;         // 얼음 반사 되먹임(얼음 비율 1당 °C)
        public float BUR21 = 1e14f;          // 대기 산소 21%와 평형인 유기물 매몰 속도(에너지/일/km²)
    }

    public sealed class Strain
    {
        public int id, slot, sp, parent, K; public float[] g; public long born; public double gen, total, peak, kills;
        public float hue; public bool dev; public double trend, prevTot;
        public Rec rec; public float predE, muE;   // 계통 기록, 잡아먹힘 사망률·성장률 이동평균(멸종 원인)
        // 유전형만으로 정해지는 모양 값
        public float esd0, aspect, fA, longAx, thinAx, rho;
        public float[] C = new float[World.KM];   // 단계별 세포 수(발생하지 않으면 단계 1개)
        public double[] stage = new double[World.KM];   // 단계별 개체 수 합(Totals)
    }

    // 계통 기록: 계통이 사라져도 남아 조상 사슬·혈통 따라가기·타임라인에 쓴다. why 0 생존 1 멸종 2 합쳐짐(into) 3 정리됨
    public sealed class Rec
    {
        public int id, parent, sp, into = -1; public long born, died = -1; public byte why; public bool eaten;
        public float[] g; public float hue; public double peak;
    }

    public sealed class Species
    {
        public int id, parent; public float[] rep; public long born, ext = -1; public double total, peak, kills;
        public string name, ko; public float hue; public bool named; public Look look;
        // 겉모습 표지(−1…1, 화면만 씀): 0 몸 길쭉함 · 1 색조 · 2 무늬 종류 · 3 무늬 촘촘함 · 4 무늬 진하기 · 5 부속지 수 · 6 몸꼴(갓 높이·가지 벌림) · 7 부속지 길이.
        // 자식 종은 부모 것을 물려받고 한두 형질이 크게 바뀐다(종을 가르는 진단 형질). 그래서 친척은 닮고 같은 종은 하나도 없다
        public float[] vis = new float[8];
        public static void Inherit(Species ch, Species par, int seed)
        {
            var r = new Rng((ulong)seed * 1000003UL + (ulong)ch.id);
            static float Refl(float x) => x > 1 ? 2 - x : x < -1 ? -2 - x : x;
            for (int i = 0; i < 8; i++) ch.vis[i] = Refl(par.vis[i] + 0.15f * (float)(2 * r.U() - 1));
            int a = (int)(r.U() * 8), b = (a + 1 + (int)(r.U() * 7)) % 8;
            ch.vis[a] = Refl(par.vis[a] + (r.U() < 0.5 ? -1 : 1) * (0.6f + 0.4f * (float)r.U()));
            ch.vis[b] = Refl(par.vis[b] + (r.U() < 0.5 ? -1 : 1) * (0.4f + 0.3f * (float)r.U()));
            ch.hue = (par.hue + (r.U() < 0.5 ? -1 : 1) * (35 + 35 * (float)r.U()) + 360) % 360;
        }
    }

    // 칸 하나에서 계통 하나·단계 하나의 실제 모양(가소성·온도 반영). 화면이 그릴 때도 쓴다.
    public struct Morph
    {
        public float perf, kt, esd, cellL, spines, C, M, D, L, Leff, thick, expo, pcrit, speed, sink, pe, gapeC, lnOptC, gapeB, Rs, RsA, RsB, Fc, org, lnCell, lnL, lnLeff;
        public float ovl;   // 군체 세포 하나가 받는 확산 공급 ÷ 홀로 떠 있을 때(경계층 겹침, 1 = 단세포)
        public float cruise, limb, jaw, armor, burrow, transp, school, toxin, herb, imaxF, lnPrey, graze;   // 4단계 부품: 돌아다니는 속도(매복이면 느림), 부속지·턱 효율, 갑옷, 땅 파기, 투명·떼·독, 식물 소화, 먹이 크기비
        public bool benthic;   // 땅 파는 생물: 바닥 칸에 산다   // Rs = RsA + RsB × 빛 몫(눈). ln 값은 포식 계산용
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

    public sealed partial class World
    {
        // 칸 = 해역(지도에서 수심대·위도대가 같고 이어진 칸 묶음) × 수심층. 해역마다 해저 아래 층은 비어 있다(부피 0)
        public const int RMAX = 24 + Geo.NLAND, NY = 12, LS = RMAX, V = LS * NY, KM = 8;   // 크기 단계 최대 8
        public static readonly float[] ZE = { 0, 6, 12, 20, 30, 45, 65, 90, 130, 200, 400, 1000, 4500 };   // 층 경계 수심(m): 빛이 드는 위쪽을 잘게(16층도 시험: 계산만 늘었다)
        public const int SMAX = 256, NI = 1000, TB = 132;   // TB: 모양 캐시 수온 칸(0.5 °C, −2–64 °C)

        public Params P = new();
        public int seed; public long tick; public float[] env = new float[Ev.N], target = new float[Ev.N];
        public float rate = 0.0001f;      // 환경 변화 속도(범위 대비 틱당). 천천히 바꿔야 진화적 구조가 일어난다
        public bool parallel = true;

        public readonly double[] cnt = new double[SMAX * V * KM];   // 계통 × 칸 × 단계 개체 수(실제 개체 수)
        public readonly float[] mu = new float[SMAX * V * KM];      // 지난 틱 성장률(/h) — 주화성·화면용
        public readonly float[] cue = new float[SMAX * V];          // 포식 신호 0–1 (가소성)
        public readonly float[] gP = new float[SMAX * V * KM], gB = new float[SMAX * V * KM], kR = new float[SMAX * V * KM];   // 지난 틱 포식·세균 섭식 이득, 잡아먹힘 사망률(화면·시험용)
        public readonly double[] nut = new double[V], bac = new double[V], chl = new double[V], dox = new double[V], o2d = new double[V];
        public readonly double[] pom = new double[V], dom = new double[V];   // 유기물 입자·녹은 유기물(μM N)
        public readonly double[] cya = new double[V];   // 남세균(마리/mL): 진화하지 않는 바탕 생산자. 빛·영양염으로 자라 산소를 내고, 원생생물이 세균처럼 먹는다
        public double buried;            // 퇴적물에 묻힌 유기물 누계(에너지, 10 μm 세포 단위)
        public readonly float[] T = new float[V], I = new float[V], U = new float[V];
        public readonly Geo geo = new();
        public readonly double[] vol = new double[V];                           // 칸 부피(mL). 해저 아래 = 0
        public readonly float[] dz = new float[V], zc = new float[V], kb = new float[V];
        public readonly float[] ex = new float[V];                              // 물 밖에 드러나는 몫(조간대 TIDE, 육지 1, 바다 0)   // 층 두께·가운데 수심(m), 아래 층과 섞이는 속도(Kz/거리, m/h)
        public readonly int[] nyR = new int[RMAX];                              // 해역마다 물이 있는 층 수
        public readonly float[] mld = new float[RMAX], sst = new float[RMAX];   // 혼합층 깊이(m)·표층 수온
        public int NR => geo.NR;
        public double volTot;                                                   // 바다 전체 부피(mL)
        public int Bot(int c) => (nyR[c] - 1) * LS + c;                         // 해역 c의 바닥 칸
        public int Vox(int c, double z) { int y = 0; while (y < nyR[c] - 1 && ZE[y + 1] <= z) y++; return y * LS + c; }   // 해역 c, 수심 z(m) → 칸
        readonly int[] tb = new int[V];                                         // 칸 → 모양 캐시 수온 칸
        double[] qa = new double[0];                                            // 해역 사이 맞닿은 면 ÷ 중심 거리(링크 × 층, m). × Kh = 교환 유량
        double qmax;                                                            // 칸마다 Σ qa ÷ 부피의 최댓값(1/m²). × Kh·틱 = 틱당 바뀌는 물 몫 → 수평 섞기를 몇 번에 나눌지
        List<(int nb, int li)>[] adj = new List<(int, int)>[0];                 // 해역 → 이웃 해역·링크
        int nsub = 1;                                                           // 세로 섞임 잘게 나눈 횟수(틱마다)
        public readonly Strain[] strains = new Strain[SMAX];
        public readonly List<int> live = new();
        public readonly Dictionary<int, Species> species = new();
        public readonly Dictionary<int, Rec> recs = new();
        public int pin = -1;   // 화면이 따라가는 계통 id(기록 정리에서 뺀다)
        public readonly List<(long t, string type, string text)> events = new();
        public readonly HashSet<Look> looksSeen = new();   // 나온 적 있는 몸 설계(순서 없음)
        public bool landed, landA;                          // 땅에 자리 잡은 생물·동물이 나왔나
        public double totalCount, totalBio, births, deaths, kills;
        public long mutTry, mutKeep, hgtTry, hgtKeep; public readonly HashSet<int> hgtSeen = new();   // 돌연변이 후보·기록한 수(누계)
        public readonly long[] mutHab = new long[6];   // 사는 곳별(바다·조간대·육지) 돌연변이 후보·정착 수
        public readonly double[] birthHab = new double[3], cntHab = new double[3];   // 사는 곳별 출생·개체 수(누계, 점검용)
        public readonly History hist = new();
        public readonly double[] prof = new double[6];   // 단계별 누적 ms(캐시·칸·이동·화학·돌연변이·합계)
        int nextId = 1, nextSp = 1;
        readonly double[] vBirth = new double[V], vDeath = new double[V], vKill = new double[V], dep = new double[V];
        readonly float[] satA = new float[SMAX * V * KM];   // 지난 틱 포식자 배부름(Holling II 1/(1+먹이/최대)): 돌연변이 침입 적합도 계산용
        readonly double[] depS = new double[SMAX * LS], genAcc = new double[SMAX * V], killAcc = new double[SMAX * V];
        readonly ThreadLocal<Scratch> scratch = new(() => new Scratch());
        readonly Morph[] mc = new Morph[SMAX * TB * KM];   // 계통 × 수온 칸 × 단계 모양 캐시(가소성 없는 계통). 유전형이 안 바뀌므로 계통이 사는 동안 유지
        readonly bool[] mcOk = new bool[SMAX * TB]; float mcSalt = -1;

        // 모양: 가소성이 있으면 칸마다(포식 신호), 없으면 수온 칸 캐시(감지 거리만 그 칸 빛으로)
        public Morph M(int s, int v, int k)
        {
            var st = strains[s];
            if (st.g[Gn.Plastic] > 0.02f || !mcOk[s * TB + tb[v]]) return MorphAt(st, v, k, st.g[Gn.Plastic] > 0.02f ? cue[s * V + v] : 0);
            var m = mc[(s * TB + tb[v]) * KM + k]; m.Rs = m.RsA + m.RsB * I[v] / (I[v] + 0.05f); return m;
        }
        readonly bool[] pres = new bool[SMAX * V * KM];    // 틱 시작 때 있었나(이웃 칸 확인용 — 병렬 칸 계산이 서로의 개체 수 변화를 읽지 않게)
        void Cache(int s)
        {
            var st = strains[s];
            for (int q = Ix(s, 0, 0), e = q + V * KM; q < e; q++) pres[q] = cnt[q] > 0;
            if (st.g[Gn.Plastic] > 0.02f) return;
            for (int v = 0; v < V; v++)
            {
                int b = s * TB + tb[v]; if (mcOk[b] || vol[v] <= 0) continue;
                bool any = false; for (int k = 0; k < st.K && !any; k++) any = pres[Ix(s, v, k)] || v >= LS && pres[Ix(s, v - LS, k)] || v + LS < V && pres[Ix(s, v + LS, k)];
                if (!any) continue;
                for (int k = 0; k < st.K; k++) mc[b * KM + k] = MorphT(st, -2 + (tb[v] + 0.5f) * 0.5f, k, 0);
                mcOk[b] = true;
            }
        }

        sealed class Scratch
        {
            public int[] idx = new int[NI], stg = new int[NI], ord = new int[NI]; public float[] key = new float[NI]; public Morph[] m = new Morph[NI];
            public double[] gainP = new double[NI], gainB = new double[NI], kill = new double[NI], risk = new double[NI];
            public int[] pj = new int[NI]; public double[] pw = new double[NI];
            public double[] dD = new double[NI], dUp = new double[NI], dB = new double[NI], need = new double[NI], aff = new double[NI], ing = new double[NI], eff = new double[NI], dens = new double[NI];
            public double[] host = new double[NI]; public Dictionary<int, double> spH = new();
            public int[] spi = new int[NI]; public double[] spW = new double[NI]; public Dictionary<int, int> spMap = new(); public int nsp;
            public double[] B = new double[14]; public double[] row = new double[V * KM], rowB = new double[V];
        }

        public World(int seed = 1, float[] envInit = null) { Reset(seed, envInit); }

        public void Reset(int seed, float[] envInit = null)
        {
            this.seed = seed; tick = 0; nextId = 1; nextSp = 1;
            for (int e = 0; e < Ev.N; e++) env[e] = target[e] = envInit != null ? envInit[e] : Ev.Def[e].init;
            Array.Clear(cnt, 0, cnt.Length); Array.Clear(mu, 0, mu.Length); Array.Clear(cue, 0, cue.Length); Array.Clear(strains, 0, SMAX); Array.Clear(mcOk, 0, mcOk.Length);
            live.Clear(); species.Clear(); recs.Clear(); events.Clear(); hist.Clear(); looksSeen.Clear(); looksSeen.Add(Look.Cell); landed = landA = false;
            Array.Clear(dep, 0, V); Array.Clear(depS, 0, depS.Length); Array.Clear(chl, 0, chl.Length);
            geo.sea = 0; geo.Generate(seed); if (P.PRIMO > 0) geo.Drown(P.PRIMO); else Array.Copy(geo.h, geo.hM, geo.h.Length); geo.Regions(RMAX - Geo.NLAND); Geometry(); EarthReset(); Tweaks();
            for (int v = 0; v < V; v++)
            {
                bool w = vol[v] > 0;
                nut[v] = w ? P.NSED * env[Ev.Nut] / 100 * 0.6 : 0; bac[v] = w ? 1e6 : 0; dox[v] = w ? env[Ev.O2] : 0; pom[v] = w ? 0.5 : 0; dom[v] = w ? 2 : 0; cya[v] = w ? 1e5 : 0;
            }
            buried = 0;
            var g0 = new float[Gn.N]; for (int i = 0; i < Gn.N; i++) g0[i] = Gn.Def[i].init;
            var sp = NewSpecies(g0, 0); sp.name = "Protomonas primigenia"; sp.ko = "원시 편모충 (공통 조상)"; sp.named = true;
            var s = NewStrain(g0, sp.id, 0, 0);
            var r = new Rng((ulong)seed);
            for (int v = 0; v < V; v++) if (vol[v] > 0 && zc[v] < 100 && ex[v] < 1) cnt[Ix(s.slot, v, 0)] = Math.Round(vol[v] * (400 + 200 * r.U()));   // 빛이 드는 100 m 위, 400–600 마리/mL
            Log("start", $"원시 바다(해역 {NR}곳)에 공통 조상 원생생물이 퍼져 있습니다 (mL당 약 500마리).");
            Layers(); Totals();
        }

        // 지도 → 해역 물기둥: 층 두께·부피, 이웃 해역과 맞닿은 면(수평 교환). 지형이 바뀌면 다시 부른다
        void Geometry()
        {
            Array.Clear(vol, 0, vol.Length); Array.Clear(dz, 0, dz.Length); Array.Clear(zc, 0, zc.Length); Array.Clear(nyR, 0, nyR.Length); Array.Clear(kb, 0, kb.Length); Array.Clear(ex, 0, V); volTot = 0;
            for (int c = 0; c < NR; c++)
            {
                float D = geo.rDepth[c]; int n = 0;
                while (n < NY && ZE[n] < D) n++;
                if (n > 1 && n < NY && D - ZE[n - 1] < 0.3f * (ZE[n] - ZE[n - 1])) n--;   // 얇은 바닥 조각은 위층에 붙인다
                nyR[c] = n;
                for (int y = 0; y < n; y++)
                {
                    int v = y * LS + c; float bot = y == n - 1 ? D : ZE[y + 1];
                    dz[v] = bot - ZE[y]; zc[v] = (ZE[y] + bot) / 2; vol[v] = geo.rArea[c] * (double)dz[v] * 1e6; if (c < geo.NO) volTot += vol[v];
                }
                ex[c] = geo.rKind[c] == 1 ? P.TIDE : geo.rKind[c] == 2 ? 1 : 0;
            }
            adj = new List<(int, int)>[RMAX]; for (int c = 0; c < RMAX; c++) adj[c] = new();
            qa = new double[geo.links.Count * NY];
            for (int li = 0; li < geo.links.Count; li++)
            {
                var (a, b, e, dist) = geo.links[li]; adj[a].Add((b, li)); adj[b].Add((a, li));
                double f = geo.rKind[a] == 2 || geo.rKind[b] == 2 ? P.LANDX : 1;
                for (int y = 0; y < Math.Min(nyR[a], nyR[b]); y++) qa[li * NY + y] = f * e * Geo.CELL * Math.Min(dz[y * LS + a], dz[y * LS + b]) / dist;
            }
            // 얕고 좁은 조간대는 한 틱에 제 부피보다 많은 물을 바다와 바꾼다 → 한 번에 풀면 값이 넘쳐 음수·발산(NaN). Mix가 이 값으로 잘게 나눈다
            var fo = new double[V];
            for (int li = 0; li < geo.links.Count; li++)
            {
                var (a, b, _, _) = geo.links[li];
                for (int y = 0; y < NY && qa[li * NY + y] > 0; y++) { int va = y * LS + a, vb = y * LS + b; fo[va] += qa[li * NY + y] / (vol[va] * 1e-6); fo[vb] += qa[li * NY + y] / (vol[vb] * 1e-6); }
            }
            qmax = fo.Max();
        }

        public static int Ix(int s, int v, int k) => (s * V + v) * KM + k;
        public void Log(string type, string text) { events.Add((tick, type, text)); if (events.Count > 400) events.RemoveAt(0); }

        Species NewSpecies(float[] g, int parent)
        {
            var sp = new Species { id = nextSp++, parent = parent, rep = (float[])g.Clone(), born = tick, hue = 190 };
            if (species.TryGetValue(parent, out var ps)) Species.Inherit(sp, ps, seed);
            species[sp.id] = sp; return sp;
        }

        Strain NewStrain(float[] g, int sp, int parent, double gen)
        {
            int slot = -1;
            for (int i = 0; i < SMAX; i++) if (strains[i] == null) { slot = i; break; }
            if (slot < 0) return null;
            var s = new Strain { id = nextId++, slot = slot, sp = sp, parent = parent, g = g, born = tick, gen = gen, hue = (nextId * 97.3f) % 360 };
            Shape(s); strains[slot] = s; live.Add(slot); Array.Clear(mcOk, slot * TB, TB);
            s.rec = recs[s.id] = new Rec { id = s.id, parent = parent, sp = sp, born = tick, g = g, hue = s.hue };
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
            if (g[Gn.Cells] >= 4 && g[Gn.Diff] >= 0.5f) s.rho += 80 * g[Gn.Exo] + 600 * g[Gn.Mineral] * MathF.Max(g[Gn.Exo], 0.15f * g[Gn.Skel]);   // 키틴 ~1.2, 껍데기·뼈(탄산칼슘·인산칼슘 ~2.7)
            // 발생: 생식-체세포 분화(diff>0.5)가 있고 16세포 이상이면 한 세포(알)에서 자라는 개체 → 크기 단계(질량 10배마다)
            float cA = g[Gn.Cells] >= 4 && g[Gn.Diff] >= 0.5f ? MathF.Pow(2, g[Gn.Cells]) : MathF.Pow(2, MathF.Floor(g[Gn.Cells]));
            s.dev = g[Gn.Cells] >= 4 && g[Gn.Diff] >= 0.5f;
            if (!s.dev) { s.K = 1; s.C[0] = cA; return; }
            float c0 = MathF.Pow(2, MathF.Min(g[Gn.Egg], g[Gn.Cells] - 1));
            s.K = Math.Clamp((int)MathF.Ceiling(MathF.Log(cA / c0, 5)) + 1, 2, KM);
            for (int k = 0; k < s.K; k++) s.C[k] = c0 * MathF.Pow(cA / c0, k / (float)(s.K - 1));
        }

        public static float ThermalPerf(float[] g, float t)
        {
            // 최적 온도 위쪽이 더 가파름(Huey & Kingsolver 1989). 폭이 넓으면 정점이 낮다
            float d = t - g[Gn.Topt], w = d > 0 ? g[Gn.Tbreadth] * 0.6f : g[Gn.Tbreadth];
            return MathF.Pow(8 / g[Gn.Tbreadth], 0.2f) * MathF.Exp(-(d / w) * (d / w));
        }

        public float Kt(float t) => Math.Clamp(MathF.Exp(P.E_ACT * (1 / 293.15f - 1 / (t + 273.15f))), 0.25f, 4);

        // 칸 v에서 계통 s·단계 k의 실제 모양. q = 포식 신호(가소성)
        public Morph MorphAt(Strain s, int v, int k, float q) { var m = MorphT(s, T[v], k, q); m.Rs = m.RsA + m.RsB * I[v] / (I[v] + 0.05f); return m; }

        // 수온 t에서의 모양(감지 거리는 어둠 기준 RsA, 눈 몫 RsB는 빛에 따라 MorphAt·M이 더한다)
        // 세포 C개가 붙은 덩어리의 확산 정전 용량 ÷ (C × 세포 하나). 실(D=1) = 긴 회전타원체, 판(2) = 납작한 회전타원체, 공(3) = 구. 사이 모양은 ln으로 잇는다
        static float Ovl(float C, float D)
        {
            if (C <= 1) return 1;
            double A = C, Rp = Math.Sqrt(C);
            double f = Math.Sqrt(A * A - 1) / Math.Log(A + Math.Sqrt(A * A - 1)) / C;     // 긴 회전타원체(반축 C·a, a)
            double p = Math.Sqrt(Rp * Rp - 1) / Math.Acos(1 / Rp) / C;                  // 납작한 회전타원체(반축 √C·a, √C·a, a)
            double b = Math.Pow(C, -2 / 3.0);                                           // 구(반지름 C^⅓·a)
            double l = D <= 2 ? Math.Log(f) + (Math.Log(p) - Math.Log(f)) * (D - 1) : Math.Log(p) + (Math.Log(b) - Math.Log(p)) * (D - 2);
            return (float)Math.Exp(l);
        }

        public Morph MorphT(Strain s, float t, int k, float q)
        {
            var g = s.g; Morph m;
            m.perf = ThermalPerf(g, t); m.kt = Kt(t);
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
            m.lnCell = MathF.Log(cellL); m.lnL = MathF.Log(m.L); m.lnLeff = MathF.Log(m.Leff);
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
            float bd = s.dev ? 1 : 0;                                             // 몸 부품은 발생하는 다세포에서만
            m.pcrit = (4e-8f * he * he + 2 * g[Gn.Circ]) * (1 + g[Gn.Neuro]) * (1 + 1.5f * bd * g[Gn.Exo] * (1 - g[Gn.Circ]));   // 외골격(큐티클)은 피부 호흡을 막는다 → 아가미가 없으면 산소가 더 필요
            m.sessile = g[Gn.Attach] > 0.5f && (!s.dev || k > 0);                 // 발생하는 부착 생물은 유생(단계 0)만 떠다닌다
            m.org = Math.Clamp(g[Gn.Circ] + g[Gn.Neuro], 0, 1) * (s.dev ? 1 : 0);
            // 헤엄: 편모 추진은 크기와 거의 무관, 섬모는 표면적에 비례해 큰 세포에서 빨라진다 (Fenchel 1987).
            // 신경·근육이 있으면 몸길이/초로 헤엄(어류 1–3 BL/s), 척삭·골격·좌우대칭이 효율을 올림
            float drag = 1 + 0.1f * MathF.Abs(g[Gn.Elong]) + 0.8f * m.spines + 0.3f * g[Gn.Lorica];
            float sp = (150 * g[Gn.Flagella] + 250 * g[Gn.Cilia] * MathF.Pow(m.esd / 10, 0.8f)) / drag / (1 + 0.5f * g[Gn.Wall]);
            if (m.C > 1) sp *= MathF.Pow(m.C, -0.1f) * (1 + 0.5f * g[Gn.Diff]);
            // 근육은 지렛대(척삭·골격, 외골격)에 붙어야 힘을 낸다. 부속지(다리·지느러미)는 지렛대와 마디(또는 척추)가 있어야 쓸모 있다. 무거운 갑옷은 느리게 한다
            float lever = MathF.Max(g[Gn.Skel], 0.8f * g[Gn.Exo]), segF = MathF.Max(MathF.Min(1, g[Gn.Seg] / 3), g[Gn.Skel]);
            m.limb = bd * g[Gn.Limb] * MathF.Max(0.3f, MathF.Max(g[Gn.Skel], g[Gn.Exo])) * (0.4f + 0.6f * segF);
            float heavy = 1 + 0.6f * bd * g[Gn.Exo] * (0.5f + g[Gn.Mineral]);
            float muscle = s.dev ? 2 * g[Gn.Neuro] * (0.3f + 0.7f * lever) * (0.4f + 0.6f * g[Gn.Bilat]) * (1 + 0.6f * m.limb) / heavy * m.L : 0;
            m.speed = m.sessile ? 0 : MathF.Max(sp, muscle);                      // μm/s
            m.cruise = m.speed * (1 - 0.9f * g[Gn.Ambush]);                       // 매복: 평소엔 거의 안 움직이고(만남·들킴·헤엄 비용↓) 덮칠 때·도망칠 때만 전속력
            m.burrow = bd * g[Gn.Burrow] * (k > 0 || s.K == 1 ? 1 : 0); m.benthic = m.burrow > 0.5f && !m.sessile;   // 유생은 떠다닌다
            m.armor = bd * MathF.Max(g[Gn.Exo] * (0.5f + 0.5f * g[Gn.Mineral]), 0.6f * g[Gn.Skel] * g[Gn.Mineral]);
            m.toxin = g[Gn.Toxin]; m.herb = g[Gn.Herb]; m.imaxF = 1 - 0.25f * g[Gn.Herb];   // 식물을 삭이는 긴 장 = 음식이 오래 머문다
            m.school = bd * g[Gn.School] * g[Gn.Eye] * g[Gn.Neuro];
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
            // 경계층 겹침은 물이 몸을 스칠수록 준다(Sherwood: Sh = 1 + 0.6 Re^½ Sc^⅓, Sc ≈ 1000). 붙어 사는 몸은 바닥 물살, 떠다니는 몸은 가라앉음·헤엄 속도
            float uRel = m.sessile ? P.UFLOW : MathF.Max(MathF.Abs(m.sink) / 3600, m.speed * 1e-6f);
            m.ovl = MathF.Min(1, Ovl(m.C, m.D) * (1 + 6 * MathF.Sqrt(uRel * m.L)));   // Re = u·L/ν (L μm × 1e-6 ÷ ν 1e-6)
            m.pe = g[Gn.Phago] * (1 - g[Gn.Wall]) * (1 - g[Gn.Wall]) * (1 - P.MIXO * g[Gn.Photo]);           // 세포벽은 식세포 작용을 막는다
            // 세포 수준 섭식(식세포 작용·여과): 입 크기와 좋아하는 먹이 크기 (Hansen 1994: 와편모류 1:1, 편모충 3:1, 섬모충 8:1)
            m.gapeC = m.esd * s.longAx * (0.45f + 0.6f * m.pe);
            float engulf = Math.Clamp((m.pe - 0.7f) / 0.3f, 0, 1);
            m.lnOptC = 1.0986f + 0.98f * g[Gn.Cilia] - 1.0986f * engulf;
            // 세포 수준 청소율: 섭식류(편모·섬모가 만드는 물살). 군체·해면은 겉 세포가 모두 거른다
            m.Fc = m.pe < 0.02f ? 0 : P.CLR * m.pe * MathF.Pow(m.esd / 10, 2.3f) * (0.5f + sp / 150) * MathF.Pow(m.C, 0.85f) * m.expo * (1 - 0.8f * g[Gn.Ambush]);   // 매복하면 물살을 덜 일으킨다
            // 몸 수준 섭식(소화관): 입 = 몸길이의 ~1/3, 감지 거리 = 기계 감각(신경) + 눈(빛이 있어야)
            m.jaw = bd * g[Gn.Jaw] * MathF.Max(0.2f, MathF.Max(g[Gn.Skel], g[Gn.Exo]));   // 턱은 단단한 데 붙어야 문다(턱뼈·큰턱)
            m.gapeB = s.dev ? m.L * 0.33f * g[Gn.Gut] * (1 + 1.2f * m.jaw) : 0;
            m.lnPrey = g[Gn.PreyR]; m.graze = MathF.Max(m.limb, m.jaw);           // 긁어 먹기(다리·치설·턱): 붙어 사는 먹이
            // 투명함: 색소·엽록체·껍데기·큰 몸은 숨길 수 없다
            m.transp = g[Gn.Transp] * (1 - g[Gn.Pigment]) * (1 - 0.8f * g[Gn.Photo]) * (1 - 0.7f * bd * g[Gn.Mineral] * MathF.Max(g[Gn.Exo], g[Gn.Skel])) * (1 - 0.5f * MathF.Min(1, m.L / 5e4f));
            m.RsA = m.L * (0.3f + 0.4f * g[Gn.Neuro]); m.RsB = m.L * 0.8f * g[Gn.Eye] * g[Gn.Eye] * g[Gn.Neuro];   // 1 g 어류 탐색 부피 ~1 m³/h (Hartvig 2011)
            m.Rs = m.RsA;
            return m;
        }

        // ---------- 환경 ----------
        public void SetTarget(int e, float v) => target[e] = v;

        void MoveEnv()
        {
            for (int e = 0; e < Ev.N; e++)
            {
                if (e == Ev.O2 && o2Bio) continue;                                   // 생물이 정하는 산소
                ref float x = ref e == Ev.Temp ? ref tBase : ref env[e];             // 수온 목표 = 기후 바탕(지구 시스템이 사건·CO₂·얼음 몫을 더한다)
                float cur = x, tg = target[e]; if (cur == tg) continue;
                float st = rate >= 1 ? float.MaxValue : (Ev.Def[e].hi - Ev.Def[e].lo) * rate * P.DT;
                x = MathF.Abs(tg - cur) <= st ? tg : cur + MathF.Sign(tg - cur) * st;
            }
        }

        // 표층 수온: 기준 수온(환경 '수온' = 적도 근처 바다 −3 °C) − 위도(sin²), 계절 폭은 중위도에서 가장 크다
        public float SST(float lat, float sea)
        {
            float s = MathF.Sin(lat * MathF.PI / 180);
            return MathF.Max(-1.8f, env[Ev.Temp] + 3 - 30 * s * s + P.SEASON_T * MathF.Sin(2 * lat * MathF.PI / 180) * sea);
        }

        // 해역·층마다 수온(혼합층 아래로 깊은 물 수온까지), 자외선, 세로 섞임, 빛(층 평균, 엽록소·탁도 그늘)
        void Layers()
        {
            float sea = MathF.Sin(2 * MathF.PI * (tick * P.DT / 24f / 365f - 0.25f)), win = (1 - sea) / 2;   // 0일 = 한겨울, 반년 = 한여름(sea +1), win 1 = 한겨울
            float td = MathF.Max(-1, SST(60, -1) + 1);                              // 깊은 물 = 극지 겨울 바다에서 가라앉은 물
            double mx = 0;
            for (int c = 0; c < NR; c++)
            {
                float lat = geo.rLat[c], sl = MathF.Sin(lat * MathF.PI / 180), D = geo.rDepth[c];
                float raw = env[Ev.Temp] + rdT[c] + 3 - 30 * sl * sl + P.SEASON_T * MathF.Sin(2 * lat * MathF.PI / 180) * sea;
                float ts = sst[c] = MathF.Max(-1.8f, raw), ml = mld[c] = 20 + 350 * win * sl * sl;   // 겨울 혼합층: 고위도일수록 깊다(고위도 수백 m)
                iceR[c] = Math.Clamp((-1.8f - raw) / 3, 0, 1);                                      // 바다얼음: 빛을 가리고 기체 교환을 막는다
                float L0 = env[Ev.Light] / 100 * (0.35f + 0.65f * MathF.Cos(lat * MathF.PI / 180)) * (1 + MathF.Min(0.9f, 2 * P.SEASON_L * sl) * sea) * (1 - dark) * (1 - 0.85f * iceR[c]);
                float kT = P.KT * MathF.Exp(-D / 50), kuv = P.KUV + 4 * kT;
                double od = 0;
                for (int y = 0; y < nyR[c]; y++)
                {
                    int v = y * LS + c; float z = zc[v], h = dz[v], top = ZE[y];
                    T[v] = z <= ml ? ts : td + (ts - td) * MathF.Exp(-(z - ml) / 400);
                    tb[v] = Math.Clamp((int)((T[v] + 2) * 2), 0, TB - 1);
                    U[v] = env[Ev.Uv] / 100 * MathF.Exp(-kuv * top) * (1 - MathF.Exp(-kuv * h)) / (kuv * h);
                    double k = P.KW + kT + P.KCHL * chl[v];
                    I[v] = (float)(L0 * Math.Exp(-od) * (1 - Math.Exp(-k * h)) / (k * h)); od += k * h;
                    if (y < nyR[c] - 1)
                    {
                        float ze = ZE[y + 1], Kz = ze < ml ? P.KZ_ML : D < 250 ? P.KZ_SHELF : P.KZ_DEEP;
                        kb[v] = Kz * 3600 / ((h + dz[v + LS]) / 2) + (ze < 300 ? P.UPW * geo.rCoast[c] : 0);
                        mx = Math.Max(mx, kb[v] / Math.Min(h, dz[v + LS]));
                    }
                }
            }
            nsub = Math.Max(1, (int)Math.Ceiling(mx * P.DT / 0.4));
        }

        // ---------- 한 틱 ----------
        public void Step()
        {
            tick++;
            MoveEnv(); EarthStep(); Layers();
            if (MathF.Abs(env[Ev.Salt] - mcSalt) > 0.5f) { Array.Clear(mcOk, 0, mcOk.Length); mcSalt = env[Ev.Salt]; }   // 염분 → 물 밀도 → 침강이 바뀌면 모양 캐시를 다시
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
        // 칸 v의 계산 대상(여기나 위아래 칸에 있는 계통·단계)과 먹이 후보(여기 있는 것, 크기순). clr: 빠진 것의 성장률을 0으로
        int Gather(Scratch S, int v, bool clr, out int np)
        {
            int n = 0, y = v / LS, yb = nyR[v % LS] - 1; const int LK = LS * KM;
            S.spMap.Clear();
            foreach (int s in live)
            {
                var st = strains[s]; int b = Ix(s, v, 0);
                for (int k = 0; k < st.K && n < NI; k++)
                {
                    int q = b + k;
                    if (!pres[q] && !(y > 0 && pres[q - LK]) && !(y < yb && pres[q + LK])) { if (clr) mu[q] = 0; continue; }
                    S.idx[n] = s; S.stg[n] = k; S.m[n] = M(s, v, k); S.dens[n] = cnt[q] / vol[v];
                if (P.SW > 1) { if (!S.spMap.TryGetValue(st.sp, out int li)) S.spMap[st.sp] = li = S.spMap.Count; S.spi[n] = li; }
                    S.gainP[n] = S.gainB[n] = S.kill[n] = S.risk[n] = S.eff[n] = 0; n++;
                }
            }
            np = 0;
            for (int i = 0; i < n; i++) if (S.dens[i] > 0) { S.ord[np] = i; S.key[np] = S.m[i].Leff; np++; }
            Array.Sort(S.key, S.ord, 0, np);
            return n;
        }

        // 포식자 mi가 먹이 mj를 삼키는 청소율(mL/h) × 크기 선호. 세포 섭식(물살)과 몸 섭식(만남) 중 큰 쪽
        static double Attack(in Morph mi, in Morph mj, double kt)
        {
            if (mj.sessile && !mi.sessile && mi.speed < 1 && mi.gapeB <= 0) return 0;
            double F = 0;
            if (mi.Fc > 0 && mj.Leff <= mi.gapeC)
            {
                double lr = mi.lnCell - mj.lnLeff - mi.lnOptC, pc = Math.Exp(-lr * lr / 0.8);
                if (pc > 1e-4) F = mi.Fc * pc;
            }
            if (mi.gapeB > 0 && mj.Leff <= mi.gapeB)
            {
                double lr = mi.lnL - mj.lnLeff - mi.lnPrey, pb = Math.Exp(-lr * lr / 1.2);   // 포식자:먹이 길이비(유전자, 처음 ~4.5 = 질량비 ~100)
                if (pb > 1e-4)
                {
                    // 감지 거리: 눈으로 보는 몫은 투명한 먹이에서 줄어든다. 만남 속도는 평소 돌아다니는 속도(매복이면 먹이가 다가와야 만난다)
                    double R = (mi.Rs - (mi.Rs - mi.RsA) * 0.85 * mj.transp + (mi.L + mj.L) / 2) * 1e-4, vr = Math.Sqrt(mi.cruise * mi.cruise + mj.cruise * mj.cruise) * 0.36 + 0.36 * 20;
                    F = Math.Max(F, Math.PI * R * R * vr * kt * pb);                   // 만남 부피(mL/h) = π R² v_rel (Gerritsen & Strickler 1977)
                }
            }
            if (mj.sessile) F *= 1 + 2 * mi.graze;                                  // 다리·치설·턱으로 긁어 먹기
            if (mj.benthic) F *= 1 - 0.8 * mj.burrow * (1 - Math.Max(mi.burrow, 0.5 * mi.graze));   // 모래 속: 파는 포식자만 꺼낸다
            return F;
        }
        // 빠르고 보는 먹이는 잘 도망친다. 독이 있으면 뱉는다. 빽빽한 무리 속 한 마리는 덜 잡힌다(희석·혼란: 이웃이 몸길이 10배 안에 있어야)
        static double Escape(float[] gj, in Morph mj, double vi, double dj)
        {
            double e = 1 / (1 + 0.5 * mj.speed / (vi + 30)) * (1 - 0.5 * gj[Gn.Eye] * gj[Gn.Neuro]) * (1 - 0.7 * mj.toxin);
            if (mj.school > 0) { double nb = dj * Math.Pow(10 * mj.L * 1e-4, 3); e /= 1 + 3 * mj.school * Math.Min(1, nb); }
            return e;
        }
        // 삼켜도 살아남는 몫(각·벽·골판·갑옷). 턱은 갑옷을 깨고, 식물 소화는 세포벽·광합성 먹이(질소가 적고 섬유가 많다)를 삭인다
        static double Digest(float[] gj, in Morph mj, in Morph mi) => (1 - 0.7 * gj[Gn.Lorica]) * (1 - 0.3 * gj[Gn.Wall] * (1 - mi.herb)) * (1 - 0.2 * gj[Gn.Photo] * (1 - mi.herb))
            * (1 - 0.4 * gj[Gn.Skel] * (mi.gapeB > 0 ? 1 : 0)) * (1 - 0.6 * mj.armor * (1 - 0.7 * mi.jaw));
        static double Molt(Strain st) => st.dev ? 1 - 0.3 * st.g[Gn.Exo] : 1;   // 외골격은 벗고 새로 지어야 자란다(탈피): 자라기가 느리다
        double Imax(in Morph m, double kt) => P.MAXING * m.M * kt * Math.Pow(Math.Max(1, m.M / P.M0), -0.25 * m.org) * m.imaxF;
        double Eff(float[] g, in Morph m, int v)
        {
            double o2s = dox[v] / (dox[v] + P.O2_K), aer = 1 + P.AER_GAIN * g[Gn.Aerobic] * o2s * Math.Min(1, dox[v] / Math.Max(1e-3, m.pcrit)) + 0.4 * (1 - g[Gn.Aerobic]) * (1 - o2s);
            return P.PRED_EFF * Math.Min(1, aer / 2.5) * m.perf;
        }
        // 저산소: 산소가 임계(Pcrit)의 2배보다 낮으면 근육·신경 조직이 버티지 못한다 (어류 LC50 ~1.5 mg/L, 포식자는 0.5 mg/L 아래에서 사라짐)
        double Hypoxia(float[] g, in Morph m, int v) => m.org > 0 ? 0.05 * g[Gn.Neuro] * Math.Max(0, 1 - dox[v] / Math.Max(1e-3, 2 * m.pcrit)) : 0;
        // 바깥 사망(모형 밖 포식자·노화, 큰 생물일수록 낮다: 질량^−¼) + 계통별 바이러스(흔할수록, kill-the-winner)
        double Mort(in Morph m, double kt, double dens) => P.DEATH * Math.Min(kt, Math.Sqrt(kt)) * Math.Pow(Math.Max(1, m.M), P.SZM)
            + P.VIR * (P.VSP > 0 ? Math.Min(dens / P.VIR_K, P.VIR_CAP) : dens / (dens + P.VIR_K)) * Math.Min(kt, 1.5);
        // 바이러스가 만나는 숙주 밀도(VSP): 칸 v에서 종 sp의 겉 세포(/mL). 안쪽 세포는 군체가 막아 준다(Phaeocystis, Jacobsen 2007)
        double HostD(int sp, int v)
        {
            double h = 0;
            foreach (int s in live) { var st = strains[s]; if (st.sp != sp) continue; for (int k = 0; k < st.K; k++) { double c = cnt[Ix(s, v, k)]; if (c > 0) h += c / vol[v] * st.C[k] * M(s, v, k).expo; } }
            return h;
        }
        double Load(float[] g, int v) => P.LOAD * g[Gn.Mutrate] * (1 + P.UV_MUT * U[v] * (1 - 0.9 * g[Gn.Pigment]));   // 해로운 돌연변이로 죽는 자손 몫

        // 포식자 mi(유전자 gi)가 칸 v에서 만나는 먹이(에너지/h, 배부름 전): 세균·남세균(fb), 먹이 후보(S.pj·S.pw에), 부스러기(fd).
        // densI > 0이면 먹이 쪽 포식 신호(S.risk)도 더한다
        double Food(Scratch S, int v, int np, in Morph mi, float[] gi, double kt, double densI, out double fb, out double fd, out double fs, out int n2)
        {
            double pico = bac[v] + cya[v], detE = pom[v] / P.QN;   // 세균 + 남세균(크기가 비슷해 같은 먹이로 본다), 부스러기 에너지 밀도(10 μm 세포/mL 환산)
            // 세균(0.6 μm): 6 μm 안팎 세포가 가장 잘 먹고, 섬모(여과 섭식)는 큰 세포도 세균을 먹게 해 준다
            double lb = Math.Log(mi.esd / 6) / (2.2 + 2 * gi[Gn.Cilia]);
            fb = mi.Fc * Math.Exp(-lb * lb) * pico / (pico + 2e5);
            double food = fb * pico * P.BAC_E;
            double lo = Math.Min(mi.Fc > 0 ? mi.cellL / Math.Exp(mi.lnOptC + 2.5) : 1e30, mi.gapeB > 0 ? mi.L / 400 : 1e30);
            double hi = Math.Max(mi.Fc > 0 ? mi.gapeC : 0, mi.gapeB);
            n2 = 0;
            for (int p = LowerBound(S.key, np, (float)lo); p < np && S.key[p] <= hi; p++)
            {
                int j = S.ord[p];   // 같은 계통도 먹는다(동족 포식) — 빼면 같은 크기의 돌연변이만 부모에게 잡아먹히는 허상이 생긴다
                double a = Attack(in mi, in S.m[j], kt); if (a <= 0) continue;
                double w = a * Escape(strains[S.idx[j]].g, in S.m[j], mi.speed, S.dens[j]) * S.dens[j];
                S.pj[n2] = j; S.pw[n2] = w; n2++;
                food += w * S.m[j].M;
                if (densI > 0) S.risk[j] += densI * a;                              // 포식 신호(카이로몬)
            }
            if (P.SW > 1 && n2 > 1)   // 포식 전환: 같은 먹은 양을 먹이 종별 만남^α 비율로 다시 나눈다 → 흔한 종이 더, 드문 종이 덜 잡힌다
            {
                int ns = S.spMap.Count; Array.Clear(S.spW, 0, ns); double F = 0, Fa = 0;
                for (int p = 0; p < n2; p++) { double x = S.pw[p] * S.m[S.pj[p]].M; S.spW[S.spi[S.pj[p]]] += x; F += x; }
                for (int a = 0; a < ns; a++) if (S.spW[a] > 0) Fa += Math.Pow(S.spW[a], P.SW);
                if (Fa > 0) for (int p = 0; p < n2; p++) { double W = S.spW[S.spi[S.pj[p]]]; S.pw[p] *= Math.Pow(W, P.SW - 1) * F / Fa; }
            }
            // 부스러기(POM): 세포 수준 섭식은 고운 부스러기(5 μm), 소화관은 뭉친 바다눈(50 μm)을 같은 크기 규칙으로 먹는다. 움직이지 않는 먹이
            fd = 0;
            if (mi.Fc > 0 && 5 <= mi.gapeC) { double lr = Math.Log(mi.cellL / 5) - mi.lnOptC; fd = mi.Fc * Math.Exp(-lr * lr / 0.8); }
            if (mi.gapeB >= 50)
            {
                double lr = Math.Log(mi.L / 50) - mi.lnPrey, R = (mi.Rs + (mi.L + 50) / 2) * 1e-4, vr = mi.cruise * 0.36 + 0.36 * 20;
                fd = Math.Max(fd, Math.PI * R * R * vr * kt * Math.Exp(-lr * lr / 1.2));
            }
            fd *= 0.4 + 1.2 * gi[Gn.Detr];                                          // 부스러기를 얼마나 삼키나: 밥통을 채워 다른 먹이를 밀어낸다(잡식 ↔ 전문가)
            // 퇴적물(바닥에 쌓인 유기물): 바닥 칸에서 소화관 있는 동물이 기거나 파며 훑는 넓이(m²/h) × 넓이당 유기물. 땅 파면 훨씬 많이
            fs = 0;
            if (mi.gapeB > 0 && !mi.sessile && v == Bot(v % LS) && dep[v] > 0)
                fs = mi.L * 1e-6 * Math.Max(mi.cruise, 0.2 * mi.speed) * 1e-6 * 3600 * (1 + 3 * mi.burrow) * (0.2 + 0.8 * gi[Gn.Detr]) * kt;
            double sedE = fs > 0 ? dep[v] / geo.rArea[v % LS] : 0;
            return food + fd * detE * P.DET_Q + fs * sedE * P.SED_Q;
        }

        void Voxel(int v)
        {
            vBirth[v] = vDeath[v] = vKill[v] = 0;
            if (vol[v] <= 0) return;
            var S = scratch.Value; var r = new Rng(((ulong)seed << 40) ^ ((ulong)tick << 12) ^ (ulong)v);
            int n = Gather(S, v, true, out int np);
            if (n == 0) return;
            float t = T[v], kt = Kt(t), DT = P.DT; double Vv = vol[v];
            float o2s = (float)(dox[v] / (dox[v] + P.O2_K));
            double bacEat = 0, detEat = 0, sedEat = 0, detE = pom[v] / P.QN, pico = bac[v] + cya[v], sedE = dep[v] / geo.rArea[v % LS];
            for (int i = 0; i < n; i++)
            {
                ref var mi = ref S.m[i];
                if (mi.Fc <= 0 && mi.gapeB <= 0) continue;
                var gi = strains[S.idx[i]].g; double dI = S.dens[i];
                double food = Food(S, v, np, in mi, gi, kt, dI, out double fb, out double fd, out double fs, out int n2);
                double sat = 1 / (1 + food / Math.Max(Imax(in mi, kt), 1e-12));     // Holling II: 소화 시간
                satA[Ix(S.idx[i], v, S.stg[i])] = (float)sat;
                double eff = S.eff[i] = Eff(gi, in mi, v);
                double bEat = fb * pico * sat;
                S.gainB[i] = bEat * P.BAC_E * eff;
                bacEat += bEat * dI;
                double dEat = fd * detE * sat;                                       // 먹은 부스러기(에너지/h/개체)
                S.gainP[i] += dEat * P.DET_Q * eff; detEat += dEat * dI;
                double sEat = fs * sedE * sat;                                       // 먹은 퇴적물 유기물(에너지/h/개체)
                S.gainP[i] += sEat * P.SED_Q * eff; sedEat += sEat * dI;
                for (int p = 0; p < n2; p++)
                {
                    int j = S.pj[p]; double eat = S.pw[p] * sat, dg = Digest(strains[S.idx[j]].g, in S.m[j], in mi);
                    S.gainP[i] += eat * dg * S.m[j].M * eff;
                    if (dI > 0) S.kill[j] += dI * eat * dg / S.dens[j];
                }
            }
            // 먹은 질소(세균·부스러기·잡아먹힌 먹이)는 먹은 쪽이 먼저 몸 짓는 데 쓰고, 남는 것은 배설(영양염)·똥(POM)으로
            double foodN = 0;
            if (bacEat > 0 && pico > 0) { double e = Math.Min(pico * 0.95, bacEat * DT), fc = cya[v] / pico; cya[v] -= e * fc; bac[v] -= e * (1 - fc); foodN += e * 2.9e-7; }
            if (detEat > 0) { double e = Math.Min(pom[v], detEat * DT * P.QN); pom[v] -= e; foodN += e; }
            if (sedEat > 0) { double e = Math.Min(dep[v] * 0.5, sedEat * Vv * DT); dep[v] -= e; foodN += e * P.QN / Vv; }   // 퇴적물(에너지 총량) → 먹은 질소(μM)
            // 에너지 수지 → 성장·출생·사망
            if (P.VSP > 0)
            {
                var spH = S.spH; spH.Clear();
                for (int i = 0; i < n; i++) { int sp = strains[S.idx[i]].sp; spH[sp] = spH.GetValueOrDefault(sp) + S.dens[i] * S.m[i].C * S.m[i].expo; }
                for (int i = 0; i < n; i++) S.host[i] = spH[strains[S.idx[i]].sp];
            }
            double pool = nut[v], born = 0, died = 0, killed = 0, o2c = 0, o2p = 0, needW = 0, deadN = 0, domUse = 0, exu = 0;
            for (int i = 0; i < n; i++)
            {
                int s = S.idx[i], k = S.stg[i], q = Ix(s, v, k); var st = strains[s]; ref var m = ref S.m[i];
                double E = Budget(st, v, ref m, S.gainP[i], S.gainB[i], S.B);
                double cellB = m.esd * m.esd * m.esd * 1e-3 + P.FIXB, Mb = m.C * cellB;   // 이 단계 개체 하나를 짓는 비용
                float mm = (float)(E / Mb);
                double hd = Hypoxia(st.g, in m, v);
                mu[q] = (float)(mm - hd); gP[q] = (float)S.gainP[i]; gB[q] = (float)S.gainB[i]; kR[q] = (float)S.kill[i];
                if (k == 0) cue[s * V + v] = (float)(S.risk[i] / (S.risk[i] + 0.02));
                S.dD[i] = S.dUp[i] = S.dB[i] = S.need[i] = S.ing[i] = 0;
                double c = cnt[q]; if (c <= 0) continue;
                domUse += c * S.B[0] / 0.4 * DT * P.QN / Vv;                           // 삼투 영양: 녹은 유기물을 흡수(에너지 40%만 남는다)
                exu += c * S.B[1] * P.EXU * DT * P.QN / Vv;
                o2c += c * S.B[12] * (0.3 + 0.7 * st.g[Gn.Aerobic] * o2s); o2p += c * S.B[1] * 1.3;
                double load = Load(st.g, v);
                double dr = Mort(in m, kt, P.VSP > 0 ? S.host[i] : S.dens[i]) + (mm < 0 ? -2 * mm : 0) + S.kill[i] + hd;
                double d = r.Binom(c, 1 - Math.Exp(-dr * DT));
                killed += dr > 0 ? d * S.kill[i] / dr : 0;
                double nInd = Mb * P.QN / Vv;                                        // 개체 하나가 품은 영양염(μM)
                double up = 0, bth = 0, c0b = st.C[0] * cellB * P.QN / Vv;
                if (E > 0)
                {
                    if (!st.dev) bth = r.Poisson(c * mm * DT * (1 - load));         // 분열
                    else if (k < st.K - 1)
                    {
                        double gamma = mm / (st.C[k + 1] / st.C[k] - 1) * Molt(st);              // 다음 단계로 넘어가는 속도. 단계 안 개체는 C_k로 세므로 번 만큼만 자라야 질량이 보존된다(ln 비로 하면 질량이 저절로 생겨 20년 뒤 폭주). 단계 5배 간격: 실제보다 (r−1)/ln r ≈ 2.5배 늦게 자란다
                        up = r.Binom(c - d, 1 - Math.Exp(-gamma * DT));
                    }
                    else bth = r.Poisson(c * E * 0.9 / (st.C[0] * cellB) * DT * (1 - load));   // 성체: 알·번식체
                }
                double need = bth * c0b + (st.dev && k < st.K - 1 ? c * Math.Max(0, E) * DT * P.QN / Vv : 0);
                double kf = dr > 0 ? S.kill[i] / dr : 0;                                // 잡아먹혀 죽은 몫은 먹은 쪽 질소, 나머지는 사체
                foodN += d * kf * nInd; deadN += d * (1 - kf) * nInd;
                S.dD[i] = d; S.dUp[i] = up; S.dB[i] = bth; S.need[i] = need;
                S.aff[i] = 10 * st.fA / m.esd * m.expo;                              // 표면적:부피가 큰 쪽이 영양염을 먼저 잡는다(R* 이론, Tilman 1982)
                // 먹이를 먹는 생물은 먹이의 질소로 몸을 짓는다(남는 것은 내놓음 = 미생물 고리). 먹이 질소는 이번 틱 죽은 먹이가 물에 돌려준 몫
                S.ing[i] = S.eff[i] > 0 ? c * (S.gainP[i] + S.gainB[i]) / S.eff[i] * DT * P.QN / Vv : 0;
                died += d; killAcc[s * V + v] += d * (dr > 0 ? S.kill[i] / dr : 0);
            }
            double du = Math.Min(domUse, dom[v]); dom[v] -= du; pool += du;          // 흡수한 DOM 질소는 영양염 풀로(몸 짓기는 아래 need가 가져간다)
            double xu = Math.Min(exu, pool * 0.5); pool -= xu; dom[v] += xu;          // 삼출: 광합성 생물이 잡은 질소 일부가 DON으로
            double resv = 0; for (int i = 0; i < n; i++) resv += Math.Min(S.need[i], S.ing[i]);
            double fr = resv > foodN ? foodN / resv : 1, pool2 = pool * 0.9, left = foodN - resv * fr;
            pool += left * (1 - P.F_EGEST); pool2 += left * (1 - P.F_EGEST) * 0.9;
            pom[v] += left * P.F_EGEST + deadN * (1 - P.F_LYSIS); dom[v] += deadN * P.F_LYSIS;
            for (int i = 0; i < n; i++) needW += (S.need[i] - Math.Min(S.need[i], S.ing[i])) * S.aff[i];
            double share = needW > 0 ? pool2 / needW : 0, used = 0;
            for (int i = 0; i < n; i++)
            {
                if (S.dD[i] == 0 && S.dUp[i] == 0 && S.dB[i] == 0) continue;
                int s = S.idx[i], k = S.stg[i], q = Ix(s, v, k);
                double own = Math.Min(S.need[i], S.ing[i]), got = own * fr + (S.need[i] - own) * Math.Min(1, share * S.aff[i]);
                double f = S.need[i] > 0 ? got / S.need[i] : 1;                      // 영양염이 모자라면 못 짓는다
                if (f < 1) { S.dB[i] = r.Binom(S.dB[i], f); S.dUp[i] = r.Binom(S.dUp[i], f); }
                used += (S.need[i] - own) * Math.Min(1, share * S.aff[i]);         // 영양염 풀에서 가져간 몫(먹은 질소 몫은 위에서 이미 뺐다)
                born += S.dB[i]; genAcc[s * V + v] += S.dB[i];
                cnt[q] -= S.dD[i] + S.dUp[i];
                if (S.dUp[i] > 0) cnt[q + 1] += S.dUp[i];
                if (S.dB[i] > 0) cnt[Ix(s, v, 0)] += S.dB[i];
            }
            nut[v] = Math.Max(0, pool - used);
            o2d[v] = (o2p - o2c) * DT / Vv;                                         // 용존 산소 변화(에너지/mL) — Chem()에서 환산
            vBirth[v] = born; vDeath[v] = died; vKill[v] = killed;
        }

        static int LowerBound(float[] a, int n, float x) { int lo = 0, hi = n; while (lo < hi) { int m = (lo + hi) >> 1; if (a[m] < x) lo = m + 1; else hi = m; } return lo; }

        // 개체 하나의 시간당 에너지 수지(10 μm 세포 1개분 = 1). B에 항목별로 채운다(화면 개체 정보에도 씀):
        // 0 녹은 유기물 1 광합성 2 세균 3 포식 | 4 기초 대사 5 형질 유지비 6 몸 구조·모양 7 삼투 8 삼투 손상 9 자외선 10 산소 독성·부족 11 온도 | 12 호흡 합계 13 순수지
        public double Budget(Strain st, int v, ref Morph m, double gainP, double gainB, double[] B)
        {
            var g = st.g; float t = T[v]; float kt = m.kt, perf = m.perf;
            double V10 = m.esd * m.esd * m.esd * 1e-3, A10 = m.esd * m.esd * 1e-2 * st.fA, C = m.C;
            double aff = P.COLD > 0 ? Math.Pow(m.ovl, P.COLD) : 1;                 // 군체: 묽을 때 세포마다 덜 받는다(진할 때는 흡수 속도가 한계라 같다)
            double n = nut[v], nup = n / (n + P.KN / aff), work = perf * kt;
            double o2 = dox[v], o2s = o2 / (o2 + P.O2_K), hyp = Math.Min(1, o2 / Math.Max(1e-3, m.pcrit));
            double aer = 1 + P.AER_GAIN * g[Gn.Aerobic] * o2s * hyp + 0.4 * (1 - g[Gn.Aerobic]) * (1 - o2s);
            double Il = I[v];
            double up = P.C_OSMO * A10 * dom[v] / (dom[v] + P.KDOM_O / aff) * work * aer * m.expo;   // 삼투 영양: 녹은 유기물(DOM)
            double pkg = 1 / (1 + 0.025 * m.esd * st.thinAx);                    // package effect: 두꺼운 세포는 빛 흡수 효율↓ (Finkel 2010)
            double phPot = P.C_PHOTO * g[Gn.Photo] * (1 - P.MIXO * g[Gn.Phago]) * V10 * pkg * Il / (Il + P.IK) * work * (1 - 0.1 * g[Gn.Pigment]) * m.expo;
            double nCap = P.KUP * A10 * nup * m.expo * kt;                         // 질소·인 흡수 능력: 표면적:부피가 큰 세포가 빈영양에 강함
            double ph = Math.Min(phPot, nCap);
            B[0] = up * C; B[1] = ph * C; B[2] = gainB; B[3] = gainP;
            double colF = C > 1 ? Math.Min(1, (C - 1) / 15) : 0, lab = 1 - 0.5 * g[Gn.Diff] * colF;   // 분업: 체세포가 운동·감각을 맡음
            double sc = m.org > 0 ? Math.Pow(Math.Max(1, m.M / P.M0), -0.25 * m.org) : 1;                // 조직·순환이 있으면 대사 ∝ 질량^0.75 (DeLong 2010)
            const double K = 0.4;   // 웹판 계수(틱당)를 시간당으로
            B[4] = (P.BASAL * V10 + P.FIX) * kt * C * sc;
            // 독(질소가 많은 2차 대사물)·식물 소화 효소·투명한 조직·떼짓기(경계·맞추기)
            B[5] = ((0.006 * g[Gn.Photo] + 0.002 * g[Gn.Phago] + 0.004 * g[Gn.Aerobic] + 0.002 * g[Gn.Vacuole] + 0.003 * g[Gn.Buoy]
                    + 0.004 * g[Gn.Toxin] + 0.002 * g[Gn.Herb] + 0.001 * g[Gn.Transp] + 0.002 * m.school) * V10
                + lab * 0.002 * g[Gn.Flagella] * (m.esd / 8)
                + lab * (0.001 * g[Gn.Eye] + 0.004 * g[Gn.Eye] * g[Gn.Eye]) * Math.Pow(V10, 0.6)
                + (0.0015 * g[Gn.Chemo] + 0.003 * g[Gn.Pigment] + 0.005 * g[Gn.Wall] + 0.002 * lab * g[Gn.Cilia] + 0.01 * g[Gn.Dry]) * A10) * K * kt * C * sc;
            // 몸 구조: 가시·각(표면적), 모양 유지(세포벽이 있으면 공짜에 가깝다), 군체 접착·분업, 부착 기관, 가소성,
            // 동물 기관(소화관·신경·순환·골격·좌우대칭): 기관 조직 유지비. 신경 조직은 비싸다(척추동물 뇌 = 대사의 수 %–20%)
            B[6] = ((0.003 * m.spines + 0.004 * g[Gn.Lorica] + 0.002 * g[Gn.Elong] * g[Gn.Elong] * (1 - 0.7 * g[Gn.Wall])) * A10
                + (C > 1 ? 0.0012 + 0.002 * g[Gn.Diff] : 0) + 0.002 * g[Gn.Attach] + 0.0008 * g[Gn.Plastic]
                + (st.dev ? (0.0015 * g[Gn.Gut] + 0.004 * g[Gn.Neuro] + 0.002 * g[Gn.Circ] + 0.002 * g[Gn.Skel] + 0.0008 * g[Gn.Bilat]) * V10 : 0)
                // 몸 부품: 마디 되풀이, 부속지, 외골격(클수록 무겁다: 몸 5 cm면 두 배 — 절지동물 크기 한계), 광물화(찬물에선 탄산칼슘이 녹아 비싸다), 턱, 땅 파기(마디가 있으면 연동으로 덜 든다)
                + (st.dev ? (0.0004 * g[Gn.Seg] + 0.0015 * g[Gn.Limb] + 0.002 * g[Gn.Exo] * (1 + m.L / 5e4) + 0.002 * g[Gn.Mineral] * Math.Max(g[Gn.Exo], 0.5 * g[Gn.Skel]) * (1 + Math.Max(0, (15 - t) / 15))
                    + 0.001 * g[Gn.Jaw] + 0.003 * m.burrow / (1 + g[Gn.Seg] / 2)) * V10 : 0)) * K * kt * C * sc;
            float S = env[Ev.Salt]; double internal_ = Math.Max(P.OSMO_FLOOR, Math.Min(S, g[Gn.Osmo]));
            double osm = (P.OSMO_INT * internal_ + P.OSMO_CAP * g[Gn.Osmo]) * V10, od = 0;
            if (S > g[Gn.Osmo] * 1.08f) od = P.W_OUT * (S - g[Gn.Osmo] * 1.08) * A10;      // 탈수. 8%까지는 순응으로 버팀
            else if (S < P.OSMO_FLOOR)
            {
                double influx = P.W_IN * (P.OSMO_FLOOR - S) * A10 * (1 - 0.9 * g[Gn.Wall]);  // 민물: 물이 들어와 부푼다. 세포벽 팽압이 막음
                double pump = Math.Min(influx, P.PUMP * g[Gn.Vacuole] * A10);
                osm += pump * P.PUMP_COST; od = influx - pump;
            }
            if (ex[v] > 0) { double wet = (1 - g[Gn.Dry]) * (1 - 0.3 * g[Gn.Exo]); od += P.DESIC * ex[v] * wet * wet * A10; }   // 물 밖: 겉에서 물이 날아간다. 외골격(큐티클)이 조금 막아 준다
            B[7] = osm * kt * C * m.expo; B[8] = od * C * m.expo;                // 다세포는 겉 세포만 바깥물과 맞닿는다
            double prot = 1 - 0.9 * g[Gn.Pigment];
            B[9] = P.UV_DMG * U[v] * prot * (1 - 0.5 * g[Gn.Lorica]) * A10 * m.expo * C;
            // 산소: 혐기성의 산소 독성 + 산소가 임계보다 낮을 때 신경·근육 조직의 산소 부족
            B[10] = (P.O2_TOX * (o2 / 21) * (1 - g[Gn.Aerobic]) * (1 - g[Gn.Aerobic]) * V10 + (1 - hyp) * g[Gn.Neuro] * 0.004 * V10 * kt) * C * sc;
            B[11] = ((perf < 0.3 ? 0.012 * (0.3 - perf) / 0.3 : 0) + (t > 62 ? 0.02 * (t - 62) : 0)) * V10 * C * sc;   // 진핵 한계 ~60°C. 대사처럼 몸 크기에 따라 준다
            double swim = 0.0004 * (m.esd / 10) * (m.cruise / 100) * (m.cruise / 100) * kt;   // 세포 헤엄 동력은 작다(Stokes). 매복하면 덜 헤엄친다
            if (m.org > 0 && m.L > 0) { double bl = m.cruise / m.L, re = m.L * m.cruise * 1e-6; swim = Math.Max(swim, 0.3 * B[4] * bl * bl / 4 * Math.Min(1, re)); }   // 근육 유영: 2 BL/s면 기초 대사의 30%. 레이놀즈 수 1 밑(섬모로 헤엄치는 작은 유생)은 점성 영역이라 BL/s가 커도 싸다
            double cost = B[4] + B[5] + B[6] + B[7] + B[8] + B[9] + B[10] + B[11] + swim;
            B[12] = B[4] + B[5] + B[6] + swim;                                     // 산소를 쓰는 호흡(용존 산소 소비)
            double net = B[0] + B[1] * (1 - P.EXU) + B[2] + B[3] - cost;            // EXU: 광합성 산물 일부는 녹은 유기물로 내보낸다(아래 Voxel이 DOM으로)
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
            {
                if (vol[v] <= 0) continue;
                int y = v / LS, c = v % LS, yb = nyR[c] - 1; double h = dz[v];
                for (int k = 0; k < st.K; k++)
                {
                    int q = (v * KM) + k; double n = cnt[b0 + q]; if (n <= 0) continue;
                    var m = M(s, v, k);
                    if (m.sessile || m.benthic) { dst[y < yb ? q + LK : q] += n; continue; }   // 부착 생물·땅 파는 생물은 바닥에 내려앉아 산다
                    double hov = Math.Max(0, Math.Abs(m.sink) - m.speed * 3600e-6);   // 헤엄치는 생물은 제 속도만큼 가라앉음·떠오름을 버틴다(헤엄 비용은 늘 냄)
                    double fs = Math.Sign(m.sink) * hov * DT / h;                   // 침강(+아래) 또는 떠오름(−)
                    double sw = Math.Min(0.9, m.speed * 3600e-6 * DT / h);
                    double here = mu[b0 + q], up = y > 0 ? mu[b0 + q - LK] : here, dn = y < yb ? mu[b0 + q + LK] : here;
                    double bias = Math.Tanh((dn - up) * 300) * sense;              // +면 아래가 낫다
                    if (Math.Max(up, dn) < here) bias *= 0.3;                      // 지금 자리가 제일 좋으면 덜 움직임
                    double mixd = Math.Max(0, 1 - sw);                             // 빨리 헤엄치는 동물은 물살에 덜 휩쓸린다
                    double pD = Math.Max(0, fs), pU = Math.Max(0, -fs) + sw * Math.Max(0, -bias);
                    if (y < yb) pD += sw * Math.Max(0, bias) + Math.Min(0.4, kb[v] / h * DT) * mixd;   // 바닥층에서는 가라앉는 몫만 퇴적물로(헤엄·섞임은 묻히지 않는다)
                    if (y > 0) pU += Math.Min(0.4, kb[v - LS] / h * DT) * mixd;    // 난류에 휩쓸림(틱당 한 층까지)
                    // 수평: 이웃 해역과 맞닿은 면으로 해류·소용돌이에 실려 간다. 헤엄치는 동물은 무작위 이동 확산(v²τ/2, τ ~ 1일)을 더한다
                    double vs = m.speed * 1e-6, kh = (P.KH_H + vs * vs * 43200) * 3600 * DT / (vol[v] * 1e-6), pH = 0;
                    foreach (var (nb, li) in adj[c]) pH += qa[li * NY + y] * kh;
                    double tot = pD + pU + pH;
                    if (tot > 0.95) { double f = 0.95 / tot; pD *= f; pU *= f; pH *= f; kh *= f; }
                    double nd = r.Binom(n, pD), nu = r.Binom(n - nd, pU / Math.Max(1e-9, 1 - pD));
                    double rest = n - nd - nu, hx = pH > 0 ? r.Binom(rest, Math.Min(1, pH / Math.Max(1e-9, 1 - pD - pU))) : 0;
                    rest -= hx;
                    if (y < yb) dst[q + LK] += nd;
                    else depS[s * LS + c] += nd * m.M;                             // 바닥 밖 = 퇴적물에 묻힘(분해되며 산소를 씀). 계통별 줄에 써서 병렬 경쟁 없음
                    if (y > 0) dst[q - LK] += nu; else rest += nu;                 // 수면에 뜬 것은 그대로
                    if (y == 0 && !st.dev && NR > 1)                               // 먼 거리 이동(바람·폭풍·떠다니는 것에 붙어): 미생물은 어디에나 퍼지고 환경이 고른다(Baas Becking). 갇힌 바다도 언젠가 채워진다
                    {
                        double j = r.Binom(rest, P.JUMP * DT);
                        if (j > 0) { int to = (int)(r.U() * (NR - 1)); if (to >= c) to++; dst[to * KM + k] += j; rest -= j; }
                    }
                    for (int a = 0; a < adj[c].Count && hx > 0; a++)               // 이웃마다 면 넓이 비례로 나눈다
                    {
                        var (nb, li) = adj[c][a]; double pa = qa[li * NY + y] * kh; if (pa <= 0) continue;
                        double x = pa >= pH * 0.999 ? hx : r.Binom(hx, pa / pH); pH -= pa; hx -= x;
                        dst[(y * LS + nb) * KM + k] += x;
                    }
                    dst[q] += rest + hx;
                }
            }
            Array.Copy(dst, 0, cnt, b0, V * KM);
        }

        // 영양염·세균·용존 산소: 섞임, 바닥 공급, 세균 성장, 수면 기체 교환. 엽록소(그늘)
        void Chem()
        {
            float DT = P.DT;
            double nsed = P.NSED * env[Ev.Nut] / 100;
            for (int c = 0; c < NR; c++)
            {
                int vb = Bot(c); nut[vb] += Math.Min(1, P.KSED * DT / dz[vb]) * (nsed * rnM[c] * (geo.rKind[c] == 2 ? P.NLAND * wMul : 1) - nut[vb]);   // 바닥 퇴적물(육지는 흙)
                nut[c] += P.RIV * env[Ev.Nut] / 50 * geo.rCoast[c] * wMul * rnM[c] * DT / dz[c];      // 강: 해안선이 긴 해역의 표층, 풍화가 빠르면 더 많이
            }
            Mix(nut); Mix(bac); Mix(dox); Mix(pom); Mix(dom); Mix(cya);
            // 부스러기 침강: 층마다 WS·DT/두께 비율이 아래층으로(두께가 다르면 농도를 환산), 바닥층에서는 퇴적물로(에너지 = N/QN × 부피)
            for (int c = 0; c < NR; c++) for (int y = nyR[c] - 1; y >= 0; y--)
            {
                int v = y * LS + c; double f = pom[v] * Math.Min(0.9, P.WS * DT / dz[v]); pom[v] -= f;
                if (y < nyR[c] - 1) pom[v + LS] += f * dz[v] / dz[v + LS]; else dep[v] += f / P.QN * vol[v];
            }
            foreach (int s in live) for (int c = 0; c < NR; c++) { double m = depS[s * LS + c]; if (m == 0) continue; dep[Bot(c)] += m; depS[s * LS + c] = 0; }
            float sal = env[Ev.Salt];
            for (int v = 0; v < V; v++)
            {
                if (vol[v] <= 0) continue;
                int c = v % LS; bool bot = v == Bot(c); float t = T[v];
                // 부스러기 → DOM(세균 효소), 세균은 DOM을 먹고 자라며 (1−Y)는 숨 쉬어 영양염으로 돌려준다. 죽은 세균(바이러스)은 다시 DOM
                double kt = Kt(t), hyd = pom[v] * Math.Min(0.5, P.KH * kt * DT); pom[v] -= hyd; dom[v] += hyd;
                double g = 0.08 * kt * dom[v] / (dom[v] + P.KDOM_B) * Math.Min(1, 0.3 + dox[v] / 2);
                const double NB = 2.9e-7;                                          // 세균 한 마리 ≈ 4 fg N (μM per 마리/mL)
                double prod = Math.Min(bac[v] * g * DT, dom[v] * 0.9 * P.BAC_Y / NB), uptake = prod * NB / P.BAC_Y, mort = bac[v] * P.BAC_M * (bac[v] / P.BAC_REF) * DT;
                double db = prod - mort, rem = uptake - prod * NB;                 // rem = 숨 쉬어 무기물로 돌아간 질소
                bac[v] = Math.Max(1e3, bac[v] + db);
                dom[v] = Math.Max(0, dom[v] - uptake + mort * NB); nut[v] += rem;
                // 남세균: 빛 × 영양염으로 자라고(광합성 → 산소), 바이러스로 죽으면 DOM. 엽록소 그늘에도 들어간다
                double gc = P.CYA_G * kt * I[v] / (I[v] + P.CYA_IK) * nut[v] / (nut[v] + 0.3) * (1 - ex[v]);   // 바탕 남세균은 바다 것: 물 밖에서는 못 자란다(땅의 생산자는 진화로 나와야)
                double pc = Math.Min(cya[v] * gc * DT, nut[v] * 0.9 / NB), mc = cya[v] * P.CYA_M * (cya[v] / P.CYA_REF) * DT;
                cya[v] = Math.Max(10, cya[v] + pc - mc); nut[v] -= pc * NB; dom[v] += mc * NB;
                double o2cy = pc * P.BAC_E * 1.3;
                // 산소 용해도는 따뜻하고 짤수록 낮다 → 같은 호흡이 산소를 더 빨리 깎는다
                double sol = Math.Exp(-0.0205 * (t - 20)) * (1 - 0.0052 * sal / 1.0 * 0.1), kO2 = P.KO2 / Math.Max(0.2, sol);
                // 바닥 퇴적물: 가라앉은 유기물이 하루 ~3%씩 분해되며 산소를 쓰고 영양염을 돌려준다(KDEC)
                double dec = bot ? dep[v] * P.KDEC * DT : 0, bur = bot ? dep[v] * P.BURY * DT : 0;
                dep[v] -= dec + bur; buried += bur; nut[v] += dec * P.QN / vol[v];
                double resp = o2d[v] - rem / P.QN - dec / vol[v] + o2cy;                  // 세균 호흡(숨 쉰 유기물만큼 산소)·퇴적물 분해
                dox[v] = Math.Clamp(dox[v] + resp * kO2, 0, 21 + 10 * Math.Min(zc[v], 10) + 0.3 * env[Ev.O2]);
            }
            for (int c = 0; c < NR; c++) dox[c] += Math.Min(1, P.KEX * DT / dz[c]) * (1 - iceR[c]) * (env[Ev.O2] - dox[c]);   // 수면: 대기와 평형(얼음 밑은 막힘)
            Array.Clear(chl, 0, V);
            foreach (int s in live)
            {
                var st = strains[s]; if (st.g[Gn.Photo] < 0.01f) continue;
                double ph0 = st.g[Gn.Photo] * Math.Pow(st.esd0 / 10, 3);
                for (int v = 0; v < V; v++) for (int k = 0; k < st.K; k++) { double c = cnt[Ix(s, v, k)]; if (c > 0) chl[v] += c / vol[v] * ph0 * st.C[k]; }
            }
            for (int v = 0; v < V; v++) chl[v] += cya[v] * P.BAC_E;
        }

        // 농도 섞기: 세로(해역 안 층 사이, Kz)는 혼합층이 몇 시간 만에 섞이므로 잘게 나눠 풀고, 수평(이웃 해역, 맞닿은 면 × Kh)은 느려서 한 번에
        void Mix(double[] a)
        {
            var tmp = scratch.Value.rowB; double dt = P.DT / nsub;
            for (int it = 0; it < nsub; it++)
            {
                Array.Copy(a, tmp, V);
                for (int v = 0; v < V; v++)
                {
                    if (vol[v] <= 0) continue;
                    int y = v / LS; double d = 0;
                    if (y > 0) d += kb[v - LS] * (tmp[v - LS] - tmp[v]);
                    if (y < nyR[v % LS] - 1) d += kb[v] * (tmp[v + LS] - tmp[v]);
                    a[v] = tmp[v] + d / dz[v] * dt;
                }
            }
            double kh = P.KH_H * 3600 * P.DT; int hs = Math.Max(1, (int)Math.Ceiling(qmax * kh * 2)); kh /= hs;   // 한 번에 바뀌는 물이 칸 부피의 절반을 넘지 않게
            for (int h = 0; h < hs; h++)
            {
                Array.Copy(a, tmp, V);
                for (int li = 0; li < geo.links.Count; li++)
                {
                    var (ra, rb, _, _) = geo.links[li];
                    for (int y = 0; y < NY; y++)
                    {
                        double Q = qa[li * NY + y] * kh; if (Q <= 0) break;   // m³(한 번에)
                        int va = y * LS + ra, vb = y * LS + rb; double f = Q * (tmp[vb] - tmp[va]);
                        a[va] += f / (vol[va] * 1e-6); a[vb] -= f / (vol[vb] * 1e-6);
                    }
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
                    // 태어난 곳: 출생 수에 비례해 칸 하나를 뽑고, 그 해역에서 침입 적합도를 재고 그 해역에 둔다(지역 적응 → 해역마다 다른 갈래)
                    double bsum = 0; for (int v = 0; v < V; v++) bsum += genAcc[b + v];
                    if (bsum <= 0) break;
                    double x = r.U() * bsum; int vb = 0; for (; vb < V - 1; vb++) { x -= genAcc[b + vb]; if (x <= 0) break; }
                    int c = vb % LS; double tot = 0, bc = 0;
                    for (int y = 0; y < nyR[c]; y++) { int v = y * LS + c; bc += genAcc[b + v]; for (int a = 0; a < st.K; a++) tot += cnt[Ix(s, v, a)]; }
                    if (tot <= 0) continue;
                    // 물가로 쓸려 간 돌연변이: 바다의 부모는 조간대·육지 물막에서 마르며 줄기만 해 거기서는 태어나지 않는다(번식 0).
                    // 그래서 돌연변이의 MUT_EDGE 몫은 한 단계 더 마른 이웃(부모가 흘러들어 와 있는 곳)으로 옮겨 그곳에서 자리 잡기를 겨룬다
                    if (P.MUT_EDGE > 0 && geo.rKind[c] < 2 && r.U() < P.MUT_EDGE)   // 바다 → 조간대·육지, 조간대 → 육지
                    {
                        int e = -1, ne = 0; double te = 0;
                        foreach (var (o, _) in adj[c]) if (geo.rKind[o] > geo.rKind[c])
                        {
                            double t2 = 0; for (int y = 0; y < nyR[o]; y++) for (int a = 0; a < st.K; a++) t2 += cnt[Ix(s, y * LS + o, a)];
                            if (t2 > 0 && r.U() * ++ne < 1) { e = o; te = t2; }
                        }
                        if (e >= 0) { c = e; tot = te; }
                    }
                    float hp = st.dev ? 0 : P.HGT * (st.C[0] <= 1 ? 1 : 0.3f);
                    int hj = -1; var g = hp > 0 && r.U() < hp ? Transfer(st, c, ref r, out hj) : null; bool hgt = g != null; if (!hgt) g = Mutate(st.g, ref r);
                    if (hgt) hgtTry++;
                    mutTry++; mutHab[geo.rKind[c]]++; if (Selection(st, g, c) <= 0 && r.U() >= P.MUT_DEL) continue; mutKeep++; mutHab[3 + geo.rKind[c]]++;
                    if (hgt) { hgtKeep++; if (hgtSeen.Add(hj)) Log("grade", $"처음으로 수평 유전자 이동이 자리 잡았습니다: {Taxa.Name(st)}가 이웃에게서 '{Gn.Def[hj].name}' 유전자를 얻음 ({geo.Name(c)})"); }   // 부모보다 못한 돌연변이는 대부분 자리 잡지 못한다(분기 과정)
                    double n0 = Math.Max(1, Math.Min(bU * bc / bsum * P.MUT_LUMP, P.MUT_F0 * tot));
                    var ns = NewStrain(g, st.sp, st.id, st.gen);
                    if (ns == null) { if (!Prune(s)) return; ns = NewStrain(g, st.sp, st.id, st.gen); if (ns == null) return; }
                    Split(s, ns, n0, tot, c, ref r);
                }
            }
        }

        // 침입 적합도(/h): 유전형 g가 해역 c에서 부모 par 사이에 드물게 생겼을 때의 성장률 − 부모 성장률.
        // 성장률 ≈ ln R0 / 세대 시간(분기 과정): R0 = Π(다음 단계까지 살아 자랄 확률) × 성체 평생 자손. 단세포는 분열률/사망률.
        // 단계마다 부모가 가장 많은 층에서, 지난 틱 포식자 배부름·먹이·환경으로 Budget을 다시 계산한다. 바이러스는 부모와 같이 받는다고 본다
        public double Selection(Strain par, float[] g, int c)
        {
            var mt = new Strain { g = g }; Shape(mt);
            return Growth(mt, par, c) - Growth(par, par, c);
        }

        double Growth(Strain x, Strain par, int c)
        {
            double lnR = 0, Tg = 0;
            for (int k = 0; k < x.K; k++)
            {
                int pk = 0; double bd = 1e30; for (int a = 0; a < par.K; a++) { double dd = Math.Abs(Math.Log(par.C[a] / x.C[k])); if (dd < bd) { bd = dd; pk = a; } }
                int vb = -1; double bc = 0;
                for (int y = 0; y < nyR[c]; y++) { double n = cnt[Ix(par.slot, y * LS + c, pk)]; if (n > bc) { bc = n; vb = y * LS + c; } }
                if (vb < 0) for (int y = 0; y < nyR[c]; y++) { double n = 0; for (int a = 0; a < par.K; a++) n += cnt[Ix(par.slot, y * LS + c, a)]; if (n > bc) { bc = n; vb = y * LS + c; } }
                if (vb < 0) return -1;
                double dP = cnt[Ix(par.slot, vb, pk)] / vol[vb];
                var (mm, d, bA) = Rates(x, vb, k, dP, P.VSP > 0 ? HostD(par.sp, vb) : dP);
                if (!x.dev) { lnR = Math.Log(Math.Max(1e-9, mm) / d); Tg = 1 / d; }
                else if (k < x.K - 1) { double gm = Math.Max(0, mm) / (x.C[k + 1] / x.C[k] - 1) * Molt(x); lnR += Math.Log(Math.Max(1e-9, gm / (gm + d))); Tg += 1 / (gm + d); }
                else { lnR += Math.Log(Math.Max(1e-9, bA) / d); Tg += 1 / d; }
            }
            return lnR / Tg;
        }

        // 유전형 x(임시 계통이어도 됨)가 칸 v·단계 k에서 내는 (성장·분열률, 사망률, 성체 출생률) /h. Voxel과 같은 식
        (double mm, double d, double bA) Rates(Strain x, int v, int k, double densVir, double host)
        {
            var S = scratch.Value; int n = Gather(S, v, false, out int np);
            var m = MorphAt(x, v, k, 0); double kt = m.kt;
            double food = Food(S, v, np, in m, x.g, kt, 0, out double fb, out double fd, out double fs, out int n2);
            double sat = 1 / (1 + food / Math.Max(Imax(in m, kt), 1e-12)), eff = Eff(x.g, in m, v);
            double gB = fb * (bac[v] + cya[v]) * sat * P.BAC_E * eff, gP = (fd * pom[v] / P.QN * P.DET_Q + fs * dep[v] / geo.rArea[v % LS] * P.SED_Q) * sat * eff;
            for (int p = 0; p < n2; p++) { int j = S.pj[p]; gP += S.pw[p] * sat * Digest(strains[S.idx[j]].g, in S.m[j], in m) * S.m[j].M * eff; }
            double kill = 0;   // 이 칸 포식자들이 나를 잡아먹는 속도
            for (int i = 0; i < n; i++)
            {
                if (S.dens[i] <= 0) continue; ref var mi = ref S.m[i]; if (mi.Fc <= 0 && mi.gapeB <= 0) continue;
                double a = Attack(in mi, in m, kt); if (a <= 0) continue;
                kill += S.dens[i] * a * Escape(x.g, in m, mi.speed, densVir) * satA[Ix(S.idx[i], v, S.stg[i])] * Digest(x.g, in m, in mi);
            }
            double E = Budget(x, v, ref m, gP, gB, S.B);
            double cellB = m.esd * m.esd * m.esd * 1e-3 + P.FIXB, mm = E / (m.C * cellB), load = Load(x.g, v);
            double d = Mort(in m, kt, host) + (mm < 0 ? -2 * mm : 0) + kill + Hypoxia(x.g, in m, v);
            return (mm * (1 - load), d, Math.Max(0, E) * 0.9 / (x.C[0] * cellB) * (1 - load));
        }

        // 부모 s의 분포(칸·단계)에서 n0마리를 새 계통 ns로 떼어 준다(reg ≥ 0이면 그 해역에서만). 단계 수가 바뀌면 세포 수가 가장 가까운 단계로
        void Split(int s, Strain ns, double n0, double tot, int reg, ref Rng r)
        {
            var st = strains[s]; var map = new int[KM];
            for (int a = 0; a < st.K; a++) { int bi = 0; double bd = 1e30; for (int c = 0; c < ns.K; c++) { double dd = Math.Abs(Math.Log(ns.C[c] / st.C[a])); if (dd < bd) { bd = dd; bi = c; } } map[a] = bi; }
            double f = n0 / Math.Max(1, tot);
            if (n0 <= 1)   // 한 마리: 그 해역에서 가장 많은 칸·단계에서
            {
                int bq = -1, bv = 0, ba = 0; double bn = 0;
                for (int v = 0; v < V; v++) if (reg < 0 || v % LS == reg) for (int a = 0; a < st.K; a++) { int q = Ix(s, v, a); if (cnt[q] > bn) { bn = cnt[q]; bq = q; bv = v; ba = a; } }
                if (bq >= 0 && bn >= 1) { cnt[bq] -= 1; cnt[Ix(ns.slot, bv, map[ba])] += 1; }
            }
            else
                for (int v = 0; v < V; v++)
                {
                    if (reg >= 0 && v % LS != reg) continue;
                    for (int a = 0; a < st.K; a++)
                    {
                        int q = Ix(s, v, a); double c = cnt[q]; if (c <= 0) continue;
                        double m = r.Binom(c, f); cnt[q] -= m; cnt[Ix(ns.slot, v, map[a])] += m;
                    }
                }
        }

        // 시험용: 계통 s에서 유전형 g인 계통을 비율 frac만큼 떼어 만든다(침입 적합도 탐침)
        public Strain Inject(int s, float[] g, double frac)
        {
            var st = strains[s]; var ns = NewStrain(g, st.sp, st.id, st.gen); if (ns == null) return null;
            var r = new Rng((ulong)(tick * 7919 + ns.id)); Split(s, ns, Math.Max(2, frac * st.total), st.total, -1, ref r); ns.prevTot = 0; return ns;
        }
        public void PruneTo(int free) { while (SMAX - live.Count < free && Prune(-1)) { } }

        // 자리가 없으면 먼저 거의 같은 계통끼리 합친다(같은 유전형 묶음으로 보는 근사, 개체는 그대로).
        // 그래야 돌연변이가 많아도 드물고 다른 계통(진화의 앞줄인 큰 생물)이 계통 칸 부족으로 지워지지 않는다.
        // 합칠 짝이 없으면 줄어드는 작은 계통을 지운다. ponytail: 한 번에 16쌍, 계통 수² 거리 계산(256이면 ~2 ms)
        bool Prune(int keep)
        {
            var cand = new List<(double d, int s, int n)>();
            // 사는 곳(개체가 가장 많은 바다·조간대·육지)이 다르면 합치지 않는다: 땅에 막 올라온 작은 계통이 바다의 부모에 합쳐져
            // 마름 견디기를 잃고 사라지는 일을 막는다(육지 물막·조간대는 부피가 작아 개체 수로는 늘 작은 쪽이다)
            var hk = new Dictionary<int, int>();
            int HK(int s)
            {
                if (hk.TryGetValue(s, out int k)) return k;
                var st = strains[s]; Span<double> x = stackalloc double[3];
                for (int v = 0; v < V; v++) { int c = v % LS; if (c >= NR) continue; double n = 0; for (int a = 0; a < st.K; a++) n += cnt[Ix(s, v, a)]; x[geo.rKind[c]] += n; }
                return hk[s] = x[2] > x[1] && x[2] > x[0] ? 2 : x[1] > x[0] ? 1 : 0;
            }
            foreach (int s in live)
            {
                var st = strains[s]; if (s == keep || tick - st.born < 72 || st.trend > 0) continue;   // 퍼지는 새 돌연변이는 건드리지 않는다
                double dm = P.MERGE_D; int nn = -1;
                foreach (int o in live) { var so = strains[o]; if (o == s || so.total < st.total) continue; double d = Dist(st.g, so.g); if (d < dm && HK(o) == HK(s)) { dm = d; nn = o; } }
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
                double sc = (tick - st.born < 72 ? 1e9 : st.trend) + (HK(s) > 0 ? 1e8 : 0);   // 물가 계통(늘 작다)은 바다 계통을 다 본 뒤에
                if (sc < bt) { bt = sc; best = s; }
            }
            if (best < 0) foreach (int s in live) if (s != keep && strains[s].total < bt) { bt = strains[s].total; best = s; }
            if (best < 0) return false;
            Remove(best, 3); return true;
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
            b.total += a.total; Remove(s, 2, b.id);
        }

        // 기록 정리: 후손 없이 사라진 가지부터 버린다(짧게 산 것 먼저). 살아 있는 계통의 조상과 따라가는 혈통은 남는다
        void PruneRecs()
        {
            var par = new HashSet<int>(); foreach (var r in recs.Values) { par.Add(r.parent); if (r.into >= 0) par.Add(r.into); }
            var dead = recs.Values.Where(r => r.died >= 0 && !par.Contains(r.id) && r.id != pin).OrderBy(r => r.died - r.born).ToList();
            for (int i = 0; i < dead.Count && recs.Count > 4000; i++) recs.Remove(dead[i].id);
        }

        void Remove(int s, byte why = 1, int into = -1)
        {
            var st = strains[s]; var rc = st.rec;
            if (rc != null) { rc.died = tick; rc.why = why; rc.into = into; rc.sp = st.sp; rc.peak = st.peak; rc.eaten = st.predE > Math.Max(0, -st.muE); }
            Array.Clear(cnt, Ix(s, 0, 0), V * KM); Array.Clear(mu, Ix(s, 0, 0), V * KM); Array.Clear(cue, s * V, V);
            Array.Clear(genAcc, s * V, V); Array.Clear(killAcc, s * V, V);
            strains[s] = null; live.Remove(s);
        }

        // 수평 유전자 이동: 해역 c에 사는 다른 계통(세포 수에 비례해 뽑음)에게서 생리 유전자 하나를 얻는다.
        // 원생생물의 예: 염분·온도 내성(Galdieria, Schönknecht 2013), 혐기 대사(Andersson 2003), 자외선 차단 MAA(Starcevic 2008), 마름 견디기(담륜충, Gladyshev 2008).
        // 몸 설계·세포 크기·광합성(색소체는 내공생으로 얻는다)은 옮기지 않는다
        static readonly int[] HGT_G = { Gn.Osmo, Gn.Topt, Gn.Tbreadth, Gn.Aerobic, Gn.Pigment, Gn.Toxin, Gn.Wall, Gn.Dry };
        float[] Transfer(Strain st, int c, ref Rng r, out int hj)
        {
            hj = -1; double tot = 0;
            foreach (int o in live) { if (o == st.slot) continue; var so = strains[o]; for (int y = 0; y < nyR[c]; y++) for (int k = 0; k < so.K; k++) tot += cnt[Ix(o, y * LS + c, k)] * so.C[k]; }
            if (tot <= 0) return null;
            double x = r.U() * tot; Strain d = null;
            foreach (int o in live)
            {
                if (o == st.slot) continue; var so = strains[o];
                for (int y = 0; y < nyR[c] && d == null; y++) for (int k = 0; k < so.K; k++) { x -= cnt[Ix(o, y * LS + c, k)] * so.C[k]; if (x <= 0) { d = so; break; } }
                if (d != null) break;
            }
            if (d == null) return null;
            var diff = HGT_G.Where(j => Math.Abs(d.g[j] - st.g[j]) > Gn.Def[j].sd).ToArray();
            if (diff.Length == 0) return null;
            hj = diff[(int)(r.U() * diff.Length)];
            var g = (float[])st.g.Clone(); g[hj] = d.g[hj]; return g;
        }

        public float[] Mutate(float[] g, ref Rng r)
        {
            var c = (float[])g.Clone(); int n = 1 + (int)r.Poisson(0.3);   // 대부분 유전자 하나(0.5로 올리면 해로운 다중 돌연변이만 늘었다)
            for (int i = 0; i < n; i++)
            {
                int j = (int)(r.U() * Gn.N); if (Gn.BodyOnly(j) && !Gn.BodyReady(g)) j = (int)(r.U() * Gn.Seg); var G = Gn.Def[j];   // 몸이 없으면 몸 부품 대신 다른 유전자
                double d = r.Gauss() * G.sd * P.MUT_SD; if (r.U() < P.MUT_BIG) d *= 4;         // 드물게 효과 큰 돌연변이(대부분 해롭다: Fisher)
                if ((j == Gn.Cells || j == Gn.Egg) && r.U() < 0.15) d = r.U() < 0.5 ? 1 : -1;   // 세포 수 두 배·절반(분열 뒤 안 떨어지기)
                c[j] = j == Gn.Mutrate ? Math.Clamp(c[j] * (float)Math.Exp(d), G.lo, G.hi) : Math.Clamp(c[j] + (float)d, G.lo, G.hi);
            }
            return c;
        }

        public static double Dist(float[] a, float[] b)
        {
            bool body = Gn.BodyReady(a) || Gn.BodyReady(b);
            double s = 0; for (int i = 0; i < Gn.N; i++) { float sc = Gn.Def[i].sc; if (sc > 0 && (body || !Gn.BodyOnly(i))) { double d = (a[i] - b[i]) / sc; s += d * d; } }
            return Math.Sqrt(s);
        }

        void Totals()
        {
            totalCount = 0; totalBio = 0; births = deaths = kills = 0;
            for (int v = 0; v < V; v++) { births += vBirth[v]; deaths += vDeath[v]; kills += vKill[v]; }
            foreach (var sp in species.Values) sp.total = 0;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                int s = live[i]; var st = strains[s]; double tot = 0, gb = 0, kl = 0, bio = 0, kw = 0, mw = 0; int b = s * V;
                for (int k = 0; k < st.K; k++) st.stage[k] = 0;
                for (int v = 0; v < V; v++)
                {
                    int hc = v % LS; if (hc < NR) { int hk = geo.rKind[hc]; birthHab[hk] += genAcc[b + v]; for (int k = 0; k < st.K; k++) cntHab[hk] += cnt[Ix(s, v, k)]; }
                    gb += genAcc[b + v]; kl += killAcc[b + v]; genAcc[b + v] = 0; killAcc[b + v] = 0;
                    for (int k = 0; k < st.K; k++) { int q = Ix(s, v, k); double c = cnt[q]; st.stage[k] += c; kw += c * kR[q]; mw += c * mu[q]; }
                }
                for (int k = 0; k < st.K; k++) { tot += st.stage[k]; bio += st.stage[k] * st.C[k]; }
                if (tot <= 0) { Remove(s); continue; }
                st.predE = 0.8f * st.predE + 0.2f * (float)(kw / tot); st.muE = 0.8f * st.muE + 0.2f * (float)(mw / tot);
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

        // 하루마다: 종 분화·처음 나온 몸 설계 판정, 기록
        void Daily()
        {
            double thr = Math.Max(1e4, totalCount * 2e-4);
            for (int li = 0; li < live.Count; li++)
            {
                var st = strains[live[li]];
                var lk = Taxa.Of(st);
                if (!looksSeen.Contains(lk) && st.total >= Math.Min(thr, 50)) { looksSeen.Add(lk); Log("grade", $"처음으로 {Taxa.Name(st)} 몸 설계 등장"); }
                if (st.total < thr && !(st.dev && st.total >= 50)) continue;      // 발생하는 다세포는 수가 적어도 종으로 인정
                var sp = species[st.sp];
                if (Dist(st.g, sp.rep) <= P.SPEC_D) continue;
                var ns = NewSpecies(st.g, sp.id); ns.look = lk; Names.Name(ns, species.Values);
                ns.named = true; st.sp = ns.id;
                foreach (int o in live) { var so = strains[o]; if (so.sp == sp.id && Dist(so.g, ns.rep) < Dist(so.g, sp.rep)) so.sp = ns.id; }
                Log("sp", $"신종: {ns.name} — {Taxa.Name(st)} · {ns.ko} ({Names.Of(sp)}에서 갈라짐)");
            }
            // 상륙: 마름을 견디는 계통이 육지 물막에 mL당 10(10 μm 세포 몫) 넘게 자리 잡으면(바다에서 날려 온 것만으로는 이만큼 못 쌓인다)
            for (int c = geo.NO; c < NR && !landA; c++) if (geo.rKind[c] == 2) foreach (int s in live)
            {
                var st = strains[s]; if (st.g[Gn.Dry] < 0.5f) continue;
                double b = 0; for (int k = 0; k < st.K; k++) b += cnt[Ix(s, c, k)] * st.C[k];
                if (b / vol[c] < 10) continue;
                if (!landed) { landed = true; Log("grade", $"처음으로 생물이 땅에 올라왔습니다: {Taxa.Name(st)} ({geo.Name(c)})"); }
                if (st.dev && (st.g[Gn.Gut] > 0.5f || st.g[Gn.Neuro] > 0.5f)) { landA = true; Log("grade", $"처음으로 동물이 땅에 올라왔습니다: {Taxa.Name(st)} ({geo.Name(c)})"); break; }
            }
            if (recs.Count > 6000) PruneRecs();
            hist.Add(this);
        }

        public string Time(long ticks)   // 틱 → 사람이 읽는 기간
        {
            long h = (long)(ticks * P.DT), d = h / 24; if (d < 1) return h + "시간"; if (d < 365) return d + "일";
            return (d / 365) + "년 " + (d % 365) + "일";
        }

        public float[] MeanGenesBy(Func<Strain, double> weight)
        {
            var m = new double[Gn.N]; double w = 0;
            foreach (int s in live) { var st = strains[s]; double ww = weight(st); for (int i = 0; i < Gn.N; i++) m[i] += st.g[i] * ww; w += ww; }
            var r = new float[Gn.N]; for (int i = 0; i < Gn.N; i++) r[i] = w > 0 ? (float)(m[i] / w) : 0; return r;
        }
        public float[] MeanGenes() => MeanGenesBy(s => s.total);
        public double Biomass(Strain st) { double b = 0; for (int k = 0; k < st.K; k++) b += st.stage[k] * st.C[k]; return b * Math.Pow(st.esd0 / 10, 3); }
        public float AdultL(Strain st) => MorphT(st, 15, st.K - 1, 0).L;   // 성체 몸길이(μm, 15 °C 기준)
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
            ("Arthro", "articulata", "마디", g => (g[Gn.Seg] - 2) / 1),
            ("Podo", "pedata", "다리·지느러미", g => (g[Gn.Limb] - 0.45f) / 0.15f),
            ("Ostraco", "armata", "갑옷", g => (g[Gn.Exo] - 0.45f) / 0.15f),
            ("Calci", "calcarea", "껍데기·뼈", g => (g[Gn.Mineral] - 0.45f) / 0.15f),
            ("Gnatho", "mordax", "턱", g => (g[Gn.Jaw] - 0.45f) / 0.15f),
            ("Phyto", "herbivora", "초식", g => (g[Gn.Herb] - 0.45f) / 0.15f),
            ("Sapro", "detritivora", "부스러기 먹기", g => (g[Gn.Detr] - 0.8f) / 0.1f),
            ("Lochi", "insidians", "매복", g => (g[Gn.Ambush] - 0.45f) / 0.15f),
            ("Orycto", "fossor", "땅 파기", g => (g[Gn.Burrow] - 0.45f) / 0.15f),
            ("Toxo", "venenata", "독", g => (g[Gn.Toxin] - 0.4f) / 0.15f),
            ("Hyalo", "hyalina", "투명", g => (g[Gn.Transp] - 0.45f) / 0.15f),
            ("Agelo", "gregaria", "떼짓기", g => (g[Gn.School] - 0.45f) / 0.15f),
            ("Macro", "vorax", "큰 먹이", g => (0.9f - g[Gn.PreyR]) / 0.25f),
            ("Micro", "colatrix", "작은 먹이 거르기", g => (g[Gn.PreyR] - 2.6f) / 0.4f),
        };
        static readonly string[] PLAIN = { "primigenia", "communis", "vulgaris", "simplex", "antiqua", "nova", "modesta", "pelagica", "errans", "parva" };

        static string Suffix(float[] g, Look lk)
        {
            switch (lk)
            {
                case Look.Jawless: case Look.Armored: case Look.Placoderm: case Look.Shark: case Look.Bony: return "ichthys";
                case Look.Lancelet: return "chorda";
                case Look.Arthropod: case Look.Copepod: return "caris";
                case Look.Trilobite: return "lobus";
                case Look.Snail: return "gastra";
                case Look.Clam: return "concha";
                case Look.Annelid: case Look.Polychaete: return "nereis";
                case Look.ArrowWorm: return "sagitta";
                case Look.Flatworm: return "helmis";
                case Look.Echino: return "aster";
                case Look.Polyp: case Look.Coral: case Look.Jelly: return "cnida";
            }
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
            string genus = (d.Count > 0 ? d[0].pre : "Proto") + Suffix(sp.rep, sp.look);
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
