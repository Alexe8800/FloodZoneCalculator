#nullable enable

using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation
{
    // =====================================================================================
    // MainForm.Ui.cs — конструкторы интерфейса окна расчёта.
    // Здесь собираются визуальные блоки трёх вкладок (результаты / исходные данные / створы)
    // и общие фабрики контролов (гриды, группы, кнопки, поля). Логика наполнения данными
    // живёт в MainForm.Data.cs, а отрисовка графиков/схем — в Drawing/Charts.
    // =====================================================================================
    public sealed partial class CalculationForm
    {
        // ---- Вкладка «Результаты расчёта»: внутренние вкладки графики + таблицы ----
        private Control BuildResultsWorkspace()
        {
            var innerTabs = MakeDarkTabs();
            var chartsTab = ModernTheme.MakeTabPage("Графики");
            var dataTab = ModernTheme.MakeTabPage("Табличные данные");
            var conclusionsTab = ModernTheme.MakeTabPage("Выводы");
            var dashboard = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(2),
                BackColor = ModernTheme.Panel,
            };
            dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 54F));
            dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 46F));

            var topRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.Transparent };
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
            topRow.Controls.Add(BuildHydraulicBlock(), 0, 0);
            topRow.Controls.Add(BuildBalanceBlock(), 1, 0);

            var bottomRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.Transparent };
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54F));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46F));
            bottomRow.Controls.Add(BuildOperationBlock(), 0, 0);
            bottomRow.Controls.Add(BuildGenerationBlock(), 1, 0);

            dashboard.Controls.Add(topRow, 0, 0);
            dashboard.Controls.Add(bottomRow, 0, 1);
            chartsTab.Controls.Add(dashboard);

            var data = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(4),
                BackColor = Color.Transparent,
            };
            data.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            data.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            var summary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Color.Transparent };
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            summary.Controls.Add(BuildResultsTable(), 0, 0);
            summary.Controls.Add(BuildBalanceDataBlock(), 1, 0);
            data.Controls.Add(summary, 0, 0);
            data.Controls.Add(BuildGenerationDataBlock(), 0, 1);
            dataTab.Controls.Add(data);

            innerTabs.TabPages.Add(chartsTab);
            innerTabs.TabPages.Add(dataTab);
            conclusionsTab.Controls.Add(BuildConclusionsBlock());
            innerTabs.TabPages.Add(conclusionsTab);
            return innerTabs;
        }

        // Блок «Выводы по расчёту»: многострочное поле, текст которого сохраняется
        // вместе с расчётом и включается в отчёт Word/PDF.
        private Control BuildConclusionsBlock()
        {
            var box = MakeGroup("Выводы по расчёту");
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
            };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            panel.Controls.Add(new Label
            {
                Text = "Свободный текст выводов — будет включён в отчёт (Word/PDF) в раздел «Выводы» и сохранён вместе с расчётом.",
                Dock = DockStyle.Fill,
                AutoSize = false,
                Height = 34,
                Font = ModernTheme.FontSmall,
                ForeColor = ModernTheme.TextSub,
                BackColor = Color.Transparent,
            }, 0, 0);
            _conclusionsText = new ModernTextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = ModernTheme.FontMain,
            };
            _conclusionsText.TextChanged += (_, _) =>
            {
                if (!_suppressConclusionsSync) _reportConclusions = _conclusionsText.Text;
            };
            panel.Controls.Add(_conclusionsText, 0, 1);
            box.Controls.Add(panel);
            return box;
        }

        // Итоговая таблица ключевых результатов расчёта (заполняется в UpdateMainResults).
        private Control BuildResultsTable()
        {
            var box = MakeGroup("Результаты расчёта");
            _mainResultsGrid = MakeReadOnlyGrid("Параметр", "Ед. изм.", "Значение");
            AddGridRow(_mainResultsGrid, "Пропускная способность", "м³/с", "—");
            AddGridRow(_mainResultsGrid, "Максимальный расход", "м³/с", "—");
            AddGridRow(_mainResultsGrid, "Минимальный расход", "м³/с", "—");
            AddGridRow(_mainResultsGrid, "Средняя скорость потока", "м/с", "—");
            AddGridRow(_mainResultsGrid, "Уровень верхнего бьефа", "м БС", "—");
            AddGridRow(_mainResultsGrid, "Уровень нижнего бьефа", "м БС", "—");
            AddGridRow(_mainResultsGrid, "КПД гидроузла", "%", "—");
            AddGridRow(_mainResultsGrid, "Выработка электроэнергии (годовая)", "млн кВт·ч", "—");
            AddGridRow(_mainResultsGrid, "Коэффициент надёжности", "—", "—");
            box.Controls.Add(_mainResultsGrid);
            return box;
        }

        // ---- Вкладка «Данные объекта»: исходные данные, схема, изодаты, кнопки сохранения ----
        private Control BuildSourceWorkspace()
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = ModernTheme.Panel };
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(4), BackColor = Color.Transparent };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            var objectBox = MakeGroup("Исходные данные объекта");
            objectBox.Height = 214;
            objectBox.Controls.Add(MakeInfoTable(
                new[] { "Object type", "Name", "River", "Commissioning date", "Owner" },
                new[] { "ObjectType", "ObjectName", "River", "CommissioningDate", "Owner" }));
            layout.Controls.Add(objectBox, 0, 0);
            layout.SetColumnSpan(objectBox, 2);
            var scheme = MakeGroup("Схема объекта");
            scheme.Height = 300;
            var schemeLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Color.Transparent };
            _schemeImage = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
            var schemeButton = ModernTheme.MakeButton("Добавить изображение схемы", primary: false, height: 34);
            schemeButton.Dock = DockStyle.Top;
            schemeButton.AutoSize = false;
            schemeButton.Click += OnChooseSchemeClick;
            schemeLayout.Controls.Add(_schemeImage, 0, 0);
            schemeLayout.Controls.Add(schemeButton, 0, 1);
            scheme.Controls.Add(schemeLayout);
            layout.Controls.Add(scheme, 0, 1);
            layout.Controls.Add(BuildSourceBlock("Гидрологические данные",
                new[] { "Catchment area", "Average annual runoff", "Max flow", "Min flow", "Calculation probability" },
                new[] { "CatchmentArea", "AvgRunoff", "MaxFlow", "MinFlow", "Provision" }), 1, 1);
            layout.Controls.Add(BuildSourceBlock("Конструктивные параметры",
                new[] { "Dam type", "Dam length", "Max dam height", "Spillway capacity", "Spillway opening count", "Units count", "Installed capacity" },
                new[] { "DamType", "DamLengthM", "DamMaxHeightM", "SpillwayCapacityM3s", "SpillwayOpeningsCount", "UnitsCount", "DesignPowerMw" }), 0, 2);
            layout.Controls.Add(BuildSourceBlock("Входные параметры расчёта",
                new[] { "Normal headwater level (NHL)", "Minimum level (LWL)", "Dead volume level (DVL)", "Full reservoir volume", "Useful reservoir volume", "Roughness coefficient (n)", "Water temperature", "Hydro node efficiency", "Reliability coefficient" },
                new[] { "NormalHeadwaterM", "MinWaterLevelM", "DeadVolumeLevelM", "ReservoirFullVolume", "ReservoirUsefulVolume", "RoughnessCoeff", "WaterTemperature", "EfficiencyPct", "ReliabilityCoefficient" }), 1, 2);
            var provisions = MakeGroup("Изодаты (кривые обеспеченности)");
            provisions.Height = 460;
            _flowProvisionGrid = MakeEditableGrid("Вероятность, %", "Расход, м³/с");
            foreach (var probability in new[] { "0,1", "1", "2", "5", "10", "20", "50", "90", "99" })
                _flowProvisionGrid.Rows.Add(probability, "");
            _flowChart = new Panel { Dock = DockStyle.Bottom, Height = 190 };
            _flowChart.Paint += (_, e) => DrawFlowChart(e.Graphics, _flowChart.ClientRectangle, false);
            _flowProvisionGrid.Dock = DockStyle.Top;
            _flowProvisionGrid.Height = 250;
            _flowProvisionGrid.CellValueChanged += (_, _) => _flowChart.Invalidate();
            provisions.Controls.Add(_flowChart);
            provisions.Controls.Add(_flowProvisionGrid);
            layout.Controls.Add(provisions, 0, 3);
            layout.SetColumnSpan(provisions, 2);
            var curve = MakeGroup("Кривая обеспеченности расходов");
            curve.Height = 240;
            _provisionCurveChart = new Panel { Dock = DockStyle.Fill };
            _provisionCurveChart.Paint += (_, e) => DrawFlowChart(e.Graphics, _provisionCurveChart.ClientRectangle, true);
            curve.Controls.Add(_provisionCurveChart);
            layout.Controls.Add(curve, 0, 4);
            layout.SetColumnSpan(curve, 2);

            var saveButton = ModernTheme.MakeButton("Сохранить введённые исходные данные в БД", primary: true, height: 40);
            saveButton.Dock = DockStyle.Top;
            saveButton.AutoSize = false;
            saveButton.Click += async (s, e) => await SaveSourceDataAsync();
            layout.Controls.Add(saveButton, 0, 5);
            layout.SetColumnSpan(saveButton, 2);
            scroll.Controls.Add(layout);
            return scroll;
        }

        // ---- Вкладка «Cross sections and bank points»: дерево створов + детали ----
        private Control BuildCrossSectionsWorkspace()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(4),
                BackColor = Color.Transparent,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 74F));

            layout.Controls.Add(BuildSectionsGroup(), 0, 0);

            var details = MakeGroup("Точки выбранного створа");
            var detailLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(8),
                BackColor = Color.Transparent,
            };
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 46F));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 54F));
            _sectionTitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Выберите створ в списке",
                Font = ModernTheme.FontSubHeading,
                ForeColor = ModernTheme.TextMain,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _sectionInfo = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Количество точек левого и правого берега.",
                ForeColor = ModernTheme.TextSub,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _sectionParameters = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Параметры створа появятся после выбора.",
                ForeColor = ModernTheme.TextSub,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _sourceInfo = new Label
            {
                Dock = DockStyle.Top,
                Text = "Данные не загружены",
                TextAlign = ContentAlignment.MiddleLeft,
                Height = 22,
                ForeColor = ModernTheme.TextMuted,
                BackColor = Color.Transparent,
                Font = ModernTheme.FontSmall,
            };
            _bankPointsGrid = MakeReadOnlyGrid(
                "Источник", "Берег", "Тип данных", "Точка №", "Расстояние, м", "Значение / отметка");
            _bankPointsGrid.Columns[0].FillWeight = 95;
            _bankPointsGrid.Columns[1].FillWeight = 70;
            _bankPointsGrid.Columns[2].FillWeight = 115;
            _bankPointsGrid.Columns[3].FillWeight = 65;
            _bankPointsGrid.Columns[4].FillWeight = 105;
            _bankPointsGrid.Columns[5].FillWeight = 115;
            var gridContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
            };
            gridContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            gridContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            gridContainer.Controls.Add(_sourceInfo, 0, 0);
            gridContainer.Controls.Add(_bankPointsGrid, 0, 1);

            // Панель выбора типа изодаты для поперечного разреза.
            var typeBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
            };
            typeBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            typeBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            var typeCaption = new Label
            {
                Text = "Тип изодаты для разреза:",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ModernTheme.TextSub,
                BackColor = Color.Transparent
            };
            _izodataTypeCombo = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            ModernTheme.ApplyToCombo(_izodataTypeCombo);
            _izodataTypeCombo.Items.AddRange(new object[]
            {
                "Глубина", "Скорость", "Расчётная осушка", "Фактическая осушка"
            });
            _izodataTypeCombo.SelectedIndex = 0;
            _izodataTypeCombo.SelectedIndexChanged += (_, _) =>
            {
                _selectedIzodataType = IzodataTypeForIndex(_izodataTypeCombo.SelectedIndex);
                _crossSectionImage?.Invalidate();
            };
            typeBar.Controls.Add(typeCaption, 0, 0);
            typeBar.Controls.Add(_izodataTypeCombo, 1, 0);

            // Поперечный разрез створа (зеркальные берега) по выбранному типу изодаты.
            _crossSectionImage = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(3, 6, 3, 3)
            };
            _crossSectionImage.Paint += (_, e) =>
                DrawSelectedCrossSection(e.Graphics, _crossSectionImage.ClientRectangle);

            detailLayout.Controls.Add(_sectionTitle, 0, 0);
            detailLayout.Controls.Add(_sectionInfo, 0, 1);
            detailLayout.Controls.Add(_sectionParameters, 0, 2);
            detailLayout.Controls.Add(typeBar, 0, 3);
            detailLayout.Controls.Add(_crossSectionImage, 0, 4);
            detailLayout.Controls.Add(gridContainer, 0, 5);
            details.Controls.Add(detailLayout);
            layout.Controls.Add(details, 1, 0);
            return layout;
        }

        // --- Блоки правой/нижней части вкладки результатов ---

        // Круговая диаграмма баланса водопользования + редактируемая таблица долей.
        private GroupBox BuildBalanceBlock()
        {
            var box = MakeGroup("Баланс водопользования");
            _balanceGrid = MakeEditableGrid("Отрасль", "Доля, %");
            foreach (var row in new[] { new[] { "Выработка электроэнергии", "65" }, new[] { "Водоснабжение", "20" }, new[] { "Судоходство", "10" }, new[] { "Прочие нужды", "5" } })
                _balanceGrid.Rows.Add(row);
            _balanceChart = new Panel { Dock = DockStyle.Fill };
            _balanceChart.Paint += (_, e) => DrawBalanceChart(e.Graphics, _balanceChart.ClientRectangle);
            _balanceGrid.CellValueChanged += (_, _) => _balanceChart.Invalidate();
            _balanceGrid.CellEndEdit += (_, _) => _balanceChart.Invalidate();
            box.Controls.Add(_balanceChart);
            return box;
        }

        // Таблица долей баланса (для вкладки «Tabular data»).
        private GroupBox BuildBalanceDataBlock()
        {
            var box = MakeGroup("Баланс: входные доли, %");
            box.Controls.Add(_balanceGrid);
            return box;
        }

        // Таблица эксплуатационных характеристик по периодам.
        private GroupBox BuildOperationBlock()
        {
            var box = MakeGroup("Эксплуатационные характеристики");
            _operationGrid = MakeEditableGrid("Период", "Q ср, м³/с", "Q макс, м³/с", "Q мин, м³/с", "ВБ, м", "НБ, м");
            foreach (var period in new[] { "Весенний паводок", "Летняя межень", "Осенний паводок", "Зимняя межень" })
                _operationGrid.Rows.Add(period, "", "", "", "", "");
            _operationGrid.Columns[0].FillWeight = 190;
            foreach (var c in new[] { 1, 2, 3 }) _operationGrid.Columns[c].FillWeight = 110;
            box.Controls.Add(_operationGrid);
            return box;
        }

        // Столбчатый график выработки по месяцам.
        private GroupBox BuildGenerationBlock()
        {
            var box = MakeGroup("Выработка, млн кВт·ч");
            _generationChart = new Panel { Dock = DockStyle.Fill };
            _generationChart.Paint += (_, e) => DrawGenerationChart(e.Graphics, _generationChart.ClientRectangle);
            box.Controls.Add(_generationChart);
            return box;
        }

        // Ввод месячных данных (расход, напор, выработка) для графика выше.
        private GroupBox BuildGenerationDataBlock()
        {
            var box = MakeGroup("Помесячные данные выработки");
            _generationGrid = MakeEditableGrid("Месяц", "Расход, м³/с", "Напор, м", "Выработка, млн кВт·ч");
            foreach (var month in new[] { "Янв", "Фев", "Мар", "Апр", "Май", "Июн", "Июл", "Авг", "Сен", "Окт", "Ноя", "Дек" })
                _generationGrid.Rows.Add(month, "", "", "");
            _generationGrid.CellValueChanged += (_, _) => _generationChart.Invalidate();
            _generationGrid.CellEndEdit += (_, _) =>
            {
                UpdateMainResults();
                _generationChart.Invalidate();
            };
            box.Controls.Add(_generationGrid);
            return box;
        }

        // Гидравлический профиль (слева — рисунок схемы, справа — таблица параметров створа).
        private GroupBox BuildHydraulicBlock()
        {
            var box = MakeGroup("Гидравлический профиль");
            box.Height = 340;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2, BackColor = Color.Transparent };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
            _hydraulicProfileImage = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _hydraulicProfileImage.Paint += (_, e) => DrawHydraulicProfile(e.Graphics, _hydraulicProfileImage.ClientRectangle);
            // Lc / Zб / Kgm — параметры съёмки, не считаются приложением: даём редактировать их вручную
            // и сохраняем в таблицу cross_sections (Upsert через SaveCrossSectionAsync).
            _hydraulicGrid = MakeEditableGrid("Параметр", "Значение", "Ед.");
            _hydraulicGrid.Columns[0].ReadOnly = true;
            _hydraulicGrid.Columns[2].ReadOnly = true;
            _hydraulicGrid.Rows.Add("Lc", "", "м");
            _hydraulicGrid.Rows.Add("Zб", "", "м");
            _hydraulicGrid.Rows.Add("Hб", "—", "м");
            _hydraulicGrid.Rows.Add("Bб", "—", "м");
            _hydraulicGrid.Rows.Add("Vб", "—", "м/с");
            _hydraulicGrid.Rows.Add("Kgm", "", "—");
            // Hб/Bб/Vб — результаты расчёта, значение фиксируем «только для чтения».
            for (var i = 2; i <= 4; i++) _hydraulicGrid.Rows[i].Cells[1].ReadOnly = true;
            foreach (var i in new[] { 0, 1, 5 })
            {
                _hydraulicGrid.Rows[i].Cells[1].Style.BackColor = Color.FromArgb(96, 66, 14);
                _hydraulicGrid.Rows[i].Cells[1].Style.ForeColor = ModernTheme.TextMain;
                _hydraulicGrid.Rows[i].Cells[1].Style.SelectionBackColor = ModernTheme.Cyan700;
                _hydraulicGrid.Rows[i].Cells[1].Style.SelectionForeColor = Color.White;
            }
            _hydraulicGrid.CellEndEdit += OnHydraulicCellEndEdit;
            layout.Controls.Add(_hydraulicProfileImage, 0, 0);
            layout.Controls.Add(_hydraulicGrid, 1, 0);
            box.Controls.Add(layout);
            return box;
        }

        // --- Общие фабрики контролов ---

        // Блок исходных данных с заголовком: подписи слева, поля/значения справа.
        private GroupBox BuildSourceBlock(string title, string[] names, string[] keys)
        {
            var box = MakeGroup(title);
            box.Height = Math.Max(190, 54 + names.Length * 32);
            box.Controls.Add(MakeInfoTable(names, keys));
            return box;
        }

        // Таблица «параметр → значение». Для справльных полей — read-only TextBox,
        // для числовых вводимых — редактируемый TextBox с живой валидацией/пересчётом.
        private TableLayoutPanel MakeInfoTable(string[] names, string[] keys)
        {
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = names.Length, Padding = new Padding(6, 2, 6, 2), BackColor = Color.Transparent };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            for (var i = 0; i < names.Length; i++)
            {
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
                table.Controls.Add(new Label
                {
                    Text = names[i],
                    Dock = DockStyle.Fill,
                    Font = ModernTheme.FontMain,
                    Padding = new Padding(2, 4, 6, 2),
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true,
                    ForeColor = ModernTheme.TextSub,
                    BackColor = Color.Transparent
                }, 0, i);
                if (keys[i] == "ObjectType" || keys[i] == "ObjectName" ||
                    keys[i] == "River" || keys[i] == "CommissioningDate" ||
                    keys[i] == "Owner")
                {
                    var value = new TextBox
                    {
                        Text = "Не заполнено",
                        Dock = DockStyle.Fill,
                        Padding = new Padding(4),
                        Multiline = false,
                        ReadOnly = true,
                        Cursor = Cursors.Default,
                        BackColor = ModernTheme.Background,
                        ForeColor = ModernTheme.TextMain,
                        BorderStyle = BorderStyle.FixedSingle,
                        Font = ModernTheme.FontMain,
                    };
                    value.Enter += (s, e) => value.SelectionStart = 0;
                    _sourceValues[keys[i]] = value;
                    table.Controls.Add(value, 1, i);
                }
                else
                {
                    var input = new ModernTextBox
                    {
                        Dock = DockStyle.Fill,
                        Multiline = false,
                        Height = 28
                    };
                    input.TextChanged += (_, _) =>
                    {
                        ValidateSourceInputs();
                        UpdateInstructionTables();
                        UpdateMainResults();
                        _hydraulicProfileImage?.Invalidate();
                    };
                    _sourceInputs[keys[i]] = input;
                    table.Controls.Add(input, 1, i);
                }
            }
            return table;
        }

        // Каркас DataGridView с набором колонок по заголовкам (тёмная тема).
        private static DataGridView MakeReadOnlyGrid(params string[] columns)
        {
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BorderStyle = BorderStyle.None };
            ModernTheme.ApplyToGrid(grid);
            foreach (var column in columns)
                grid.Columns.Add(column, column);
            return grid;
        }

        // Тот же грид, но разрешающий редактирование и ввод строк.
        private static DataGridView MakeEditableGrid(params string[] columns)
        {
            var grid = MakeReadOnlyGrid(columns);
            grid.ReadOnly = false;
            grid.AllowUserToAddRows = false;
            return grid;
        }

        private static void AddGridRow(DataGridView grid, params string[] values) => grid.Rows.Add(values);

        // Список створов (дерево). Выбор обрабатывается в OnSectionChanged (MainForm.Sections.cs).
        private GroupBox BuildSectionsGroup()
        {
            var box = MakeGroup("Список створов");
            _sectionsTree = new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false,
                FullRowSelect = true,
            };
            ModernTheme.ApplyToTree(_sectionsTree);
            _sectionsTree.AfterSelect += OnSectionChanged;
            box.Controls.Add(_sectionsTree);
            return box;
        }

        // Группы «Основные данные»/«Расчётные характеристики»/«Информация об объекте» —
        // зарезервированы под старый макет; оставлены для совместимости с сборкой полей.
        private GroupBox BuildBasicDataGroup()
        {
            var box = MakeGroup("Основные данные");
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(8) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
            _sectionTitle = AddField(grid, 0, "Створ:", "-");
            AddField(grid, 1, "Номер:", "-");
            AddField(grid, 2, "Точек слева:", "-");
            AddField(grid, 3, "Точек справа:", "-");
            AddField(grid, 4, "Исходный файл:", "-");
            _sourceInfo = AddField(grid, 5, "Состояние:", "Ожидание данных");
            box.Controls.Add(grid);
            return box;
        }

        private GroupBox BuildResultsGroup()
        {
            var box = MakeGroup("Расчётные характеристики");
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(8) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            _resultInfo.Add(AddField(grid, 0, "Максимальная глубина:", "-"));
            _resultInfo.Add(AddField(grid, 1, "Максимальная скорость:", "-"));
            _resultInfo.Add(AddField(grid, 2, "Уровень воды:", "-"));
            _resultInfo.Add(AddField(grid, 3, "Ширина затопления слева:", "-"));
            _resultInfo.Add(AddField(grid, 4, "Ширина затопления справа:", "-"));
            _resultInfo.Add(AddField(grid, 5, "Площадь затопления:", "-"));
            box.Controls.Add(grid);
            return box;
        }

        private GroupBox BuildInfoGroup()
        {
            var box = MakeGroup("Информация об объекте");
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            var picture = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(4), BorderStyle = BorderStyle.FixedSingle };
            _sectionInfo = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Выберите створ слева.\n\nЗдесь появятся краткие сведения и результаты расчёта.",
                Padding = new Padding(8),
                AutoSize = false,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            layout.Controls.Add(picture, 0, 0);
            layout.Controls.Add(new Label
            {
                Text = "Гидротехническое сооружение",
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 8, 0, 0),
                Font = new Font(Font, System.Drawing.FontStyle.Bold),
                AutoSize = false
            }, 0, 1);
            layout.Controls.Add(_sectionInfo, 0, 2);
            box.Controls.Add(layout);
            return box;
        }

        // Стандартная «коробка» с заголовком и отступами — базовый контейнер всех блоков.
        private GroupBox MakeGroup(string text)
        {
            return new ModernGroupBox { Text = text, Dock = DockStyle.Fill, Padding = new Padding(12, 36, 12, 12), Margin = new Padding(4, 6, 4, 4) };
        }

        // Внутренний TabControl в тёмной теме.
        private static TabControl MakeDarkTabs()
        {
            return new TabControl
            {
                Dock = DockStyle.Fill,
                BackColor = ModernTheme.Background,
                ForeColor = ModernTheme.TextSub,
                Font = ModernTheme.FontBold,
                Padding = new Point(12, 5),
            };
        }

        // Добавляет в таблицу строку «подпись + значение» и возвращает Label значения.
        private static Label AddField(TableLayoutPanel grid, int row, string title, string value)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            grid.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 6, 4, 0),
                ForeColor = ModernTheme.TextSub,
                BackColor = Color.Transparent
            }, 0, row);
            var result = new Label
            {
                Text = value,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(6, 5, 4, 0),
                BackColor = ModernTheme.Surface,
                ForeColor = ModernTheme.TextMain
            };
            grid.Controls.Add(result, 1, row);
            return result;
        }

        // Кнопка панели действий внизу окна.
        private Button MakeActionButton(string text, EventHandler handler, bool primary = true, Color? accent = null)
        {
            var button = ModernTheme.MakeButton(text, primary: primary, height: 36);
            button.AutoSize = true;
            if (accent.HasValue && button is ModernButton modern)
                modern.AccentColor = accent.Value;
            button.Click += handler;
            return button;
        }
    }
}
