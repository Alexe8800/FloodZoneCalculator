#nullable enable

using FloodZoneCalculator.Domain;
using FloodZoneCalculator.Infrastructure;
using FloodZoneCalculator.Presentation.ReportExport;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation
{
    // =====================================================================================
    // MainForm.Export.cs — формирование отчёта (Word/PDF) из данных текущего расчёта.
    //
    //   BuildExportCatalog   — реестр графиков (заголовок + функция отрисовки).
    //   BuildReportSections  — реестр автоматических разделов (таблицы/расчёты/выводы).
    //   BuildTitlePage       — титульный лист (объект, реквизиты, год).
    //   OnExportChartsClick  — диалог выбора → сборка отчёта → Word/PDF.
    //
    // Разделы НЕ пишутся вручную — они извлекаются из уже заполненных таблиц и данных
    // объекта/расчёта. Пользователь лишь отмечает, что включить в отчёт.
    // =====================================================================================
    public sealed partial class CalculationForm
    {
        private const int ExportChartWidth = 1400;
        private const int ExportChartHeight = 900;

        // ------------------------------ Графики ------------------------------
        private List<(string Title, Action<Graphics, Rectangle> Render)> BuildExportCatalog()
        {
            var catalog = new List<(string, Action<Graphics, Rectangle>)>
            {
                ("Баланс водопользования (круговая диаграмма)", (g, r) => DrawBalanceChart(g, r)),
                ("Кривая обеспеченности расходов", (g, r) => DrawFlowChart(g, r, false)),
                ("Кривая обеспеченности расходов (логарифмическая шкала)", (g, r) => DrawFlowChart(g, r, true)),
                ("Помесячная выработка электроэнергии", (g, r) => DrawGenerationChart(g, r)),
            };

            for (var i = 0; i < AnalyticsTitles.Length; i++)
            {
                var index = i; // локальная копия для замыкания
                catalog.Add((AnalyticsTitles[i], (g, r) => DrawAnalyticsChart(g, r, index)));
            }
            return catalog;
        }

        private static Bitmap RenderChartToBitmap(Action<Graphics, Rectangle> render)
        {
            var bitmap = new Bitmap(ExportChartWidth, ExportChartHeight);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                render(g, new Rectangle(0, 0, ExportChartWidth, ExportChartHeight));
            }
            return bitmap;
        }

        // ------------------------------ Разделы (таблицы/расчёты) ------------------------------
        // Каждый раздел — фабрика, возвращающая элемент отчёта или null, если данных нет.
        private List<(string Title, Func<ReportElement?> Build)> BuildReportSections()
        {
            return new List<(string, Func<ReportElement?>)>
            {
                ("Сведения об объекте", BuildObjectInfoSection),
                ("Основные результаты расчёта", () => GridTable("Основные результаты расчёта", _mainResultsGrid)),
                ("Характеристики створов", BuildSectionsTable),
                ("Кривая обеспеченности расходов", () => GridTable("Кривая обеспеченности расходов", _flowProvisionGrid)),
                ("Баланс водопользования", () => GridTable("Баланс водопользования", _balanceGrid)),
                ("Помесячная выработка электроэнергии", () => GridTable("Помесячная выработка электроэнергии", _generationGrid)),
                ("Эксплуатационные характеристики по периодам", () => GridTable("Эксплуатационные характеристики по периодам", _operationGrid)),
                ("Выводы (текст с вкладки «Выводы»)", BuildConclusionsSection),
            };
        }

        // Снимок DataGridView в таблицу отчёта; пустые строки отбрасываются.
        private static ReportTable? GridTable(string title, DataGridView? grid)
        {
            if (grid == null || grid.IsDisposed) return null;
            var headers = grid.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText).ToArray();
            if (headers.Length == 0) return null;

            var rows = new List<string[]>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                var values = new string[headers.Length];
                var any = false;
                for (var c = 0; c < headers.Length; c++)
                {
                    var value = Convert.ToString(row.Cells[c].Value, CultureInfo.CurrentCulture) ?? "";
                    values[c] = value;
                    if (!string.IsNullOrWhiteSpace(value)) any = true;
                }
                if (any) rows.Add(values);
            }
            return rows.Count == 0 ? null : new ReportTable(title, headers, rows);
        }

        private ReportElement? BuildObjectInfoSection()
        {
            var rows = new List<string[]>();
            void Add(string label, string? value)
            {
                var v = (value ?? "").Trim();
                if (v.Length > 0 && v != "—") rows.Add(new[] { label, v });
            }

            Add("Наименование", _currentObject?.Name);
            Add("Тип сооружения", _currentObject?.TypeName);
            Add("Река", _objectProfile.River);
            Add("Собственник / балансодержатель", FirstNonEmpty(_objectProfile.Owner, _objectProfile.BalanceAffiliation));
            Add("Дата ввода в эксплуатацию", _objectProfile.CommissioningDate);
            Add("Класс сооружения", FirstNonEmpty(_objectProfile.GtsClass, _objectProfile.HydroClass));
            Add("Тип плотины", _objectProfile.DamType);
            Add("Длина плотины, м", _objectProfile.DamLengthM);
            Add("Максимальная высота плотины, м", _objectProfile.DamMaxHeightM);
            Add("НПУ (уровень верхнего бьефа), м", _objectProfile.NormalHeadwaterM);
            Add("МПР (уровень нижнего бьефа), м", _objectProfile.MinWaterLevelM);
            Add("Полный объём водохранилища, млн м³", _objectProfile.ReservoirVolumeMlnM3);
            Add("Полезный объём водохранилища, млн м³", _objectProfile.ReservoirUsefulVolumeMlnM3);
            Add("Количество агрегатов, шт.", _objectProfile.UnitsCount);
            Add("Установленная мощность, МВт", _objectProfile.DesignPowerMw);
            Add("Пропускная способность водосброса, м³/с", _objectProfile.SpillwayCapacityM3s);
            Add("КПД гидроузла, %", _objectProfile.EfficiencyPct);
            Add("Среднегодовая выработка, млн кВт·ч", _objectProfile.AverageAnnualGenerationMkwh);
            Add("Коэффициент надёжности", _objectProfile.ReliabilityCoefficient);

            return rows.Count == 0
                ? null
                : new ReportTable("Сведения об объекте", new[] { "Показатель", "Значение" }, rows);
        }

        private ReportElement? BuildSectionsTable()
        {
            if (_sections == null || _sections.Count == 0) return null;
            var headers = new[]
            {
                "№ створа", "Расстояние от ГУ, м", "Макс. глубина, м", "Ширина, м",
                "Макс. скорость, м/с", "Площадь затопления (лев.), м²", "Площадь затопления (прав.), м²",
            };
            var rows = _sections.Select(s => new[]
            {
                s.Number.ToString(CultureInfo.CurrentCulture),
                Num(s.DistanceFromDam),
                Num(s.MaxDepth),
                Num(s.Width > 0 ? s.Width : s.FloodWidthLeft + s.FloodWidthRight),
                Num(s.MaxVelocity),
                Num(s.FloodAreaLeft),
                Num(s.FloodAreaRight),
            }).ToList();
            return new ReportTable("Характеристики створов", headers, rows);
        }

        private ReportElement? BuildConclusionsSection()
        {
            if (string.IsNullOrWhiteSpace(_reportConclusions)) return null;
            var lines = _reportConclusions
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Select(l => l.TrimEnd())
                .ToList();
            return new ReportText("Выводы", lines);
        }

        // ------------------------------ Титульный лист ------------------------------
        private ReportTitlePage BuildTitlePage()
        {
            var year = DateTime.Now.Year.ToString(CultureInfo.CurrentCulture);
            var page = new ReportTitlePage
            {
                Organization = FirstNonEmpty(_objectProfile.Owner, _objectProfile.BalanceAffiliation,
                    _objectProfile.ResponsiblePerson),
                ObjectName = _currentObject?.Name ?? "Гидротехническое сооружение",
                DocumentTitle = "Отчёт по расчёту зоны затопления и подтопления",
                Subtitle = BuildSubtitle(),
                Location = FirstNonEmpty(_objectProfile.SubjectRf, _objectProfile.District),
                Year = year,
                DateText = "Дата формирования: " + DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture),
            };

            AddFact(page, "Тип сооружения", _currentObject?.TypeName);
            AddFact(page, "Река", _objectProfile.River);
            AddFact(page, "Дата ввода в эксплуатацию", _objectProfile.CommissioningDate);
            AddFact(page, "Тип плотины", _objectProfile.DamType);
            AddFact(page, "НПУ (УВБ), м", _objectProfile.NormalHeadwaterM);
            AddFact(page, "Установленная мощность, МВт", _objectProfile.DesignPowerMw);
            AddFact(page, "Количество агрегатов, шт.", _objectProfile.UnitsCount);
            return page;
        }

        private string BuildSubtitle()
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(_objectProfile.River))
                parts.Add("река " + _objectProfile.River.Trim());
            var type = FirstNonEmpty(_objectProfile.GtsClass, _objectProfile.HydroClass, _currentObject?.TypeName);
            if (!string.IsNullOrWhiteSpace(type))
                parts.Add(type.Trim());
            return string.Join("  ·  ", parts);
        }

        private static void AddFact(ReportTitlePage page, string label, string? value)
        {
            var v = (value ?? "").Trim();
            if (v.Length > 0 && v != "—")
                page.Facts.Add(new KeyValuePair<string, string>(label, v));
        }

        // ------------------------------ Сборка и экспорт ------------------------------
        private void OnExportChartsClick(object? sender, EventArgs e)
        {
            if (_sections.Count == 0 && string.IsNullOrWhiteSpace(_currentObject?.Name))
            {
                MessageBox.Show(this, "Сначала импортируйте Excel-файл и выполните расчёт.",
                    "Нет данных", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var charts = BuildExportCatalog();
            var sections = BuildReportSections();

            using (var dialog = new ChartExportForm(
                charts.Select(c => c.Title), sections.Select(s => s.Title)))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                var selectedSections = new HashSet<string>(dialog.SelectedSectionTitles);
                var selectedCharts = new HashSet<string>(dialog.SelectedChartTitles);

                var content = new ReportContent { TitlePage = BuildTitlePage() };

                try
                {
                    // Разделы (таблицы/расчёты/выводы) — первыми, графики — следом.
                    foreach (var section in sections)
                    {
                        if (!selectedSections.Contains(section.Title)) continue;
                        var element = section.Build();
                        if (element != null) content.Elements.Add(element);
                    }

                    foreach (var chart in charts)
                    {
                        if (!selectedCharts.Contains(chart.Title)) continue;
                        content.Elements.Add(new ReportChart(chart.Title, RenderChartToBitmap(chart.Render)));
                    }

                    if (content.Elements.Count == 0)
                    {
                        MessageBox.Show(this, "Выберите хотя бы один график или раздел для отчёта.",
                            "Пустой отчёт", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (dialog.ExportPdf)
                    {
                        if (PdfReportExporter.Export(this, content))
                            SetStatus("PDF-отчёт отправлен на печать («Microsoft Print to PDF»).");
                        else
                            SetStatus("Экспорт в PDF отменён.");
                    }
                    else
                    {
                        using var save = new SaveFileDialog
                        {
                            Filter = "Документ Word (*.docx)|*.docx",
                            FileName = "flood_report.docx",
                        };
                        if (save.ShowDialog(this) == DialogResult.OK)
                        {
                            WordReportExporter.Export(save.FileName, content);
                            SetStatus("Word-отчёт сохранён: " + save.FileName);
                            MessageBox.Show(this, "Отчёт Word сохранён:\n" + save.FileName, "Готово",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Ошибка формирования отчёта.", ex);
                    MessageBox.Show(this, ex.Message, "Ошибка экспорта",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    foreach (var chart in content.Elements.OfType<ReportChart>())
                        chart.Image.Dispose();
                }
            }
        }

        // ------------------------------ Мелкие помощники ------------------------------
        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? "";

        private static string Num(double value) =>
            value > 0 ? value.ToString("0.##", CultureInfo.CurrentCulture) : "—";
    }
}
