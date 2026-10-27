#nullable enable

using FloodZoneCalculator.Domain;
using FloodZoneCalculator.Infrastructure;
using FloodZoneDb.Client;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation
{
    // =====================================================================================
    // MainForm.Sections.cs — навигация по створам и таблица точек берегов выбранного створа.
    //
    //   SelectSection / SelectRelativeSection — переходы «начало/назад/вперёд/конец» по дереву.
    //   OnSectionChanged  — обработчик выбора створа: сводит данные БД и расчётного створа,
    //                       заполняет подписи, параметры и таблицу точек, просит перерисовать профиль.
    //   PopulateBankPointsGrid — строки точек из БД (PostgreSQL) и из расчёта (Excel).
    //   Get*Name          — перевод технических значений в русские подписи.
    // =====================================================================================
    public sealed partial class CalculationForm
    {
        // Выбирает створ по индексу в дереве (индекс за границами => последний доступный).
        private void SelectSection(int index)
        {
            if (_sectionsTree == null || _sectionsTree.Nodes.Count == 0 ||
                _sectionsTree.Nodes[0].Nodes.Count == 0)
                return;
            var nodes = _sectionsTree.Nodes[0].Nodes;
            _sectionsTree.SelectedNode = nodes[Math.Min(index, nodes.Count - 1)];
        }

        // Сдвиг выбора на offset створов от текущего; если выбора нет — переходит к первому.
        private void SelectRelativeSection(int offset)
        {
            if (_sectionsTree.SelectedNode == null)
            {
                SelectSection(0);
                return;
            }
            var nodes = _sectionsTree.Nodes.Count == 0 ? null : _sectionsTree.Nodes[0].Nodes;
            if (nodes == null || nodes.Count == 0) return;
            var next = Math.Max(0, Math.Min(nodes.Count - 1,
                _sectionsTree.SelectedNode.Index + offset));
            SelectSection(next);
        }

        // Кнопки «К списку расчётов»/«Закрыть» — просто закрывают окно расчёта.
        private void OnBackToObjectsClick(object? sender, EventArgs e)
        {
            Close();
        }

        // Центральный обработчик выбора створа: определяет, есть ли запись БД и/или расчётный створ
        // с тем же номером, и на основе этого заполняет подписи, параметры и таблицу точек.
        private void OnSectionChanged(object? sender, TreeViewEventArgs e)
        {
            var record = e.Node.Tag as CrossSectionRecord;
            var section = e.Node.Tag as CrossSection;
            if (record == null && section == null)
                return;

            var number = record?.Number ?? section!.Number;
            _currentSectionNumber = number;
            var databaseRecord = record ??
                _databaseCrossSections.FirstOrDefault(item => item.Number == number);
            var calculationSection = section ??
                _sections.FirstOrDefault(item => item.Number == number);
            PopulateBankPointsGrid(calculationSection, databaseRecord);
            if (_sectionTitle != null)
                _sectionTitle.Text = "Створ №" + number;
            if (_sourceInfo != null)
            {
                var sources = new List<string>();
                if (databaseRecord != null)
                    sources.Add("PostgreSQL");
                if (calculationSection != null)
                    sources.Add(string.IsNullOrWhiteSpace(_sourceFile)
                        ? "расчёт"
                        : Path.GetFileName(_sourceFile));
                _sourceInfo.Text = sources.Count == 0
                    ? "Точки не загружены"
                    : "Источники точек: " + string.Join(", ", sources);
            }
            if (_sectionInfo != null)
            {
                var leftCount = _bankPointsGrid.Rows.Cast<DataGridViewRow>()
                    .Count(row => Convert.ToString(row.Cells[1].Value) == "Левый");
                var rightCount = _bankPointsGrid.Rows.Cast<DataGridViewRow>()
                    .Count(row => Convert.ToString(row.Cells[1].Value) == "Правый");
                _sectionInfo.Text = $"Точек левого берега: {leftCount}    " +
                    $"Точек правого берега: {rightCount}    Всего: {_bankPointsGrid.Rows.Count}";
            }
            if (_sectionParameters != null)
            {
                _sectionParameters.Text = databaseRecord != null
                    ? $"Lc: {FormatOptional(databaseRecord.DistanceFromHydroUnitM)} м    " +
                      $"Zб: {FormatOptional(databaseRecord.BottomLevelZb)} м    " +
                      $"Hб: {FirstAvailable(FormatOptional(databaseRecord.DepthHb), FormatSectionDepth(calculationSection))} м    " +
                      $"Bб: {FirstAvailable(FormatOptional(databaseRecord.WidthBb), FormatSectionWidth(calculationSection))} м    " +
                      $"Vб: {FirstAvailable(FormatOptional(databaseRecord.VelocityVb), FormatSectionVelocity(calculationSection))} м/с    " +
                      $"Kgm: {FormatOptional(databaseRecord.Kgm)}\r\n" +
                      $"Высота берегов: {FormatOptional(databaseRecord.LeftBankHeightM)} / {FormatOptional(databaseRecord.RightBankHeightM)} м    " +
                      $"Ширина поймы: {FormatOptional(databaseRecord.LeftFloodplainWidthM)} / {FormatOptional(databaseRecord.RightFloodplainWidthM)} м"
                    : section != null
                        ? $"Глубина по Excel, макс.: {FormatSectionDepth(section)} м    " +
                          $"Ширина охвата изодат: {FormatSectionWidth(section)} м\r\n" +
                          $"Скорость по Excel, макс.: {FormatSectionVelocity(section)} м/с. " +
                          "Lc, Zб и Kgm в файле изодат отсутствуют."
                        : "Параметры створа отсутствуют.";
            }

            if (databaseRecord != null)
            {
                _suppressHydraulicEdit = true;
                try { UpdateHydraulicParameters(databaseRecord, calculationSection); }
                finally { _suppressHydraulicEdit = false; }
            }
            else
            {
                _suppressHydraulicEdit = true;
                try { UpdateHydraulicParameters(calculationSection); }
                finally { _suppressHydraulicEdit = false; }
            }

            if (section != null)
            {
                if (_resultInfo.Count >= 6)
                {
                    _resultInfo[0].Text = section.MaxDepth.ToString("0.###");
                    _resultInfo[1].Text = section.MaxVelocity.ToString("0.###");
                    _resultInfo[2].Text = "—";
                    _resultInfo[3].Text = section.FloodWidthLeft.ToString("0.###");
                    _resultInfo[4].Text = section.FloodWidthRight.ToString("0.###");
                    _resultInfo[5].Text =
                        (section.FloodAreaLeft + section.FloodAreaRight).ToString("0.###");
                }
            }

            _hydraulicProfileImage?.Invalidate();
            _crossSectionImage?.Invalidate();
        }

        // Заполняет таблицу точек: сначала береговые точки из БД, затем точки из расчётного створа.
        private void PopulateBankPointsGrid(
            CrossSection section,
            CrossSectionRecord databaseRecord)
        {
            if (_bankPointsGrid == null)
                return;

            _bankPointsGrid.Rows.Clear();
            if (databaseRecord != null)
            {
                foreach (var point in databaseRecord.BankPoints ?? new List<BankPointRecord>())
                {
                    AddGridRow(_bankPointsGrid, "PostgreSQL", GetSideName(point.Side),
                        GetPointTypeName(point.PointType), point.PointNumber.ToString(),
                        point.DistanceM, point.ElevationM);
                }
            }

            if (section != null)
            {
                foreach (var bank in new[] { Bank.Left, Bank.Right })
                foreach (var type in Enum.GetValues(typeof(IzodataType)).Cast<IzodataType>())
                {
                    var points = section.GetPoints(bank, type);
                    for (var i = 0; i < points.Count; i++)
                    {
                        var point = points[i];
                        AddGridRow(_bankPointsGrid, "Расчёт", GetSideName(bank.ToString()),
                            GetIzodataTypeName(type), (i + 1).ToString(),
                            point.Distance.ToString("0.###", CultureInfo.CurrentCulture),
                            point.Value.ToString("0.###", CultureInfo.CurrentCulture));
                    }
                }
            }
        }

        // ---- Перевод технических значений в подписи для таблицы точек ----

        private static string GetSideName(string side) => side switch
        {
            "Left" => "Левый",
            "Right" => "Правый",
            "Center" => "Центр",
            _ => side
        };

        private static string GetPointTypeName(string pointType) => pointType switch
        {
            "Bank" => "Берег",
            "Horizontal" => "Горизонталь",
            "ChannelBank" => "Бровка русла",
            "Bottom" => "Дно",
            _ => pointType
        };

        private static string GetIzodataTypeName(IzodataType type) => type switch
        {
            IzodataType.Depth => "Глубина",
            IzodataType.Velocity => "Скорость",
            IzodataType.DryingCalculated => "Расчётная осушка",
            IzodataType.DryingFactual => "Фактическая осушка",
            _ => type.ToString()
        };

        // Обработка ручного ввода Lc/Zб/Kgm: обновляем запись cross_sections (или создаём новую)
        // и делаем upsert в БД. Строки Hб/Bб/Vб закрыты на редактирование, они считаются.
        private async void OnHydraulicCellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (_suppressHydraulicEdit) return;
            if (e.RowIndex < 0 || e.ColumnIndex != 1) return;
            if (e.RowIndex != 0 && e.RowIndex != 1 && e.RowIndex != 5) return;
            if (_currentSectionNumber <= 0) return;

            var text = Convert.ToString(_hydraulicGrid.Rows[e.RowIndex].Cells[1].Value) ?? "";
            text = text.Trim();
            // Пустое значение — разрешаем сохранить как «нет данных».
            var record = _databaseCrossSections.FirstOrDefault(r => r.Number == _currentSectionNumber);
            var isNew = record == null;
            if (record == null)
            {
                record = new CrossSectionRecord
                {
                    ObjectId = _objectId,
                    Number = _currentSectionNumber,
                    // Подтягиваем расчётные Hб/Bб/Vб из Excel-створа, чтобы при первом сохранении
                    // уже введённые вручную параметры не затёрлись пустотой.
                    DepthHb = (_sections.FirstOrDefault(s => s.Number == _currentSectionNumber)?.MaxDepth ?? 0)
                        .ToString("0.###", CultureInfo.CurrentCulture),
                    WidthBb = FormatSectionWidth(_sections.FirstOrDefault(s => s.Number == _currentSectionNumber)),
                    VelocityVb = FormatSectionVelocity(_sections.FirstOrDefault(s => s.Number == _currentSectionNumber)),
                };
                if (record.DepthHb == "—") record.DepthHb = "";
                if (record.WidthBb == "—") record.WidthBb = "";
                if (record.VelocityVb == "—") record.VelocityVb = "";
                _databaseCrossSections.Add(record);
            }
            switch (e.RowIndex)
            {
                case 0: record.DistanceFromHydroUnitM = text; break;
                case 1: record.BottomLevelZb = text; break;
                case 5: record.Kgm = text; break;
            }
            try
            {
                var id = await _database.SaveCrossSectionAsync(record);
                record.Id = id;
                SetStatus("Створ №" + _currentSectionNumber + ": " +
                    (e.RowIndex == 0 ? "Lc" : e.RowIndex == 1 ? "Zб" : "Kgm") + " сохранён в БД.");
                // Обновляем дерево створов, чтобы отобразить подписи со свежими Lc/Zб/Kgm.
                if (isNew) RefreshSectionsTreeFromDatabase();
                _hydraulicProfileImage?.Invalidate();
                _crossSectionImage?.Invalidate();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Не удалось сохранить параметры створа №" + _currentSectionNumber + ".", ex);
                SetStatus("Параметры створа не сохранены: " + ex.Message);
            }
        }

        // Пересобирает дерево створов, если появился новый номер в cross_sections —
        // иначе пользователь не увидит, что запись уже существует в БД.
        private void RefreshSectionsTreeFromDatabase()
        {
            if (_sectionsTree == null || _sections.Count > 0) return;
            _sectionsTree.Nodes.Clear();
            var root = new TreeNode("Гидротехнические сооружения");
            foreach (var record in _databaseCrossSections)
                root.Nodes.Add(new TreeNode("Створ №" + record.Number) { Tag = record });
            _sectionsTree.Nodes.Add(root);
            root.Expand();
            if (root.Nodes.Count > 0)
                _sectionsTree.SelectedNode = root.Nodes[0];
        }
    }
}
