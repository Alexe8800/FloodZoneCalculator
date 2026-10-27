#nullable enable

using FloodZoneCalculator.Application;
using FloodZoneCalculator.Infrastructure;
using FloodZoneDb.Client;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation
{
    public sealed class ObjectBrowserForm : Form
    {
        private readonly FloodZoneConnection _database;
        private readonly ModernTextBox _searchText = new ModernTextBox();
        private readonly TreeView _objectsList = new TreeView();
        private readonly ErrorProvider _errors = new ErrorProvider();
        private readonly Dictionary<string, Control> _fields = new Dictionary<string, Control>();
        private readonly Dictionary<ComboBox, HashSet<string>> _builtInComboValues = new Dictionary<ComboBox, HashSet<string>>();
        private readonly Dictionary<ComboBox, List<string>> _comboCatalog = new Dictionary<ComboBox, List<string>>();
        private readonly HashSet<ComboBox> _sortingCombos = new HashSet<ComboBox>();
        private readonly HashSet<ComboBox> _selectedCombos = new HashSet<ComboBox>();
        private bool _updatingCombo;
        private readonly bool _isOffline;
        private readonly ToolStripStatusLabel _statusLabel;
        private readonly PictureBox _image = new PictureBox();
        private string _imagePath = "";
        private string _imageBase64 = "";
        private ObjectProfile _loadedProfile = new ObjectProfile();
        private Button _calculationButton = null!;
        private List<DatabaseObject> _objects = new List<DatabaseObject>();
        private long _selectedObjectId;
        private long _profileLoadVersion;
        private readonly Dictionary<string, TextBox> _calculationInputFields = new Dictionary<string, TextBox>();
        private static readonly HashSet<string> DecimalFields = new HashSet<string>
        {
            "DesignPowerMw", "AverageAnnualGenerationMkwh", "DamLengthM", "DamMaxHeightM",
            "NormalHeadwaterM", "MinWaterLevelM", "ReservoirVolumeMlnM3",
            "SpillwayCapacityM3s", "VoltageKv", "PhysicalWearPct", "Latitude", "Longitude"
        };
        private static readonly HashSet<string> IntegerFields = new HashSet<string>
        {
            "UnitsCount", "SpillwayOpeningsCount"
        };
        private static readonly HashSet<string> DateFields = new HashSet<string>
        {
            "CommissioningDate", "LastInspectionDate", "NextInspectionDate"
        };

        private static readonly string[] Statuses =
        {
            "Эксплуатируется", "На реконструкции", "На консервации",
            "Временно не эксплуатируется", "Выведено из эксплуатации",
            "Аварийное", "Строительство"
        };
        private static readonly Dictionary<string, string> ObjectTypeNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["hydro_node"] = "Гидроузел",
                ["dam"] = "Плотина",
                ["spillway"] = "Водосброс",
                ["building"] = "Здание и сооружение",
                ["equipment"] = "Оборудование"
            };

        public ObjectBrowserForm(FloodZoneConnection database, bool isOffline = false)
        {
            _isOffline = isOffline;
            _database = database;
            ModernTheme.ApplyToForm(this);
            Text = "Список объектов — FloodZoneCalculator";
            Width = 1400;
            Height = 900;
            MinimumSize = new Size(1100, 700);
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;
            Font = ModernTheme.FontMain;

            var header = new ThemedPanel
            {
                Dock = DockStyle.Top, Height = 58,
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
                g.DrawString("Список объектов", ModernTheme.FontSubHeading, titleBrush, titleArea,
                    new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Near });
                if (_isOffline)
                {
                    using var offBrush = new SolidBrush(ModernTheme.AccentLight);
                    var offArea = new RectangleF(rect.Width - 150, 0, 132, rect.Height);
                    g.DrawString("Оффлайн-режим", ModernTheme.FontSmall, offBrush, offArea,
                        new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Far });
                }
            };
            var statusStrip = new StatusStrip
            {
                BackColor = ModernTheme.Panel,
                ForeColor = ModernTheme.TextSub,
                SizingGrip = false,
            };
            statusStrip.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(50, 6, 182, 212), 1f);
                e.Graphics.DrawLine(pen, 0, 0, statusStrip.Width, 0);
            };
            _statusLabel = new ToolStripStatusLabel("Загрузка объектов...") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            statusStrip.Items.Add(_statusLabel);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(12),
                ColumnCount = 3, RowCount = 1,
                BackColor = ModernTheme.Background,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 265));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 370));

            layout.Controls.Add(BuildLeftPanel(), 0, 0);
            layout.Controls.Add(BuildCenterPanel(), 1, 0);
            layout.Controls.Add(BuildRightPanel(), 2, 0);

            Controls.Add(layout);
            Controls.Add(statusStrip);
            Controls.Add(header);
            Shown += async (s, e) => await LoadDatabaseInfoAsync();
        }

        private Control BuildLeftPanel()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));

            _searchText.Dock = DockStyle.Fill;
            if (_isOffline)
            {
                _searchText.Text = "Поиск недоступен в оффлайн-режиме";
                _searchText.ReadOnly = true;
            }
            else
            {
                _searchText.Text = "";
                _searchText.TextChanged += (s, e) => RefreshObjectList();
            }
            _searchText.Placeholder = "Поиск по названию...";
            panel.Controls.Add(_searchText, 0, 0);

            var listBox = new ModernGroupBox { Text = "Список объектов", Dock = DockStyle.Fill, Padding = new Padding(12, 34, 12, 12) };
            _objectsList.Dock = DockStyle.Fill;
            _objectsList.AfterSelect += (s, e) => SelectListObject();
            listBox.Controls.Add(_objectsList);
            ModernTheme.ApplyToTree(_objectsList);
            panel.Controls.Add(listBox, 0, 1);

            var nav = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            nav.Controls.Add(MakeButton("←", (s, e) => MoveSelection(-1)), 0, 0);
            nav.Controls.Add(MakeButton("→", (s, e) => MoveSelection(1)), 1, 0);
            panel.Controls.Add(nav, 0, 2);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 0), BackColor = Color.Transparent };
            actions.Controls.Add(MakeButton("＋ Новый", (s, e) => ClearObjectForm()));
            var delete = MakeButton("Удалить", DeleteObjectClick);
            if (delete is ModernButton modernDelete)
                modernDelete.AccentColor = ModernTheme.Error;
            if (_isOffline) delete.Enabled = false;
            delete.Name = "deleteObject";
            delete.Enabled = false;
            actions.Controls.Add(delete);
            panel.Controls.Add(actions, 0, 3);
            return panel;
        }

        private Control BuildCenterPanel()
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var stack = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, Padding = new Padding(0)
            };
            var basic = BuildBasicData();
            var technical = BuildTechnicalData();
            var condition = BuildConditionData();
            var calculationInputs = BuildCalculationInputs();
            stack.Controls.Add(basic);
            stack.Controls.Add(technical);
            stack.Controls.Add(condition);
            stack.Controls.Add(calculationInputs);
            var actions = new FlowLayoutPanel { Width = 760, Height = 52, Padding = new Padding(0, 8, 0, 0), BackColor = Color.Transparent };
            var save = MakeButton("Сохранить карточку", async (s, e) => await SaveObjectAsync());
            _calculationButton = MakeButton("Открыть расчёты", (s, e) => OpenCalculationClick());
            _calculationButton.Enabled = !_isOffline;
            actions.Controls.Add(_calculationButton);
            stack.Controls.Add(actions);
            if (_isOffline) save.Enabled = false;
            void ResizeCards()
            {
                var width = Math.Max(760, scroll.ClientSize.Width - 20);
                basic.Width = width;
                technical.Width = width;
                condition.Width = width;
                actions.Width = width;
            }
            scroll.Resize += (s, e) => ResizeCards();
            scroll.HandleCreated += (s, e) => ResizeCards();
            scroll.Controls.Add(stack);
            return scroll;
        }

        private Control BuildBasicData()
        {
            var box = new ModernGroupBox { Text = "Основные данные", Width = 780, Height = 375, Padding = new Padding(14, 40, 14, 14) };
            var grid = NewGrid(2, 9);
            AddCombo(grid, 0, "Object type", "TypeCode", ObjectTypeNames.Values);
            AddText(grid, 1, "Name", "Name");
            AddCombo(grid, 2, "River", "River", new[] { "Волга", "Кама", "Дон", "Днепр", "Обь", "Енисей" });
            AddText(grid, 3, "Purpose", "Purpose", true);
            AddText(grid, 4, "Commissioning date", "CommissioningDate");
            AddCombo(grid, 5, "Owner", "Owner", new[] { "АО «Волжская ГЭС»", "ПАО «РусГидро»" });
            AddCombo(grid, 6, "Balance affiliation", "BalanceAffiliation", new[] { "Энергетика", "Водное хозяйство", "Муниципальная", "Федеральная" });
            AddText(grid, 7, "Responsible person", "ResponsiblePerson");
            AddCombo(grid, 8, "Status", "Status", Statuses);
            AddCombo(grid, 0, "GTS class", "GtsClass", new[] { "I (высший)", "II", "III", "IV" }, 1);
            AddCombo(grid, 1, "Reliability category", "ReliabilityCategory", new[] { "Особо ответственное", "Ответственное", "Обычное" }, 1);
            AddCombo(grid, 2, "Design organization", "DesignOrganization", new[] { "Гидропроект", "Ленгидропроект" }, 1);
            AddText(grid, 3, "Project code", "ProjectCode", false, 1);
            AddText(grid, 4, "Design power, MW", "DesignPowerMw", false, 1);
            AddText(grid, 5, "Units count", "UnitsCount", false, 1);
            AddText(grid, 6, "Average annual generation, mln kWh", "AverageAnnualGenerationMkwh", false, 1);
            box.Controls.Add(grid);
            return box;
        }

        private Control BuildTechnicalData()
        {
            var box = new ModernGroupBox { Text = "Технические характеристики", Width = 780, Height = 305, Padding = new Padding(14, 40, 14, 14) };
            var grid = NewGrid(2, 6);
            AddCombo(grid, 0, "Dam type", "DamType", new[] { "Бетонная", "Земляная", "Каменно-земляная" });
            AddCombo(grid, 0, "Spillway type", "SpillwayType", new[] { "Поверхностный", "Глубинный", "Сифонный" }, 1);
            AddText(grid, 1, "Dam length, m", "DamLengthM");
            AddText(grid, 1, "Max capacity, m³/s", "SpillwayCapacityM3s", false, 1);
            AddText(grid, 2, "Max dam height, m", "DamMaxHeightM");
            AddText(grid, 2, "Spillway opening count", "SpillwayOpeningsCount", false, 1);
            AddText(grid, 3, "Normal headwater level, m", "NormalHeadwaterM");
            AddCombo(grid, 3, "Turbine type", "TurbineType", new[] { "Каплан", "Френсис", "Пропеллерная" }, 1);
            AddText(grid, 4, "Min water level, m", "MinWaterLevelM");
            AddCombo(grid, 4, "Generator type", "GeneratorType", new[] { "Синхронные", "Асинхронные" }, 1);
            AddText(grid, 5, "Reservoir volume, mln m³", "ReservoirVolumeMlnM3");
            AddText(grid, 5, "Voltage, kV", "VoltageKv", false, 1);
            box.Controls.Add(grid);
            return box;
        }

        private Control BuildConditionData()
        {
            var box = new ModernGroupBox { Text = "Состояние объекта", Width = 780, Height = 190, Padding = new Padding(14, 40, 14, 14) };
            var grid = NewGrid(3, 2);
            AddText(grid, 0, "Physical wear, %", "PhysicalWearPct");
            AddCombo(grid, 0, "Technical condition", "TechnicalCondition", new[] { "Хорошее", "Удовлетворительное", "Неудовлетворительное", "Аварийное" }, 1);
            AddText(grid, 0, "Last inspection", "LastInspectionDate", false, 2);
            AddText(grid, 1, "Next inspection", "NextInspectionDate");
            AddCombo(grid, 1, "Accident risk", "AccidentRate", new[] { "Отсутствует", "Низкая", "Средняя", "Высокая" }, 1);
            AddText(grid, 1, "Special notes", "SpecialMarks", false, 2);
            box.Controls.Add(grid);
            return box;
        }

        private Control BuildCalculationInputs()
        {
            var box = new ModernGroupBox { Text = "Исходные данные для расчёта", Width = 780, Height = 320, Padding = new Padding(14, 40, 14, 14) };
            var grid = NewGrid(2, 6);
            var fields = new[]
            {
                ("N", "N"), ("ReservoirVolumeWv", "Wv"), ("ReservoirDepthHv", "Hv"),
                ("ReservoirAreaSv", "Sv"), ("ReservoirWidthBv", "Bv"), ("LowerReachDepthHb0", "Hb0"),
                ("LowerReachWidthBb0", "Bb0"), ("LowerReachVelocityVb0", "Vb0"), ("BreakDepthHr", "Hr"),
                ("DestructionDegreeEr", "Er"), ("BreachThresholdP", "P"), ("WaterLevelZv", "Zv")
            };
            for (var i = 0; i < fields.Length; i++)
                AddCalculationInput(grid, i / 2, fields[i].Item2, fields[i].Item1, i % 2);
            box.Controls.Add(grid);
            return box;
        }

        private void AddCalculationInput(TableLayoutPanel grid, int row, string label, string key, int column)
        {
            var cell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(2), BackColor = Color.Transparent };
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            cell.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = ModernTheme.TextSub, BackColor = Color.Transparent }, 0, 0);
            var input = new ModernTextBox { Dock = DockStyle.Fill, Height = 30 };
            _calculationInputFields[key] = input;
            cell.Controls.Add(input, 1, 0);
            grid.Controls.Add(cell, column, row);
        }

        private Control BuildRightPanel()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 290));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var imageBox = new ModernGroupBox { Text = "Изображение объекта", Dock = DockStyle.Fill, Padding = new Padding(12, 36, 12, 12) };
            var imagePanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            imagePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            imagePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            _image.Dock = DockStyle.Fill;
            _image.SizeMode = PictureBoxSizeMode.Zoom;
            _image.BackColor = Color.WhiteSmoke;
            _image.BorderStyle = BorderStyle.FixedSingle;
            _image.DoubleClick += (s, e) => ChooseImage();
            var addImageButton = ModernTheme.MakeButton("Добавить изображение", primary: false, height: 32);
            addImageButton.Dock = DockStyle.Fill;
            addImageButton.Margin = new Padding(3, 4, 3, 3);
            addImageButton.AutoSize = false;
            addImageButton.Click += (s, e) => ChooseImage();
            imagePanel.Controls.Add(_image, 0, 0);
            imagePanel.Controls.Add(addImageButton, 0, 1);
            imageBox.Controls.Add(imagePanel);
            panel.Controls.Add(imageBox, 0, 0);

            var location = new ModernGroupBox { Text = "Расположение", Dock = DockStyle.Fill, Padding = new Padding(12, 36, 12, 12) };
            var locationGrid = NewGrid(1, 5);
            AddText(locationGrid, 0, "RF subject", "SubjectRf");
            AddText(locationGrid, 1, "District", "District");
            AddText(locationGrid, 2, "Address", "Address", true);
            AddText(locationGrid, 3, "Latitude", "Latitude");
            AddText(locationGrid, 4, "Longitude", "Longitude");
            location.Controls.Add(locationGrid);
            panel.Controls.Add(location, 0, 1);

            var notes = new ModernGroupBox { Text = "Примечания", Dock = DockStyle.Fill, Padding = new Padding(12, 36, 12, 12) };
            var remarks = new ModernTextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
            _fields["Remarks"] = remarks;
            notes.Controls.Add(remarks);
            panel.Controls.Add(notes, 0, 2);
            return panel;
        }

        private static TableLayoutPanel NewGrid(int columns, int rows)
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = rows, AutoSize = false };
            for (var i = 0; i < columns; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            for (var i = 0; i < rows; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
            return grid;
        }

        private void AddText(TableLayoutPanel grid, int row, string label, string key, bool multiline = false, int column = 0)
        {
            var cell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(2), BackColor = Color.Transparent };
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            Control input;
            if (DateFields.Contains(key))
            {
                input = new DateTimePicker
                {
                    Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom,
                    CustomFormat = "dd.MM.yyyy", ShowCheckBox = true, Checked = false,
                    BackColor = ModernTheme.Surface, ForeColor = ModernTheme.TextMain,
                    Font = ModernTheme.FontMain,
                };
            }
            else
            {
                input = new ModernTextBox { Dock = DockStyle.Fill, Multiline = multiline, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None };
            }
            input.TextChanged += (s, e) => SetInvalidHighlight(input, false);
            if (input is DateTimePicker inputPicker)
                inputPicker.ValueChanged += (s, e) => SetInvalidHighlight(input, false);
            cell.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                ForeColor = ModernTheme.TextSub,
                BackColor = Color.Transparent
            }, 0, 0);
            cell.Controls.Add(input, 1, 0);
            _fields[key] = input;
            grid.Controls.Add(cell, column, row);
        }

        private void AddCombo(TableLayoutPanel grid, int row, string label, string key, IEnumerable<string> items, int column = 0)
        {
            var cell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(2), BackColor = Color.Transparent };
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
            ModernTheme.ApplyToCombo(combo);
            combo.TextChanged += (s, e) => SetInvalidHighlight(combo, false);
            var catalog = items.Select(NormalizeValue).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            combo.Items.AddRange(catalog.Cast<object>().ToArray());
            _builtInComboValues[combo] = new HashSet<string>(
                catalog, StringComparer.OrdinalIgnoreCase);
            _comboCatalog[combo] = catalog;
            combo.TextChanged += (s, e) => UpdateComboSuggestions(combo);
            combo.DropDown += (s, e) => UpdateComboSuggestions(combo, false, false);
            combo.SelectionChangeCommitted += (s, e) =>
            {
                _selectedCombos.Add(combo);
                combo.DroppedDown = false;
            };
            var menu = new ContextMenuStrip();
            menu.Items.Add("Удалить текущее значение", null,
                async (s, e) => await DeleteComboValueAsync(key, combo));
            combo.ContextMenuStrip = menu;
            cell.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                ForeColor = ModernTheme.TextSub,
                BackColor = Color.Transparent
            }, 0, 0);
            cell.Controls.Add(combo, 1, 0);
            _fields[key] = combo;
            grid.Controls.Add(cell, column, row);
        }

        private static Button MakeButton(string text, EventHandler handler)
        {
            var button = ModernTheme.MakeButton(text, primary: true, height: 34);
            button.AutoSize = true;
            button.Click += handler;
            return button;
        }

        private async Task LoadDatabaseInfoAsync()
        {
            if (_isOffline)
            {
                _objects = new List<DatabaseObject>();
                RefreshObjectList();
                SetStatus("Оффлайн-режим: база данных недоступна. Создание объектов и расчёт доступны, сохранение и поиск отключены.");
                return;
            }
            try
            {
                _objects = (await _database.LoadObjectsAsync()).ToList();
                RefreshObjectList();
                SetStatus("В базе найдено объектов: " + _objects.Count + ".");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка загрузки объектов.", ex);
                SetStatus("Ошибка чтения объектов: " + ex.Message);
            }
        }

        private void RefreshObjectList()
        {
            var search = _searchText.Text.Trim();
            _objectsList.BeginUpdate();
            _objectsList.Nodes.Clear();
            var groups = _objects.Where(x => string.IsNullOrWhiteSpace(search) ||
                x.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .GroupBy(x => x.TypeName)
                .OrderBy(x => x.Key);
            foreach (var group in groups)
            {
                var parent = _objectsList.Nodes.Add(ObjectTypeNames.TryGetValue(group.Key, out var typeName)
                    ? typeName
                    : (string.IsNullOrWhiteSpace(group.Key) ? "Без типа" : group.Key));
                foreach (var item in group.OrderBy(x => x.Name))
                    parent.Nodes.Add(item.Name).Tag = item;
                parent.Expand();
            }
            _objectsList.EndUpdate();
            if (_objectsList.Nodes.Count > 0 && _selectedObjectId == 0 &&
                _objectsList.Nodes[0].Nodes.Count > 0)
                _objectsList.SelectedNode = _objectsList.Nodes[0].Nodes[0];
        }

        private void SelectListObject()
        {
            if (!(_objectsList.SelectedNode?.Tag is DatabaseObject selected)) return;
            _selectedObjectId = selected.Id;
            ClearImage();
            var typeName = GetObjectTypeDisplayName(selected.TypeCode);
            AddComboValue("TypeCode", typeName);
            SetField("TypeCode", typeName);
            SetField("Name", selected.Name);
            _calculationButton.Enabled = true;
            EnableDelete(true);
            _ = LoadSelectedProfileAsync(selected.Id);
        }

        private void MoveSelection(int offset)
        {
            var nodes = _objectsList.Nodes.Cast<TreeNode>().SelectMany(x => x.Nodes.Cast<TreeNode>()).ToList();
            var current = nodes.IndexOf(_objectsList.SelectedNode);
            if (nodes.Count == 0) return;
            var index = Math.Max(0, Math.Min(nodes.Count - 1, current + offset));
            _objectsList.SelectedNode = nodes[index];
        }

        private async Task LoadSelectedProfileAsync(long id)
        {
            var version = ++_profileLoadVersion;
            try
            {
                var profile = await _database.LoadObjectProfileAsync(id);
                if (version != _profileLoadVersion || id != _selectedObjectId)
                    return;
                ApplyProfile(profile ?? new ObjectProfile());
            }
            catch (Exception ex) { AppLogger.Error("Ошибка загрузки профиля объекта.", ex); SetStatus("Профиль не загружен: " + ex.Message); }
        }

        private ObjectProfile ReadProfile()
        {
            var p = CloneProfile(_loadedProfile);
            foreach (var property in typeof(ObjectProfile).GetProperties())
                if (_fields.TryGetValue(property.Name, out var control))
                {
                    var value = GetControlText(control);
                    property.SetValue(p, control is ComboBox ? NormalizeValue(value) : value);
                }
            p.ImagePath = _imagePath;
            p.ImageBase64 = _imageBase64;
            p.CalculationInputs = new CalculationInput();
            foreach (var pair in _calculationInputFields)
            {
                var property = typeof(CalculationInput).GetProperty(pair.Key);
                if (property == null) continue;
                var text = pair.Value.Text.Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;
                try { property.SetValue(p.CalculationInputs, Convert.ChangeType(text, property.PropertyType, CultureInfo.CurrentCulture)); }
                catch (FormatException) { SetStatus($"Некорректное значение {pair.Key}."); }
            }
            return p;
        }

        private static ObjectProfile CloneProfile(ObjectProfile source)
        {
            if (source == null) return new ObjectProfile();
            try
            {
                return JsonSerializer.Deserialize<ObjectProfile>(JsonSerializer.Serialize(source))
                    ?? new ObjectProfile();
            }
            catch { return new ObjectProfile(); }
        }

        private void ApplyProfile(ObjectProfile profile)
        {
            _loadedProfile = profile ?? new ObjectProfile();
            ClearImage();
            foreach (var property in typeof(ObjectProfile).GetProperties())
                if (_fields.TryGetValue(property.Name, out var control))
                {
                    if (control is ComboBox combo)
                        AddComboValue(property.Name,
                            Convert.ToString(property.GetValue(profile)) ?? "");
                    SetControlText(control, Convert.ToString(property.GetValue(profile)) ?? "");
                }
            // Поля группы «Input calculation data» хранятся в отдельном словаре _calculationInputFields
            // и не подпадают под рефлекссию по ObjectProfile — заполняем их явно из profile.CalculationInputs,
            // иначе при повторном сохранении ReadProfile перезапишет расчётные данные пустотой.
            var ci = _loadedProfile.CalculationInputs ?? new CalculationInput();
            foreach (var pair in _calculationInputFields)
            {
                var property = typeof(CalculationInput).GetProperty(pair.Key);
                pair.Value.Text = property == null
                    ? ""
                    : Convert.ToString(property.GetValue(ci)) ?? "";
            }
            if (!string.IsNullOrWhiteSpace(_loadedProfile.ImageBase64))
            {
                try
                {
                    using var stream = new MemoryStream(Convert.FromBase64String(_loadedProfile.ImageBase64));
                    using var source = Image.FromStream(stream);
                    _image.Image = new Bitmap(source);
                    _imagePath = _loadedProfile.ImagePath;
                    _imageBase64 = _loadedProfile.ImageBase64;
                }
                catch (Exception ex) { AppLogger.Error("Не удалось загрузить изображение объекта.", ex); }
            }
            else if (!string.IsNullOrWhiteSpace(_loadedProfile.ImagePath) && File.Exists(_loadedProfile.ImagePath))
            {
                SetImage(_loadedProfile.ImagePath);
                _imagePath = _loadedProfile.ImagePath;
            }
        }
        private async Task SaveObjectAsync()
        {
            if (_isOffline)
            {
                MessageBox.Show(this, "Сохранение объектов недоступно в оффлайн-режиме.", "Оффлайн-режим", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                if (!ValidateFields(false))
                {
                    SetStatus("Исправьте ошибки в полях перед сохранением.");
                    return;
                }
                var typeDisplayName = GetField("TypeCode").Trim();
                var type = GetObjectTypeCode(typeDisplayName);
                var name = GetField("Name").Trim();
                if (string.IsNullOrWhiteSpace(typeDisplayName) || string.IsNullOrWhiteSpace(name))
                    throw new InvalidOperationException("Заполните тип объекта и наименование.");
                await _database.EnsureObjectTypeAsync(type, typeDisplayName);
                AddComboValue("TypeCode", typeDisplayName);
                foreach (var pair in _fields)
                    if (pair.Value is ComboBox combo && !string.IsNullOrWhiteSpace(combo.Text))
                    {
                        var normalized = NormalizeValue(combo.Text);
                        combo.Text = normalized;
                        AddComboValue(pair.Key, normalized);
                    }
                if (_selectedObjectId <= 0)
                    _selectedObjectId = await _database.CreateObjectAsync(type, name);
                else
                    await _database.UpdateObjectAsync(_selectedObjectId, type, name);
                await _database.SaveObjectProfileAsync(_selectedObjectId, ReadProfile());
                await LoadDatabaseInfoAsync();
            }
            catch (Exception ex) { AppLogger.Error("Ошибка сохранения объекта.", ex); SetStatus("Ошибка сохранения: " + ex.Message); }
        }
        private async void DeleteObjectClick(object? sender, EventArgs e)
        {
            if (_isOffline)
            {
                MessageBox.Show(this, "Удаление объектов недоступно в оффлайн-режиме.", "Оффлайн-режим", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_selectedObjectId <= 0) return;
            if (MessageBox.Show(this, "Удалить выбранный объект и его расчёты?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { await _database.DeleteObjectAsync(_selectedObjectId); ClearObjectForm(); await LoadDatabaseInfoAsync(); }
            catch (Exception ex) { AppLogger.Error("Ошибка удаления объекта.", ex); SetStatus("Ошибка удаления: " + ex.Message); }
        }

        private void ClearObjectForm()
        {
            _selectedObjectId = 0;
            foreach (var control in _fields.Values) SetControlText(control, "");
            foreach (var input in _calculationInputFields.Values) input.Text = "";
            SetField("TypeCode", ObjectTypeNames["hydro_node"]);
            SetField("Status", Statuses[0]);
            ClearImage();
            _imagePath = "";
            _calculationButton.Enabled = !_isOffline;
            EnableDelete(false);
            _objectsList.SelectedNode = null;
            SetStatus("Введите данные нового объекта.");
        }

        private void ChooseImage()
        {
            using var dialog = new OpenFileDialog { Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                SetImage(dialog.FileName);
                _imagePath = dialog.FileName;
                try { _imageBase64 = Convert.ToBase64String(File.ReadAllBytes(dialog.FileName)); }
                catch (Exception ex)
                {
                    _imageBase64 = "";
                    AppLogger.Error("Не удалось прочитать выбранное изображение объекта.", ex);
                }
            }
        }

        private async Task DeleteComboValueAsync(string key, ComboBox combo)
        {
            var displayName = NormalizeValue(combo.Text);
            if (string.IsNullOrWhiteSpace(displayName) ||
                IsBuiltInComboValue(key, displayName))
            {
                SetStatus("Встроенные значения удалить нельзя.");
                return;
            }
            if (MessageBox.Show(this, "Удалить значение «" + displayName + "»?",
                "Удаление значения", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            try
            {
                if (key == "TypeCode")
                    await _database.DeleteObjectTypeAsync(GetObjectTypeCode(displayName));
                else if (_fields.TryGetValue(key, out var control) &&
                    control is ComboBox valueCombo &&
                    _builtInComboValues.TryGetValue(valueCombo, out var builtIns) &&
                    builtIns.Contains(displayName))
                    throw new InvalidOperationException("Встроенное значение удалить нельзя.");
                RemoveComboValue(combo, displayName);
                if (_comboCatalog.TryGetValue(combo, out var catalog))
                    catalog.RemoveAll(x => string.Equals(x, displayName, StringComparison.OrdinalIgnoreCase));
                combo.Text = "";
                SetStatus("Значение удалено.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка удаления значения справочника.", ex);
                SetStatus("Значение не удалено: " + ex.Message);
            }
        }

        private void AddComboValue(string key, string value)
        {
            if (!_fields.TryGetValue(key, out var control) || !(control is ComboBox combo) ||
                string.IsNullOrWhiteSpace(value))
                return;
            value = NormalizeValue(value);
            if (_comboCatalog.TryGetValue(combo, out var catalog) &&
                !catalog.Contains(value, StringComparer.OrdinalIgnoreCase))
                catalog.Add(value);
            UpdateComboSuggestions(combo, false);
        }

        private void UpdateComboSuggestions(ComboBox combo, bool open = true, bool filterByText = true)
        {
            if (_updatingCombo || _sortingCombos.Contains(combo))
                return;
            _sortingCombos.Add(combo);
            var text = NormalizeValue(combo.Text);
            var selectionWasCommitted = _selectedCombos.Remove(combo);
            var values = (_comboCatalog.TryGetValue(combo, out var catalog)
                    ? catalog
                    : combo.Items.Cast<object>().Select(x => Convert.ToString(x) ?? "").ToList())
                .Where(x => x.Length > 0 && (!filterByText || text.Length == 0 ||
                    x.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            var start = combo.SelectionStart;
            combo.BeginUpdate();
            combo.Items.Clear();
            combo.Items.AddRange(values.Cast<object>().ToArray());
            combo.EndUpdate();
            _updatingCombo = true;
            try
            {
                if (combo.Text != text)
                {
                    combo.Text = text;
                    combo.SelectionStart = combo.Text.Length;
                }
                else
                    combo.SelectionStart = Math.Min(start, combo.Text.Length);
                var selectedValue = combo.SelectedItem == null
                    ? ""
                    : Convert.ToString(combo.SelectedItem) ?? "";
                selectionWasCommitted = selectionWasCommitted || (combo.SelectedIndex >= 0 &&
                    string.Equals(selectedValue, combo.Text, StringComparison.CurrentCultureIgnoreCase));
                if (open && !selectionWasCommitted && combo.Focused &&
                    combo.Text.Length > 0 && values.Length > 0)
                    combo.DroppedDown = true;
            }
            finally
            {
                _updatingCombo = false;
            }
            _sortingCombos.Remove(combo);
        }

        private static void RemoveComboValue(ComboBox combo, string value)
        {
            for (var i = combo.Items.Count - 1; i >= 0; i--)
                if (string.Equals(Convert.ToString(combo.Items[i]), value, StringComparison.OrdinalIgnoreCase))
                    combo.Items.RemoveAt(i);
        }

        private bool IsBuiltInComboValue(string key, string value)
        {
            if (key == "TypeCode")
                return ObjectTypeNames.Values.Contains(value, StringComparer.OrdinalIgnoreCase);
            return false;
        }

        private static string NormalizeValue(string value)
        {
            value = value.Trim();
            if (value.Length == 0) return value;
            return char.ToUpper(value[0], CultureInfo.CurrentCulture) +
                value.Substring(1).ToLower(CultureInfo.CurrentCulture);
        }

        private void SetImage(string path)
        {
            ClearImage();
            using (var source = Image.FromFile(path))
                _image.Image = new Bitmap(source);
        }

        private void ClearImage()
        {
            var previous = _image.Image;
            _image.Image = null;
            previous?.Dispose();
            _imagePath = "";
            _imageBase64 = "";
        }

        private static void SetInvalidHighlight(Control control, bool invalid)
        {
            if (control is TextBox || control is ComboBox || control is DateTimePicker)
                control.BackColor = invalid
                    ? Color.FromArgb(96, 66, 14)
                    : ModernTheme.Surface;
        }

        private bool ValidateFields(bool clearInvalid, bool reportInvalid = false)
            {
                var valid = true;
                Control? firstInvalid = null;
                foreach (var pair in _fields)
                {
                    var key = pair.Key;
                    var control = pair.Value;
                    var value = GetControlText(control).Trim();
                    var error = "";

                    if ((key == "TypeCode" || key == "Name") && value.Length == 0)
                        error = "Поле обязательно.";
                    else if (DateFields.Contains(key) && value.Length > 0 &&
                        !DateTime.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out _))
                        error = "Введите дату в формате ДД.ММ.ГГГГ или выберите её в календаре.";
                    else if (IntegerFields.Contains(key) && value.Length > 0 &&
                        !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                        error = "Введите целое число.";
                    else if (DecimalFields.Contains(key) && value.Length > 0 &&
                        !decimal.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                        error = "Введите число.";
                    else if (key == "PhysicalWearPct" && value.Length > 0 &&
                        (!decimal.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) ||
                         percent < 0 || percent > 100))
                        error = "Процент должен быть от 0 до 100.";
                    else if (key == "Latitude" && value.Length > 0 &&
                        (!decimal.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
                         latitude < -90 || latitude > 90))
                        error = "Широта должна быть от -90 до 90.";
                    else if (key == "Longitude" && value.Length > 0 &&
                        (!decimal.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) ||
                         longitude < -180 || longitude > 180))
                        error = "Долгота должна быть от -180 до 180.";

                    _errors.SetError(control, error);
                    SetInvalidHighlight(control, error.Length > 0);
                    if (error.Length > 0)
                    {
                        valid = false;
                        if (firstInvalid == null) firstInvalid = control;
                        if (clearInvalid && !(key == "TypeCode" || key == "Name"))
                            SetControlText(control, "");
                    }
                }
                if (!valid && firstInvalid != null)
                {
                    try { firstInvalid.Focus(); } catch { }
                    if (reportInvalid)
                        MessageBox.Show(this, "Заполните обязательные поля, выделенные жёлтым.",
                            "Проверка данных", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return valid;
            }

        private async void OpenCalculationClick()
        {
            if (_isOffline)
            {
                MessageBox.Show(this, "Расчёт недоступен в оффлайн-режиме.", "Оффлайн-режим",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!ValidateFields(false, reportInvalid: true))
            {
                SetStatus("Заполните обязательные поля перед открытием расчётов.");
                return;
            }
            // Сохраняем введённые/изменённые данные объекта (новый или уже существующий),
            // чтобы окно расчёта открывалось уже по актуальным значениям из БД.
            await SaveObjectAsync();
            if (_selectedObjectId <= 0)
                return;
            using var calculation = new CalculationForm(_selectedObjectId, _database);
            Hide();
            try { calculation.ShowDialog(this); } finally { Show(); }
            await LoadSelectedProfileAsync(_selectedObjectId);
        }

        private void EnableDelete(bool enabled)
        {
            if (Controls.Find("deleteObject", true).FirstOrDefault() is Button button)
                button.Enabled = enabled;
        }

        private string GetField(string key) => _fields.TryGetValue(key, out var control) ? GetControlText(control) : "";
        private void SetField(string key, string value) { if (_fields.TryGetValue(key, out var control)) SetControlText(control, value); }
        private static string GetObjectTypeDisplayName(string code) =>
            ObjectTypeNames.TryGetValue(code, out var name) ? name : code;
        private static string GetObjectTypeCode(string displayName)
        {
            foreach (var pair in ObjectTypeNames)
                if (string.Equals(pair.Value, displayName, StringComparison.OrdinalIgnoreCase))
                    return pair.Key;
            return displayName.Trim().ToLowerInvariant().Replace(' ', '_');
        }
        private static string GetControlText(Control control)
        {
            if (control is ComboBox combo) return combo.Text;
            if (control is DateTimePicker date) return date.Checked ? date.Value.ToString("dd.MM.yyyy") : "";
            return control.Text;
        }

        private static void SetControlText(Control control, string value)
        {
            if (control is ComboBox combo) combo.Text = value;
            else if (control is DateTimePicker date)
            {
                if (DateTime.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
                {
                    date.Value = parsed;
                    date.Checked = true;
                }
                else date.Checked = false;
            }
            else control.Text = value;
        }
        private void SetStatus(string text) => _statusLabel.Text = text;
    }
}





