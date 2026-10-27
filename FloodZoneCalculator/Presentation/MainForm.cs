#nullable enable

using FloodZoneCalculator.Application;
using FloodZoneCalculator.Domain;
using FloodZoneCalculator.Infrastructure;
using FloodZoneDb.Client;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    // CalculationForm — окно расчёта гидроузла (вкладка результатов, исходных данных, створов).
    //
    // Класс разбит на ЧАСТИЧНЫЕ (partial) файлы по смысловым зонам ответственности,
    // чтобы код было удобнее читать и менять. Все части — один и тот же класс.
    //
    //   MainForm.cs            — «ядро»: поля, конструктор, построение каркаса UI, статус.
    //   MainForm.Ui.cs         — конструкторы всех блоков интерфейса и фабрики контролов.
    //   MainForm.Drawing.cs    — отрисовка схемы гидроузла и профилей створа (GDI+).
    //   MainForm.Charts.cs     — графики: баланс, кривая обеспеченности, выработка.
    //   MainForm.Data.cs       — загрузка данных из БД и обновление полей/таблиц.
    //   MainForm.Calculation.cs— расчёт, сохранение/загрузка расчёта, отчёт, валидация.
    //   MainForm.Images.cs     — загрузка и выбор изображений (схема, гидравлический профиль).
    //   MainForm.Sections.cs   — навигация по створам и таблица точек берегов.
     //   MainForm.Analytics.cs  — вкладка «Аналитика»: каркас (список+хост), помощники, графики 1–6.
     //   MainForm.AnalyticsMore.cs — аналитические графики 7–12.
     //   MainForm.AnalyticsProfiles.cs — профили (ложе/УВК/УЭК, совмещ. разрез, максимумы, живое сечение).
    // =====================================================================================
    public sealed partial class CalculationForm : Form
    {
        // ---- Сервисы прикладного слоя (чтение Excel, расчёт, генерация отчёта) ----
        private readonly IExcelReader _reader = new ClosedXmlExcelReader();
        private readonly IFloodCalculator _calc;
        private readonly IReportGenerator _report = new TextReportGenerator();

        // ---- Идентификатор объекта и соединение с БД ----
        private readonly long _objectId;
        private readonly FloodZoneConnection _database;

        // ---- Данные текущего объекта и его створов ----
        private ObjectProfile _objectProfile = new();
        private DatabaseObject? _currentObject;

        private List<CrossSection> _sections = new();
        private List<CrossSectionRecord> _databaseCrossSections = new();
        private readonly Dictionary<int, HydraulicProfileRenderModel> _profileModels = new();
        private string _sourceFile = "";
        // Текущий выбранный створ — нужен, чтобы понимать, в какую запись cross_sections
        // писать отредактированные Lc/Zб/Kgm.
        private int _currentSectionNumber;
        // Защита от обратного срабатывания CellEndEdit при программном заполнении грида.
        private bool _suppressHydraulicEdit;

        // ---- Ссылки на контролы, к которым обращается логика (заполнение/перерисовка) ----
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
        private PictureBox _crossSectionImage = null!;
        private ComboBox _izodataTypeCombo = null!;
        // Тип изодаты, для которого строится поперечный разрез выбранного створа.
        private IzodataType _selectedIzodataType = IzodataType.Depth;
        private readonly ErrorProvider _sourceErrors = new();

        // ---- Выводы по расчёту (попадают в отчёт Word/PDF) ----
        private string _reportConclusions = "";
        private ModernTextBox _conclusionsText = null!;
        private bool _suppressConclusionsSync;

        // Осторожно обновляет текст поля «Выводы», не запуская обратную синхронизацию.
        private void SyncConclusionsBox()
        {
            if (_conclusionsText == null || _conclusionsText.IsDisposed) return;
            if (_conclusionsText.Text == _reportConclusions) return;
            _suppressConclusionsSync = true;
            _conclusionsText.Text = _reportConclusions;
            _suppressConclusionsSync = false;
        }

        // ---- Строка состояния ----
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

            ModernTheme.ApplyToForm(this);

            BuildUi();

            // После показа окна подтягиваем профиль объекта и, если есть, сохранённый расчёт.
            Shown += async (s, e) =>
            {
                await LoadObjectSummaryAsync();
                await LoadSavedCalculationAsync();
            };
        }

        // ------------------------------------------------------------------- UI

        // Каркас окна: заголовок, верхние вкладки, панель действий внизу и строка статуса.
        // Конкретное содержимое каждой вкладки собирается в MainForm.Ui.cs.
        private void BuildUi()
        {
            // Градиентная шапка сверху
            var header = new ThemedPanel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = ModernTheme.Background,
            };
            header.Paint += (s, e) =>
            {
                var g = e.Graphics;
                var rect = header.ClientRectangle;
                using var grad = ModernTheme.Gradient(rect, Color.FromArgb(8, 47, 73), ModernTheme.PrimaryDark,
                    LinearGradientOrientation.LeftToRight);
                g.FillRectangle(grad, rect);
                using var accent = new Pen(Color.FromArgb(120, 34, 211, 238), 2f);
                g.DrawLine(accent, rect.Left, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
                using var titleBrush = new SolidBrush(Color.White);
                var titleArea = new RectangleF(18, 0, rect.Width - 36, rect.Height);
                g.DrawString("FloodZoneCalculator — расчёт зоны затопления", ModernTheme.FontSubHeading, titleBrush,
                    titleArea, new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Near });
            };

            // Строка статуса внизу
            var statusBarPanel = new ThemedPanel { Dock = DockStyle.Bottom, Height = 26, BackColor = ModernTheme.Panel };
            statusBarPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(50, 6, 182, 212), 1f);
                e.Graphics.DrawLine(pen, 0, 0, statusBarPanel.Width, 0);
            };
            _statusBar = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Font = ModernTheme.FontSmall,
                ForeColor = ModernTheme.TextSub,
                BackColor = Color.Transparent,
            };
            _statusBar.Text = StatusPrefix + _statusLastMessage + "  " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            statusBarPanel.Controls.Add(_statusBar);
            var timeTimer = new Timer { Interval = 1000 };
            timeTimer.Tick += (s, e) =>
                _statusBar.Text = StatusPrefix + _statusLastMessage + "  " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            timeTimer.Start();

            // Панель действий
            var actions = new ThemedFlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(10, 8, 10, 6),
                AutoSize = false,
                BackColor = ModernTheme.Panel,
            };
            actions.Paint += (s, e) =>
            {
                using var pen = new Pen(ModernTheme.Border, 1f);
                e.Graphics.DrawLine(pen, 0, 0, actions.Width, 0);
            };
            actions.Controls.Add(MakeActionButton("Начало", (s, e) => SelectSection(0)));
            actions.Controls.Add(MakeActionButton("Назад", (s, e) => SelectRelativeSection(-1)));
            actions.Controls.Add(new Label { Text = "Навигация по расчётам", AutoSize = true, Padding = new Padding(6, 10, 12, 0), ForeColor = ModernTheme.TextMuted, BackColor = Color.Transparent });
            actions.Controls.Add(MakeActionButton("Вперёд", (s, e) => SelectRelativeSection(1)));
            actions.Controls.Add(MakeActionButton("Конец", (s, e) => SelectSection(int.MaxValue)));
            actions.Controls.Add(MakeActionButton("К списку расчётов", OnBackToObjectsClick, primary: false));
            actions.Controls.Add(new Label { AutoSize = true, Width = 18 });
            actions.Controls.Add(MakeActionButton("Импорт Excel", OnOpenClick));
            actions.Controls.Add(MakeActionButton("Рассчитать", OnComputeClick));
            actions.Controls.Add(MakeActionButton("Экспорт отчёта", OnReportClick));
            actions.Controls.Add(MakeActionButton("Формировать отчёт (Word/PDF)", OnExportChartsClick));
            actions.Controls.Add(MakeActionButton("Печать", OnPrintReportClick));
            var save = MakeActionButton("Сохранить расчёт", async (s, e) => await SaveCalculationAsync(), primary: true);
            actions.Controls.Add(save);
            actions.Controls.Add(MakeActionButton("Закрыть", OnBackToObjectsClick, primary: false));

            // Вкладки в тёмной теме
            var mainTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                BackColor = ModernTheme.Background,
                ForeColor = ModernTheme.TextSub,
                Font = ModernTheme.FontBold,
                Padding = new Point(14, 6),
                Appearance = TabAppearance.Normal,
            };
            var resultsTab = ModernTheme.MakeTabPage("Результаты расчёта");
            var sourceTab = ModernTheme.MakeTabPage("Данные объекта");
            var sectionsTab = ModernTheme.MakeTabPage("Створы и точки берегов");
            var analyticsTab = ModernTheme.MakeTabPage("Аналитика");
            resultsTab.Controls.Add(BuildResultsWorkspace());
            sourceTab.Controls.Add(BuildSourceWorkspace());
            sectionsTab.Controls.Add(BuildCrossSectionsWorkspace());
            analyticsTab.Controls.Add(BuildAnalyticsWorkspace());
            mainTabs.TabPages.AddRange(new[] { resultsTab, sourceTab, sectionsTab, analyticsTab });

            Controls.Add(mainTabs);
            Controls.Add(actions);
            Controls.Add(statusBarPanel);
            Controls.Add(header);
        }

        // Сохраняет текст для строки статуса (фактический вывод — через таймер в BuildUi).
        private void SetStatus(string text) => _statusLastMessage = text;
    }

    // Панель, которая полностью перерисовывается при изменении размера —
    // без этого градиентные шапки/панели оставляют «шлейфы» после максимизации.
    internal sealed class ThemedPanel : Panel
    {
        public ThemedPanel()
        {
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }
    }

    internal sealed class ThemedFlowLayoutPanel : FlowLayoutPanel
    {
        public ThemedFlowLayoutPanel()
        {
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }
    }
}
