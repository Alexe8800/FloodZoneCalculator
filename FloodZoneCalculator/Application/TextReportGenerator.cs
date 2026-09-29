using FloodZoneCalculator.Domain;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FloodZoneCalculator.Application
{
    public sealed class TextReportGenerator : IReportGenerator
    {
        private static readonly CultureInfo Ci = CultureInfo.GetCultureInfo("ru-RU");

        public string Generate(IReadOnlyList<CrossSection> sections, string sourceFile)
        {
            var sb = new StringBuilder();
            sb.AppendLine(new string('=', 78));
            sb.AppendLine("ОТЧЁТ О РАСЧЁТЕ ЗОНЫ ЗАТОПЛЕНИЯ");
            sb.AppendLine(new string('=', 78));
            sb.AppendLine("Дата расчёта: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
            if (!string.IsNullOrEmpty(sourceFile))
                sb.AppendLine("Исходный файл: " + sourceFile);
            sb.AppendLine("Критерий ВКЗМ: глубина >= " + FloodCalculator.VkzmDepth + " м И скорость >= " + FloodCalculator.VkzmVelocity + " м/с");
            sb.AppendLine("Критерий КР: глубина >= " + FloodCalculator.CriticalDepth + " м");
            sb.AppendLine();

            double totalFloodArea = 0, totalVkzmArea = 0;
            double floodL = 0, floodR = 0, vkzmL = 0, vkzmR = 0;

            foreach (var cs in sections)
            {
                sb.AppendLine(new string('-', 78));
                sb.AppendLine("СТВОР №" + cs.Number);
                sb.AppendLine(new string('-', 78));
                sb.AppendLine("  Отметка уреза воды Zб: ............... " + Fmt(cs.WaterLevel) + " м");
                sb.AppendLine("  Максимальная глубина: ............... " + Fmt(cs.MaxDepth) + " м");
                sb.AppendLine("  Максимальная отметка затопления: ..... " + Fmt(cs.MaxLevel) + " м");
                sb.AppendLine("  Максимальная скорость течения: ....... " + Fmt(cs.MaxVelocity) + " м/с");
                sb.AppendLine("  Ширина зоны затопления (лев.): ....... " + Fmt(cs.FloodWidthLeft) + " м");
                sb.AppendLine("  Ширина зоны затопления (прав.): ...... " + Fmt(cs.FloodWidthRight) + " м");
                sb.AppendLine("  Площадь зоны затопления (лев.): ...... " + Fmt(cs.FloodAreaLeft) + " м2");
                sb.AppendLine("  Площадь зоны затопления (прав.): ..... " + Fmt(cs.FloodAreaRight) + " м2");
                sb.AppendLine("  Ширина ВКЗМ (лев.): .................. " + Fmt(cs.VkzmWidthLeft) + " м");
                sb.AppendLine("  Ширина ВКЗМ (прав.): ................. " + Fmt(cs.VkzmWidthRight) + " м");
                sb.AppendLine("  Площадь ВКЗМ (лев.): ................. " + Fmt(cs.VkzmAreaLeft) + " м2");
                sb.AppendLine("  Площадь ВКЗМ (прав.): ................ " + Fmt(cs.VkzmAreaRight) + " м2");
                sb.AppendLine("  Ширина КР-зоны (лев.): ............... " + Fmt(cs.CriticalWidthLeft) + " м");
                sb.AppendLine("  Ширина КР-зоны (прав.): .............. " + Fmt(cs.CriticalWidthRight) + " м");
                sb.AppendLine();

                AppendTable(sb, "Изодата глубины - левый берег", cs.GetPoints(Bank.Left, IzodataType.Depth));
                AppendTable(sb, "Изодата глубины - правый берег", cs.GetPoints(Bank.Right, IzodataType.Depth));
                AppendTable(sb, "Изодата скорости - левый берег", cs.GetPoints(Bank.Left, IzodataType.Velocity));
                AppendTable(sb, "Изодата скорости - правый берег", cs.GetPoints(Bank.Right, IzodataType.Velocity));

                floodL += cs.FloodAreaLeft;
                floodR += cs.FloodAreaRight;
                vkzmL += cs.VkzmAreaLeft;
                vkzmR += cs.VkzmAreaRight;
                totalFloodArea += cs.FloodAreaLeft + cs.FloodAreaRight;
                totalVkzmArea += cs.VkzmAreaLeft + cs.VkzmAreaRight;
            }

            sb.AppendLine(new string('=', 78));
            sb.AppendLine("ИТОГО ПО ВСЕМ СТВОРАМ");
            sb.AppendLine(new string('=', 78));
            sb.AppendLine("  Площадь затопления (лев.):  " + Fmt(floodL) + " м2 (" + (floodL / 1e6).ToString("F6", Ci) + " км2)");
            sb.AppendLine("  Площадь затопления (прав.): " + Fmt(floodR) + " м2 (" + (floodR / 1e6).ToString("F6", Ci) + " км2)");
            sb.AppendLine("  Общая площадь затопления: ... " + Fmt(totalFloodArea) + " м2 (" + (totalFloodArea / 1e6).ToString("F6", Ci) + " км2)");
            sb.AppendLine("  Площадь ВКЗМ (лев.): ........ " + Fmt(vkzmL) + " м2");
            sb.AppendLine("  Площадь ВКЗМ (прав.): ....... " + Fmt(vkzmR) + " м2");
            sb.AppendLine("  Общая площадь ВКЗМ: ......... " + Fmt(totalVkzmArea) + " м2 (" + (totalVkzmArea / 1e6).ToString("F6", Ci) + " км2)");
            sb.AppendLine();
            sb.AppendLine(new string('=', 78));
            return sb.ToString();
        }

        private static void AppendTable(StringBuilder sb, string title, IReadOnlyList<IzodataPoint> points)
        {
            sb.AppendLine("  " + title + ":");
            sb.AppendLine("      Значение |   Удаление");
            foreach (var p in points.OrderByDescending(p => p.Value))
                sb.AppendLine("  " + Fmt(p.Value).PadLeft(12) + " | " + Fmt(p.Distance).PadLeft(12));
            sb.AppendLine();
        }

        private static string Fmt(double v) => v.ToString("F3", Ci);
    }
}
