// 난수: SplitMix64. 시드가 같으면 같은 세계.
using System;

namespace Evo
{
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
}
