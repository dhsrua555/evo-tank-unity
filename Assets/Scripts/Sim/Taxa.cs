// 자동 분류: 형질(부품) 조합으로 몸 설계를 읽고, 지구 생물 중 닮은 무리 이름을 붙인다.
// 사다리가 아니다 — 목표도 순서도 없다. 어떤 조합이든 나올 수 있고, 닮은 무리가 없으면 몸 설계 이름만 붙는다.
using System;

namespace Evo
{
    // 단세포 몸 설계(실제 원생생물 무리의 몸 구조)
    public enum Plan { Plain, Ciliate, Diatom, Dino, Radiolarian, Euglena, Choano, Amoeba }

    // 몸 설계: 화면 그리기(Bodies)와 이름표가 같이 쓴다
    public enum Look { Cell, Colony, Blob, Seaweed, Sponge, Polyp, Coral, Jelly, Echino, Flatworm, ArrowWorm, Annelid, Polychaete, Arthropod, Copepod, Trilobite, Snail, Clam, Lancelet, Jawless, Armored, Placoderm, Shark, Bony }

    public static class Taxa
    {
        public static readonly string[] PlanName = { "", "섬모충", "돌말", "와편모충", "방산충", "유글레나", "깃편모충", "아메바" };
        public static readonly string[] LookName = { "원생생물", "군체", "다세포 덩어리", "해조류", "해면", "말미잘", "산호", "해파리", "극피동물", "편형동물", "화살벌레", "환형동물", "갯지렁이",
            "절지동물", "요각류", "삼엽충", "고둥", "조개", "창고기", "칠성장어", "갑주어", "판피어", "상어", "경골어류" };
        public const int NL = 24;

        public static Plan PlanOf(Strain st)
        {
            var g = st.g; bool fl = g[Gn.Flagella] > 0.25f;
            if (g[Gn.Cilia] > 0.35f) return Plan.Ciliate;                                              // 짚신벌레·종벌레·유종섬모충
            if (!fl && g[Gn.Wall] > 0.5f && g[Gn.Photo] > 0.3f) return Plan.Diatom;                    // 규산 껍질 + 광합성, 편모 없음
            if (fl && g[Gn.Wall] > 0.25f) return Plan.Dino;                                            // 갑옷 + 편모 둘(가로·세로)
            if (!fl && g[Gn.Wall] > 0.35f && g[Gn.Phago] > 0.4f) return Plan.Radiolarian;             // 골격 + 포식, 축족으로 먹이를 잡는다
            if (fl && g[Gn.Wall] < 0.3f && st.aspect > 1.8f) return Plan.Euglena;                     // 길쭉 + 유연한 겉막 + 편모 하나
            if (fl && g[Gn.Phago] > 0.6f && g[Gn.Photo] < 0.15f && g[Gn.Size] < 4.2f) return Plan.Choano;   // 작은 포식 편모충: 깃으로 세균을 거른다
            if (!fl && g[Gn.Phago] > 0.55f && g[Gn.Wall] < 0.2f && g[Gn.Lorica] < 0.3f) return Plan.Amoeba;
            return Plan.Plain;
        }

        public static float AdultL(Strain s) => s.esd0 * MathF.Pow(s.C[s.K - 1], 1 / 3f) * s.longAx;   // μm(대략)

