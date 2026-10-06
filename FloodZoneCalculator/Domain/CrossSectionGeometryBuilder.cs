using FloodZoneDb.Client;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FloodZoneCalculator.Domain
{
    public sealed class CrossSectionGeometryBuilder
    {
        public CrossSectionGeometry Build(CrossSectionRecord section)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));

            var warnings = new List<string>();
            var left = BuildSide(section, "Left", warnings);
            var center = BuildSide(section, "Center", warnings);
            var right = BuildSide(section, "Right", warnings);
            var ordered = left.Concat(center).Concat(right)
                .OrderBy(point => point.DistanceM)
                .ThenBy(point => point.PointNumber)
                .ToList();
            var segments = ordered
                .Zip(ordered.Skip(1), (start, end) => new ProfileSegment(start, end))
                .ToList();

            if (ordered.Count == 0) warnings.Add("Нет подтверждённых точек профиля.");

            return new CrossSectionGeometry(
                section.Number,
                Parse(section.DistanceFromHydroUnitM, "Lc", warnings),
                Parse(section.BottomLevelZb, "Zб", warnings),
                Parse(section.DepthHb, "Hб", warnings),
                Parse(section.WidthBb, "Bб", warnings),
                Parse(section.VelocityVb, "Vб", warnings),
                Parse(section.Kgm, "Kgm", warnings),
                left,
                center,
                right,
                ordered,
                segments,
                warnings);
        }

        private static List<ProfilePoint> BuildSide(
            CrossSectionRecord section,
            string side,
            ICollection<string> warnings)
        {
            var points = new List<ProfilePoint>();
            foreach (var point in section.BankPoints
                         .Where(point => string.Equals(point.Side, side, StringComparison.OrdinalIgnoreCase)))
            {
                if (!IsSupportedType(point.PointType) ||
                    (string.Equals(side, "Center", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(point.PointType, "Bottom", StringComparison.OrdinalIgnoreCase)))
                {
                    warnings.Add("Пропущена недопустимая точка на стороне " + side + ".");
                    continue;
                }

                if (!TryParse(point.DistanceM, out var distance) ||
                    !TryParse(point.ElevationM, out var elevation))
                {
                    warnings.Add("Пропущена точка №" + point.PointNumber +
                        " на стороне " + side + ": расстояние или отметка нечисловые.");
                    continue;
                }

                points.Add(new ProfilePoint(distance, elevation, side, point.PointType, point.PointNumber));
            }

            if (points.GroupBy(point => point.PointNumber).Any(group => group.Count() > 1))
                warnings.Add("На стороне " + side + " есть повторяющиеся номера точек.");

            return points
                .OrderBy(point => point.DistanceM)
                .ThenBy(point => point.PointNumber)
                .ToList();
        }

        private static bool IsSupportedType(string pointType)
        {
            return string.Equals(pointType, "Bank", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(pointType, "Horizontal", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(pointType, "ChannelBank", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(pointType, "Bottom", StringComparison.OrdinalIgnoreCase);
        }

        private static double Parse(string value, string name, ICollection<string> warnings)
        {
            if (TryParse(value, out var result)) return result;
            if (!string.IsNullOrWhiteSpace(value))
                warnings.Add("Параметр " + name + " не удалось преобразовать в число.");
            return 0;
        }

        private static bool TryParse(string value, out double result)
        {
            return double.TryParse(
                (value ?? "").Trim().Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result);
        }
    }
}
