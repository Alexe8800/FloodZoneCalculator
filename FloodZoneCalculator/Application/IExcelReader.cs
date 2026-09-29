using FloodZoneCalculator.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FloodZoneCalculator.Application
{
    public interface IExcelReader
    {
        IReadOnlyList<CrossSection> Read(string path);
    }
}