        public static Look Of(Strain s)
        {
            var g = s.g;
            if (g[Gn.Cells] < 1) return Look.Cell;
            if (!s.dev) return Look.Colony;
            bool animal = g[Gn.Gut] > 0.5f || g[Gn.Neuro] > 0.5f;
            if (!animal) return g[Gn.Photo] > 0.3f ? Look.Seaweed : g[Gn.Attach] > 0.5f && g[Gn.Phago] > 0.3f ? Look.Sponge : Look.Blob;
            float armor = MathF.Max(g[Gn.Exo] * (0.5f + 0.5f * g[Gn.Mineral]), 0.6f * g[Gn.Skel] * g[Gn.Mineral]);
            if (g[Gn.Bilat] < 0.5f)   // 방사대칭
            {
                if (armor > 0.35f && g[Gn.Gut] > 0.5f) return Look.Echino;                            // 광물 골판 + 관족(성게·불가사리)
                if (g[Gn.Attach] > 0.5f) return g[Gn.Gut] < 0.5f ? Look.Sponge : g[Gn.Mineral] > 0.4f ? Look.Coral : Look.Polyp;
                return Look.Jelly;
            }
            float L = AdultL(s);
            if (g[Gn.Skel] >= 0.4f)   // 척삭·척추
            {
                bool fish = g[Gn.Circ] > 0.5f && g[Gn.Eye] > 0.4f && g[Gn.Neuro] > 0.5f && g[Gn.Skel] > 0.6f && g[Gn.Attach] < 0.5f && L >= 5e3f;
                if (!fish) return Look.Lancelet;
                if (g[Gn.Jaw] < 0.4f) return g[Gn.Mineral] >= 0.4f ? Look.Armored : Look.Jawless;
                if (g[Gn.Exo] >= 0.4f && g[Gn.Mineral] >= 0.5f) return Look.Placoderm;                   // 턱 + 머리 갑옷판
                return g[Gn.Mineral] >= 0.4f ? Look.Bony : Look.Shark;
            }
            bool segd = g[Gn.Seg] >= 2;
            if (g[Gn.Exo] >= 0.4f && segd && g[Gn.Limb] >= 0.3f)                                     // 마디 + 외골격 + 다리
            {
                if (g[Gn.Mineral] >= 0.5f) return Look.Trilobite;                                       // 방해석 갑옷
                return L < 5e3f && g[Gn.Burrow] < 0.5f && g[Gn.Attach] < 0.5f ? Look.Copepod : Look.Arthropod;
            }
            if (g[Gn.Exo] >= 0.4f && g[Gn.Mineral] >= 0.4f && !segd) return g[Gn.Burrow] >= 0.5f || g[Gn.Attach] > 0.5f ? Look.Clam : Look.Snail;   // 껍데기
            if (segd) return g[Gn.Limb] >= 0.3f ? Look.Polychaete : Look.Annelid;
            if (g[Gn.Jaw] >= 0.4f && g[Gn.Transp] >= 0.4f) return Look.ArrowWorm;                  // 투명한 매복 포식자, 턱 가시
            return Look.Flatworm;
        }

        // 이름표: "절지동물 닮음", 단세포는 "섬모충 닮음", 닮은 무리가 없으면 몸 설계만
        public static string Name(Strain s)
        {
            if (s.g[Gn.Dry] < 0.25f) return WaterName(s);   // 상륙 사건과 같은 문턱
            var lk = Of(s);   // 마름을 잘 견디면 땅에서 산다: 닮은 육상 무리가 있으면 그 이름, 없으면 '육상'을 붙인다
            return lk == Look.Seaweed ? "이끼 닮음" : lk == Look.Annelid ? "지렁이 닮음" : lk == Look.Snail ? "달팽이 닮음" : lk == Look.Arthropod || lk == Look.Copepod ? "노래기 닮음" : "육상 " + WaterName(s);
        }
        static string WaterName(Strain s)
        {
            var lk = Of(s);
            if (lk == Look.Cell) { var p = PlanOf(s); return p == Plan.Plain ? (s.g[Gn.Photo] > 0.3f ? "광합성 원생생물" : "편모충") : PlanName[(int)p] + " 닮음"; }
            if (lk == Look.Colony) { var g = s.g; return g[Gn.Photo] > 0.3f ? (g[Gn.Form] > 0.7f ? "볼복스 닮음" : g[Gn.Form] < 0.33f ? "실 모양 조류 닮음" : "판 모양 군체") : "군체"; }
            if (lk == Look.Blob) return LookName[(int)lk];
            return LookName[(int)lk] + " 닮음";
        }
        public static string Name(Look lk) => lk <= Look.Blob ? LookName[(int)lk] : LookName[(int)lk] + " 닮음";
    }
}
