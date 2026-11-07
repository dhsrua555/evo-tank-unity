// 종: 종을 연 계통의 유전체에서 D_SP 안에 있는 계통들. 미리 정한 분류는 없다.
// 큰 갈래: 갈래를 연 종에서 D_CLADE 넘게 멀어진 종이 새 갈래를 연다. 하는 일(먹이 역할)은 오늘 탄소를 어디서 얻었는지로 매일 판정.
using System;
using System.Collections.Generic;

namespace Life
{
    public enum Role { 광합성, 화학합성, 포식, 청소, 분해, 혼합 }

    public sealed class Species
    {
        public int id, parent = -1, clade, born, died = -1;
        public string name;
        public float[] g, g0;             // 대표 유전체(가장 많은 계통), 창시 유전체(종을 연 계통)
        public int rep = -1;              // 대표 계통 slot
        public double biomass, peak;      // mol C
        public int strains;
        public Role role = Role.혼합;
        public readonly double[] income = new double[5];   // 빛 고정, 화학 고정, 산 먹이, 사체, 녹은 유기물(오늘, mol C)
        public bool Alive => died < 0;
    }

    public sealed class Clade { public int id, founder, born; public string name; public float[] g; public int parent = -1; }

    public sealed partial class Planet
    {
        public readonly List<Species> species = new();     // id = 번호(멸종해도 남김: 계통수 창에서만 보임)
        public readonly List<Clade> clades = new();
        public int nAliveSp;

        static readonly string[] SYL = { "ar", "bo", "ce", "di", "el", "fa", "gu", "hi", "ix", "lo", "mu", "no", "or", "pa", "qu", "ri", "sa", "te", "ul", "vi", "xe", "zo", "ka", "ny", "th", "phy", "chl", "cyan", "my", "rho" };
        static readonly string[] END = { "a", "us", "um", "is", "ella", "ina", "ops", "ax", "ia", "on", "ites", "oides" };
        static string Name(int id, int salt)
        {
            var r = new Evo.Rng((ulong)(id * 7919 + salt * 104729 + 1));
            string s = SYL[(int)(r.U() * SYL.Length)] + SYL[(int)(r.U() * SYL.Length)] + (r.U() < 0.5 ? SYL[(int)(r.U() * SYL.Length)] : "") + END[(int)(r.U() * END.Length)];
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        Species NewSpecies(int parent, float[] g)
        {
            var sp = new Species { id = species.Count, parent = parent, born = (int)Day, g = g, g0 = g };
            sp.name = Name(sp.id, seed);
            if (parent < 0) { sp.clade = NewClade(sp, -1); }
            else
            {
                var c = clades[species[parent].clade];
                sp.clade = Genome.Dist(g, c.g) > Cfg.D_CLADE ? NewClade(sp, c.id) : c.id;
            }
            species.Add(sp); return sp;
        }

        int NewClade(Species founder, int parent)
        {
            var c = new Clade { id = clades.Count, founder = founder.id, born = (int)Day, g = founder.g, parent = parent, name = Name(founder.id, seed + 31337) + "류" };
            clades.Add(c); return c.id;
        }

        // 하루 한 번: 계통마다 자기 종의 창시 유전체에서 D_SP 넘게 멀어졌고 그 종 안에서 자리를 잡았으면(생물량 5 % 이상)
        // 가까운 살아 있는 종에 들어가거나 새 종을 연다(부모 = 원래 종). 사슬처럼 이어 붙이지 않으므로 종이 한 덩어리로 뭉치지 않는다.
        void Speciate()
        {
            var order = new List<Strain>(alive); order.Sort((a, b) => b.total.CompareTo(a.total));
            foreach (var sp in species) if (sp.Alive) { sp.biomass = 0; sp.strains = 0; sp.rep = -1; Array.Clear(sp.income, 0, 5); }
            foreach (var s in order) if (s.species >= 0) species[s.species].biomass += s.total;
            foreach (var s in order)
            {
                if (s.species < 0) { s.species = NewSpecies(-1, (float[])s.g.Clone()).id; continue; }
                var sp = species[s.species];
                if (Genome.Dist(s.g, sp.g0) <= Cfg.D_SP || s.total < 0.05 * sp.biomass) continue;
                Species best = null; double bd = Cfg.D_SP;
                foreach (var o in species) { if (!o.Alive || o == sp) continue; double d = Genome.Dist(s.g, o.g0); if (d <= bd) { bd = d; best = o; } }
                s.species = (best ?? NewSpecies(sp.id, (float[])s.g.Clone())).id;
            }
            foreach (var sp in species) if (sp.Alive) sp.biomass = 0;
            foreach (var s in order)
            {
                var sp = species[s.species]; sp.biomass += s.total; sp.strains++;
                if (sp.rep < 0) { sp.rep = s.slot; sp.g = s.g; }     // 가장 많은 계통이 대표
                for (int q = 0; q < 5; q++) { double x = 0; for (int v = 0; v < V; v++) x += inc[(q * Cfg.SMAX + s.slot) * V + v]; sp.income[q] += x; }
            }
            foreach (var sp in species) if (sp.Alive) sp.peak = Math.Max(sp.peak, sp.biomass);
            nAliveSp = 0;
            foreach (var sp in species)
            {
                if (!sp.Alive) continue;
                if (sp.strains == 0) { sp.died = (int)Day; sp.rep = -1; continue; }
                nAliveSp++;
                double sum = 0; for (int q = 0; q < 5; q++) sum += sp.income[q];
                int k = 0; for (int q = 1; q < 5; q++) if (sp.income[q] > sp.income[k]) k = q;
                sp.role = sum > 0 && sp.income[k] > 0.5 * sum ? (Role)k : Role.혼합;
            }
        }
    }
}
