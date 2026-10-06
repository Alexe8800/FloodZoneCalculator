using System;
using System.Collections.Generic;
using System.Linq;

namespace FloodZoneCalculator.Domain
{
    public sealed class CrossSectionGeometry
    {
        public CrossSectionGeometry(
            int number,
            double distanceFromHydroUnitM,
            double bottomLevelZb,
            double depthHb,
            double widthBb,
            double velocityVb,
            double kgm,
            IReadOnlyList<ProfilePoint> leftPoints,
            IReadOnlyList<ProfilePoint> centerPoints,
            IReadOnlyList<ProfilePoint> rightPoints,
            IReadOnlyList<ProfilePoint> orderedPoints,
            IReadOnlyList<ProfileSegment> segments,
            IReadOnlyList<string> warnings)
        {
            Number = number;
            DistanceFromHydroUnitM = distanceFromHydroUnitM;
            BottomLevelZb = bottomLevelZb;
            DepthHb = depthHb;
            WidthBb = widthBb;
            VelocityVb = velocityVb;
            Kgm = kgm;
            LeftPoints = leftPoints;
            CenterPoints = centerPoints;
            RightPoints = rightPoints;
            OrderedPoints = orderedPoints;
            Segments = segments;
            Warnings = warnings;
        }

        public int Number { get; }
        public double DistanceFromHydroUnitM { get; }
        public double BottomLevelZb { get; }
        public double DepthHb { get; }
        public double WidthBb { get; }
        public double VelocityVb { get; }
        public double Kgm { get; }
        public IReadOnlyList<ProfilePoint> LeftPoints { get; }
        public IReadOnlyList<ProfilePoint> CenterPoints { get; }
        public IReadOnlyList<ProfilePoint> RightPoints { get; }
        public IReadOnlyList<ProfilePoint> OrderedPoints { get; }
        public IReadOnlyList<ProfileSegment> Segments { get; }
        public IReadOnlyList<string> Warnings { get; }
        public IReadOnlyList<ProfilePoint> Points =>
            OrderedPoints;
    }

    public sealed class ProfileSegment
    {
        public ProfileSegment(ProfilePoint start, ProfilePoint end)
        {
            Start = start;
            End = end;
        }

        public ProfilePoint Start { get; }
        public ProfilePoint End { get; }
    }
}
