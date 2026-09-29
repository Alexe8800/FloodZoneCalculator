using FloodZoneCalculator.Domain;
using System.Collections.Generic;


namespace FloodZoneCalculator.Application
{
    public interface IReportGenerator
    {
        string Generate(IReadOnlyList<CrossSection> sections, string sourceFile);
    }
}
