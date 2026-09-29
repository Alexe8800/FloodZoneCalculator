using System;
using System.Collections.Generic;

namespace FloodZoneCalculator.Domain
{
    public static class Formulas
    {
        public const double G = 9.81;
        public const double RhoWater = 1000.0;

        // ---- 2.1 Шези ----
        public static double ChezyTau0ByVelocity(double rho, double g, double v, double c)
            => rho * g * v * v / (c * c);

        public static double ChezyTau0BySlope(double rho, double g, double r, double i)
            => rho * g * r * i;

        // ---- 2.2 Вейсбах-Дарси ----
        public static double WeisbachDarcyTau0(double lambda, double rho, double v)
            => lambda * rho * v * v / 2.0;

        public static double DynamicVelocity(double tau0, double rho = RhoWater)
            => Math.Sqrt(tau0 / rho);

        // ---- 2.3 динамическая скорость ----
        public static double DynamicVelocityBySlope(double g, double h, double i)
            => Math.Sqrt(g * h * i);

        // ---- 2.4 ----
        public static double ChezyCFromVStar(double vStar, double v, double g = G)
            => vStar <= 0 ? double.PositiveInfinity : Math.Sqrt(g) * v / vStar;

        public static double DarcyLambdaFromVStar(double vStar, double v)
            => v <= 0 ? 0 : 8.0 * Math.Pow(vStar / v, 2);

        // ---- 2.13 Зегжда ----
        public static double ZegzhdaCd(double h, double delta, double g = G)
            => delta <= 0 || h <= 0 ? 0 : Math.Sqrt(g) * (5.66 * Math.Log10(h / delta) + 6.0);

        // ---- 2.14 Штриклер-Маннинг ----
        public static double StricklerManningCd(double h, double d50, double g = G)
            => d50 <= 0 || h <= 0 ? 0 : Math.Sqrt(g) * 6.67 * Math.Pow(h / d50, 1.0 / 6.0);

        // ---- 2.15 ----
        public static double StricklerNd(double d50, double g = G)
            => d50 <= 0 ? 0 : 0.15 / Math.Sqrt(g) * Math.Pow(d50, -1.0 / 6.0);

        public static double ManningC(double n, double r)
            => n <= 0 || r <= 0 ? 0 : (1.0 / n) * Math.Pow(r, 1.0 / 6.0);

        public static double ChezyVelocity(double c, double r, double i)
            => c * Math.Sqrt(Math.Max(0, r) * Math.Max(0, i));

        // ---- 2.16 Кимроз ----
        public static double KimrozCw(double lw, double hw, double h, double g = G)
            => hw <= 0 || lw <= 0 || h <= 0
                ? 0
                : Math.Sqrt(g) * 3.16 * Math.Sqrt(lw / hw) * Math.Pow(h / hw, 1.0 / 8.0);

        // ---- 2.17 Синиенко ----
        public static double SinienkoCw(double lw, double h0w, double l0w, double g = G)
        {
            var denom = h0w + 0.033 * l0w;
            return denom <= 0 || lw <= 0 ? 0 : Math.Sqrt(g) * Math.Sqrt(8.75 * lw / denom);
        }

        // ---- 2.18 Абу Алам-Кеннеди ----
        public static double AbuAlamaKennedyLambdaW(double g, double cw)
            => cw <= 0 ? 0 : 2.0 * g / (cw * cw);

        // ---- 2.24 Хук-Оними ----
        public static double HookeC(double v, double nu, double ra, double hu, double g = G)
            => nu <= 0 || hu <= 0
                ? 0
                : Math.Sqrt(g) * 1.12 * Math.Sqrt(v / Math.Pow(g * nu, 1.0 / 3.0)) * (ra / hu);

        // ---- 2.25-2.27 Гриффитс ----
        public static double GriffithsC1(double h, double d50, double g = G)
            => d50 <= 0 || h <= 0 ? 0 : Math.Sqrt(g) * (5.56 * Math.Log10(h / d50) + 2.15);

