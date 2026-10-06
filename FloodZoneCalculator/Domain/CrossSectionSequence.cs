using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FloodZoneDb.Client;

namespace FloodZoneCalculator.Domain;

public sealed class CrossSectionSequence
{
    public CrossSectionSequence(IEnumerable<CrossSectionRecord> sections)
    {
        if (sections == null)
            throw new ArgumentNullException(nameof(sections));

        var items = sections
            .Select(section =>
            {
                if (section == null)
                    throw new ArgumentNullException(nameof(sections));

                return new CrossSectionSequenceItem(
                    section.Number,
                    ParseDistance(section.DistanceFromHydroUnitM));
            })
            .ToArray();

        if (items.Length == 0)
            throw new ArgumentException(
                "Последовательность створов не может быть пустой.",
                nameof(sections));

        var duplicate = items
            .GroupBy(item => item.DistanceFromHydroUnitM)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
            throw new ArgumentException(
                "Продольная последовательность содержит дубликат DistanceFromHydroUnitM.",
                nameof(sections));

        Items = Array.AsReadOnly(
            items.OrderBy(item => item.DistanceFromHydroUnitM).ToArray());
    }

    public IReadOnlyList<CrossSectionSequenceItem> Items { get; }

    private static double ParseDistance(string value)
    {
        if (!double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var distance) ||
            double.IsNaN(distance) ||
            double.IsInfinity(distance))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "DistanceFromHydroUnitM должен быть конечным числом.");
        }

        if (distance < 0)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "DistanceFromHydroUnitM не может быть отрицательным.");

        return distance;
    }
}

public sealed class CrossSectionSequenceItem
{
    public CrossSectionSequenceItem(int number, double distanceFromHydroUnitM)
    {
        if (double.IsNaN(distanceFromHydroUnitM) ||
            double.IsInfinity(distanceFromHydroUnitM))
        {
            throw new ArgumentOutOfRangeException(
                nameof(distanceFromHydroUnitM),
                distanceFromHydroUnitM,
                "DistanceFromHydroUnitM должен быть конечным числом.");
        }

        if (distanceFromHydroUnitM < 0)
            throw new ArgumentOutOfRangeException(
                nameof(distanceFromHydroUnitM),
                distanceFromHydroUnitM,
                "DistanceFromHydroUnitM не может быть отрицательным.");

        Number = number;
        DistanceFromHydroUnitM = distanceFromHydroUnitM;
    }

    public int Number { get; }
    public double DistanceFromHydroUnitM { get; }
}
