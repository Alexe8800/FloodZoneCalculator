using System;
using System.Collections.Generic;
using System.Linq;
using FloodZoneDb.Client;

namespace FloodZoneCalculator.Domain;

public sealed class EducationalFullHydraulicCalculator
{
    private readonly ShearVelocityCalculator _shearVelocityCalculator;
    private readonly DischargeCalculator _dischargeCalculator;

    public EducationalFullHydraulicCalculator()
        : this(
            new ShearVelocityCalculator(),
            new DischargeCalculator())
    {
    }

    public EducationalFullHydraulicCalculator(
        ShearVelocityCalculator shearVelocityCalculator,
        DischargeCalculator dischargeCalculator)
    {
        _shearVelocityCalculator = shearVelocityCalculator
            ?? throw new ArgumentNullException(nameof(shearVelocityCalculator));
        _dischargeCalculator = dischargeCalculator
            ?? throw new ArgumentNullException(nameof(dischargeCalculator));
    }

    public EducationalFullHydraulicResult Calculate(
        EducationalDepthProfile profile,
        EducationalDepthHydraulicResult geometry,
        IReadOnlyCollection<IsodatHydraulicPoint> isodatPoints,
        double gravity,
        double kinematicViscosity,
        double educationalWaterDepth,
        double hydraulicSlope)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));
        if (isodatPoints == null)
            throw new ArgumentNullException(nameof(isodatPoints));
        if (isodatPoints.Any(point => point == null))
            throw new ArgumentException("Набор содержит пустую учебную точку.", nameof(isodatPoints));

        ValidatePositiveFinite(gravity, nameof(gravity));
        ValidatePositiveFinite(kinematicViscosity, nameof(kinematicViscosity));
        ValidatePositiveFinite(educationalWaterDepth, nameof(educationalWaterDepth));
        ValidatePositiveFinite(hydraulicSlope, nameof(hydraulicSlope));
        if (geometry.CrossSectionNumber != profile.CrossSectionNumber
            || geometry.Side != profile.Side
            || geometry.PointCount != profile.Points.Count)
            throw new ArgumentException(
                "Результат геометрии 2.41 не соответствует Depth-профилю.",
                nameof(geometry));
        if (geometry.EducationalWaterDepth != educationalWaterDepth)
            throw new ArgumentException(
                "Уровень глубины должен совпадать с использованным в расчёте геометрии 2.41.",
                nameof(geometry));

        var minX = profile.Points.Min(point => (double)point.X);
        var maxX = profile.Points.Max(point => (double)point.X);
        var width = maxX - minX;
        if (!IsFinite(width) || width <= 0.0)
            throw new InvalidOperationException(
                $"Учебная ширина B должна быть положительной для створа {profile.CrossSectionNumber}, {profile.Side}.");

        var observedVelocities = isodatPoints
            .Where(point =>
                point.CrossSectionNumber == profile.CrossSectionNumber
                && point.Side == profile.Side
                && point.ObservedVelocity.HasValue)
            .Select(point => (double)point.ObservedVelocity!.Value)
            .ToArray();
        if (observedVelocities.Length == 0)
            throw new InvalidOperationException(
                $"Для створа {profile.CrossSectionNumber}, {profile.Side} отсутствуют исходные Velocity-наблюдения.");

        var radius = geometry.EducationalHydraulicRadius;
        var shearVelocity = _shearVelocityCalculator.Calculate(radius, hydraulicSlope, gravity);
        var chezyCoefficient = GrishaninChezyCalculator.Calculate(
            gravity,
            kinematicViscosity,
            educationalWaterDepth,
            width);
        var calculatedVelocity = ChezyVelocityCalculator.Calculate(
            chezyCoefficient,
            radius,
            hydraulicSlope);
        var discharge = _dischargeCalculator.Calculate(calculatedVelocity, geometry.Omega);

        return new EducationalFullHydraulicResult(
            profile.CrossSectionNumber,
            profile.Side,
            geometry.PointCount,
            minX,
            maxX,
            width,
            geometry,
            gravity,
            kinematicViscosity,
            educationalWaterDepth,
            hydraulicSlope,
            shearVelocity,
            chezyCoefficient,
            calculatedVelocity,
            discharge,
            observedVelocities.Min(),
            observedVelocities.Max());
    }

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (!IsFinite(value) || value <= 0.0)
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Учебный параметр должен быть конечным числом больше нуля.");
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}
