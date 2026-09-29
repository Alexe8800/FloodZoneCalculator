using FloodZoneCalculator.Domain;
using System.Collections.Generic;

namespace FloodZoneCalculator.Application
{
    public interface IInterpolator
    {
        double? ValueAtDistance(IReadOnlyList<IzodataPoint> points, double distance);
        double? DistanceAtValue(IReadOnlyList<IzodataPoint> points, double value);
    }
}
