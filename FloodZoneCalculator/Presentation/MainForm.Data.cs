#nullable enable

using FloodZoneCalculator.Domain;
using FloodZoneCalculator.Infrastructure;
using FloodZoneDb.Client;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation
{
    // =====================================================================================
    // MainForm.Data.cs — наполнение интерфейса данными и их форматирование.
    //
    //   LoadObjectSummaryAsync      — загружает профиль объекта и раскладывает значения по полям.
    //   LoadCalculationDetailsAsync — подтягивает изодаты/баланс/выработку из БД в гриды.
    //   LoadDatabaseProfilesAsync   — строит модели профилей створов из записей БД.
    //   UpdateMainResults / UpdateInstructionTables — пересчёт и обновление таблиц/графиков.
    //   UpdateHydraulicParameters   — заполнение таблицы параметров створа.
    //   Set*/Format*/Read* helpers  — мелкая утилитарная работа со значениями и парсингом.
    // =====================================================================================
    public sealed partial class CalculationForm
    {
        // ---- Загрузка исходных данных объекта и связанных расчётных таблиц ----

        // Точка входа загрузки: список объектов → профиль → профили створов → детали расчёта,
        // затем разносим значения по read-only полям и редактируемым вводам.
        private async Task LoadObjectSummaryAsync()
        {
            try
            {
                var objects = await _database.LoadObjectsAsync();
                _currentObject = objects.FirstOrDefault(x => x.Id == _objectId);
                _objectProfile = await _database.LoadObjectProfileAsync(_objectId) ?? new ObjectProfile();
                await LoadDatabaseProfilesAsync();
                await LoadCalculationDetailsAsync();
                RestoreInputGridsFromProfile();
                SetSourceValue("ObjectType", _currentObject?.TypeName);
                SetSourceValue("ObjectName", _currentObject?.Name);
                SetSourceValue("River", _objectProfile.River);
                SetSourceValue("CommissioningDate", _objectProfile.CommissioningDate);
                SetSourceValue("Owner", _objectProfile.Owner);
                SetSourceValue("DamType", _objectProfile.DamType);
                SetSourceValue("DamLengthM", _objectProfile.DamLengthM, "м");
                SetSourceValue("DamMaxHeightM", _objectProfile.DamMaxHeightM, "м");
                SetSourceValue("SpillwayOpeningsCount", _objectProfile.SpillwayOpeningsCount, "шт.");
                SetSourceValue("UnitsCount", _objectProfile.UnitsCount, "шт.");
                SetSourceValue("DesignPowerMw", _objectProfile.DesignPowerMw, "МВт");
                SetSourceValue("NormalHeadwaterM", _objectProfile.NormalHeadwaterM, "м");
                SetSourceValue("ReservoirFullVolume", _objectProfile.ReservoirVolumeMlnM3, "млн м³");
                SetSourceValue("MaxFlow", "—", "м³/с",
                    "не хранится в профиле объекта; вводится оператором");
                SetSourceValue("MinFlow", "—", "м³/с",
                    "не хранится в профиле объекта; вводится оператором");
                SetSourceValue("CatchmentArea", null, "км²",
                    "нет поля в текущем профиле; вводится оператором");
                SetSourceValue("AvgRunoff", null, "км³",
                    "нет поля в текущем профиле; вводится оператором");
                SetSourceValue("Provision", null, "%",
                    "нет поля в текущем профиле; вводится оператором");
                SetSourceValue("DeadVolumeLevel", null, "м",
                    "нет поля в текущем профиле; вводится оператором");
                SetSourceValue("ReservoirUsefulVolume", null, "млн м³",
                    "нет поля в текущем профиле; вводится оператором");
                SetSourceValue("RoughnessCoeff", null, "—",
                    "нет поля в текущем профиле; вводится оператором");
                SetSourceValue("WaterTemperature", null, "°C",
                    "нет поля в текущем профиле; вводится оператором");
                SetInputValue("DamType", _objectProfile.DamType);
                SetInputValue("DamLengthM", _objectProfile.DamLengthM);
                SetInputValue("DamMaxHeightM", _objectProfile.DamMaxHeightM);
                SetInputValue("SpillwayCapacityM3s", _objectProfile.SpillwayCapacityM3s);
                SetInputValue("SpillwayOpeningsCount", _objectProfile.SpillwayOpeningsCount);
                SetInputValue("UnitsCount", _objectProfile.UnitsCount);
                SetInputValue("DesignPowerMw", _objectProfile.DesignPowerMw);
                SetInputValue("NormalHeadwaterM", _objectProfile.NormalHeadwaterM);
                SetInputValue("MinWaterLevelM", _objectProfile.MinWaterLevelM);
                SetInputValue("ReservoirFullVolume", _objectProfile.ReservoirVolumeMlnM3);
                SetInputValue("CatchmentArea", _objectProfile.CatchmentAreaKm2);
                SetInputValue("AvgRunoff", _objectProfile.AverageAnnualRunoffKm3);
                SetInputValue("MaxFlow", _objectProfile.MaxFlowM3s);
                SetInputValue("MinFlow", _objectProfile.MinFlowM3s);
                SetInputValue("Provision", _objectProfile.ProvisionPct);
                SetInputValue("DeadVolumeLevel", _objectProfile.DeadVolumeLevelM);
                SetInputValue("ReservoirUsefulVolume", _objectProfile.ReservoirUsefulVolumeMlnM3);
                SetInputValue("RoughnessCoeff", _objectProfile.RoughnessCoeffN);
                SetInputValue("WaterTemperature", _objectProfile.WaterTemperatureC);
                SetInputValue("EfficiencyPct", _objectProfile.EfficiencyPct);
                SetInputValue("ReliabilityCoefficient", _objectProfile.ReliabilityCoefficient);
                LoadSchemeImage(_objectProfile.SchemeImagePath, _objectProfile.SchemeImageBase64);
                UpdateMainResults();
                UpdateInstructionTables();
                if (_currentObject != null)
                    SetStatus("Объект: " + _currentObject.Name + ". Готово.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка загрузки исходных данных объекта расчёта.", ex);
                SetStatus("Исходные данные объекта не загружены: " + ex.Message);
            }
        }

        // Загружает сохранённые изодаты расхода, баланс и помесячную выработку в соответствующие гриды.
        private async Task LoadCalculationDetailsAsync()
        {
            try
            {
                var provision = await _database.LoadFlowProvisionAsync(_objectId);
                if (_flowProvisionGrid != null)
                {
                    _flowProvisionGrid.Rows.Clear();
                    if (provision.Count > 0)
                    {
                        foreach (var (p, q) in provision)
                        {
                            _flowProvisionGrid.Rows.Add(
                                p.ToString("0.0", CultureInfo.CurrentCulture),
                                q.ToString("N2", CultureInfo.CurrentCulture));
                        }
                    }
                    else
                    {
                        foreach (var probability in new[] { "0,1", "1", "2", "5", "10", "20", "50", "90", "99" })
                            _flowProvisionGrid.Rows.Add(probability, "");
                    }
                }

                var balance = await _database.LoadWaterBalanceAsync(_objectId);
                if (_balanceGrid != null)
                {
                    _balanceGrid.Rows.Clear();
                    if (balance.Count > 0)
                    {
                        foreach (var (cat, share) in balance)
                            _balanceGrid.Rows.Add(cat, share.ToString("0.0", CultureInfo.CurrentCulture));
                    }
                    else
                    {
                        foreach (var row in new[]
                        {
                            new[] { "Выработка электроэнергии", "65" },
                            new[] { "Водоснабжение", "20" },
                            new[] { "Судоходство", "10" },
                            new[] { "Прочее", "5" }
                        })
                            _balanceGrid.Rows.Add(row);
                    }
                }

                var generation = await _database.LoadPowerGenerationAsync(_objectId);
                if (_generationGrid != null)
                {
                    _generationGrid.Rows.Clear();
                    var months = new[] { "Янв", "Фев", "Мар", "Апр", "Май", "Июн", "Июл", "Авг", "Сен", "Окт", "Ноя", "Дек" };
                    if (generation.Count > 0)
                    {
                        foreach (var (month, gen) in generation)
                        {
                            var name = month > 0 && month <= 12 ? months[month - 1] : "Месяц";
                            _generationGrid.Rows.Add(name, "", "", gen.ToString("N2", CultureInfo.CurrentCulture));
                        }
                    }
                    else
                    {
                        foreach (var month in months)
                            _generationGrid.Rows.Add(month, "", "", "");
                    }
                }

                UpdateMainResults();
                _flowChart?.Invalidate();
                _generationChart?.Invalidate();
                _balanceChart?.Invalidate();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка загрузки расчётных данных объекта.", ex);
            }
        }

        // Восстанавливает введённые оператором таблицы из профиля: эксплуатационные характеристики
        // (периоды Q/уровни) и помесячную выработку (расход/напор/выработка). Профиль — более полный
        // источник, чем нормализованные таблицы БД (в них сохраняется только выработка), поэтому
        // применяем поверх LoadCalculationDetailsAsync. Если в профиле пусто — оставляем то, что грузится из БД.
        private void RestoreInputGridsFromProfile()
        {
            if (_operationGrid != null && _objectProfile.OperationPeriods.Count > 0)
            {
                for (var i = 0; i < _operationGrid.Rows.Count && i < _objectProfile.OperationPeriods.Count; i++)
                {
                    var e = _objectProfile.OperationPeriods[i];
                    var row = _operationGrid.Rows[i];
                    row.Cells[0].Value = e.Period;
                    row.Cells[1].Value = e.QAvg;
                    row.Cells[2].Value = e.QMax;
                    row.Cells[3].Value = e.QMin;
                    row.Cells[4].Value = e.Hwl;
                    row.Cells[5].Value = e.Lwl;
                }
            }

            if (_generationGrid != null && _objectProfile.MonthlyGeneration.Count > 0)
            {
                var months = new[] { "Янв", "Фев", "Мар", "Апр", "Май", "Июн", "Июл", "Авг", "Сен", "Окт", "Ноя", "Дек" };
                _generationGrid.Rows.Clear();
                foreach (var e in _objectProfile.MonthlyGeneration)
                {
                    var name = e.Month > 0 && e.Month <= 12 ? months[e.Month - 1] : "Месяц";
                    _generationGrid.Rows.Add(name, e.Flow, e.Head, e.Generation);
                }
            }
        }

        // Читает створы из БД и строит для каждого модель отрисовки профиля; если створов из Excel
        // нет, заполняет дерево створов записями из базы.
        private async Task LoadDatabaseProfilesAsync()
        {
            var records = await _database.LoadCrossSectionsAsync(_objectId);
            _databaseCrossSections = records.ToList();
            var builder = new CrossSectionGeometryBuilder();
            _profileModels.Clear();
            foreach (var record in records)
                _profileModels[record.Number] = new HydraulicProfileRenderModel(builder.Build(record));

            if (_sections.Count == 0 && records.Count > 0)
            {
                _sectionsTree.Nodes.Clear();
                var root = new TreeNode("Гидротехнические сооружения");
                foreach (var record in records)
                    root.Nodes.Add(new TreeNode("Створ №" + record.Number) { Tag = record });
                _sectionsTree.Nodes.Add(root);
                root.Expand();
                _sectionsTree.SelectedNode = root.Nodes[0];
            }
        }

        // Размещает значение в read-only поле исходных данных; при пустом — пояснение почему.
        private void SetSourceValue(string key, string? value, string unit = "",
            string missingReason = "нет данных в БД")
        {
            if (!_sourceValues.TryGetValue(key, out var label))
                return;
            label.Text = string.IsNullOrWhiteSpace(value)
                ? "Не заполнено (" + missingReason + ")"
                : value + (string.IsNullOrWhiteSpace(unit) ? "" : " " + unit);
        }

        // Размещает значение в редактируемом числовом поле исходных данных.
        private void SetInputValue(string key, string? value)
        {
            if (_sourceInputs.TryGetValue(key, out var input))
                input.Text = value ?? "";
        }

        // ---- Обновление расчётных таблиц ----

        // Пересчитывает и заполняет основную таблицу результатов из профиля и введённых значений.
        private void UpdateMainResults()
        {
            if (_mainResultsGrid == null) return;
            _mainResultsGrid.Rows[0].Cells[2].Value = FormatOptional(_objectProfile.SpillwayCapacityM3s);
            _mainResultsGrid.Rows[1].Cells[2].Value = FormatOptional(InputValue("MaxFlow"));
            _mainResultsGrid.Rows[2].Cells[2].Value = FormatOptional(InputValue("MinFlow"));
            var velocities = _sections
                .Where(section => section.MaxVelocity > 0)
                .Select(section => section.MaxVelocity)
                .ToList();
            _mainResultsGrid.Rows[3].Cells[2].Value = velocities.Count == 0
                ? "—"
                : velocities.Average().ToString("N2", CultureInfo.CurrentCulture);
            _mainResultsGrid.Rows[4].Cells[2].Value = FormatOptional(_objectProfile.NormalHeadwaterM);
            _mainResultsGrid.Rows[5].Cells[2].Value = FormatOptional(_objectProfile.MinWaterLevelM);
            _mainResultsGrid.Rows[6].Cells[2].Value = FormatOptional(_objectProfile.EfficiencyPct);
            var generated = _generationGrid == null
                ? 0
                : _generationGrid.Rows.Cast<DataGridViewRow>()
                    .Sum(row => ReadCellNumber(row.Cells[3].Value));
            _mainResultsGrid.Rows[7].Cells[2].Value = generated > 0
                ? generated.ToString("N2")
                : FormatOptional(_objectProfile.AverageAnnualGenerationMkwh);
            _mainResultsGrid.Rows[8].Cells[2].Value = FormatOptional(_objectProfile.ReliabilityCoefficient);
        }

        private static string DisplayResult(string value) =>
            string.IsNullOrWhiteSpace(value) ? "—" : value;

        // Реакция на изменение вводов: обновляет гриды изодат/эксплуатации, таблицы и графики,
        // а также просит перерисовать схему гидроузла (зависит от уровней).
        private void UpdateInstructionTables()
        {
            var upper = FormatOptional(InputValue("NormalHeadwaterM"));
            var lower = FormatOptional(InputValue("MinWaterLevelM"));
            _hydraulicProfileImage?.Invalidate();

            var hasMaximumFlow = TryReadInputNumber("MaxFlow", out var qMax);
            var hasMinimumFlow = TryReadInputNumber("MinFlow", out var qMin);
            var hasFlows = hasMaximumFlow && hasMinimumFlow && qMax > 0 && qMin >= 0 && qMin <= qMax;

            if (_flowProvisionGrid != null)
            {
                for (var i = 0; i < _flowProvisionGrid.Rows.Count; i++)
                {
                    if (!hasFlows)
                    {
                        _flowProvisionGrid.Rows[i].Cells[1].Value = "";
                        continue;
                    }
                    var probability = ReadCellNumber(_flowProvisionGrid.Rows[i].Cells[0].Value);
                    var factor = 1 - Math.Min(1, Math.Max(0, probability / 100));
                    _flowProvisionGrid.Rows[i].Cells[1].Value = (qMin + (qMax - qMin) * factor).ToString("N2");
                }
            }

            if (_operationGrid != null)
            {
                for (var i = 0; i < _operationGrid.Rows.Count; i++)
                {
                    _operationGrid.Rows[i].Cells[4].Value = upper;
                    _operationGrid.Rows[i].Cells[5].Value = lower;
                }
            }

            UpdateMainResults();
            _flowChart?.Invalidate();
            _provisionCurveChart?.Invalidate();
            _generationChart?.Invalidate();
        }

        // ---- Параметры выбранного створа (таблица «Hydraulic profile») ----

        // Модель профиля для выбранного узла дерева створов (по номеру), либо null.
        private HydraulicProfileRenderModel? GetSelectedProfileModel()
        {
            if (_sectionsTree == null)
                return null;

            if (_sectionsTree.SelectedNode?.Tag is CrossSectionRecord record &&
                _profileModels.TryGetValue(record.Number, out var recordModel))
                return recordModel;
            if (_sectionsTree.SelectedNode?.Tag is CrossSection section &&
                _profileModels.TryGetValue(section.Number, out var sectionModel))
                return sectionModel;
            return null;
        }

        // Заполняет таблицу параметров створа из записи БД с добором из расчётного створа.
        private void UpdateHydraulicParameters(
            CrossSectionRecord record,
            CrossSection? calculationSection)
        {
            var values = new[]
            {
                RawOrEmpty(record.DistanceFromHydroUnitM),
                RawOrEmpty(record.BottomLevelZb),
                FirstAvailable(FormatOptional(record.DepthHb), FormatSectionDepth(calculationSection)),
                FirstAvailable(FormatOptional(record.WidthBb), FormatSectionWidth(calculationSection)),
                FirstAvailable(FormatOptional(record.VelocityVb), FormatSectionVelocity(calculationSection)),
                RawOrEmpty(record.Kgm)
            };
            for (var i = 0; i < values.Length && i < _hydraulicGrid.Rows.Count; i++)
                _hydraulicGrid.Rows[i].Cells[1].Value = values[i];
        }

        private static string FirstAvailable(string preferred, string fallback) =>
            preferred == "—" ? fallback : preferred;

        // Для редактируемых ячеек (Lc/Zб/Kgm) показываем сырое значение: пустая строка означает,
        // что оператор ещё не ввёл параметр съёмки, и позволяет начать ввод без удаления «—».
        private static string RawOrEmpty(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "" : value.Trim();

        // Заполняет таблицу параметров только из расчётного створа (без данных БД).
        private void UpdateHydraulicParameters(CrossSection? section)
        {
            var values = new[]
            {
                "",
                "",
                FormatSectionDepth(section),
                FormatSectionWidth(section),
                FormatSectionVelocity(section),
                ""
            };
            for (var i = 0; i < values.Length && i < _hydraulicGrid.Rows.Count; i++)
                _hydraulicGrid.Rows[i].Cells[1].Value = values[i];
        }

        // ---- Форматирование и парсинг значений ----

        private static string FormatOptional(string value) =>
            TryParseDecimal(value, out var number)
                ? number.ToString("0.##", CultureInfo.CurrentCulture)
                : "—";

        private static string FormatSectionDepth(CrossSection? section) =>
            section == null || section.MaxDepth <= 0
                ? "—"
                : section.MaxDepth.ToString("0.##", CultureInfo.CurrentCulture);

        private static string FormatSectionVelocity(CrossSection? section) =>
            section == null || section.MaxVelocity <= 0
                ? "—"
                : section.MaxVelocity.ToString("0.##", CultureInfo.CurrentCulture);

        // Ширина створа = сумма|max расстояний левого и правого берега по изодатам глубины.
        private static string FormatSectionWidth(CrossSection? section)
        {
            if (section == null)
                return "—";
            var left = section.GetPoints(Bank.Left, IzodataType.Depth);
            var right = section.GetPoints(Bank.Right, IzodataType.Depth);
            var width = (left.Count == 0 ? 0 : left.Max(point => point.Distance)) +
                        (right.Count == 0 ? 0 : right.Max(point => point.Distance));
            return width <= 0 ? "—" : width.ToString("0.##", CultureInfo.CurrentCulture);
        }

        // Пытается прочитать число из редактируемого поля исходных данных по ключу.
        private bool TryReadInputNumber(string key, out double value)
        {
            if (_sourceInputs.TryGetValue(key, out var input) &&
                TryParseDecimal(input.Text, out var parsed))
            {
                value = (double)parsed;
                return true;
            }
            value = 0;
            return false;
        }

        private static double ReadCellNumber(object? value) =>
            ParseDoubleOrZero(Convert.ToString(value) ?? "");

        private static double ParseDoubleOrZero(string value) =>
            TryParseDecimal(value, out var number) ? (double)number : 0;
    }
}