        public static double GriffithsC2(double h, double d50, double g = G)
            => d50 <= 0 || h <= 0 ? 0 : Math.Sqrt(g) * (5.56 * Math.Log10(h / d50) + 1.0);

        public static double GriffithsGravelC(double v, double d50, double g = G)
            => d50 <= 0 ? 0 : Math.Sqrt(g) * 6.25 * Math.Pow(v / Math.Sqrt(g * d50), 0.34);

        // ---- 3.1 Бернулли ----
        public static double BernoulliEnergy(double z, double alpha, double v, double hDl, double g = G)
            => z + alpha * v * v / (2.0 * g) + hDl;

        // ---- 3.5, 3.6 уклон трения ----
        public static double FrictionSlopeByQk(double q, double k)
            => k <= 0 ? 0 : q * q / (k * k);

        public static double FrictionSlopeByCvh(double v, double c, double h)
            => c <= 0 || h <= 0 ? 0 : v * v / (c * c * h);

        public static double CoriolisAlpha(double intU3, double v, double omega)
            => v == 0 || omega == 0 ? 1 : intU3 / (Math.Pow(v, 3) * omega);

        public static double BoussinesqBeta(double intU2, double v, double omega)
            => v == 0 || omega == 0 ? 1 : intU2 / (v * v * omega);

        // ---- 3.19 Сен-Венан ----
        public static double SaintVenantLhs(double dvDt, double alpha0, double dv2Dl)
            => dvDt + alpha0 / 2.0 * dv2Dl;

        public static double SaintVenantRhs(double g, double i, double c, double v, double hc)
            => c <= 0 || hc <= 0 ? 0 : g * i - g / (c * c) * v * v / hc;

        // ---- 6.1 Ньютон ----
        public static double NewtonViscosity(double mu, double dudz) => mu * dudz;

        // ---- 6.12 логарифмический профиль ----
        public static double LogVelocityProfile(double z, double delta, double uDelta,
            double vStar, double kappa = 0.4)
            => z <= 0 || delta <= 0 ? 0 : vStar * (1.0 / kappa) * Math.Log(z / delta) + uDelta;

        // ---- 6.20 Буссинеск ----
        public static double BoussinesqTau(double rho, double nuT, double dudz)
            => rho * nuT * dudz;

        // ---- 6.21 Прандтль ----
        public static double PrandtlMixingLength(double l, double dudz)
            => l * l * Math.Abs(dudz);

        // ---- 6.22 Рейнольдс ----
        public static double ReynoldsTauProfile(double tau0, double z, double h)
            => h <= 0 ? 0 : tau0 * (1.0 - z / h);

        // ---- 6.24-6.26 Колмогоров, k-epsilon ----
        public static double KolmogorovNuT(double l, double b)
            => l * Math.Sqrt(Math.Max(0, b));

        public static double KolmogorovDissipation(double c, double rho, double b, double l)
            => l <= 0 ? 0 : c * rho * Math.Pow(b, 1.5) / l;

        public static double KEpsilonNuT(double k, double eps, double cMu = 0.09)
            => eps <= 0 ? 0 : cMu * k * k / eps;

        // ---- вспомогательные ----
        public static double TrapezoidalArea(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
        {
            if (xs.Count != ys.Count || xs.Count < 2) return 0;
            double s = 0;
            for (int i = 0; i < xs.Count - 1; i++)
                s += 0.5 * (ys[i] + ys[i + 1]) * (xs[i + 1] - xs[i]);
            return Math.Abs(s);
        }

        public static double GradientDryingVelocity(double zMax, double zBase, double tHours)
            => tHours <= 0 ? 0 : (zMax - zBase) / tHours;

        public static double LinearInterp(double x, double x0, double y0, double x1, double y1)
            => x1 == x0 ? y0 : y0 + (x - x0) * (y1 - y0) / (x1 - x0);
    }
}
