#nullable enable

using FloodZoneCalculator.Application;
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
    public sealed class CalculationForm : Form
    {
        private readonly IExcelReader _reader = new ClosedXmlExcelReader();
        private readonly IFloodCalculator _calc;
        private readonly IReportGenerator _report = new TextReportGenerator();
        private readonly long _objectId;
        private readonly FloodZoneConnection _database;
        private ObjectProfile _objectProfile = new();
        private DatabaseObject? _currentObject;

        private List<CrossSection> _sections = new();
        private List<CrossSectionRecord> _databaseCrossSections = new();
        private readonly Dictionary<int, HydraulicProfileRenderModel> _profileModels = new();
        private string _sourceFile = "";

        private TreeView _sectionsTree = null!;
        private DataGridView _bankPointsGrid = null!;
        private Label _sectionTitle = null!;
        private Label _sectionInfo = null!;
        private Label _sectionParameters = null!;
        private Label _sourceInfo = null!;
        private readonly List<Label> _resultInfo = new();
        private DataGridView _mainResultsGrid = null!;
        private DataGridView _flowProvisionGrid = null!;
        private DataGridView _balanceGrid = null!;
        private DataGridView _operationGrid = null!;
        private DataGridView _generationGrid = null!;
        private DataGridView _hydraulicGrid = null!;
        private Panel _balanceChart = null!;
        private Panel _flowChart = null!;
        private Panel _provisionCurveChart = null!;
        private Panel _generationChart = null!;
        private readonly Dictionary<string, Control> _sourceValues = new();
        private readonly Dictionary<string, TextBox> _sourceInputs = new();
        private PictureBox _schemeImage = null!;
        private PictureBox _hydraulicProfileImage = null!;
        private readonly ErrorProvider _sourceErrors = new();

        private Label _statusBar = null!;
        private string _statusLastMessage = "Готово";

        private const string StatusPrefix = "Пользователь: admin    Режим: Просмотр | ";

        public CalculationForm(long objectId, FloodZoneConnection database)
        {
            _objectId = objectId;
            _database = database;
            _calc = new FloodCalculator(new LinearInterpolator());

            Text = "Расчёт зоны затопления";
            Width = 1600;
            Height = 1000;
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new System.Drawing.Size(1200, 700);

            BuildUi();
            Shown += async (s, e) =>
            {
                await LoadObjectSummaryAsync();
                await LoadSavedCalculationAsync();
            };
        }

        // ------------------------------------------------------------------- UI

        private void BuildUi()
        {
            // Заголовок — обычный текст, стандартный вид окна
            var titleLabel = new Label
            {
                Text = "FloodZoneCalculator — расчёт зоны затопления",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.Black,
                AutoSize = true,
                Padding = new Padding(16, 8, 0, 0)
            };
            Controls.Add(titleLabel);

            // Строка статуса внизу
            _statusBar = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                AutoSize = false,
                Padding = new Padding(10, 0, 0, 0)
            };
            _statusBar.Text = StatusPrefix + _statusLastMessage + "  " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            var timeTimer = new Timer { Interval = 1000 };
            timeTimer.Tick += (s, e) =>
                _statusBar.Text = StatusPrefix + _statusLastMessage + "  " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            timeTimer.Start();
            Controls.Add(_statusBar);

            // Панель действий
            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8, 6, 8, 6),
                AutoSize = false
            };
            actions.Controls.Add(MakeActionButton("Начало", (s, e) => SelectSection(0)));
            actions.Controls.Add(MakeActionButton("Назад", (s, e) => SelectRelativeSection(-1)));
            actions.Controls.Add(new Label { Text = "Навигация по расчётам", AutoSize = true, Padding = new Padding(4, 8, 12, 0), ForeColor = Color.Gray });
            actions.Controls.Add(MakeActionButton("Вперёд", (s, e) => SelectRelativeSection(1)));
            actions.Controls.Add(MakeActionButton("Конец", (s, e) => SelectSection(int.MaxValue)));
            actions.Controls.Add(MakeActionButton("К списку расчётов", OnBackToObjectsClick, primary: false));
            actions.Controls.Add(new Label { AutoSize = true, Width = 18 });
            actions.Controls.Add(MakeActionButton("Импорт Excel", OnOpenClick));
            actions.Controls.Add(MakeActionButton("Рассчитать", OnComputeClick));
            actions.Controls.Add(MakeActionButton("Экспорт отчёта", OnReportClick));
            actions.Controls.Add(MakeActionButton("Печать", OnPrintReportClick));
            var save = MakeActionButton("Сохранить расчёт", async (s, e) => await SaveCalculationAsync(), primary: true);
            actions.Controls.Add(save);
            actions.Controls.Add(MakeActionButton("Закрыть", OnBackToObjectsClick, primary: false));

            // Стандартный таб-контроллер
            var mainTabs = new TabControl { Dock = DockStyle.Fill };
            var resultsTab = new TabPage("Результаты расчёта");
            var sourceTab = new TabPage("Данные объекта");
            var sectionsTab = new TabPage("Створы и точки берегов");
            resultsTab.Controls.Add(BuildResultsWorkspace());
            sourceTab.Controls.Add(BuildSourceWorkspace());
            sectionsTab.Controls.Add(BuildCrossSectionsWorkspace());
            mainTabs.TabPages.AddRange(new[] { resultsTab, sourceTab, sectionsTab });

            Controls.Add(mainTabs);
            Controls.Add(actions);
            Controls.Add(_statusBar);
            Controls.Add(titleLabel);
        }


        private Control BuildResultsWorkspace()
        {
            var innerTabs = new TabControl { Dock = DockStyle.Fill };
            var chartsTab = new TabPage("Графики");
            var dataTab = new TabPage("Табличные данные");
            var dashboard = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(2)
            };
            dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 54F));
            dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 46F));

            var topRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
            topRow.Controls.Add(BuildHydraulicBlock(), 0, 0);
            topRow.Controls.Add(BuildBalanceBlock(), 1, 0);

            var bottomRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
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
                Padding = new Padding(4)
            };
            data.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            data.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            var summary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            summary.Controls.Add(BuildResultsTable(), 0, 0);
            summary.Controls.Add(BuildBalanceDataBlock(), 1, 0);
            data.Controls.Add(summary, 0, 0);
            data.Controls.Add(BuildGenerationDataBlock(), 0, 1);
            dataTab.Controls.Add(data);

            innerTabs.TabPages.Add(chartsTab);
            innerTabs.TabPages.Add(dataTab);
            return innerTabs;
        }

        private Control BuildResultsTable()
        {
            var box = MakeGroup("РЕЗУЛЬТАТЫ РАСЧЁТА / Основные результаты");
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

        private Control BuildSourceWorkspace()
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(4) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            var objectBox = MakeGroup("Исходные данные объекта");
            objectBox.Height = 180;
            objectBox.Controls.Add(MakeInfoTable(
                new[] { "Тип объекта", "Наименование", "Река", "Дата ввода", "Собственник" },
                new[] { "ObjectType", "ObjectName", "River", "CommissioningDate", "Owner" }));
            layout.Controls.Add(objectBox, 0, 0);
            var scheme = MakeGroup("Схема объекта");
            scheme.Height = 180;
            var schemeLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            _schemeImage = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
            var schemeButton = new Button
            {
                Text = "Добавить изображение схемы",
                Dock = DockStyle.Top,
                Height = 34
            };
            schemeButton.Click += OnChooseSchemeClick;
            schemeLayout.Controls.Add(_schemeImage, 0, 0);
            schemeLayout.Controls.Add(schemeButton, 0, 1);
            scheme.Controls.Add(schemeLayout);
            layout.Controls.Add(scheme, 1, 0);
            layout.Controls.Add(BuildSourceBlock("Гидрологические данные",
                new[] { "Площадь водосбора", "Среднегодовой сток", "Максимальный расход", "Минимальный расход", "Обеспеченность расчёта" },
                new[] { "CatchmentArea", "AvgRunoff", "MaxFlow", "MinFlow", "Provision" }), 0, 1);
            layout.Controls.Add(BuildSourceBlock("Конструктивные параметры",
                new[] { "Тип плотины", "Длина плотины", "Макс. высота плотины", "Пропускная способность водосброса", "Количество водосбросов", "Количество агрегатов", "Установленная мощность" },
                new[] { "DamType", "DamLengthM", "DamMaxHeightM", "SpillwayCapacityM3s", "SpillwayOpeningsCount", "UnitsCount", "DesignPowerMw" }), 1, 1);
            layout.Controls.Add(BuildSourceBlock("Исходные расчётные параметры",
                new[] { "Нормальный подпорный уровень (НПУ)", "Минимальный уровень (УНБ)", "Уровень мёртвого объёма (УМО)", "Полный объём водохранилища", "Полезный объём водохранилища", "Коэффициент шероховатости (n)", "Температура воды", "КПД гидроузла", "Коэффициент надёжности" },
                new[] { "NormalHeadwaterM", "MinWaterLevelM", "DeadVolumeLevelM", "ReservoirFullVolume", "ReservoirUsefulVolume", "RoughnessCoeff", "WaterTemperature", "EfficiencyPct", "ReliabilityCoefficient" }), 0, 2);
            var provisions = MakeGroup("Изодаты (кривые обеспеченности)");
            provisions.Height = 460;
            _flowProvisionGrid = MakeEditableGrid("Обеспеченность, %", "Расход, м³/с");
            foreach (var probability in new[] { "0,1", "1", "2", "5", "10", "20", "50", "90", "99" })
                _flowProvisionGrid.Rows.Add(probability, "");
            _flowChart = new Panel { Dock = DockStyle.Bottom, Height = 190 };
            _flowChart.Paint += (_, e) => DrawFlowChart(e.Graphics, _flowChart.ClientRectangle, false);
            _flowProvisionGrid.Dock = DockStyle.Top;
            _flowProvisionGrid.Height = 250;
            _flowProvisionGrid.CellValueChanged += (_, _) => _flowChart.Invalidate();
            provisions.Controls.Add(_flowChart);
            provisions.Controls.Add(_flowProvisionGrid);
            layout.Controls.Add(provisions, 1, 2);
            var curve = MakeGroup("Кривая обеспеченности расходов");
            curve.Height = 240;
            _provisionCurveChart = new Panel { Dock = DockStyle.Fill };
            _provisionCurveChart.Paint += (_, e) => DrawFlowChart(e.Graphics, _provisionCurveChart.ClientRectangle, true);
            curve.Controls.Add(_provisionCurveChart);
            layout.Controls.Add(curve, 0, 3);
            layout.SetColumnSpan(curve, 2);

            var saveButton = new Button
            {
                Text = "Сохранить введённые исходные данные в БД",
                Dock = DockStyle.Top,
                Height = 36
            };
            saveButton.Click += async (s, e) => await SaveSourceDataAsync();
            layout.Controls.Add(saveButton, 0, 4);
            layout.SetColumnSpan(saveButton, 2);
            scroll.Controls.Add(layout);
            return scroll;
        }

        private Control BuildCrossSectionsWorkspace()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(4)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 74F));

            layout.Controls.Add(BuildSectionsGroup(), 0, 0);

            var details = MakeGroup("Точки выбранного створа");
            var detailLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8)
            };
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _sectionTitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Выберите створ в списке",
                Font = new Font(Font, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _sectionInfo = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Количество точек левого и правого берега.",
                TextAlign = ContentAlignment.MiddleLeft
            };
            _sectionParameters = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Параметры створа появятся после выбора.",
                TextAlign = ContentAlignment.MiddleLeft
            };
            _sourceInfo = new Label
            {
                Dock = DockStyle.Top,
                Text = "Данные не загружены",
                TextAlign = ContentAlignment.MiddleLeft,
                Height = 22
            };
            _bankPointsGrid = MakeReadOnlyGrid(
                "Источник", "Берег", "Тип данных", "№ точки", "Расстояние, м", "Значение / отметка");
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
                RowCount = 2
            };
            gridContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            gridContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            gridContainer.Controls.Add(_sourceInfo, 0, 0);
            gridContainer.Controls.Add(_bankPointsGrid, 0, 1);
            detailLayout.Controls.Add(_sectionTitle, 0, 0);
            detailLayout.Controls.Add(_sectionInfo, 0, 1);
            detailLayout.Controls.Add(_sectionParameters, 0, 2);
            detailLayout.Controls.Add(gridContainer, 0, 3);
            details.Controls.Add(detailLayout);
            layout.Controls.Add(details, 1, 0);
            return layout;
        }

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

        private GroupBox BuildBalanceDataBlock()
        {
            var box = MakeGroup("Баланс: исходные доли, %");
            box.Controls.Add(_balanceGrid);
            return box;
        }

        private GroupBox BuildOperationBlock()
        {
            var box = MakeGroup("Режимные характеристики");
            _operationGrid = MakeEditableGrid("Период", "Q ср, м³/с", "Q макс, м³/с", "Q мин, м³/с", "УВБ, м", "УНБ, м");
            foreach (var period in new[] { "Весенний паводок", "Летняя межень", "Осенний паводок", "Зимняя межень" })
                _operationGrid.Rows.Add(period, "", "", "", "", "");
            _operationGrid.Columns[0].FillWeight = 145;
            box.Controls.Add(_operationGrid);
            return box;
        }

        private GroupBox BuildGenerationBlock()
        {
            var box = MakeGroup("Выработка электроэнергии, млн кВт·ч");
            _generationChart = new Panel { Dock = DockStyle.Fill };
            _generationChart.Paint += (_, e) => DrawGenerationChart(e.Graphics, _generationChart.ClientRectangle);
            box.Controls.Add(_generationChart);
            return box;
        }

        private GroupBox BuildGenerationDataBlock()
        {
            var box = MakeGroup("Месячные исходные данные выработки");
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

        private GroupBox BuildHydraulicBlock()
        {
            var box = MakeGroup("Гидравлический профиль");
            box.Height = 340;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
            _hydraulicProfileImage = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _hydraulicProfileImage.Paint += (_, e) => DrawHydraulicProfile(e.Graphics, _hydraulicProfileImage.ClientRectangle);
            _hydraulicGrid = MakeReadOnlyGrid("Параметр", "Значение", "Ед. изм.");
            _hydraulicGrid.Rows.Add("Lc", "—", "м");
            _hydraulicGrid.Rows.Add("Zб", "—", "м");
            _hydraulicGrid.Rows.Add("Hб", "—", "м");
            _hydraulicGrid.Rows.Add("Bб", "—", "м");
            _hydraulicGrid.Rows.Add("Vб", "—", "м/с");
            _hydraulicGrid.Rows.Add("Kgm", "—", "—");
            layout.Controls.Add(_hydraulicProfileImage, 0, 0);
            layout.Controls.Add(_hydraulicGrid, 1, 0);
            box.Controls.Add(layout);
            return box;
        }

        private static DataGridView MakeEditableGrid(params string[] columns)
        {
            var grid = MakeReadOnlyGrid(columns);
            grid.ReadOnly = false;
            grid.AllowUserToAddRows = false;
            return grid;
        }

        private void DrawHydraulicProfile(Graphics graphics, Rectangle bounds)
        {
            graphics.Clear(Color.White);
            if (bounds.Width < 120 || bounds.Height < 100)
                return;

            var hasUpper = TryParseDecimal(_objectProfile.NormalHeadwaterM, out var upperValue);
            var hasLower = TryParseDecimal(_objectProfile.MinWaterLevelM, out var lowerValue);
            var hasBottom = TryParseDecimal(_objectProfile.DeadVolumeLevelM, out var bottomValue);
            var bottom = hasBottom ? (double)bottomValue : 0;
            var upper = hasUpper ? (double)upperValue : bottom + 10;
            var lower = hasLower ? (double)lowerValue : bottom + (upper - bottom) * 0.35;
            if (upper <= bottom)
                upper = bottom + 10;
            if (lower < bottom || lower > upper)
                lower = bottom + (upper - bottom) * 0.35;

            var plot = new Rectangle(18, 30, Math.Max(40, bounds.Width - 36), Math.Max(40, bounds.Height - 78));
            var waterLevelY = MapLevel(upper, bottom, upper, plot);
            var lowerLevelY = MapLevel(lower, bottom, upper, plot);
            var groundY = plot.Bottom - plot.Height * 0.13f;
            var damLeft = plot.Left + plot.Width * 0.36f;
            var damRight = plot.Left + plot.Width * 0.66f;
            var crestY = plot.Top + plot.Height * 0.16f;
            var channelBottom = plot.Bottom - plot.Height * 0.05f;

            using (var water = new SolidBrush(Color.FromArgb(125, 200, 235)))
            {
                graphics.FillRectangle(water, plot.Left, waterLevelY, damLeft - plot.Left, channelBottom - waterLevelY);
                graphics.FillRectangle(water, damRight, lowerLevelY, plot.Right - damRight, channelBottom - lowerLevelY);
            }

            using (var soil = new SolidBrush(Color.FromArgb(177, 132, 86)))
            using (var soilEdge = new Pen(Color.FromArgb(93, 70, 51), 1.2f))
            {
                var leftBank = new[]
                {
                    new PointF(plot.Left, groundY),
                    new PointF(damLeft - 12, groundY),
                    new PointF(damLeft - 3, channelBottom),
                    new PointF(plot.Left, channelBottom)
                };
                var rightBank = new[]
                {
                    new PointF(damRight + 12, channelBottom),
                    new PointF(damRight + 22, groundY),
                    new PointF(plot.Right, groundY),
                    new PointF(plot.Right, channelBottom)
                };
                graphics.FillPolygon(soil, leftBank);
                graphics.FillPolygon(soil, rightBank);
                graphics.DrawLines(soilEdge, leftBank.Take(3).ToArray());
                graphics.DrawLines(soilEdge, rightBank.Take(3).ToArray());
            }

            using (var concrete = new SolidBrush(Color.FromArgb(126, 134, 143)))
            using (var outline = new Pen(Color.FromArgb(75, 85, 99), 1.5f))
            {
                var dam = new[]
                {
                    new PointF(damLeft, crestY),
                    new PointF(damRight, crestY),
                    new PointF(damRight + 14, channelBottom),
                    new PointF(damLeft - 14, channelBottom)
                };
                graphics.FillPolygon(concrete, dam);
                graphics.DrawPolygon(outline, dam);

                var gateTop = crestY + 9;
                var gateBottom = Math.Max(gateTop + 4, MapLevel(upper, bottom, upper, plot) + plot.Height * 0.2f);
                using var gateFill = new SolidBrush(Color.FromArgb(235, 241, 245));
                using var gateLine = new Pen(Color.FromArgb(75, 85, 99), 1);
                var gateCount = Math.Max(2, Math.Min(5,
                    (int)ParseDoubleOrZero(_objectProfile.SpillwayOpeningsCount)));
                for (var i = 1; i <= gateCount; i++)
                {
                    var center = damLeft + (damRight - damLeft) * i / (gateCount + 1);
                    var gate = new RectangleF(center - 7, gateTop, 14, gateBottom - gateTop);
                    graphics.FillRectangle(gateFill, gate);
                    graphics.DrawRectangle(gateLine, gate.X, gate.Y, gate.Width, gate.Height);
                }
                graphics.DrawLine(outline, damLeft - 6, crestY - 3, damRight + 6, crestY - 3);
            }

            using (var flow = new Pen(Color.FromArgb(37, 99, 235), 2))
            {
                graphics.DrawLine(flow, damLeft - 34, waterLevelY + 8, damLeft - 8, waterLevelY + 8);
                graphics.DrawLine(flow, damRight + 8, lowerLevelY + 8, damRight + 34, lowerLevelY + 8);
            }

            using var axis = new Pen(Color.DarkGray, 1);
            graphics.DrawLine(axis, plot.Left, channelBottom, plot.Right, channelBottom);
            DrawProfileLabel(graphics, "УВБ " + (hasUpper ? upper.ToString("N2") + " м" : "—"), plot.Left + 4, waterLevelY - 22);
            DrawProfileLabel(graphics, "УНБ " + (hasLower ? lower.ToString("N2") + " м" : "—"), damRight + 14, lowerLevelY - 22);
            var damLength = FormatOptional(_objectProfile.DamLengthM);
            DrawProfileLabel(graphics, "Длина плотины: " + (damLength == "—" ? "—" : damLength + " м"),
                damLeft, channelBottom + 5);
            TextRenderer.DrawText(graphics, "Верхний бьеф", Font, new Point(plot.Left, plot.Top + 2), Color.FromArgb(31, 41, 55));
            TextRenderer.DrawText(graphics, "Нижний бьеф", Font, new Point((int)damRight + 18, plot.Top + 2), Color.FromArgb(31, 41, 55));
            TextRenderer.DrawText(graphics, "Схема гидроузла", Font, new Point(plot.Left + plot.Width / 2 - 48, plot.Bottom + 25), Color.FromArgb(107, 114, 128));
        }

        private void DrawHydraulicProfileFromDatabase(Graphics graphics, Rectangle bounds)
        {
            graphics.Clear(Color.White);
            var model = GetSelectedProfileModel();
            if (model != null && model.Points.Count > 0)
            {
                DrawDatabaseProfile(graphics, bounds, model);
                return;
            }

            if (_sectionsTree.SelectedNode?.Tag is CrossSection section &&
                (section.GetPoints(Bank.Left, IzodataType.Depth).Count > 0 ||
                 section.GetPoints(Bank.Right, IzodataType.Depth).Count > 0))
            {
                DrawImportedDepthCurves(graphics, bounds, section);
                return;
            }

            graphics.DrawString(
                "Нет данных для графика. Импортируйте Excel с изодатами глубины.",
                Font, Brushes.DimGray, 16, 16);
        }

        private void DrawDatabaseProfile(
            Graphics graphics,
            Rectangle bounds,
            HydraulicProfileRenderModel model)
        {
            var points = model.Points.ToList();
            var minX = Math.Min(0, points.Min(point => point.DistanceM));
            var maxX = Math.Max(1, points.Max(point => point.DistanceM));
            var minY = points.Min(point => point.ElevationM);
            var maxY = points.Max(point => point.ElevationM);
            if (Math.Abs(maxY - minY) < 0.001) { maxY += 1; minY -= 1; }
            var plot = new Rectangle(58, 24, Math.Max(40, bounds.Width - 86), Math.Max(40, bounds.Height - 62));
            Func<double, float> mapX = x => plot.Left + (float)((x - minX) / (maxX - minX) * plot.Width);
            Func<double, float> mapY = y => plot.Bottom - (float)((y - minY) / (maxY - minY) * plot.Height);

            using (var axis = new Pen(Color.FromArgb(75, 85, 99), 1))
            using (var grid = new Pen(Color.LightGray, 1))
            {
                graphics.DrawLine(axis, mapX(0), plot.Top, mapX(0), plot.Bottom);
                graphics.DrawLine(axis, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
                for (var i = 0; i <= 4; i++)
                {
                    var level = minY + (maxY - minY) * i / 4d;
                    var y = mapY(level);
                    graphics.DrawLine(grid, plot.Left, y, plot.Right, y);
                    graphics.DrawString(level.ToString("0.##"), Font, Brushes.DimGray, 4, y - 8);
                }
            }
            DrawProfileLine(graphics, model.Points, mapX, mapY);
            graphics.DrawString("Расстояние от фарватера, м", Font, Brushes.DimGray,
                Math.Max(4, plot.Left + plot.Width / 2 - 70), plot.Bottom + 24);
            graphics.DrawString("Отметка, м", Font, Brushes.DimGray, 4, 4);
        }

        private void DrawImportedDepthCurves(
            Graphics graphics,
            Rectangle bounds,
            CrossSection section)
        {
            var left = section.GetPoints(Bank.Left, IzodataType.Depth)
                .OrderBy(point => point.Distance).ToList();
            var right = section.GetPoints(Bank.Right, IzodataType.Depth)
                .OrderBy(point => point.Distance).ToList();
            var maxDistance = Math.Max(
                left.Count == 0 ? 0 : left.Max(point => point.Distance),
                right.Count == 0 ? 0 : right.Max(point => point.Distance));
            var maxDepth = Math.Max(
                left.Count == 0 ? 0 : left.Max(point => point.Value),
                right.Count == 0 ? 0 : right.Max(point => point.Value));
            if (maxDistance <= 0 || maxDepth <= 0)
            {
                graphics.DrawString(
                    "В Excel нет положительных расстояний и глубин для графика.",
                    Font, Brushes.DimGray, 16, 16);
                return;
            }

            var plot = new Rectangle(58, 34, Math.Max(40, bounds.Width - 86),
                Math.Max(40, bounds.Height - 72));
            float MapX(double distance) =>
                plot.Left + (float)(distance / maxDistance * plot.Width);
            float MapY(double depth) =>
                plot.Bottom - (float)(depth / maxDepth * plot.Height);

            using (var grid = new Pen(Color.FromArgb(220, 226, 232), 1))
            using (var axis = new Pen(Color.FromArgb(75, 85, 99), 1))
            {
                for (var i = 0; i <= 4; i++)
                {
                    var y = plot.Bottom - i * plot.Height / 4f;
                    graphics.DrawLine(grid, plot.Left, y, plot.Right, y);
                    graphics.DrawString(
                        (maxDepth * i / 4d).ToString("0.##", CultureInfo.CurrentCulture),
                        Font, Brushes.DimGray, 4, y - 8);
                }
                graphics.DrawLine(axis, plot.Left, plot.Top, plot.Left, plot.Bottom);
                graphics.DrawLine(axis, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
            }

            DrawIsodataCurve(graphics, left, MapX, MapY, Color.FromArgb(37, 99, 235));
            DrawIsodataCurve(graphics, right, MapX, MapY, Color.FromArgb(249, 115, 22));
            graphics.DrawString(
                $"Створ №{section.Number}: глубина по изодатам Excel",
                Font, Brushes.DimGray, plot.Left, 8);
            graphics.DrawString("Расстояние, м", Font, Brushes.DimGray,
                Math.Max(4, plot.Left + plot.Width / 2 - 35), plot.Bottom + 24);
            graphics.DrawString("Глубина, м", Font, Brushes.DimGray, 4, 4);
            using var leftBrush = new SolidBrush(Color.FromArgb(37, 99, 235));
            using var rightBrush = new SolidBrush(Color.FromArgb(249, 115, 22));
            graphics.FillRectangle(leftBrush, plot.Right - 145, 8, 10, 10);
            graphics.DrawString("Левый берег", Font, Brushes.DimGray, plot.Right - 131, 5);
            graphics.FillRectangle(rightBrush, plot.Right - 65, 8, 10, 10);
            graphics.DrawString("Правый", Font, Brushes.DimGray, plot.Right - 51, 5);
        }

        private static void DrawIsodataCurve(
            Graphics graphics,
            IReadOnlyList<IzodataPoint> points,
            Func<double, float> mapX,
            Func<double, float> mapY,
            Color color)
        {
            if (points.Count == 0)
                return;

            using var pen = new Pen(color, 2);
            using var brush = new SolidBrush(color);
            var mapped = points
                .Select(point => new PointF(mapX(point.Distance), mapY(point.Value)))
                .ToArray();
            if (mapped.Length > 1)
                graphics.DrawLines(pen, mapped);
            foreach (var point in mapped)
                graphics.FillEllipse(brush, point.X - 3, point.Y - 3, 6, 6);
        }

        private static void DrawProfileLine(Graphics graphics, IReadOnlyList<ProfilePoint> points,
            Func<double, float> mapX, Func<double, float> mapY)
        {
            if (points.Count == 0) return;
            using var line = new Pen(Color.FromArgb(30, 105, 160), 2);
            using var pointBrush = new SolidBrush(Color.FromArgb(30, 105, 160));
            for (var i = 1; i < points.Count; i++)
                graphics.DrawLine(line, mapX(points[i - 1].DistanceM), mapY(points[i - 1].ElevationM),
                    mapX(points[i].DistanceM), mapY(points[i].ElevationM));
            foreach (var point in points)
            {
                var x = mapX(point.DistanceM);
                var y = mapY(point.ElevationM);
                graphics.FillEllipse(pointBrush, x - 3, y - 3, 6, 6);
                graphics.DrawString(point.PointNumber.ToString(CultureInfo.InvariantCulture),
                    SystemFonts.DefaultFont, Brushes.Black, x + 4, y - 8);
            }
        }

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

        private void UpdateHydraulicParameters(
            CrossSectionRecord record,
            CrossSection? calculationSection)
        {
            var values = new[]
            {
                FormatOptional(record.DistanceFromHydroUnitM),
                FormatOptional(record.BottomLevelZb),
                FirstAvailable(FormatOptional(record.DepthHb), FormatSectionDepth(calculationSection)),
                FirstAvailable(FormatOptional(record.WidthBb), FormatSectionWidth(calculationSection)),
                FirstAvailable(FormatOptional(record.VelocityVb), FormatSectionVelocity(calculationSection)),
                FormatOptional(record.Kgm)
            };
            for (var i = 0; i < values.Length && i < _hydraulicGrid.Rows.Count; i++)
                _hydraulicGrid.Rows[i].Cells[1].Value = values[i];
        }

        private static string FirstAvailable(string preferred, string fallback) =>
            preferred == "—" ? fallback : preferred;

        private void UpdateHydraulicParameters(CrossSection? section)
        {
            var values = new[]
            {
                "—",
                "—",
                FormatSectionDepth(section),
                FormatSectionWidth(section),
                FormatSectionVelocity(section),
                "—"
            };
            for (var i = 0; i < values.Length && i < _hydraulicGrid.Rows.Count; i++)
                _hydraulicGrid.Rows[i].Cells[1].Value = values[i];
        }

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

        private static int MapLevel(double level, double bottom, double upper, Rectangle plot)
        {
            var fraction = (level - bottom) / Math.Max(0.01, upper - bottom);
            fraction = Math.Max(0, Math.Min(1, fraction));
            return plot.Bottom - (int)(fraction * plot.Height);
        }

        private void DrawProfileLabel(Graphics graphics, string text, float x, float y)
        {
            TextRenderer.DrawText(graphics, text, Font, new Point((int)x, (int)y),
                Color.Black, TextFormatFlags.NoPadding);
        }

        private void DrawBalanceChart(Graphics graphics, Rectangle bounds)
        {
            graphics.Clear(Color.White);
            var values = new List<double>();
            foreach (DataGridViewRow row in _balanceGrid.Rows)
            {
                var text = Convert.ToString(row.Cells[1].Value) ?? "";
                values.Add(TryParseDecimal(text, out var value) && value >= 0 ? (double)value : 0);
            }

            var total = values.Sum();
            int diameter;
            if (total <= 0)
            {
                using var empty = new SolidBrush(Color.LightGray);
                diameter = Math.Max(50, Math.Min(bounds.Height - 24, bounds.Width / 2));
                var pieEmpty = new Rectangle(10, Math.Max(8, (bounds.Height - diameter) / 2), diameter, diameter);
                graphics.FillEllipse(empty, pieEmpty);
                TextRenderer.DrawText(graphics, "Введите доли", Font,
                    new Point(pieEmpty.Left, pieEmpty.Bottom + 2), Color.Gray);
                return;
            }

            var colors = new[] { Color.FromArgb(37, 99, 235), Color.FromArgb(249, 115, 22), Color.FromArgb(34, 197, 94), Color.FromArgb(156, 163, 175) };
            diameter = Math.Max(50, Math.Min(bounds.Height - 24, bounds.Width / 2));
            var pie = new Rectangle(10, Math.Max(8, (bounds.Height - diameter) / 2), diameter, diameter);
            var start = -90f;
            for (var i = 0; i < values.Count; i++)
            {
                var sweep = (float)(values[i] / total * 360);
                using var brush = new SolidBrush(colors[i % colors.Length]);
                graphics.FillPie(brush, pie, start, sweep);
                start += sweep;
            }

            var legendX = pie.Right + 16;
            var legendWidth = Math.Max(10, bounds.Right - legendX - 8);
            for (var i = 0; i < _balanceGrid.Rows.Count; i++)
            {
                var label = Convert.ToString(_balanceGrid.Rows[i].Cells[0].Value) ?? "";
                var percent = values[i] / total * 100;
                var y = Math.Max(8, (bounds.Height - _balanceGrid.Rows.Count * 28) / 2) + i * 28;
                using var brush = new SolidBrush(colors[i % colors.Length]);
                graphics.FillRectangle(brush, legendX, y + 3, 12, 12);
                TextRenderer.DrawText(graphics, label + " " + percent.ToString("0.##") + "%",
                    Font, new Rectangle(legendX + 18, y, legendWidth - 18, 25),
                    Color.FromArgb(31, 41, 55), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private void DrawFlowChart(Graphics graphics, Rectangle bounds, bool logarithmic)
        {
            graphics.Clear(Color.White);
            var points = ReadFlowPoints();
            DrawAxes(graphics, bounds, "Расход, м³/с", logarithmic ? "Обеспеченность, % (лог.)" : "Обеспеченность, %");
            if (points.Count < 2) return;

            var plot = GetPlotRectangle(bounds);
            var minX = logarithmic ? Math.Max(0.01, points.Min(p => p.P)) : 0;
            var maxX = 100d;
            var maxY = Math.Max(1, points.Max(p => p.Q));
            PointF Map(FlowPoint p)
            {
                var x = logarithmic
                    ? (Math.Log10(Math.Max(minX, p.P)) - Math.Log10(minX)) /
                      (Math.Log10(maxX) - Math.Log10(minX))
                    : p.P / maxX;
                return new PointF((float)(plot.Left + x * plot.Width),
                    (float)(plot.Bottom - p.Q / maxY * plot.Height));
            }

            using var pen = new Pen(Color.FromArgb(37, 99, 235), 2);
            var mapped = points.Select(Map).ToArray();
            graphics.DrawLines(pen, mapped);
            using var brush = new SolidBrush(Color.FromArgb(37, 99, 235));
            foreach (var point in mapped)
                graphics.FillEllipse(brush, point.X - 3, point.Y - 3, 6, 6);
        }

        private void DrawGenerationChart(Graphics graphics, Rectangle bounds)
        {
            graphics.Clear(Color.White);
            var plot = GetPlotRectangle(bounds, 28, 12, 24, 34);
            using var gridPen = new Pen(Color.FromArgb(229, 231, 235));
            for (var i = 0; i <= 4; i++)
            {
                var y = plot.Bottom - i * plot.Height / 4f;
                graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            }
            var values = _generationGrid.Rows.Cast<DataGridViewRow>()
                .Select(row => ReadCellNumber(row.Cells[3].Value)).ToArray();
            if (values.All(value => value <= 0))
            {
                TextRenderer.DrawText(
                    graphics,
                    "Заполните месячные значения во вкладке «Табличные данные».",
                    Font,
                    new Rectangle(plot.Left + 8, plot.Top + 12, plot.Width - 16, 42),
                    Color.DimGray);
                return;
            }
            var max = Math.Max(1, values.Max());
            var width = plot.Width / Math.Max(1, values.Length);
            using var brush = new SolidBrush(Color.FromArgb(37, 99, 235));
            using var axisPen = new Pen(Color.FromArgb(107, 114, 128));
            graphics.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
            graphics.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
            for (var i = 0; i < values.Length; i++)
            {
                var gridY = plot.Bottom - i * plot.Height / 4f;
                graphics.DrawString((max * i / 4d).ToString("0.#", CultureInfo.CurrentCulture),
                    Font, Brushes.DimGray, 2, gridY - 8);
                var height = values[i] / max * plot.Height;
                var bar = new RectangleF(plot.Left + i * width + width * .18f,
                    plot.Bottom - (float)height, width * .64f, (float)height);
                graphics.FillRectangle(brush, bar);
                TextRenderer.DrawText(graphics, Convert.ToString(_generationGrid.Rows[i].Cells[0].Value) ?? "",
                    Font, new Rectangle((int)(plot.Left + i * width), plot.Bottom + 2,
                        Math.Max(1, (int)width), 18), Color.FromArgb(55, 65, 81),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
            }
            TextRenderer.DrawText(graphics, "Месяц", Font, new Point(plot.Left + plot.Width / 2 - 20, plot.Bottom + 19),
                Color.FromArgb(31, 41, 55));
        }

        private List<FlowPoint> ReadFlowPoints()
        {
            return _flowProvisionGrid.Rows.Cast<DataGridViewRow>()
                .Select(row => new FlowPoint(ReadCellNumber(row.Cells[0].Value), ReadCellNumber(row.Cells[1].Value)))
                .Where(point => point.P > 0 && point.Q >= 0)
                .OrderBy(point => point.P)
                .ToList();
        }

        private static Rectangle GetPlotRectangle(Rectangle bounds, int left = 48, int top = 22, int right = 18, int bottom = 34) =>
            new Rectangle(bounds.Left + left, bounds.Top + top,
                Math.Max(10, bounds.Width - left - right), Math.Max(10, bounds.Height - top - bottom));

        private void DrawAxes(Graphics graphics, Rectangle bounds, string yTitle, string xTitle)
        {
            var plot = GetPlotRectangle(bounds);
            using var gridPen = new Pen(Color.FromArgb(229, 231, 235));
            using var axisPen = new Pen(Color.FromArgb(107, 114, 128));
            for (var i = 0; i <= 4; i++)
            {
                var y = plot.Bottom - i * plot.Height / 4f;
                graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            }
            graphics.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
            graphics.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
            TextRenderer.DrawText(graphics, yTitle, Font, new Point(4, 4), Color.Black);
            TextRenderer.DrawText(graphics, xTitle, Font, new Point(plot.Left + 10, plot.Bottom + 10), Color.Black);
        }

        private readonly struct FlowPoint
        {
            public FlowPoint(double p, double q) { P = p; Q = q; }
            public double P { get; }
            public double Q { get; }
        }

        private GroupBox BuildSourceBlock(string title, string[] names, string[] keys)
        {
            var box = MakeGroup(title);
            box.Height = Math.Max(190, names.Length * 31 + 20);
            box.Controls.Add(MakeInfoTable(names, keys));
            return box;
        }

        private TableLayoutPanel MakeInfoTable(string[] names, string[] keys)
        {
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = names.Length, Padding = new Padding(8) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            for (var i = 0; i < names.Length; i++)
            {
                table.Controls.Add(new Label
                {
                    Text = names[i],
                    Dock = DockStyle.Fill,
                    Padding = new Padding(2, 5, 2, 2),
                    TextAlign = ContentAlignment.MiddleLeft
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
                        Cursor = Cursors.Default
                    };
                    value.Enter += (s, e) => value.SelectionStart = 0;
                    _sourceValues[keys[i]] = value;
                    table.Controls.Add(value, 1, i);
                }
                else
                {
                    var input = new TextBox
                    {
                        Dock = DockStyle.Fill,
                        Multiline = false,
                        Height = 24
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

        private static DataGridView MakeReadOnlyGrid(params string[] columns)
        {
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None };
            foreach (var column in columns)
                grid.Columns.Add(column, column);
            return grid;
        }

        private static void AddGridRow(DataGridView grid, params string[] values) => grid.Rows.Add(values);

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

        private GroupBox BuildSectionsGroup()
        {
            var box = MakeGroup("Список створов");
            _sectionsTree = new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false,
                FullRowSelect = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            _sectionsTree.AfterSelect += OnSectionChanged;
            box.Controls.Add(_sectionsTree);
            return box;
        }

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

        private GroupBox MakeGroup(string text)
        {
            return new GroupBox { Text = text, Dock = DockStyle.Fill, Padding = new Padding(10, 28, 10, 10), Margin = new Padding(4, 6, 4, 4) };
        }

        private static Label AddField(TableLayoutPanel grid, int row, string title, string value)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            grid.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 6, 4, 0)
            }, 0, row);
            var result = new Label
            {
                Text = value,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(5, 5, 2, 0),
                BackColor = Color.White
            };
            grid.Controls.Add(result, 1, row);
            return result;
        }

        private Button MakeActionButton(string text, EventHandler handler, bool primary = true, Color? accent = null)
        {
            var button = new Button
            {
                Text = text,
                Height = 34,
                AutoSize = true,
                Padding = new Padding(10, 4, 10, 4),
                Cursor = Cursors.Hand
            };
            button.Click += handler;
            return button;
        }

        // --------------------------------------------------------------- Handlers

        private void OnOpenClick(object? sender, EventArgs e)
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
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка импорта Excel: " + dialog.FileName, ex);
                MessageBox.Show(this, "Не удалось импортировать Excel:\r\n" + ex.Message,
                    "Ошибка импорта", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

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

        private async Task LoadObjectSummaryAsync()
        {
            try
            {
                var objects = await _database.LoadObjectsAsync();
                _currentObject = objects.FirstOrDefault(x => x.Id == _objectId);
                _objectProfile = await _database.LoadObjectProfileAsync(_objectId) ?? new ObjectProfile();
                await LoadDatabaseProfilesAsync();
                await LoadCalculationDetailsAsync();
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

        private void SetSourceValue(string key, string? value, string unit = "",
            string missingReason = "нет данных в БД")
        {
            if (!_sourceValues.TryGetValue(key, out var label))
                return;
            label.Text = string.IsNullOrWhiteSpace(value)
                ? "Не заполнено (" + missingReason + ")"
                : value + (string.IsNullOrWhiteSpace(unit) ? "" : " " + unit);
        }

        private void SetInputValue(string key, string? value)
        {
            if (_sourceInputs.TryGetValue(key, out var input))
                input.Text = value ?? "";
        }

        private void LoadSchemeImage(string path, string base64 = "")
        {
            if (_schemeImage == null) return;
            _schemeImage.Image?.Dispose();
            _schemeImage.Image = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(base64))
                {
                    using var stream = new MemoryStream(Convert.FromBase64String(base64));
                    using var source = Image.FromStream(stream);
                    _schemeImage.Image = new Bitmap(source);
                }
                else if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    using var source = Image.FromFile(path);
                    _schemeImage.Image = new Bitmap(source);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка загрузки схемы объекта.", ex);
            }
        }

        private void LoadHydraulicProfileImage(string path, string base64 = "")
        {
            if (_hydraulicProfileImage == null) return;
            _hydraulicProfileImage.Image?.Dispose();
            _hydraulicProfileImage.Image = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(base64))
                {
                    using var stream = new MemoryStream(Convert.FromBase64String(base64));
                    using var source = Image.FromStream(stream);
                    _hydraulicProfileImage.Image = new Bitmap(source);
                }
                else if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    using var source = Image.FromFile(path);
                    _hydraulicProfileImage.Image = new Bitmap(source);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка загрузки изображения гидравлического профиля.", ex);
            }
        }

        private void OnChooseSchemeClick(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Изображения (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Выберите схему объекта"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            _objectProfile.SchemeImagePath = dialog.FileName;
            _objectProfile.SchemeImageBase64 = Convert.ToBase64String(File.ReadAllBytes(dialog.FileName));
            LoadSchemeImage(dialog.FileName, _objectProfile.SchemeImageBase64);
            SetStatus("Схема выбрана. Нажмите сохранение исходных данных.");
        }

        private void OnChooseHydraulicProfileClick(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Изображения (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Выберите изображение гидравлического профиля"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            _objectProfile.HydraulicProfileImagePath = dialog.FileName;
            _objectProfile.HydraulicProfileImageBase64 = Convert.ToBase64String(File.ReadAllBytes(dialog.FileName));
            LoadHydraulicProfileImage(dialog.FileName, _objectProfile.HydraulicProfileImageBase64);
            SetStatus("Изображение гидравлического профиля выбрано. Нажмите сохранение исходных данных.");
        }

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

        private static bool TryParseDecimal(string value, out decimal number)
        {
            value = value.Trim().Replace(',', '.');
            return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }

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

        private string InputValue(string key) =>
            _sourceInputs.TryGetValue(key, out var input) ? input.Text.Trim() : "";

        private void SelectSection(int index)
        {
            if (_sectionsTree == null || _sectionsTree.Nodes.Count == 0 ||
                _sectionsTree.Nodes[0].Nodes.Count == 0)
                return;
            var nodes = _sectionsTree.Nodes[0].Nodes;
            _sectionsTree.SelectedNode = nodes[Math.Min(index, nodes.Count - 1)];
        }

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
                SetStatus("Сохранённый расчёт загружен из БД и пересчитан.");
                AppLogger.Info("Сохранённый расчёт загружен. Объект ID: " + _objectId + ".");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка загрузки расчёта из БД.", ex);
                SetStatus("Не удалось загрузить расчёт: " + ex.Message);
            }
        }

        private async Task SaveCalculationAsync()
        {
            if (_sections.Count == 0)
            {
                MessageBox.Show(this, "Сначала импортируйте Excel и выполните расчёт.",
                    "Нет данных", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
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
                    sectionsJson, _sourceFile, provision, balance, generation);
                SetStatus("Расчёт сохранён в БД выбранного объекта.");
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

        private static string GetExceptionMessage(Exception exception)
        {
            var messages = new List<string>();
            for (var current = exception; current != null; current = current.InnerException)
                messages.Add(current.Message);
            return string.Join("\r\nПричина: ", messages);
        }

        private void OnBackToObjectsClick(object? sender, EventArgs e)
        {
            Close();
        }

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

        private void OnSectionChanged(object? sender, TreeViewEventArgs e)
        {
            var record = e.Node.Tag as CrossSectionRecord;
            var section = e.Node.Tag as CrossSection;
            if (record == null && section == null)
                return;

            var number = record?.Number ?? section!.Number;
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
                UpdateHydraulicParameters(databaseRecord, calculationSection);
            else
                UpdateHydraulicParameters(calculationSection);

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
        }

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

        private void SetStatus(string text) => _statusLastMessage = text;
    }
}
