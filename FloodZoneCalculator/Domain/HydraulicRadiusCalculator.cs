using System;

namespace FloodZoneCalculator.Domain;

public sealed class HydraulicRadiusCalculator
{
    public double Calculate(double omega, double chi)
    {
        ValidateFinite(omega, nameof(omega));
        ValidateFinite(chi, nameof(chi));

        if (omega < 0)
            throw new ArgumentOutOfRangeException(nameof(omega), omega, "Площадь omega не может быть отрицательной.");

        if (chi < 0)
            throw new ArgumentOutOfRangeException(nameof(chi), chi, "Периметр chi не может быть отрицательным.");

        if (chi == 0)
            throw new DivideByZeroException("Гидравлический радиус не определяется при chi = 0.");

        return omega / chi;
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(parameterName, value, "Значение должно быть конечным числом.");
    }
}
