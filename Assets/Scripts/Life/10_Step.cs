// 한 틱의 순서와 하루 기록. 순서: (하루 시작이면 사건·종·정리) → 기후 → 칸별 생태(병렬) → 섞임·가라앉음 → 무생물 반응 → 기체 교환 → 경계 → 진화.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Life
{
    public struct DayStat
    {
        public int day, nSp, nStrain, nClade;
        public float O2, CO2, CH4, T;             // PAL, PAL, ppm, °C
        public float bio;                          // 전체 생물량(mol C)
        public float photo;                        // 빛으로 고정한 탄소(mol C/일)
        public float oxyShare;                     // 고정한 탄소 중 산소 광합성 몫
        public float multi;                        // 덩이(평균 세포 수 ≥ 8) 생물량 몫
        public float aer, sod;                     // 태운 유기물 중 산소 호흡 몫, 생물량 가중 항산화 포화도
        public float resp, aerOx;                  // 생물량 가중 호흡 단백질 포화도, 산소 있는 물(O2 ≥ K_AER)에서 태운 것 중 산소 호흡 몫
        public float photoSh, chemoSh, preySh, detSh, docSh;   // 전체 탄소 수입 몫
        public int rPhoto, rChemo, rPrey, rDet, rDoc;          // 그 역할이 수입의 절반 넘는 종 수
        public float maxCells, maxSize;            // 가장 큰 덩이 세포 수, 크기(μm)
        public float drift;                        // 장부 어긋남(상대, C·N·P·Fe·S 중 최대)
        public int kept;                           // 오늘 정착한 돌연변이
        public float limE, limR, limN, limP, limS;  // 성장을 막은 것 몫(생물량·틱 가중): 에너지·탄소, 리보솜, 질소, 인, 칸 자원 나눔
    }

    public sealed partial class Planet
    {
        public readonly List<DayStat> history = new();
        [NonSerialized] public readonly object gate = new();      // 화면이 읽는 동안 잠금
        public double[] lastLedger;
        [NonSerialized] public Action<string> probe;           // 시험용: 단계마다 불림
        [NonSerialized] public readonly ParallelOptions po = new();
        [NonSerialized] int[] vOrd; [NonSerialized] double[] vKey;   // 앱: 화면 몫을 남기려고 병렬 수를 줄인다

        public void Step()
        {
            lock (gate)
            {
                long t0 = Stopwatch.GetTimestamp();
                if (tick % Cfg.TPD == 0) Daily();
                probe?.Invoke("daily"); Lap(0, ref t0);
                Climate(); Lap(1, ref t0);
                PredCache();
                // 칸마다 일이 크게 다르다(계통 수의 제곱): 지난 틱에 무거웠던 칸부터 하나씩 나눠 줘야 마지막에 한 스레드만 남아 돌지 않는다
                vOrd ??= new int[V]; vKey ??= new double[V];
                for (int v = 0; v < V; v++) { int f = Under(v); double w = nV[v] + (f >= 0 ? nV[f] : 0); vOrd[v] = v; vKey[v] = -w * w; }
                Array.Sort(vKey, vOrd);
                Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(vOrd, System.Collections.Concurrent.EnumerablePartitionerOptions.NoBuffering), po, v => { if (v >= NF0) return; Bio(v); int f = Under(v); if (f >= 0) Bio(f); });
                probe?.Invoke("bio"); Lap(2, ref t0);   // 퇴적면은 바로 위 물 칸 다음 차례로 같은 일감에서: 위 물의 떠 있는 먹이를 걸러 먹어 두 칸을 함께 고친다(06 Bio). 다른 칸은 이 둘을 건드리지 않는다
                Transport(); probe?.Invoke("transport");
                buryC ??= new double[V]; buryFe ??= new double[V]; buryS ??= new double[V]; buryP ??= new double[V];
                Parallel.For(0, V, po, v => { if (live[v]) Abiotic(v); }); SumBurial(); Carbonate(); probe?.Invoke("abiotic");
                Gas(); probe?.Invoke("gas");
                Boundary(); SpeedP(); probe?.Invoke("boundary"); Lap(3, ref t0);
                Evolve(); probe?.Invoke("evolve"); Lap(4, ref t0);
                tick++;
            }
        }

        public readonly double[] tStage = new double[5];   // 단계별 누적 시간(초): 하루 일, 기후, 생태, 물질, 진화
        void Lap(int k, ref long t0) { long t = Stopwatch.GetTimestamp(); tStage[k] += (t - t0) / (double)Stopwatch.Frequency; t0 = t; }

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
            double bio = 0, multi = 0, sod = 0, inc5 = 0; var sh = new double[5]; double o2 = 0, aerC = 0, catC = 0, resp = 0, aerOx = 0, catOx = 0;
            int st = Cfg.SMAX * V;
            foreach (var s in alive)
            {
                bio += s.total; if (s.t.MeanCells >= 8) multi += s.total; sod += s.total * s.t.Sat(G.SOD, 0.005); resp += s.total * s.t.Sat(G.RESP, 0.02);
                d.maxCells = Math.Max(d.maxCells, (float)s.t.MeanCells); d.maxSize = Math.Max(d.maxSize, (float)s.b.size);
                for (int v = 0; v < V; v++)
                {
                    int o = s.slot * V + v;
                    for (int q = 0; q < 5; q++) sh[q] += inc[q * st + o];
                    o2 += inc[5 * st + o]; aerC += inc[6 * st + o]; catC += inc[7 * st + o];
                    if (pool[P.O2][v] >= Cfg.K_AER) { aerOx += inc[6 * st + o]; catOx += inc[7 * st + o]; }
                }
            }
            for (int q = 0; q < 5; q++) inc5 += sh[q];
            d.bio = (float)bio; d.multi = bio > 0 ? (float)(multi / bio) : 0; d.sod = bio > 0 ? (float)(sod / bio) : 0;
            d.aer = catC > 0 ? (float)(aerC / catC) : 0; d.aerOx = catOx > 0 ? (float)(aerOx / catOx) : 0; d.resp = bio > 0 ? (float)(resp / bio) : 0;
            d.photo = (float)sh[0];
            double fix = sh[0] + sh[1]; d.oxyShare = fix > 0 ? (float)(o2 / fix) : 0;
            if (inc5 > 0) { d.photoSh = (float)(sh[0] / inc5); d.chemoSh = (float)(sh[1] / inc5); d.preySh = (float)(sh[2] / inc5); d.detSh = (float)(sh[3] / inc5); d.docSh = (float)(sh[4] / inc5); }
            int nc = 0; var cl = new HashSet<int>();
            foreach (var sp in species)
            {
                if (!sp.Alive) continue; cl.Add(sp.clade);
                switch (sp.role) { case Role.광합성: d.rPhoto++; break; case Role.화학합성: d.rChemo++; break; case Role.포식: d.rPrey++; break; case Role.청소: d.rDet++; break; case Role.분해: d.rDoc++; break; }
            }
            d.nClade = cl.Count;
            Array.Clear(limLast, 0, limLast.Length);
            for (int q = 0; q < 2 * LQ; q++) for (int v = 0; v < V; v++) limLast[q] += limBio[q * V + v];
            var lq = new double[5]; double lt = 0;
            for (int q = 0; q < 2 * LQ; q++) { lq[Math.Min(4, q % LQ)] += limLast[q]; lt += limLast[q]; }
            if (lt > 0) { d.limE = (float)(lq[0] / lt); d.limR = (float)(lq[1] / lt); d.limN = (float)(lq[2] / lt); d.limP = (float)(lq[3] / lt); d.limS = (float)(lq[4] / lt); }
            Array.Clear(limBio, 0, limBio.Length);
            // 장부: 처음과 비교(경계 순유입을 뺀 값이 그대로여야 한다)
            var L = Ledger(); lastLedger = L; double drift = 0;
            for (int i = 0; i < L.Length; i++) drift = Math.Max(drift, Math.Abs(L[i] - ledger0[i]) / Math.Max(1e-30, Math.Abs(ledger0[i])));
            d.drift = (float)drift;
            history.Add(d);
        }
    }
}
