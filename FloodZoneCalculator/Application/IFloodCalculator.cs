using FloodZoneCalculator.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FloodZoneCalculator.Application
{
    public interface IFloodCalculator
    {
        void Compute(CrossSection section);
        void ComputeAll(IEnumerable<CrossSection> sections);
    }
}
