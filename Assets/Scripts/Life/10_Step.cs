// 한 틱의 순서와 하루 기록. 순서: (하루 시작이면 사건·종·정리) → 기후 → 칸별 생태(병렬) → 섞임·가라앉음 → 무생물 반응 → 기체 교환 → 경계 → 진화.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Life
{
    public struct DayStat
    {
        public int day, nSp, nStrain, nClade;
        public float O2, CO2, CH4, T;             // PAL, PAL, ppm, °C
        public float bio;                          // 전체 생물량(mol C)
        public float oxyShare;                     // 고정한 탄소 중 산소 광합성 몫
        public float multi;                        // 덩이(평균 세포 수 ≥ 8) 생물량 몫
        public float aer, sod;                     // 태운 유기물 중 산소 호흡 몫, 생물량 가중 항산화 포화도
        public float photoSh, chemoSh, preySh, detSh, docSh;   // 전체 탄소 수입 몫
        public int rPhoto, rChemo, rPrey, rDet, rDoc;          // 그 역할이 수입의 절반 넘는 종 수
        public float maxCells, maxSize;            // 가장 큰 덩이 세포 수, 크기(μm)
        public float drift;                        // 장부 어긋남(상대, C·N·P·Fe·S 중 최대)
        public int kept;                           // 오늘 정착한 돌연변이
    }

    public sealed partial class Planet
    {
        public readonly List<DayStat> history = new();
        public readonly object gate = new();      // 화면이 읽는 동안 잠금
        public double[] lastLedger;
        public Action<string> probe;           // 시험용: 단계마다 불림
        public readonly ParallelOptions po = new();   // 앱: 화면 몫을 남기려고 병렬 수를 줄인다

        public void Step()
        {
            lock (gate)
            {
                if (tick % Cfg.TPD == 0) Daily();
                probe?.Invoke("daily");
                Climate();
                Parallel.For(0, V, po, Bio); probe?.Invoke("bio");
                Transport(); probe?.Invoke("transport");
                Parallel.For(0, V, po, v => { if (live[v]) Abiotic(v); }); probe?.Invoke("abiotic");
                Gas(); probe?.Invoke("gas");
                Boundary(); probe?.Invoke("boundary");
                Evolve(); probe?.Invoke("evolve");
                tick++;
            }
        }

        void Daily()
        {
            Events();
            double nf = 0; for (int v = 0; v < V; v++) { nf += nfixV[v]; nfixV[v] = 0; } inN += nf;
            Prune();
            Speciate();
            Record();
            Array.Clear(inc, 0, inc.Length);
            triedToday = keptToday = 0;
        }

        void Record()
        {
            var d = new DayStat { day = (int)Day, nStrain = alive.Count, nSp = nAliveSp, O2 = (float)O2PAL, CO2 = (float)CO2PAL, CH4 = (float)CH4ppm, T = (float)Tglobal, kept = keptToday };
            double bio = 0, multi = 0, sod = 0, inc5 = 0; var sh = new double[5]; double o2 = 0, aerC = 0, catC = 0;
            int st = Cfg.SMAX * V;
            foreach (var s in alive)
            {
                bio += s.total; if (s.t.MeanCells >= 8) multi += s.total; sod += s.total * s.t.Sat(G.SOD, 0.005);
                d.maxCells = Math.Max(d.maxCells, (float)s.t.MeanCells); d.maxSize = Math.Max(d.maxSize, (float)s.b.size);
                for (int v = 0; v < V; v++)
                {
                    int o = s.slot * V + v;
                    for (int q = 0; q < 5; q++) sh[q] += inc[q * st + o];
                    o2 += inc[5 * st + o]; aerC += inc[6 * st + o]; catC += inc[7 * st + o];
                }
            }
            for (int q = 0; q < 5; q++) inc5 += sh[q];
            d.bio = (float)bio; d.multi = bio > 0 ? (float)(multi / bio) : 0; d.sod = bio > 0 ? (float)(sod / bio) : 0;
            d.aer = catC > 0 ? (float)(aerC / catC) : 0;
            double fix = sh[0] + sh[1]; d.oxyShare = fix > 0 ? (float)(o2 / fix) : 0;
            if (inc5 > 0) { d.photoSh = (float)(sh[0] / inc5); d.chemoSh = (float)(sh[1] / inc5); d.preySh = (float)(sh[2] / inc5); d.detSh = (float)(sh[3] / inc5); d.docSh = (float)(sh[4] / inc5); }
            int nc = 0; var cl = new HashSet<int>();
            foreach (var sp in species)
            {
                if (!sp.Alive) continue; cl.Add(sp.clade);
                switch (sp.role) { case Role.광합성: d.rPhoto++; break; case Role.화학합성: d.rChemo++; break; case Role.포식: d.rPrey++; break; case Role.청소: d.rDet++; break; case Role.분해: d.rDoc++; break; }
            }
            d.nClade = cl.Count;
            // 장부: 처음과 비교(경계 순유입을 뺀 값이 그대로여야 한다)
            var L = Ledger(); lastLedger = L; double drift = 0;
            for (int i = 0; i < L.Length; i++) drift = Math.Max(drift, Math.Abs(L[i] - ledger0[i]) / Math.Max(1e-30, Math.Abs(ledger0[i])));
            d.drift = (float)drift;
            history.Add(d);
        }
    }
}
