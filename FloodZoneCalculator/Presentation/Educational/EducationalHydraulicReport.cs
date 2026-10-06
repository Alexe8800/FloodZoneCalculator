using System;
using System.Linq;
using System.Net;
using System.Text;

namespace FloodZoneCalculator.Presentation.Educational;

public sealed class EducationalHydraulicReport
{
    public const string Disclaimer =
        "Учебный синтетический расчёт. Правило Ri/DistanceM → X и Depth → DepthH введено специально для учебного проекта. Результаты не являются восстановлением официальной методики.";

    public string GenerateHtml(EducationalHydraulicProfileData profile)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        var hydraulic = profile.HydraulicResult;
        var comparison = profile.VelocityComparison;
        var html = new StringBuilder();
        html.AppendLine("<!doctype html><html lang=\"ru\"><head><meta charset=\"utf-8\">");
        html.AppendLine("<title>Учебный гидравлический расчёт</title>");
        html.AppendLine("<style>body{font-family:Segoe UI,Arial;margin:24px;color:#202a35}table{border-collapse:collapse;margin:12px 0 24px}th,td{border:1px solid #aab4be;padding:6px 10px;text-align:left}th{background:#edf2f7}.notice{padding:12px;background:#fff3cd;border-left:4px solid #d39e00}</style></head><body>");
        html.AppendLine("<p class=\"notice\">" + Encode(Disclaimer) + "</p>");
        html.AppendLine("<h1>Учебный гидравлический расчёт</h1>");
        html.AppendLine("<p>Створ №" + hydraulic.CrossSectionNumber + "; сторона: "
            + Encode(hydraulic.Side.ToString()) + ".</p>");
        html.AppendLine("<h2>Параметры и гидравлические результаты</h2>");
        html.AppendLine("<table><tbody>");
        AddRow(html, "Omega, м²", hydraulic.Omega);
        AddRow(html, "Chi, м", hydraulic.Chi);
        AddRow(html, "Гидравлический радиус R, м", hydraulic.HydraulicRadius);
        AddRow(html, "B, учебный параметр, м", hydraulic.B);
        AddRow(html, "g, учебный параметр, м/с²", hydraulic.Gravity);
        AddRow(html, "nu, учебный параметр, м²/с", hydraulic.KinematicViscosity);
        AddRow(html, "h, учебный параметр, м", hydraulic.EducationalWaterDepth);
        AddRow(html, "If, учебный параметр", hydraulic.HydraulicSlope);
        AddRow(html, "vStar, м/с", hydraulic.ShearVelocity);
        AddRow(html, "C", hydraulic.ChezyCoefficient);
        AddRow(html, "V_calculated, м/с", hydraulic.CalculatedVelocity);
        AddRow(html, "Q, м³/с", hydraulic.Discharge);
        html.AppendLine("</tbody></table>");

        html.AppendLine("<h2>Сравнение скоростей</h2>");
        html.AppendLine("<p>Сравнение является учебным и не является верификацией методики.</p>");
        html.AppendLine("<table><tbody>");
        AddRow(html, "Средняя наблюдаемая скорость, м/с", comparison.ObservedVelocityMean);
        AddRow(html, "Разница, м/с", comparison.Difference);
        AddRow(html, "Относительное расхождение", comparison.RelativeDifference);
        AddRow(html, "Отношение", comparison.Ratio);
        html.AppendLine("</tbody></table>");

