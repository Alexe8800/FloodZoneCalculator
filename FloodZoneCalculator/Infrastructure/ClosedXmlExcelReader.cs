using ClosedXML.Excel;
using FloodZoneCalculator.Application;
using FloodZoneCalculator.Domain;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FloodZoneCalculator.Infrastructure
{
    public sealed class ClosedXmlExcelReader : IExcelReader
    {
        public IReadOnlyList<CrossSection> Read(string path)
        {
            using var wb = new XLWorkbook(path);
            var left = ParseSheet(wb.Worksheet("Левый берег"));
            var right = ParseSheet(wb.Worksheet("Правый берег"));

            var numbers = left[IzodataType.Depth].Keys
                .Concat(right[IzodataType.Depth].Keys)
                .Distinct().OrderBy(n => n).ToList();

            var result = new List<CrossSection>();
            foreach (var n in numbers)
            {
                var cs = new CrossSection { Number = n };
                foreach (IzodataType t in System.Enum.GetValues(typeof(IzodataType)))
                {
                    if (left[t].TryGetValue(n, out var lv)) cs.Left[t] = lv;
                    if (right[t].TryGetValue(n, out var rv)) cs.Right[t] = rv;
                }
                result.Add(cs);
            }
            return result;
        }

        private static Dictionary<IzodataType, Dictionary<int, List<IzodataPoint>>>
            ParseSheet(IXLWorksheet ws) => new()
            {
                [IzodataType.Depth] = ReadBlock(ws, 1, 2, 3),
                [IzodataType.Velocity] = ReadBlock(ws, 5, 6, 7),
                [IzodataType.DryingCalculated] = ReadBlock(ws, 9, 10, 11),
                [IzodataType.DryingFactual] = ReadBlock(ws, 13, 14, 15),
            };

        private static Dictionary<int, List<IzodataPoint>> ReadBlock(
            IXLWorksheet ws, int colN, int colVal, int colDist, int startRow = 5)
        {
            var result = new Dictionary<int, List<IzodataPoint>>();
            int row = startRow;
            int maxRow = ws.LastRowUsed()?.RowNumber() ?? startRow;

            while (row <= maxRow)
            {
                var nCell = ws.Cell(row, colN).Value;
                if (nCell.IsBlank) { row++; continue; }
                if (!int.TryParse(nCell.ToString(), out int number)) break;

                double value = ToDouble(ws.Cell(row, colVal).Value);
                double dist = ToDouble(ws.Cell(row, colDist).Value);

                if (!result.TryGetValue(number, out var list))
                    result[number] = list = new List<IzodataPoint>();
                list.Add(new IzodataPoint(value, dist));
                row++;
            }
            return result;
        }

        private static double ToDouble(XLCellValue cell)
        {
            if (cell.IsBlank) return 0;
            if (cell.IsNumber) return cell.GetNumber();
            var s = cell.ToString().Trim().Replace(",", ".").Replace("\u00A0", "");
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }
    }
}
