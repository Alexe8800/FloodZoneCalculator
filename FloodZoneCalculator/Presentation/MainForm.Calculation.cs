#nullable enable

using FloodZoneCalculator.Domain;
using FloodZoneCalculator.Infrastructure;
using FloodZoneDb.Client;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation
{
    // =====================================================================================
    // MainForm.Calculation.cs — сквозная логика расчёта и обмена с БД/файлами.
    //
    //   OnOpenClick / LoadSections         — импорт Excel с изодатами и построение дерева створов.
    //   OnComputeClick / ComputeAndRefresh — вызов расчёта по всем створам и обновление UI.
    //   LoadSavedCalculationAsync          — восстановление сохранённого расчёта (JSON створов).
    //   SaveCalculationAsync               — сохранение створов + изодат/баланса/выработки в БД.
    //   SaveSourceDataAsync                — валидация и сохранение исходных данных в профиль.
    //   ValidateSourceInputs / ValidateBalance / TryParseDecimal — проверки ввода.
    //   OnReportClick / OnPrintReportClick — экспорт и печать текстового отчёта.
    // =====================================================================================
    public sealed partial class CalculationForm
    {
        // ---- Импорт Excel ----

        // Открывает файл изодат, строит створы, сразу считает и молча сохраняет расчёт в БД.
        private async void OnOpenClick(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*",
                Title = "Импорт изодат для расчёта"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                LoadSections(_reader.Read(dialog.FileName), dialog.FileName);
                ComputeAndRefresh();
                SetStatus("Excel импортирован, расчёт выполнен.");
                await SaveCalculationAsync(silent: true);
                SetStatus("Excel импортирован, расчёт выполнен и сохранён в БД.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка импорта Excel: " + dialog.FileName, ex);
                MessageBox.Show(this, "Не удалось импортировать Excel:\r\n" + ex.Message,
                    "Ошибка импорта", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Кладёт створы в память и перестраивает дерево створов, выбирая первый.
        private void LoadSections(IReadOnlyList<CrossSection> sections, string sourceFile)
        {
            _sections = sections.ToList();
            _sourceFile = sourceFile;
            _sectionsTree.Nodes.Clear();
            var root = new TreeNode("Гидротехнические сооружения");
            foreach (var cs in _sections)
                root.Nodes.Add(new TreeNode("Створ №" + cs.Number) { Tag = cs });
            _sectionsTree.Nodes.Add(root);
            root.Expand();
            if (root.Nodes.Count > 0)
                _sectionsTree.SelectedNode = root.Nodes[0];
            SetStatus("Загружено створов: " + _sections.Count + ".");
        }

        // ---- Расчёт ----

        // Кнопка «Рассчитать»: требует наличия створов, иначе подсказка про импорт Excel.
        private void OnComputeClick(object? sender, EventArgs e)
        {
            if (_sections.Count == 0)
            {
                MessageBox.Show(this, "Сначала откройте Excel-файл.", "Нет данных",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                AppLogger.Info("Начат расчёт. Створов: " + _sections.Count + ".");
                ComputeAndRefresh();
                SetStatus("Расчёт выполнен.");
                AppLogger.Info("Расчёт завершён успешно.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка расчёта.", ex);
                MessageBox.Show(this, ex.Message, "Ошибка расчёта",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Ядро: считает все створы, обновляет таблицы/графики и заново выбирает первый створ,
        // чтобы детали и профиль перерисовались по свежим результатам.
        private void ComputeAndRefresh()
        {
            _calc.ComputeAll(_sections);
            UpdateInstructionTables();
            if (_sectionsTree.Nodes.Count > 0 && _sectionsTree.Nodes[0].Nodes.Count > 0)
            {
                var first = _sectionsTree.Nodes[0].Nodes[0];
                _sectionsTree.SelectedNode = first;
                OnSectionChanged(_sectionsTree, new TreeViewEventArgs(first));
            }
            else
                UpdateMainResults();
        }

        // ---- Сохранение / загрузка сохранённого расчёта ----

        // Восстанавливает ранее сохранённый расчёт (JSON створов) из БД и пересчитывает его.
        private async Task LoadSavedCalculationAsync()
        {
            try
            {
                var saved = await _database.LoadCalculationAsync(_objectId);
                if (saved == null)
                {
                    SetStatus("Для объекта нет сохранённого расчёта. Импортируйте Excel.");
                    return;
                }
                var sections = JsonSerializer.Deserialize<List<CrossSection>>(saved.SectionsJson);
                if (sections == null || sections.Count == 0)
                    throw new InvalidOperationException("Сохранённый расчёт не содержит створов.");
                var pointCount = sections.Sum(section =>
                    section.Left.Values.Sum(points => points.Count) +
                    section.Right.Values.Sum(points => points.Count));
                if (pointCount == 0)
                    throw new InvalidOperationException(
                        "В сохранённом расчёте отсутствуют исходные точки. " +
                        "Импортируйте Excel повторно и сохраните расчёт заново.");
                AppLogger.Info("Получен сохранённый JSON расчёта: " + saved.SectionsJson.Length +
                    " символов, створов: " + sections.Count + ", точек: " + pointCount + ".");
                LoadSections(sections, saved.SourceFile);
                ComputeAndRefresh();
                _reportConclusions = saved.ReportConclusions;
                SyncConclusionsBox();
                SetStatus("Сохранённый расчёт загружен из БД и пересчитан.");
                AppLogger.Info("Сохранённый расчёт загружен. Объект ID: " + _objectId + ".");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка загрузки расчёта из БД.", ex);
                SetStatus("Не удалось загрузить расчёт: " + ex.Message);
            }
        }

        // Сохраняет створы (JSON) плюс изодаты расхода, баланс и выработку в БД выбранного объекта.
        // silent=true используется при авто-сохранении после импорта (без модального окна).
        // Собирает введённые значения текстовых полей и таблиц (эксплуатация, помесячная выработка) в _objectProfile.
        private void CollectProfileFromUi()
        {
            _objectProfile.DamType = InputValue("DamType");
            _objectProfile.DamLengthM = InputValue("DamLengthM");
            _objectProfile.DamMaxHeightM = InputValue("DamMaxHeightM");
            _objectProfile.SpillwayCapacityM3s = InputValue("SpillwayCapacityM3s");
            _objectProfile.SpillwayOpeningsCount = InputValue("SpillwayOpeningsCount");
            _objectProfile.UnitsCount = InputValue("UnitsCount");
            _objectProfile.DesignPowerMw = InputValue("DesignPowerMw");
            _objectProfile.NormalHeadwaterM = InputValue("NormalHeadwaterM");
            _objectProfile.MinWaterLevelM = InputValue("MinWaterLevelM");
            _objectProfile.ReservoirVolumeMlnM3 = InputValue("ReservoirFullVolume");
            _objectProfile.CatchmentAreaKm2 = InputValue("CatchmentArea");
            _objectProfile.AverageAnnualRunoffKm3 = InputValue("AvgRunoff");
            _objectProfile.MaxFlowM3s = InputValue("MaxFlow");
            _objectProfile.MinFlowM3s = InputValue("MinFlow");
            _objectProfile.ProvisionPct = InputValue("Provision");
            _objectProfile.DeadVolumeLevelM = InputValue("DeadVolumeLevel");
            _objectProfile.ReservoirUsefulVolumeMlnM3 = InputValue("ReservoirUsefulVolume");
            _objectProfile.RoughnessCoeffN = InputValue("RoughnessCoeff");
            _objectProfile.WaterTemperatureC = InputValue("WaterTemperature");
            _objectProfile.EfficiencyPct = InputValue("EfficiencyPct");
            _objectProfile.ReliabilityCoefficient = InputValue("ReliabilityCoefficient");

            // Эксплуатационные характеристики по периодам.
            _objectProfile.OperationPeriods = new List<OperationPeriodEntry>();
            if (_operationGrid != null)
                foreach (DataGridViewRow row in _operationGrid.Rows)
                    _objectProfile.OperationPeriods.Add(new OperationPeriodEntry
                    {
                        Period = CellText(row, 0), QAvg = CellText(row, 1), QMax = CellText(row, 2),
                        QMin = CellText(row, 3), Hwl = CellText(row, 4), Lwl = CellText(row, 5),
                    });

            // Помесячная выработка: месяц + расход/напор/выработка.
            _objectProfile.MonthlyGeneration = new List<MonthlyGenerationEntry>();
            if (_generationGrid != null)
                foreach (DataGridViewRow row in _generationGrid.Rows)
                    _objectProfile.MonthlyGeneration.Add(new MonthlyGenerationEntry
                    {
                        Month = row.Index + 1,
                        Flow = CellText(row, 1), Head = CellText(row, 2), Generation = CellText(row, 3),
                    });
        }

        private static string CellText(DataGridViewRow row, int index) =>
            Convert.ToString(row.Cells[index].Value) ?? "";

        // Сохраняет створы (JSON) плюс изодаты расхода/баланса/выработку в БД и всегда сохраняет
        // введённые данные профиля (текстовые поля, эксплуатация, помесячная выработка).
        // silent=true используется при авто-сохранении после импорта (без модального окна).
        private async Task SaveCalculationAsync(bool silent = false)
        {
            try
            {
                // 1) Профиль + таблицы сохраняются всегда — даже без импорта створов.
                CollectProfileFromUi();
                await _database.SaveObjectProfileAsync(_objectId, _objectProfile);

                // 2) Створы и связанные расчётные таблицы — только если есть импорт.
                if (_sections.Count == 0)
                {
                    SetStatus("Данные профиля сохранены. Створы не импортированы — расчёт не сохранён.");
                    if (!silent)
                        MessageBox.Show(this,
                            "Введённые данные и таблицы сохранены в профиль объекта.\r\n" +
                            "Для сохранения створов импортируйте Excel и выполните расчёт.",
                            "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var sectionsJson = JsonSerializer.Serialize(_sections);
                AppLogger.Info("Подготовлен JSON расчёта: " + sectionsJson.Length +
                    " символов, створов: " + _sections.Count + ".");
                var provision = new List<(decimal P, decimal Q)>();
                if (_flowProvisionGrid != null)
                    foreach (DataGridViewRow row in _flowProvisionGrid.Rows)
                        if (ReadCellNumber(row.Cells[1].Value) > 0)
                            provision.Add((
                                (decimal)ReadCellNumber(row.Cells[0].Value),
                                (decimal)ReadCellNumber(row.Cells[1].Value)));
                var balance = new List<(string Cat, decimal Share)>();
                if (_balanceGrid != null)
                    foreach (DataGridViewRow row in _balanceGrid.Rows)
                        if (ReadCellNumber(row.Cells[1].Value) > 0)
                            balance.Add((row.Cells[0].Value?.ToString() ?? "",
                                (decimal)ReadCellNumber(row.Cells[1].Value)));
                var generation = new List<(short Month, decimal Gen)>();
                if (_generationGrid != null)
                    foreach (DataGridViewRow row in _generationGrid.Rows)
                        if (ReadCellNumber(row.Cells[3].Value) > 0)
                            generation.Add(((short)(row.Index + 1), (decimal)ReadCellNumber(row.Cells[3].Value)));

                await _database.SaveCalculationAsync(_objectId,
                    sectionsJson, _sourceFile, provision, balance, generation, _reportConclusions);
                SetStatus("Расчёт сохранён в БД выбранного объекта.");
                if (!silent)
                    MessageBox.Show(this, "Расчёт сохранён. При следующем входе он загрузится автоматически.",
                        "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка сохранения расчёта в БД.", ex);
                MessageBox.Show(this, "Не удалось сохранить расчёт:\r\n" + GetExceptionMessage(ex),
                    "Ошибка БД", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Разворачивает цепочку InnerException в читаемое сообщение (причины идут по порядку).
        private static string GetExceptionMessage(Exception exception)
        {
            var messages = new List<string>();
            for (var current = exception; current != null; current = current.InnerException)
                messages.Add(current.Message);
            return string.Join("\r\nПричина: ", messages);
        }

        // ---- Сохранение исходных данных в профиль объекта ----

        // Проверяет вводы и баланс, переносит значения полей в _objectProfile и сохраняет профиль.
        private async Task SaveSourceDataAsync()
        {
            try
            {
                if (!ValidateSourceInputs())
                {
                    SetStatus("Исправьте ошибки в исходных данных.");
                    MessageBox.Show(this, "Проверьте выделенные поля и исправьте ошибки.",
                        "Ошибка валидации", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!ValidateBalance())
                {
                    SetStatus("Сумма баланса водопользования должна быть равна 100%.");
                    MessageBox.Show(this, "Проверьте баланс водопользования: сумма долей должна быть равна 100%.",
                        "Ошибка валидации", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                CollectProfileFromUi();
                await _database.SaveObjectProfileAsync(_objectId, _objectProfile);
                UpdateInstructionTables();
                UpdateMainResults();
                SetStatus("Исходные данные и схема сохранены в БД.");
                MessageBox.Show(this, "Данные сохранены в профиль объекта.", "Готово",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка сохранения исходных данных расчёта.", ex);
                MessageBox.Show(this, "Не удалось сохранить данные:\r\n" + ex.Message,
                    "Ошибка БД", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---- Валидация ввода ----

        // Поключает ErrorProvider к полям с некорректными значениями; возвращает true, если всё ок.
        private bool ValidateSourceInputs()
        {
            var valid = true;
            foreach (var pair in _sourceInputs)
            {
                _sourceErrors.SetError(pair.Value, "");
                var value = pair.Value.Text.Trim();
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                var error = "";
                if (pair.Key == "DamType")
                {
                    if (value.Any(char.IsDigit))
                        error = "Введите название типа плотины буквами.";
                }
                else if (pair.Key == "SpillwayOpeningsCount" || pair.Key == "UnitsCount")
                {
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var integer) || integer < 0)
                        error = "Введите целое неотрицательное число.";
                }
                else if (!TryParseDecimal(value, out var number))
                {
                    error = "Введите число, например 12,5.";
                }
                else if ((pair.Key == "Provision" || pair.Key == "EfficiencyPct") &&
                         (number < 0 || number > 100))
                {
                    error = "Процент должен быть от 0 до 100.";
                }
                else if (number < 0 && pair.Key != "WaterTemperature")
                {
                    error = "Значение не может быть отрицательным.";
                }
                else if ((pair.Key == "RoughnessCoeff" || pair.Key == "ReliabilityCoefficient") && number <= 0)
                {
                    error = "Коэффициент должен быть больше нуля.";
                }

                if (error.Length > 0)
                {
                    _sourceErrors.SetError(pair.Value, error);
                    valid = false;
                }
            }

            return valid;
        }

        // Универсальный разбор числа:Accepts и запятую, и точку как разделитель.
        private static bool TryParseDecimal(string value, out decimal number)
        {
            value = value.Trim().Replace(',', '.');
            return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }

        // Сумма долей баланса должна быть равна 100% (с допуском 0.01) и каждая — от 0 до 100.
        private bool ValidateBalance()
        {
            if (_balanceGrid == null)
                return true;

            var total = 0m;
            foreach (DataGridViewRow row in _balanceGrid.Rows)
            {
                var value = Convert.ToString(row.Cells[1].Value) ?? "";
                if (!TryParseDecimal(value, out var number) || number < 0 || number > 100)
                    return false;
                total += number;
            }
            return Math.Abs(total - 100m) <= 0.01m;
        }

        // Текущий текст редактируемого поля по ключу (пустая строка, если поля нет).
        private string InputValue(string key) =>
            _sourceInputs.TryGetValue(key, out var input) ? input.Text.Trim() : "";

        // ---- Отчёт ----

        // Сохраняет текстовый отчёт по расчёту в выбранный файл.
        private void OnReportClick(object? sender, EventArgs e)
        {
            if (_sections.Count == 0)
            {
                MessageBox.Show(this, "Сначала откройте Excel-файл.", "Нет данных",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using var dlg = new SaveFileDialog
            {
                Filter = "Текстовый файл (*.txt)|*.txt",
                FileName = "flood_report.txt"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var text = _report.Generate(_sections, _sourceFile);
                File.WriteAllText(dlg.FileName, text, Encoding.UTF8);
                SetStatus("Отчёт сохранён: " + dlg.FileName);
                MessageBox.Show(this, "Отчёт сохранён:\n" + dlg.FileName, "Готово",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка формирования отчёта.", ex);
                MessageBox.Show(this, ex.Message, "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Печатает отчёт постранично (monospace-шрифт, перенос по границе полей страницы).
        private void OnPrintReportClick(object? sender, EventArgs e)
        {
            if (_sections.Count == 0)
            {
                MessageBox.Show(this, "Сначала импортируйте Excel-файл.", "Нет данных",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var reportText = _report.Generate(_sections, _sourceFile);
            var lines = reportText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var currentLine = 0;
            using var document = new PrintDocument();
            document.PrintPage += (s, args) =>
            {
                using var font = new Font("Consolas", 9F);
                var lineHeight = font.GetHeight(args.Graphics);
                var pageLine = 0;
                while (currentLine < lines.Length &&
                    args.MarginBounds.Top + (pageLine * lineHeight) < args.MarginBounds.Bottom)
                {
                    args.Graphics.DrawString(lines[currentLine], font, Brushes.Black,
                        args.MarginBounds.Left, args.MarginBounds.Top + (pageLine * lineHeight));
                    currentLine++;
                    pageLine++;
                }
                args.HasMorePages = currentLine < lines.Length;
            };

            using var dialog = new PrintDialog { Document = document, UseEXDialog = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                document.Print();
                SetStatus("Отчёт отправлен на печать.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка печати отчёта.", ex);
                MessageBox.Show(this, "Не удалось распечатать отчёт:\n" + ex.Message,
                    "Ошибка печати", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
