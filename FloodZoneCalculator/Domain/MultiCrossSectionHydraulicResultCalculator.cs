using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FloodZoneDb.Client;

namespace FloodZoneCalculator.Domain;

public sealed class MultiCrossSectionHydraulicResultCalculator
{
    private readonly CrossSectionGeometryBuilder geometryBuilder = new();
    private readonly CrossSectionHydraulicResultCalculator hydraulicCalculator = new();

    public MultiCrossSectionHydraulicResult Calculate(
        IEnumerable<CrossSectionRecord> sections,
        HydraulicCalculationInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        if (sections == null)
            throw new ArgumentNullException(nameof(sections));

        var orderedSections = sections
            .Select(section => new
            {
                Section = section ?? throw new ArgumentException(
                    "Последовательность не может содержать null-створ.",
                    nameof(sections)),
                Distance = ParseDistance(section)
            })
            .ToArray();

        if (orderedSections.Length == 0)
            throw new ArgumentException(
                "Последовательность створов не может быть пустой.",
                nameof(sections));

        var duplicate = orderedSections
            .GroupBy(item => item.Distance)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
            throw new ArgumentException(
                "Продольная последовательность содержит дубликат DistanceFromHydroUnitM.",
                nameof(sections));

        var results = orderedSections
            .OrderBy(item => item.Distance)
            .Select(item =>
            {
                var geometry = geometryBuilder.Build(item.Section);
                var hydraulicResult = hydraulicCalculator.Calculate(
                    new CrossSectionHydraulicContext(geometry, input));

                return new CrossSectionHydraulicResultItem(
                    item.Section.Number,
                    item.Distance,
                    hydraulicResult);
            })
            .ToArray();

        return new MultiCrossSectionHydraulicResult(results);
    }

    public MultiCrossSectionHydraulicResult Calculate(
        IEnumerable<CrossSectionRecord> sections,
        double waterLevel,
        double gravity,
        double kinematicViscosity,
        double depth,
        double width,
        double hydraulicSlope)
    {
        return Calculate(
            sections,
            new HydraulicCalculationInput(
                waterLevel,
                gravity,
                kinematicViscosity,
                depth,
                width,
                hydraulicSlope));
    }

    private static double ParseDistance(CrossSectionRecord section)
    {
        if (!double.TryParse(
                section.DistanceFromHydroUnitM,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var distance) ||
            double.IsNaN(distance) ||
            double.IsInfinity(distance))
        {
            throw new ArgumentOutOfRangeException(
                nameof(section.DistanceFromHydroUnitM),
                section.DistanceFromHydroUnitM,
                "DistanceFromHydroUnitM должен быть конечным числом.");
        }

        if (distance < 0)
            throw new ArgumentOutOfRangeException(
                nameof(section.DistanceFromHydroUnitM),
                section.DistanceFromHydroUnitM,
                "DistanceFromHydroUnitM не может быть отрицательным.");

        return distance;
    }
}
