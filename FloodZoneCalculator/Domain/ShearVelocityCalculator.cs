using System;

namespace FloodZoneCalculator.Domain;

public sealed class ShearVelocityCalculator
{
    public double Calculate(double hydraulicRadius, double hydraulicSlope, double gravity)
    {
        ValidateFinite(hydraulicRadius, nameof(hydraulicRadius));
        ValidateFinite(hydraulicSlope, nameof(hydraulicSlope));
        ValidateFinite(gravity, nameof(gravity));

        if (hydraulicRadius < 0)
            throw new ArgumentOutOfRangeException(
                nameof(hydraulicRadius),
                hydraulicRadius,
                "Гидравлический радиус R не может быть отрицательным.");

        if (gravity <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(gravity),
                gravity,
                "Ускорение свободного падения g должно быть положительным.");

        var radiusSlopeProduct = hydraulicRadius * hydraulicSlope;
        if (double.IsNaN(radiusSlopeProduct) || double.IsInfinity(radiusSlopeProduct))
            throw new ArgumentOutOfRangeException(
                nameof(hydraulicRadius),
                hydraulicRadius,
                "Произведение R * If должно быть конечным числом.");

        if (radiusSlopeProduct < 0)
            throw new ArgumentOutOfRangeException(
                nameof(hydraulicSlope),
                hydraulicSlope,
                "Произведение R * If не может быть отрицательным.");

        var radicand = gravity * radiusSlopeProduct;
        if (double.IsNaN(radicand) || double.IsInfinity(radicand))
            throw new ArgumentOutOfRangeException(
                nameof(hydraulicRadius),
                hydraulicRadius,
                "Произведение g * R * If должно быть конечным числом.");

        var result = Math.Sqrt(radicand);
        if (double.IsNaN(result) || double.IsInfinity(result))
            throw new ArgumentOutOfRangeException(
                nameof(hydraulicRadius),
                hydraulicRadius,
                "Результат должен быть конечным числом.");

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
