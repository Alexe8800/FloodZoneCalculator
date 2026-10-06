using System;

namespace FloodZoneCalculator.Domain
{
    public sealed class ProfilePoint
    {
        public ProfilePoint(double distanceM, double elevationM, string side, string pointType, int pointNumber)
        {
            DistanceM = distanceM;
            ElevationM = elevationM;
            Side = side;
            PointType = pointType;
            PointNumber = pointNumber;
        }

        public double DistanceM { get; }
        public double ElevationM { get; }
        public string Side { get; }
        public string PointType { get; }
        public int PointNumber { get; }
    }
}
