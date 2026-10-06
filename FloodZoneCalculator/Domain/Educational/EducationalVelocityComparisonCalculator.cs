using System;
using System.Collections.Generic;
using System.Linq;
using FloodZoneDb.Client;

namespace FloodZoneCalculator.Domain;

public sealed class EducationalVelocityComparisonCalculator
{
    public EducationalVelocityComparisonResult Calculate(
        EducationalFullHydraulicResult hydraulicResult,
        IReadOnlyCollection<IsodatHydraulicPoint> isodatPoints)
    {
        if (hydraulicResult == null)
            throw new ArgumentNullException(nameof(hydraulicResult));
        return Calculate(
            hydraulicResult.CrossSectionNumber,
            hydraulicResult.Side,
            hydraulicResult.CalculatedVelocity,
            isodatPoints);
    }

    public EducationalVelocityComparisonResult Calculate(
        int crossSectionNumber,
        IsodatSide side,
        double calculatedVelocity,
        IReadOnlyCollection<IsodatHydraulicPoint> isodatPoints)
    {
        if (crossSectionNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(crossSectionNumber));
        if (!Enum.IsDefined(typeof(IsodatSide), side))
            throw new ArgumentOutOfRangeException(nameof(side));
        if (isodatPoints == null)
            throw new ArgumentNullException(nameof(isodatPoints));
        if (isodatPoints.Any(point => point == null))
            throw new ArgumentException("Набор содержит пустую учебную точку.", nameof(isodatPoints));
        if (!IsFinite(calculatedVelocity))
            throw new ArgumentOutOfRangeException(
                nameof(calculatedVelocity),
                "V_calculated должно быть конечным числом.");
        if (calculatedVelocity < 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(calculatedVelocity),
                calculatedVelocity,
                "V_calculated не может быть отрицательной скоростью в используемой учебной модели.");

        var observedVelocities = isodatPoints
            .Where(point =>
                point.CrossSectionNumber == crossSectionNumber
                && point.Side == side
                && point.ObservedVelocity.HasValue)
            .Select(point => (double)point.ObservedVelocity!.Value)
            .ToArray();
        if (observedVelocities.Length == 0)
            throw new InvalidOperationException(
                $"Для створа {crossSectionNumber}, {side} отсутствуют Velocity-наблюдения.");

        var observedMean = observedVelocities.Average();
        var difference = observedMean - calculatedVelocity;
        var absoluteDifference = Math.Abs(difference);
        if (!IsFinite(observedMean)
            || !IsFinite(difference)
            || !IsFinite(absoluteDifference))
            throw new ArithmeticException("Результат сравнения скоростей вышел за допустимый числовой диапазон.");

        double? relativeDifference = null;
        double? ratio = null;
        if (calculatedVelocity != 0.0)
        {
            relativeDifference = absoluteDifference / Math.Abs(calculatedVelocity);
            ratio = observedMean / calculatedVelocity;
            if (!IsFinite(relativeDifference.Value) || !IsFinite(ratio.Value))
                throw new ArithmeticException("Относительные показатели сравнения должны быть конечными числами.");
        }

        return new EducationalVelocityComparisonResult(
            crossSectionNumber,
            side,
            observedVelocities.Length,
            observedVelocities.Min(),
            observedVelocities.Max(),
            observedMean,
            calculatedVelocity,
            difference,
            absoluteDifference,
            relativeDifference,
            ratio);
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}
