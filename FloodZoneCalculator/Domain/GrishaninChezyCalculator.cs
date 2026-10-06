using System;

namespace FloodZoneCalculator.Domain;

public static class GrishaninChezyCalculator
{
    public static double Calculate(double g, double nu, double h, double B)
    {
        ValidatePositiveFinite(g, nameof(g));
        ValidatePositiveFinite(nu, nameof(nu));
        ValidatePositiveFinite(h, nameof(h));
        ValidatePositiveFinite(B, nameof(B));

        var gNu = g * nu;
        EnsureFinite(gNu, nameof(g), "Промежуточное произведение g * nu должно быть конечным.");

        var gNuCubicRoot = Math.Pow(gNu, 1.0 / 3.0);
        EnsureFinite(gNuCubicRoot, nameof(g), "Кубический корень из g * nu должен быть конечным.");

        var viscosityRatio = nu / gNuCubicRoot;
        EnsureFinite(viscosityRatio, nameof(nu), "Отношение nu / (g * nu)^(1/3) должно быть конечным.");

        var depthWidthRatio = h / B;
        EnsureFinite(depthWidthRatio, nameof(h), "Отношение h / B должно быть конечным.");

        var viscosityFactor = Math.Sqrt(viscosityRatio);
        EnsureFinite(viscosityFactor, nameof(nu), "Промежуточный корень вязкостного множителя должен быть конечным.");

        var depthWidthFactor = Math.Pow(depthWidthRatio, 1.0 / 6.0);
        EnsureFinite(depthWidthFactor, nameof(h), "Промежуточная степень h / B должна быть конечной.");

        var result = 5.25 * Math.Sqrt(g) * viscosityFactor * depthWidthFactor;
        EnsureFinite(result, nameof(g), "Коэффициент Шези C должен быть конечным.");

        return result;
    }

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Значение должно быть конечным числом больше нуля.");
    }

    private static void EnsureFinite(double value, string parameterName, string message)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameterName, value, message);
    }
}
