using System;

namespace FloodZoneCalculator.Domain;

public static class ChezyVelocityCalculator
{
    public static double Calculate(double C, double R, double If)
    {
        ValidateFinite(C, nameof(C));
        ValidateFinite(R, nameof(R));
        ValidateFinite(If, nameof(If));

        if (C <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(C),
                C,
                "Коэффициент Шези C должен быть положительным.");

        if (R < 0)
            throw new ArgumentOutOfRangeException(
                nameof(R),
                R,
                "Гидравлический радиус R не может быть отрицательным.");

        var radicand = R * If;
        if (double.IsNaN(radicand) || double.IsInfinity(radicand))
            throw new ArgumentOutOfRangeException(
                nameof(R),
                R,
                "Произведение R * If должно быть конечным числом.");

        if (radicand < 0)
            throw new ArgumentOutOfRangeException(
                nameof(If),
                If,
                "Произведение R * If не может быть отрицательным.");

        var result = C * Math.Sqrt(radicand);
        if (double.IsNaN(result) || double.IsInfinity(result))
            throw new ArgumentOutOfRangeException(
                nameof(C),
                C,
                "Скорость V должна быть конечным числом.");

        return result;
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Значение должно быть конечным числом.");
    }
}
