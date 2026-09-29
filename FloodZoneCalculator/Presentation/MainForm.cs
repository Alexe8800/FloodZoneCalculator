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
using WinFormsLabel = System.Windows.Forms.Label;

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
        private string _sourceFile = "";

        private TreeView _sectionsTree = null!;
        private WinFormsLabel _sectionTitle = null!;
        private WinFormsLabel _sectionInfo = null!;
        private WinFormsLabel _sourceInfo = null!;
        private readonly List<WinFormsLabel> _resultInfo = new();
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
        private readonly Dictionary<string, WinFormsLabel> _sourceValues = new();
        private readonly Dictionary<string, TextBox> _sourceInputs = new();
        private PictureBox _schemeImage = null!;
        private PictureBox _hydraulicProfileImage = null!;
        private ToolStripStatusLabel _statusLabel = null!;
        private readonly ErrorProvider _sourceErrors = new();

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
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            var header = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.White };
            var caption = new WinFormsLabel
            {
                Dock = DockStyle.Fill,
                Text = "РАСЧЁТЫ ДАННЫХ",
                ForeColor = Color.FromArgb(17, 24, 39),
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                Padding = new Padding(12, 8, 0, 0)
            };
            header.Controls.Add(caption);

            var status = new StatusStrip { Dock = DockStyle.Bottom, BackColor = Color.FromArgb(249, 250, 251) };
            _statusLabel = new ToolStripStatusLabel("Пользователь: admin    Режим: Просмотр");
            status.Items.Add(_statusLabel);
            status.Items.Add(new ToolStripStatusLabel { Spring = true });
            var clock = new ToolStripStatusLabel();
            status.Items.Add(clock);
            var timer = new Timer { Interval = 1000 };
            timer.Tick += (s, e) => clock.Text = "Дата и время: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            timer.Start();

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = Color.White
            };
            actions.Controls.Add(MakeActionButton("⏮", (s, e) => SelectSection(0)));
            actions.Controls.Add(MakeActionButton("◀", (s, e) => SelectRelativeSection(-1)));
            actions.Controls.Add(new WinFormsLabel { Text = "Навигация по расчётам", AutoSize = true, Padding = new Padding(4, 9, 12, 0), ForeColor = Color.FromArgb(107, 114, 128) });
            actions.Controls.Add(MakeActionButton("▶", (s, e) => SelectRelativeSection(1)));
            actions.Controls.Add(MakeActionButton("⏭", (s, e) => SelectSection(int.MaxValue)));
            actions.Controls.Add(MakeActionButton("К списку расчётов", OnBackToObjectsClick));
            actions.Controls.Add(new WinFormsLabel { AutoSize = true, Width = 18 });
            actions.Controls.Add(MakeActionButton("Импорт Excel", OnOpenClick));
            actions.Controls.Add(MakeActionButton("Экспорт отчёта", OnReportClick));
            actions.Controls.Add(MakeActionButton("Печать", OnPrintReportClick));
            var save = MakeActionButton("Сохранить расчёт", async (s, e) => await SaveCalculationAsync());
            save.BackColor = Color.FromArgb(37, 99, 235);
            actions.Controls.Add(save);
            actions.Controls.Add(MakeActionButton("Закрыть", OnBackToObjectsClick));

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(8, 0, 8, 8),
                BackColor = Color.White
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            content.Controls.Add(BuildResultsWorkspace(), 0, 0);
            content.Controls.Add(BuildSourceWorkspace(), 1, 0);

            Controls.Add(content);
            Controls.Add(actions);
            Controls.Add(status);
            Controls.Add(header);
        }

        private Control BuildResultsWorkspace()
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Controls.Add(BuildResultsTable(), 0, 0);
            layout.Controls.Add(BuildSectionsGroup(), 0, 1);
            return layout;
        }

        private Control BuildResultsTable()
        {
            var box = MakeGroup("РЕЗУЛЬТАТЫ РАСЧЁТА  /  Основные результаты");
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
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(4) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            var objectBox = MakeGroup("ИСХОДНЫЕ ДАННЫЕ  /  Объект расчёта");
            objectBox.Height = 180;
            objectBox.Controls.Add(MakeInfoTable(
                new[] { "Тип объекта", "Наименование", "Река", "Дата ввода", "Собственник" },
                new[] { "ObjectType", "ObjectName", "River", "CommissioningDate", "Owner" }));
            layout.Controls.Add(objectBox, 0, 0);
            var scheme = MakeGroup("Схема объекта");
            scheme.Height = 180;
            var schemeLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            _schemeImage = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(248, 250, 252) };
            var schemeButton = new Button { Text = "Добавить изображение схемы", Dock = DockStyle.Top, Height = 30 };
            schemeButton.Click += OnChooseSchemeClick;
            schemeLayout.Controls.Add(_schemeImage, 0, 0);
            schemeLayout.Controls.Add(schemeButton, 0, 1);
            scheme.Controls.Add(schemeLayout);
            layout.Controls.Add(scheme, 1, 0);
            layout.Controls.Add(BuildSourceBlock("Гидрологические данные",
                new[] { "Площадь водосбора", "Среднегодовой сток", "Максимальный расход", "Минимальный расход", "Обеспеченность расчёта" },
                new[] { "CatchmentArea", "AvgRunoff", "MaxFlow", "MinFlow", "Provision" }), 0, 1);
            layout.Controls.Add(BuildSourceBlock("Конструктивные параметры",
                new[] { "Тип плотины", "Длина плотины", "Макс. высота плотины", "Количество водосбросов", "Количество агрегатов", "Установленная мощность" },
                new[] { "DamType", "DamLengthM", "DamMaxHeightM", "SpillwayOpeningsCount", "UnitsCount", "DesignPowerMw" }), 1, 1);
            layout.Controls.Add(BuildSourceBlock("Исходные расчётные параметры",
                new[] { "Нормальный подпорный уровень (НПУ)", "Уровень мёртвого объёма (УМО)", "Полный объём водохранилища", "Полезный объём водохранилища", "Коэффициент шероховатости (n)", "Температура воды", "КПД гидроузла", "Коэффициент надёжности" },
                new[] { "NormalHeadwaterM", "DeadVolumeLevelM", "ReservoirFullVolume", "ReservoirUsefulVolume", "RoughnessCoeff", "WaterTemperature", "EfficiencyPct", "ReliabilityCoefficient" }), 0, 2);
            var provisions = MakeGroup("Изодаты (кривые обеспеченности)");
            provisions.Height = 460;
            _flowProvisionGrid = MakeEditableGrid("Обеспеченность, %", "Расход, м³/с");
            foreach (var probability in new[] { "0,1", "1", "2", "5", "10", "20", "50", "90", "99" })
                _flowProvisionGrid.Rows.Add(probability, "");
            _flowChart = new Panel { Dock = DockStyle.Bottom, Height = 190, BackColor = Color.White };
            _flowChart.Paint += (_, e) => DrawFlowChart(e.Graphics, _flowChart.ClientRectangle, false);
            _flowProvisionGrid.Dock = DockStyle.Top;
            _flowProvisionGrid.Height = 250;
            _flowProvisionGrid.CellValueChanged += (_, _) => _flowChart.Invalidate();
            provisions.Controls.Add(_flowChart);
            provisions.Controls.Add(_flowProvisionGrid);
            layout.Controls.Add(provisions, 1, 2);
            var curve = MakeGroup("Кривая обеспеченности расходов");
            curve.Height = 240;
            _provisionCurveChart = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            _provisionCurveChart.Paint += (_, e) => DrawFlowChart(e.Graphics, _provisionCurveChart.ClientRectangle, true);
            curve.Controls.Add(_provisionCurveChart);
            layout.Controls.Add(curve, 0, 3);
            layout.SetColumnSpan(curve, 2);
            var extra = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            extra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            extra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            extra.Controls.Add(BuildBalanceBlock(), 0, 0);
            extra.Controls.Add(BuildOperationBlock(), 1, 0);
            extra.Controls.Add(BuildGenerationBlock(), 0, 1);
            extra.Controls.Add(BuildHydraulicBlock(), 1, 1);
            layout.Controls.Add(extra, 0, 4);
            layout.SetColumnSpan(extra, 2);
            var saveInputs = new Button { Text = "Сохранить введённые исходные данные в БД", Dock = DockStyle.Top, Height = 34, BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            saveInputs.Click += async (s, e) => await SaveSourceDataAsync();
            layout.Controls.Add(saveInputs, 0, 3);
            layout.SetColumnSpan(saveInputs, 2);
            scroll.Controls.Add(layout);
            return scroll;
        }

        private GroupBox BuildBalanceBlock()
        {
            var box = MakeGroup("Баланс водопользования");
            box.Height = 190;
            _balanceGrid = MakeEditableGrid("Отрасль", "Доля, %");
            foreach (var row in new[] { new[] { "Выработка электроэнергии", "65" }, new[] { "Водоснабжение", "20" }, new[] { "Судоходство", "10" }, new[] { "Прочие нужды", "5" } })
                _balanceGrid.Rows.Add(row);
            _balanceChart = new Panel { Dock = DockStyle.Bottom, Height = 125, BackColor = Color.White };
            _balanceChart.Paint += (_, e) => DrawBalanceChart(e.Graphics, _balanceChart.ClientRectangle);
            _balanceGrid.Height = 95;
            _balanceGrid.Dock = DockStyle.Top;
            _balanceGrid.CellValueChanged += (_, _) => _balanceChart.Invalidate();
            box.Controls.Add(_balanceChart);
            box.Controls.Add(_balanceGrid);
            return box;
        }

        private GroupBox BuildOperationBlock()
        {
            var box = MakeGroup("Режимные характеристики");
            box.Height = 190;
            _operationGrid = MakeEditableGrid("Период", "Q ср, м³/с", "Q макс, м³/с", "Q мин, м³/с", "УВБ, м", "УНБ, м");
            foreach (var period in new[] { "Весенний паводок", "Летняя межень", "Осенний паводок", "Зимняя межень" })
                _operationGrid.Rows.Add(period, "", "", "", "", "");
            box.Controls.Add(_operationGrid);
            return box;
        }

        private GroupBox BuildGenerationBlock()
        {
            var box = MakeGroup("Выработка электроэнергии");
            box.Height = 390;
            _generationGrid = MakeEditableGrid("Месяц", "Расход, м³/с", "Напор, м", "Выработка, млн кВт·ч");
            foreach (var month in new[] { "Янв", "Фев", "Мар", "Апр", "Май", "Июн", "Июл", "Авг", "Сен", "Окт", "Ноя", "Дек" })
                _generationGrid.Rows.Add(month, "", "", "");
            _generationChart = new Panel { Dock = DockStyle.Bottom, Height = 175, BackColor = Color.White };
            _generationChart.Paint += (_, e) => DrawGenerationChart(e.Graphics, _generationChart.ClientRectangle);
            _generationGrid.Dock = DockStyle.Top;
            _generationGrid.Height = 190;
            _generationGrid.CellValueChanged += (_, _) => _generationChart.Invalidate();
            box.Controls.Add(_generationChart);
            box.Controls.Add(_generationGrid);
            return box;
        }

        private GroupBox BuildHydraulicBlock()
        {
            var box = MakeGroup("Гидравлический профиль");
            box.Height = 390;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            _hydraulicProfileImage = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(248, 250, 252) };
            _hydraulicGrid = MakeReadOnlyGrid("Параметр", "Значение", "Ед. изм.");
            _hydraulicGrid.Rows.Add("Уровень верхнего бьефа", "—", "м БС");
            _hydraulicGrid.Rows.Add("Уровень нижнего бьефа", "—", "м БС");
            _hydraulicGrid.Rows.Add("Отметка дна русла", "—", "м БС");
            _hydraulicGrid.Rows.Add("Напор", "—", "м");
            _hydraulicGrid.Rows.Add("Длина плотины", "—", "м");
            var button = new Button { Text = "Добавить отдельное изображение профиля", Dock = DockStyle.Fill };
            button.Click += OnChooseHydraulicProfileClick;
            var inner = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2 };
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            inner.Controls.Add(_hydraulicGrid, 0, 0);
            inner.Controls.Add(_hydraulicProfileImage, 1, 0);
            layout.Controls.Add(inner, 0, 0);
            layout.Controls.Add(button, 0, 1);
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
            if (total <= 0)
            {
                using var empty = new SolidBrush(Color.FromArgb(229, 231, 235));
                graphics.FillEllipse(empty, 8, 8, 100, 100);
                TextRenderer.DrawText(graphics, "Введите доли", Font, new Point(12, 55), Color.DimGray);
                return;
            }

            var colors = new[] { Color.FromArgb(37, 99, 235), Color.FromArgb(34, 197, 94), Color.FromArgb(249, 115, 22), Color.FromArgb(156, 163, 175) };
            var pie = new Rectangle(8, 8, 100, 100);
            var start = -90f;
            for (var i = 0; i < values.Count; i++)
            {
                var sweep = (float)(values[i] / total * 360);
                using var brush = new SolidBrush(colors[i % colors.Length]);
                graphics.FillPie(brush, pie, start, sweep);
                start += sweep;
            }
            using (var center = new SolidBrush(Color.White))
                graphics.FillEllipse(center, 35, 35, 46, 46);
            TextRenderer.DrawText(graphics, "100%", Font, new Rectangle(39, 51, 40, 18), Color.FromArgb(31, 41, 55),
                TextFormatFlags.HorizontalCenter);

            for (var i = 0; i < _balanceGrid.Rows.Count; i++)
            {
                var label = Convert.ToString(_balanceGrid.Rows[i].Cells[0].Value) ?? "";
                var percent = values[i] / total * 100;
                using var brush = new SolidBrush(colors[i % colors.Length]);
                graphics.FillRectangle(brush, 125, 10 + i * 25, 12, 12);
                TextRenderer.DrawText(graphics, label + " " + percent.ToString("0.##") + "%", Font,
                    new Point(143, 8 + i * 25), Color.FromArgb(31, 41, 55));
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
            var max = Math.Max(1, values.Max());
            var width = plot.Width / Math.Max(1, values.Length);
            using var brush = new SolidBrush(Color.FromArgb(37, 99, 235));
            for (var i = 0; i < values.Length; i++)
            {
                var height = values[i] / max * plot.Height;
                var bar = new RectangleF(plot.Left + i * width + width * .18f,
                    plot.Bottom - (float)height, width * .64f, (float)height);
                graphics.FillRectangle(brush, bar);
                TextRenderer.DrawText(graphics, Convert.ToString(_generationGrid.Rows[i].Cells[0].Value) ?? "",
                    Font, new Point((int)bar.Left, plot.Bottom + 2), Color.FromArgb(55, 65, 81));
            }
            TextRenderer.DrawText(graphics, "Выработка, млн кВт·ч", Font, new Point(4, 4), Color.FromArgb(31, 41, 55));
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
            TextRenderer.DrawText(graphics, yTitle, Font, new Point(4, 4), Color.FromArgb(31, 41, 55));
            TextRenderer.DrawText(graphics, xTitle, Font, new Point(plot.Left + 10, plot.Bottom + 10), Color.FromArgb(31, 41, 55));
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
                table.Controls.Add(new WinFormsLabel { Text = names[i], Dock = DockStyle.Fill, Padding = new Padding(2, 5, 2, 2) }, 0, i);
                if (keys[i] == "ObjectType" || keys[i] == "ObjectName" ||
                    keys[i] == "River" || keys[i] == "CommissioningDate" ||
                    keys[i] == "Owner")
                {
                    var value = new WinFormsLabel { Text = "Не заполнено", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(55, 65, 81), Padding = new Padding(2, 5, 2, 2) };
                    _sourceValues[keys[i]] = value;
                    table.Controls.Add(value, 1, i);
                }
                else
                {
                    var input = new TextBox { Dock = DockStyle.Fill };
                    input.TextChanged += (_, _) => ValidateSourceInputs();
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
            var profileMaxFlow = ParseDoubleOrZero(_objectProfile.MaxFlowM3s);
            var profileMinFlow = ParseDoubleOrZero(_objectProfile.MinFlowM3s);
            if (_sections.Count > 0)
            {
                var values = new[]
                {
                    profileMaxFlow,
                    profileMaxFlow,
                    profileMinFlow,
                    _sections.Average(x => x.MaxVelocity),
                    _sections.Max(x => x.WaterLevel),
                    _sections.Min(x => x.WaterLevel)
                };
                for (var i = 0; i < values.Length; i++)
                    _mainResultsGrid.Rows[i].Cells[2].Value = values[i].ToString("N2");
            }
            _mainResultsGrid.Rows[6].Cells[2].Value = DisplayResult(_objectProfile.EfficiencyPct);
            var generated = _generationGrid == null
                ? 0
                : _generationGrid.Rows.Cast<DataGridViewRow>()
                    .Sum(row => ReadCellNumber(row.Cells[3].Value));
            _mainResultsGrid.Rows[7].Cells[2].Value = generated > 0
                ? generated.ToString("N2")
                : DisplayResult(_objectProfile.AverageAnnualGenerationMkwh);
            _mainResultsGrid.Rows[8].Cells[2].Value = DisplayResult(_objectProfile.ReliabilityCoefficient);
        }

        private static string DisplayResult(string value) =>
            string.IsNullOrWhiteSpace(value) ? "—" : value;

        private void UpdateInstructionTables()
        {
            if (_hydraulicGrid == null)
                return;

            var upper = _sections.Count == 0 ? "—" : _sections.Max(x => x.WaterLevel).ToString("N2");
            var lower = _sections.Count == 0 ? "—" : _sections.Min(x => x.WaterLevel).ToString("N2");
            var bottom = DisplayResult(_objectProfile.DeadVolumeLevelM);
            var head = _sections.Count == 0
                ? "—"
                : (_sections.Max(x => x.WaterLevel) - _sections.Min(x => x.WaterLevel)).ToString("N2");
            _hydraulicGrid.Rows[0].Cells[1].Value = upper;
            _hydraulicGrid.Rows[1].Cells[1].Value = lower;
            _hydraulicGrid.Rows[2].Cells[1].Value = bottom;
            _hydraulicGrid.Rows[3].Cells[1].Value = head;
            _hydraulicGrid.Rows[4].Cells[1].Value = DisplayResult(_objectProfile.DamLengthM);

            var qMax = ReadInputNumber("MaxFlow");
            var qMin = ReadInputNumber("MinFlow");
            if (qMax <= 0 && _sections.Count > 0)
                qMax = _sections.Max(x => x.MaxDepth) * _sections.Max(x => x.MaxVelocity);
            if (qMin <= 0)
                qMin = qMax > 0 ? qMax * 0.04 : 0;

            if (_flowProvisionGrid != null && qMax > 0)
            {
                for (var i = 0; i < _flowProvisionGrid.Rows.Count; i++)
                {
                    var probability = ReadCellNumber(_flowProvisionGrid.Rows[i].Cells[0].Value);
                    var factor = 1 - Math.Min(1, Math.Max(0, probability / 100));
                    _flowProvisionGrid.Rows[i].Cells[1].Value = (qMin + (qMax - qMin) * factor).ToString("N2");
                }
            }

            if (_operationGrid != null && qMax > 0)
            {
                var seasonalFactors = new[] { 0.75, 0.25, 0.6, 0.2 };
                for (var i = 0; i < _operationGrid.Rows.Count; i++)
                {
                    var average = qMin + (qMax - qMin) * seasonalFactors[i];
                    _operationGrid.Rows[i].Cells[1].Value = average.ToString("N2");
                    _operationGrid.Rows[i].Cells[2].Value = (average + (qMax - qMin) * 0.25).ToString("N2");
                    _operationGrid.Rows[i].Cells[3].Value = (average - (average - qMin) * 0.5).ToString("N2");
                    _operationGrid.Rows[i].Cells[4].Value = upper;
                    _operationGrid.Rows[i].Cells[5].Value = lower;
                }
            }

            if (_generationGrid != null && qMax > 0)
            {
                var efficiency = ReadInputNumber("EfficiencyPct");
                efficiency = efficiency > 0 ? efficiency / 100 : 0.93;
                var averageFlow = (qMax + qMin) / 2;
                var hours = 730;
                for (var i = 0; i < _generationGrid.Rows.Count; i++)
                {
                    var monthly = 9.81 * averageFlow * Math.Max(0, ParseDoubleOrZero(head)) * efficiency * hours / 1000;
                    _generationGrid.Rows[i].Cells[1].Value = averageFlow.ToString("N2");
                    _generationGrid.Rows[i].Cells[2].Value = head;
                    _generationGrid.Rows[i].Cells[3].Value = monthly.ToString("N2");
                }
            }
            _flowChart?.Invalidate();
            _provisionCurveChart?.Invalidate();
            _generationChart?.Invalidate();
        }

        private double ReadInputNumber(string key) =>
            _sourceInputs.TryGetValue(key, out var input) ? ParseDoubleOrZero(input.Text) : 0;

        private static double ReadCellNumber(object? value) =>
            ParseDoubleOrZero(Convert.ToString(value) ?? "");

        private static double ParseDoubleOrZero(string value) =>
            TryParseDecimal(value, out var number) ? (double)number : 0;

        private GroupBox BuildSectionsGroup()
        {
            var box = MakeGroup("Список створов");
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var search = new TextBox { Dock = DockStyle.Fill };
            _sectionsTree = new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false,
                FullRowSelect = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            _sectionsTree.AfterSelect += OnSectionChanged;
            layout.Controls.Add(search, 0, 0);
            layout.Controls.Add(_sectionsTree, 0, 1);
            box.Controls.Add(layout);
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
            var picture = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(205, 225, 238), Margin = new Padding(4) };
            picture.Paint += (sender, args) =>
            {
                var g = args.Graphics;
                using (var water = new SolidBrush(Color.FromArgb(55, 145, 190)))
                    g.FillRectangle(water, 0, picture.Height / 2, picture.Width, picture.Height / 2);
                using (var dam = new SolidBrush(Color.FromArgb(120, 120, 120)))
                    g.FillRectangle(dam, picture.Width / 6, picture.Height / 2 - 25, picture.Width * 2 / 3, 25);
                using (var tower = new SolidBrush(Color.FromArgb(90, 90, 90)))
                    g.FillRectangle(tower, picture.Width / 3, picture.Height / 2 - 60, 28, 35);
            };
            _sectionInfo = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Выберите створ слева.\r\n\r\nЗдесь появятся краткие сведения и результаты расчёта.",
                Padding = new Padding(8),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            layout.Controls.Add(picture, 0, 0);
            layout.Controls.Add(new WinFormsLabel { Text = "Гидротехническое сооружение", Dock = DockStyle.Fill, Padding = new Padding(8, 8, 0, 0), Font = new Font(Font, System.Drawing.FontStyle.Bold) }, 0, 1);
            layout.Controls.Add(_sectionInfo, 0, 2);
            box.Controls.Add(layout);
            return box;
        }

        private static GroupBox MakeGroup(string text)
        {
            return new GroupBox { Text = text, Dock = DockStyle.Fill, Padding = new Padding(6), Margin = new Padding(4) };
        }

        private static WinFormsLabel AddField(TableLayoutPanel grid, int row, string title, string value)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            grid.Controls.Add(new WinFormsLabel { Text = title, Dock = DockStyle.Fill, Padding = new Padding(0, 6, 4, 0) }, 0, row);
            var result = new WinFormsLabel { Text = value, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(5, 5, 2, 0), BackColor = Color.White };
            grid.Controls.Add(result, 1, row);
            return result;
        }

        private static Button MakeActionButton(string text, EventHandler handler)
        {
            var button = new Button { Text = text, AutoSize = true, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 112, 192), ForeColor = Color.White, Padding = new Padding(10, 0, 10, 0) };
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
                SetStatus("Excel импортирован. Можно запускать расчёт.");
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
            SetStatus("Загружено створов: " + _sections.Count + ". Нажмите «Рассчитать».");
        }

        private async Task LoadObjectSummaryAsync()
        {
            try
            {
                var objects = await _database.LoadObjectsAsync();
                _currentObject = objects.FirstOrDefault(x => x.Id == _objectId);
                _objectProfile = await _database.LoadObjectProfileAsync(_objectId) ?? new ObjectProfile();
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
                SetInputValue("SpillwayOpeningsCount", _objectProfile.SpillwayOpeningsCount);
                SetInputValue("UnitsCount", _objectProfile.UnitsCount);
                SetInputValue("DesignPowerMw", _objectProfile.DesignPowerMw);
                SetInputValue("NormalHeadwaterM", _objectProfile.NormalHeadwaterM);
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
                LoadHydraulicProfileImage(_objectProfile.HydraulicProfileImagePath, _objectProfile.HydraulicProfileImageBase64);
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
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка загрузки схемы объекта.", ex);
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
                _objectProfile.SpillwayOpeningsCount = InputValue("SpillwayOpeningsCount");
                _objectProfile.UnitsCount = InputValue("UnitsCount");
                _objectProfile.DesignPowerMw = InputValue("DesignPowerMw");
                _objectProfile.NormalHeadwaterM = InputValue("NormalHeadwaterM");
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
                _calc.ComputeAll(_sections);
                    UpdateMainResults();
                UpdateInstructionTables();
                if (_sectionsTree.Nodes.Count > 0 && _sectionsTree.Nodes[0].Nodes.Count > 0)
                    _sectionsTree.SelectedNode = _sectionsTree.Nodes[0].Nodes[0];
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
                UpdateMainResults();
                SetStatus("Сохранённый расчёт загружен из БД.");
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
                await _database.SaveCalculationAsync(_objectId,
                    sectionsJson, _sourceFile);
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
            var cs = e.Node.Tag as CrossSection;
            if (cs == null) return;
            var index = _sections.IndexOf(cs);
            if (index < 0) return;
            if (_sectionTitle != null)
                _sectionTitle.Text = "Створ №" + cs.Number;
            if (_sourceInfo != null)
                _sourceInfo.Text = string.IsNullOrEmpty(_sourceFile)
                    ? "Данные загружены"
                    : Path.GetFileName(_sourceFile);
            if (_sectionInfo != null)
                _sectionInfo.Text = "Створ №" + cs.Number + "\r\n\r\n" +
                    "Слева точек: " + cs.GetPoints(Bank.Left, IzodataType.Depth).Count + "\r\n" +
                    "Справа точек: " + cs.GetPoints(Bank.Right, IzodataType.Depth).Count + "\r\n\r\n" +
                    "Данные створа доступны в таблице результатов.";
            if (_resultInfo.Count >= 6)
            {
                _resultInfo[0].Text = cs.MaxDepth.ToString("0.###");
                _resultInfo[1].Text = cs.MaxVelocity.ToString("0.###");
                _resultInfo[2].Text = cs.WaterLevel.ToString("0.###");
                _resultInfo[3].Text = cs.FloodWidthLeft.ToString("0.###");
                _resultInfo[4].Text = cs.FloodWidthRight.ToString("0.###");
                _resultInfo[5].Text = (cs.FloodAreaLeft + cs.FloodAreaRight).ToString("0.###");
            }
        }

        private void SetStatus(string text) => _statusLabel.Text = text;
    }
}
