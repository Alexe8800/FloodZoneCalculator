using FloodZoneCalculator.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FloodZoneCalculator.Application
{
    public sealed class LinearInterpolator : IInterpolator
    {
        public double? ValueAtDistance(IReadOnlyList<IzodataPoint> points, double distance)
        {
            if (points is null || points.Count == 0) return null;
            var pts = points.OrderBy(p => p.Distance).ToList();
            if (distance <= pts[0].Distance) return pts[0].Value;
            if (distance >= pts[pts.Count - 1].Distance) return pts[pts.Count - 1].Value;
            for (int i = 0; i < pts.Count - 1; i++)
                if (pts[i].Distance <= distance && distance <= pts[i + 1].Distance)
                    return Formulas.LinearInterp(distance,
                        pts[i].Distance, pts[i].Value,
                        pts[i + 1].Distance, pts[i + 1].Value);
            return null;
        }

        public double? DistanceAtValue(IReadOnlyList<IzodataPoint> points, double value)
        {
            if (points is null || points.Count == 0) return null;
            var pts = points.OrderBy(p => p.Value).ToList();
            if (value <= pts[0].Value) return pts[0].Distance;
            if (value >= pts[pts.Count - 1].Value) return pts[pts.Count - 1].Distance;
            for (int i = 0; i < pts.Count - 1; i++)
                if (pts[i].Value <= value && value <= pts[i + 1].Value)
                    return Formulas.LinearInterp(value,
                        pts[i].Value, pts[i].Distance,
                        pts[i + 1].Value, pts[i + 1].Distance);
            return null;
        }
    }
}
