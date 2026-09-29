using FloodZoneCalculator.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FloodZoneCalculator.Application
{     public sealed class FloodCalculator : IFloodCalculator
    {
        public const double VkzmDepth = 1.0;
        public const double VkzmVelocity = 0.7;
        public const double CriticalDepth = 3.0;
        public const double FloodDepth = 0.0;

        private const int Steps = 200;
        private readonly IInterpolator _interp;

        public FloodCalculator(IInterpolator interpolator)
        {
            _interp = interpolator ?? throw new ArgumentNullException(nameof(interpolator));
        }

        public void ComputeAll(IEnumerable<CrossSection> sections)
        {
            foreach (var cs in sections) Compute(cs);
        }

        public void Compute(CrossSection cs)
        {
            var dL = cs.GetPoints(Bank.Left, IzodataType.Depth);
            var dR = cs.GetPoints(Bank.Right, IzodataType.Depth);
            var vL = cs.GetPoints(Bank.Left, IzodataType.Velocity);
            var vR = cs.GetPoints(Bank.Right, IzodataType.Velocity);

            var allDepth = dL.Concat(dR).Select(p => p.Value).ToList();
            var allVel = vL.Concat(vR).Select(p => p.Value).ToList();

            cs.MaxDepth = allDepth.Count > 0 ? allDepth.Max() : 0;
            cs.MaxVelocity = allVel.Count > 0 ? allVel.Max() : 0;
            cs.MaxLevel = cs.WaterLevel + cs.MaxDepth;

            cs.FloodWidthLeft = _interp.DistanceAtValue(dL, FloodDepth) ?? 0;
            cs.FloodWidthRight = _interp.DistanceAtValue(dR, FloodDepth) ?? 0;

            cs.FloodAreaLeft = AreaAbove(dL, FloodDepth);
            cs.FloodAreaRight = AreaAbove(dR, FloodDepth);

            var rhL = _interp.DistanceAtValue(dL, VkzmDepth);
            var rvL = _interp.DistanceAtValue(vL, VkzmVelocity);
            cs.VkzmWidthLeft = Math.Max(rhL ?? 0, rvL ?? 0);

            var rhR = _interp.DistanceAtValue(dR, VkzmDepth);
            var rvR = _interp.DistanceAtValue(vR, VkzmVelocity);
            cs.VkzmWidthRight = Math.Max(rhR ?? 0, rvR ?? 0);

            cs.VkzmAreaLeft = VkzmArea(dL, vL, VkzmDepth, VkzmVelocity);
            cs.VkzmAreaRight = VkzmArea(dR, vR, VkzmDepth, VkzmVelocity);

            cs.CriticalWidthLeft = _interp.DistanceAtValue(dL, CriticalDepth) ?? 0;
            cs.CriticalWidthRight = _interp.DistanceAtValue(dR, CriticalDepth) ?? 0;
        }

        private double AreaAbove(IReadOnlyList<IzodataPoint> pts, double threshold)
        {
            if (pts is null || pts.Count == 0) return 0;
            double rMax = pts.Max(p => p.Distance);
            if (rMax <= 0) return 0;
            double area = 0, prev = 0, step = rMax / Steps;
            for (int i = 0; i <= Steps; i++)
            {
                double r = step * i;
                var v = _interp.ValueAtDistance(pts, r);
                double cur = v.HasValue && v.Value >= threshold ? v.Value : 0;
                if (i > 0) area += 0.5 * (prev + cur) * step;
                prev = cur;
            }
            return area;
        }

        private double VkzmArea(IReadOnlyList<IzodataPoint> depth,
                                IReadOnlyList<IzodataPoint> vel,
                                double thrH, double thrV)
        {
            double rMax = 0;
            foreach (var p in depth) rMax = Math.Max(rMax, p.Distance);
            foreach (var p in vel) rMax = Math.Max(rMax, p.Distance);
            if (rMax <= 0) return 0;
            double area = 0, prev = 0, step = rMax / Steps;
            for (int i = 0; i <= Steps; i++)
            {
                double r = step * i;
                var h = _interp.ValueAtDistance(depth, r);
                var v = _interp.ValueAtDistance(vel, r);
                bool ok = h.HasValue && h.Value >= thrH && v.HasValue && v.Value >= thrV;
                double cur = ok ? h.Value : 0;
                if (i > 0) area += 0.5 * (prev + cur) * step;
                prev = cur;
            }
            return area;
        }
    }
}
