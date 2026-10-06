using System;

namespace FloodZoneCalculator.Domain;

public sealed class DischargeCalculator
{
    public double Calculate(double velocity, double wettedArea)
    {
        ValidateFinite(velocity, nameof(velocity));
        ValidateFinite(wettedArea, nameof(wettedArea));

        if (velocity < 0)
            throw new ArgumentOutOfRangeException(
                nameof(velocity),
                velocity,
                "Скорость V не может быть отрицательной.");

        if (wettedArea < 0)
            throw new ArgumentOutOfRangeException(
                nameof(wettedArea),
                wettedArea,
                "Площадь omega не может быть отрицательной.");

        var result = velocity * wettedArea;
        if (double.IsNaN(result) || double.IsInfinity(result))
            throw new ArgumentOutOfRangeException(
                nameof(velocity),
                velocity,
                "Произведение V * omega должно быть конечным числом.");

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
