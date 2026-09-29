using System.Collections.Generic;

namespace FloodZoneCalculator.Domain
{
    public sealed class CrossSection
    {
        public int Number { get; set; }
        public double DistanceFromDam { get; set; }
        public double WaterLevel { get; set; }
        public double Depth { get; set; }
        public double Width { get; set; }
        public double Velocity { get; set; }
        public double GeomCoeff { get; set; } = 1.0;

        public Dictionary<IzodataType, List<IzodataPoint>> Left { get; set; } = new();
        public Dictionary<IzodataType, List<IzodataPoint>> Right { get; set; } = new();

        public double MaxDepth { get; set; }
        public double MaxVelocity { get; set; }
        public double MaxLevel { get; set; }

        public double FloodWidthLeft { get; set; }
        public double FloodWidthRight { get; set; }
        public double FloodAreaLeft { get; set; }
        public double FloodAreaRight { get; set; }

        public double VkzmWidthLeft { get; set; }
        public double VkzmWidthRight { get; set; }
        public double VkzmAreaLeft { get; set; }
        public double VkzmAreaRight { get; set; }

        public double CriticalWidthLeft { get; set; }
        public double CriticalWidthRight { get; set; }

        public IReadOnlyList<IzodataPoint> GetPoints(Bank bank, IzodataType type)
        {
            var d = bank == Bank.Left ? Left : Right;
            return d.TryGetValue(type, out var list)
                ? list
                : (IReadOnlyList<IzodataPoint>)System.Array.Empty<IzodataPoint>();
        }
    }
}
