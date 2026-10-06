using System;
using System.Collections.Generic;
using System.Linq;

namespace FloodZoneCalculator.Domain
{
    public sealed class HydraulicProfileRenderModel
    {
        public HydraulicProfileRenderModel(CrossSectionGeometry geometry)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            Geometry = geometry;
            Points = geometry.OrderedPoints.ToList();
        }

        public CrossSectionGeometry Geometry { get; }
        public IReadOnlyList<ProfilePoint> Points { get; }
        public IReadOnlyList<ProfilePoint> LeftPoints =>
            Points.Where(point => point.Side == "Left").ToList();
        public IReadOnlyList<ProfilePoint> RightPoints =>
            Points.Where(point => point.Side == "Right").ToList();
        public IReadOnlyList<ProfilePoint> CenterPoints =>
            Points.Where(point => point.Side == "Center").ToList();
    }
}
