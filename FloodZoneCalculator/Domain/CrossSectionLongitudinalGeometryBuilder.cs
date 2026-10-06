using System;
using System.Globalization;
using FloodZoneDb.Client;

namespace FloodZoneCalculator.Domain;

public sealed class CrossSectionLongitudinalGeometryBuilder
{
    public CrossSectionLongitudinalGeometry Build(
        AdjacentCrossSectionPair pair,
        CrossSectionRecord firstSection,
        CrossSectionRecord secondSection)
    {
        if (pair == null)
            throw new ArgumentNullException(nameof(pair));
        if (firstSection == null)
            throw new ArgumentNullException(nameof(firstSection));
        if (secondSection == null)
            throw new ArgumentNullException(nameof(secondSection));

        if (firstSection.Number != pair.First.Number)
        {
            throw new ArgumentException(
                "Первый CrossSectionRecord не соответствует первому элементу пары.",
                nameof(firstSection));
        }

        if (secondSection.Number != pair.Second.Number)
        {
            throw new ArgumentException(
                "Второй CrossSectionRecord не соответствует второму элементу пары.",
                nameof(secondSection));
        }

        return new CrossSectionLongitudinalGeometry(
            pair,
            ParseBottomLevel(firstSection.BottomLevelZb, nameof(firstSection)),
            ParseBottomLevel(secondSection.BottomLevelZb, nameof(secondSection)));
    }

    private static double ParseBottomLevel(string value, string parameterName)
    {
        if (!double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var result) ||
            double.IsNaN(result) ||
            double.IsInfinity(result))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "BottomLevelZb должен быть конечным числом.");
        }

        return result;
    }
}