        html.AppendLine("<h2>Исходные данные и профиль</h2>");
        html.AppendLine("<p>X — Ri / DistanceM, учебная координата; DepthH — учебная глубина, не отметка Z. "
            + "Линии на графике соединяют только исходные точки для отображения.</p>");
        html.AppendLine("<table><thead><tr><th>Тип</th><th>X</th><th>Значение</th><th>SourceIsodatId</th></tr></thead><tbody>");
        foreach (var point in profile.DepthPoints)
            html.AppendLine("<tr><td>Depth</td><td>" + Number((double)point.X) + "</td><td>"
                + Number((double)point.DepthH) + "</td><td>" + point.SourceIsodatId + "</td></tr>");
        foreach (var point in profile.VelocityPoints)
            html.AppendLine("<tr><td>ObservedVelocity</td><td>" + Number((double)point.X) + "</td><td>"
                + Number((double)point.ObservedVelocity!.Value) + "</td><td>" + point.SourceIsodatId + "</td></tr>");
        html.AppendLine("</tbody></table>");
        html.AppendLine("<p>EducationalWaterDepth = " + Number(hydraulic.EducationalWaterDepth) + " м</p>");
        html.AppendLine("</body></html>");
        return html.ToString();
    }

    public string GenerateText(EducationalHydraulicProfileData profile)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        var hydraulic = profile.HydraulicResult;
        var comparison = profile.VelocityComparison;
        var text = new StringBuilder();
        text.AppendLine(Disclaimer);
        text.AppendLine();
        text.AppendLine($"Створ №{hydraulic.CrossSectionNumber}; сторона: {hydraulic.Side}.");
        text.AppendLine("Учебный гидравлический расчёт");
        AppendTextValue(text, "Omega, м²", hydraulic.Omega);
        AppendTextValue(text, "Chi, м", hydraulic.Chi);
        AppendTextValue(text, "HydraulicRadius, м", hydraulic.HydraulicRadius);
        AppendTextValue(text, "B, учебный параметр, м", hydraulic.B);
        AppendTextValue(text, "g, учебный параметр, м/с²", hydraulic.Gravity);
        AppendTextValue(text, "nu, учебный параметр, м²/с", hydraulic.KinematicViscosity);
        AppendTextValue(text, "h, учебный параметр, м", hydraulic.EducationalWaterDepth);
        AppendTextValue(text, "If, учебный параметр", hydraulic.HydraulicSlope);
        AppendTextValue(text, "vStar, м/с", hydraulic.ShearVelocity);
        AppendTextValue(text, "C", hydraulic.ChezyCoefficient);
        AppendTextValue(text, "V_calculated, м/с", hydraulic.CalculatedVelocity);
        AppendTextValue(text, "Q, м³/с", hydraulic.Discharge);

        text.AppendLine();
        text.AppendLine("Сравнение является учебным и не является верификацией методики.");
        AppendTextValue(text, "Средняя наблюдаемая скорость, м/с", comparison.ObservedVelocityMean);
        AppendTextValue(text, "Разница, м/с", comparison.Difference);
        AppendTextValue(text, "Относительное расхождение", comparison.RelativeDifference);
        AppendTextValue(text, "Отношение", comparison.Ratio);

        text.AppendLine();
        text.AppendLine("Тип\tX — Ri / DistanceM\tЗначение\tSourceIsodatId");
        foreach (var point in profile.DepthPoints)
            text.AppendLine("Depth\t" + Number((double)point.X) + "\t"
                + Number((double)point.DepthH) + "\t" + point.SourceIsodatId);
        foreach (var point in profile.VelocityPoints)
            text.AppendLine("ObservedVelocity\t" + Number((double)point.X) + "\t"
                + Number((double)point.ObservedVelocity!.Value) + "\t" + point.SourceIsodatId);
        return text.ToString();
    }

    public string GenerateSummaryHtml(EducationalHydraulicSummaryModel summary)
    {
        if (summary == null)
            throw new ArgumentNullException(nameof(summary));

        var html = new StringBuilder();
        html.AppendLine("<!doctype html><html lang=\"ru\"><head><meta charset=\"utf-8\">");
        html.AppendLine("<title>Учебный синтетический гидравлический расчёт — сводка</title>");
        html.AppendLine("<style>body{font-family:Segoe UI,Arial;margin:24px;color:#202a35}table{border-collapse:collapse;margin:12px 0 24px}th,td{border:1px solid #aab4be;padding:6px 10px;text-align:left}th{background:#edf2f7}.notice{padding:12px;background:#fff3cd;border-left:4px solid #d39e00}</style></head><body>");
        html.AppendLine("<h1>Учебный синтетический гидравлический расчёт — сводка</h1>");
        html.AppendLine("<p class=\"notice\">" + Encode(Disclaimer) + "</p>");
        html.AppendLine("<p>Результаты получены с использованием специально введённых учебных правил и параметров. Они не являются восстановлением официальной методики.</p>");
        html.AppendLine("<h2>Сводная таблица</h2>");
        html.AppendLine("<table><thead><tr><th>CrossSection</th><th>Side</th><th>DepthCount</th><th>VelocityCount</th><th>Omega</th><th>Chi</th><th>R</th><th>B</th><th>vStar</th><th>C</th><th>V_calculated</th><th>Q</th><th>ObservedVelocityMean</th><th>Difference</th><th>RelativeDifference</th><th>Ratio</th></tr></thead><tbody>");
        foreach (var row in summary.Rows)
        {
            html.Append("<tr><td>").Append(row.CrossSectionNumber).Append("</td><td>")
                .Append(Encode(row.Side.ToString())).Append("</td><td>")
                .Append(row.DepthCount).Append("</td><td>").Append(row.VelocityCount).Append("</td>");
            AppendCell(html, row.Omega);
            AppendCell(html, row.Chi);
            AppendCell(html, row.HydraulicRadius);
            AppendCell(html, row.B);
            AppendCell(html, row.ShearVelocity);
            AppendCell(html, row.ChezyCoefficient);
            AppendCell(html, row.CalculatedVelocity);
            AppendCell(html, row.Discharge);
            AppendCell(html, row.ObservedVelocityMean);
            AppendCell(html, row.Difference);
            AppendCell(html, row.RelativeDifference);
            AppendCell(html, row.Ratio);
            html.AppendLine("</tr>");
        }
        html.AppendLine("</tbody></table>");
        html.AppendLine("<h2>Сравнение скоростей</h2>");
        html.AppendLine("<table><thead><tr><th>Профиль</th><th>ObservedVelocityMean</th><th>V_calculated</th><th>Difference</th><th>RelativeDifference</th><th>Ratio</th></tr></thead><tbody>");
        foreach (var row in summary.Rows)
        {
            html.Append("<tr><td>").Append(row.CrossSectionNumber).Append(' ')
                .Append(Encode(row.Side.ToString())).Append("</td>");
            AppendCell(html, row.ObservedVelocityMean);
            AppendCell(html, row.CalculatedVelocity);
            AppendCell(html, row.Difference);
            AppendCell(html, row.RelativeDifference);
            AppendCell(html, row.Ratio);
            html.AppendLine("</tr>");
        }
        html.AppendLine("</tbody></table>");
        AppendSummaryParametersHtml(html, summary);
        html.AppendLine("<h2>Профили</h2><ul>");
        foreach (var row in summary.Rows)
            html.Append("<li>").Append(row.CrossSectionNumber).Append(' ')
                .Append(Encode(row.Side.ToString())).AppendLine("</li>");
        html.AppendLine("</ul></body></html>");
        return html.ToString();
    }

    public string GenerateSummaryText(EducationalHydraulicSummaryModel summary)
    {
        if (summary == null)
            throw new ArgumentNullException(nameof(summary));

        var text = new StringBuilder();
        text.AppendLine("Учебный синтетический гидравлический расчёт — сводка");
        text.AppendLine(Disclaimer);
        text.AppendLine("Результаты получены с использованием специально введённых учебных правил и параметров. Они не являются восстановлением официальной методики.");
        text.AppendLine();
        text.AppendLine("CrossSection\tSide\tDepthCount\tVelocityCount\tOmega\tChi\tR\tB\tvStar\tC\tV_calculated\tQ\tObservedVelocityMean\tDifference\tRelativeDifference\tRatio");
        foreach (var row in summary.Rows)
        {
            text.Append(row.CrossSectionNumber).Append('\t')
                .Append(row.Side).Append('\t')
                .Append(row.DepthCount).Append('\t')
                .Append(row.VelocityCount).Append('\t')
                .Append(Number(row.Omega)).Append('\t')
                .Append(Number(row.Chi)).Append('\t')
                .Append(Number(row.HydraulicRadius)).Append('\t')
                .Append(Number(row.B)).Append('\t')
                .Append(Number(row.ShearVelocity)).Append('\t')
                .Append(Number(row.ChezyCoefficient)).Append('\t')
                .Append(Number(row.CalculatedVelocity)).Append('\t')
                .Append(Number(row.Discharge)).Append('\t')
                .Append(Number(row.ObservedVelocityMean)).Append('\t')
                .Append(Number(row.Difference)).Append('\t')
                .Append(Number(row.RelativeDifference)).Append('\t')
                .AppendLine(Number(row.Ratio));
        }

        text.AppendLine();
        text.AppendLine("Параметры (взяты из готовых результатов профиля):");
        text.AppendLine("Профиль\tg\t nu\t h (учебный параметр)\t If\t EducationalWaterDepth, м");
        foreach (var row in summary.Rows)
        {
            var result = row.Profile.HydraulicResult;
            text.Append(row.CrossSectionNumber).Append(' ').Append(row.Side).Append('\t')
                .Append(Number(result.Gravity)).Append('\t')
                .Append(Number(result.KinematicViscosity)).Append('\t')
                .Append(Number(result.EducationalWaterDepth)).Append('\t')
                .Append(Number(result.HydraulicSlope)).Append('\t')
                .AppendLine(Number(result.EducationalWaterDepth));
        }
        text.AppendLine();
        text.AppendLine("Профили: " + string.Join(", ", summary.Rows.Select(row =>
            row.CrossSectionNumber + " " + row.Side)));
        return text.ToString();
    }

    private static void AppendSummaryParametersHtml(
        StringBuilder html,
        EducationalHydraulicSummaryModel summary)
    {
        html.AppendLine("<h2>Учебные параметры</h2><table><thead><tr><th>Профиль</th><th>g</th><th>nu</th><th>h (учебный параметр)</th><th>If</th><th>EducationalWaterDepth, м</th></tr></thead><tbody>");
        foreach (var row in summary.Rows)
        {
            var result = row.Profile.HydraulicResult;
            html.Append("<tr><td>").Append(row.CrossSectionNumber).Append(' ')
                .Append(Encode(row.Side.ToString())).Append("</td>");
            AppendCell(html, result.Gravity);
            AppendCell(html, result.KinematicViscosity);
            AppendCell(html, result.EducationalWaterDepth);
            AppendCell(html, result.HydraulicSlope);
            AppendCell(html, result.EducationalWaterDepth);
            html.AppendLine("</tr>");
        }
        html.AppendLine("</tbody></table>");
    }

    private static void AppendCell(StringBuilder html, double value) =>
        html.Append("<td>").Append(Number(value)).Append("</td>");

    private static void AppendCell(StringBuilder html, double? value) =>
        html.Append("<td>").Append(value.HasValue ? Number(value.Value) : "не определено").Append("</td>");

    private static void AppendTextValue(StringBuilder text, string caption, double value) =>
        text.AppendLine(caption + ": " + Number(value));

    private static void AppendTextValue(StringBuilder text, string caption, double? value) =>
        text.AppendLine(caption + ": " + (value.HasValue ? Number(value.Value) : "не определено"));

    private static void AddRow(StringBuilder html, string caption, double value) =>
        html.AppendLine("<tr><th>" + Encode(caption) + "</th><td>" + Number(value) + "</td></tr>");

    private static void AddRow(StringBuilder html, string caption, double? value) =>
        html.AppendLine("<tr><th>" + Encode(caption) + "</th><td>"
            + (value.HasValue ? Number(value.Value) : "не определено") + "</td></tr>");

    private static string Number(double value) =>
        value.ToString("G10", System.Globalization.CultureInfo.InvariantCulture);

    private static string Number(double? value) =>
        value.HasValue ? Number(value.Value) : "не определено";

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
