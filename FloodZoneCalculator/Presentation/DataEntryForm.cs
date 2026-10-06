#nullable enable

using FloodZoneCalculator.Application;
using FloodZoneCalculator.Infrastructure;
using FloodZoneDb.Client;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation
{
    public sealed class ObjectBrowserForm : Form
    {
        private readonly FloodZoneConnection _database;
        private readonly TextBox _searchText = new TextBox();
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
            Text = "Список объектов";
            Width = 1400;
            Height = 900;
            MinimumSize = new Size(1100, 700);
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);

            var header = new Label
            {
                Dock = DockStyle.Top, Height = 48,
                Text = "Список объектов",
                BackColor = Color.FromArgb(0, 82, 145), ForeColor = Color.White,
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                Padding = new Padding(16, 10, 0, 0)
            };
            var statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("Загрузка объектов...") { Spring = true };
            statusStrip.Items.Add(_statusLabel);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(10),
                ColumnCount = 3, RowCount = 1
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 255));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));

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
            panel.Controls.Add(_searchText, 0, 0);

            var listBox = new GroupBox { Text = "Список объектов", Dock = DockStyle.Fill, Padding = new Padding(6) };
            _objectsList.Dock = DockStyle.Fill;
            _objectsList.AfterSelect += (s, e) => SelectListObject();
            listBox.Controls.Add(_objectsList);
            panel.Controls.Add(listBox, 0, 1);

            var nav = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            nav.Controls.Add(MakeButton("←", (s, e) => MoveSelection(-1)), 0, 0);
            nav.Controls.Add(MakeButton("→", (s, e) => MoveSelection(1)), 1, 0);
            panel.Controls.Add(nav, 0, 2);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 0) };
            actions.Controls.Add(MakeButton("＋ Новый", (s, e) => ClearObjectForm()));
            var delete = MakeButton("Удалить", DeleteObjectClick);
            delete.BackColor = Color.FromArgb(220, 53, 69);
            if (_isOffline) delete.Enabled = false;
            delete.ForeColor = Color.White;
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
            var actions = new FlowLayoutPanel { Width = 760, Height = 48, Padding = new Padding(0, 8, 0, 0) };
            var save = MakeButton("Сохранить карточку", async (s, e) => await SaveObjectAsync());
            _calculationButton = MakeButton("Открыть расчёты", OpenCalculationClick);
            _calculationButton.Enabled = false;
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
            var box = new GroupBox { Text = "Основные данные", Width = 780, Height = 355, Padding = new Padding(10) };
            var grid = NewGrid(2, 9);
            AddCombo(grid, 0, "Тип объекта", "TypeCode", ObjectTypeNames.Values);
            AddText(grid, 1, "Наименование", "Name");
            AddCombo(grid, 2, "Река", "River", new[] { "Волга", "Кама", "Дон", "Днепр", "Обь", "Енисей" });
            AddText(grid, 3, "Назначение", "Purpose", true);
            AddText(grid, 4, "Дата ввода в эксплуатацию", "CommissioningDate");
            AddCombo(grid, 5, "Собственник", "Owner", new[] { "АО «Волжская ГЭС»", "ПАО «РусГидро»" });
            AddCombo(grid, 6, "Балансовая принадлежность", "BalanceAffiliation", new[] { "Энергетика", "Водное хозяйство", "Муниципальная", "Федеральная" });
            AddText(grid, 7, "Ответственный", "ResponsiblePerson");
            AddCombo(grid, 8, "Статус", "Status", Statuses);
            AddCombo(grid, 0, "Класс ГТС", "GtsClass", new[] { "I (высший)", "II", "III", "IV" }, 1);
            AddCombo(grid, 1, "Категория надёжности", "ReliabilityCategory", new[] { "Особо ответственное", "Ответственное", "Обычное" }, 1);
            AddCombo(grid, 2, "Проектная организация", "DesignOrganization", new[] { "Гидропроект", "Ленгидропроект" }, 1);
            AddText(grid, 3, "Проектный шифр", "ProjectCode", false, 1);
            AddText(grid, 4, "Проектная мощность, МВт", "DesignPowerMw", false, 1);
            AddText(grid, 5, "Количество агрегатов", "UnitsCount", false, 1);
            AddText(grid, 6, "Среднегодовая выработка, млн кВт·ч", "AverageAnnualGenerationMkwh", false, 1);
            box.Controls.Add(grid);
            return box;
        }

        private Control BuildTechnicalData()
        {
            var box = new GroupBox { Text = "Технические характеристики", Width = 780, Height = 285, Padding = new Padding(10) };
            var grid = NewGrid(2, 6);
            AddCombo(grid, 0, "Тип плотины", "DamType", new[] { "Бетонная", "Земляная", "Каменно-земляная" });
            AddCombo(grid, 0, "Тип водосброса", "SpillwayType", new[] { "Поверхностный", "Глубинный", "Сифонный" }, 1);
            AddText(grid, 1, "Длина плотины, м", "DamLengthM");
            AddText(grid, 1, "Макс. пропускная способность, м³/с", "SpillwayCapacityM3s", false, 1);
            AddText(grid, 2, "Максимальная высота плотины, м", "DamMaxHeightM");
            AddText(grid, 2, "Количество водосбросных отверстий", "SpillwayOpeningsCount", false, 1);
            AddText(grid, 3, "Нормальный подпорный уровень, м", "NormalHeadwaterM");
            AddCombo(grid, 3, "Тип турбин", "TurbineType", new[] { "Каплан", "Френсис", "Пропеллерная" }, 1);
            AddText(grid, 4, "Минимальный уровень воды, м", "MinWaterLevelM");
            AddCombo(grid, 4, "Тип генераторов", "GeneratorType", new[] { "Синхронные", "Асинхронные" }, 1);
            AddText(grid, 5, "Объём водохранилища, млн м³", "ReservoirVolumeMlnM3");
            AddText(grid, 5, "Напряжение, кВ", "VoltageKv", false, 1);
            box.Controls.Add(grid);
            return box;
        }

        private Control BuildConditionData()
        {
            var box = new GroupBox { Text = "Состояние объекта", Width = 780, Height = 170, Padding = new Padding(10) };
            var grid = NewGrid(3, 2);
            AddText(grid, 0, "Физический износ, %", "PhysicalWearPct");
            AddCombo(grid, 0, "Техническое состояние", "TechnicalCondition", new[] { "Хорошее", "Удовлетворительное", "Неудовлетворительное", "Аварийное" }, 1);
            AddText(grid, 0, "Последний осмотр", "LastInspectionDate", false, 2);
            AddText(grid, 1, "Следующий осмотр", "NextInspectionDate");
            AddCombo(grid, 1, "Аварийность", "AccidentRate", new[] { "Отсутствует", "Низкая", "Средняя", "Высокая" }, 1);
            AddText(grid, 1, "Особые отметки", "SpecialMarks", false, 2);
            box.Controls.Add(grid);
            return box;
        }

        private Control BuildCalculationInputs()
        {
            var box = new GroupBox { Text = "Исходные расчётные данные", Width = 780, Height = 300, Padding = new Padding(10) };
            var grid = NewGrid(2, 6);
            var fields = new[]
            {
                ("N", "N"), ("ReservoirVolumeWv", "Wв"), ("ReservoirDepthHv", "Hв"),
                ("ReservoirAreaSv", "Sв"), ("ReservoirWidthBv", "Bв"), ("LowerReachDepthHb0", "Hб0"),
                ("LowerReachWidthBb0", "Bб0"), ("LowerReachVelocityVb0", "Vб0"), ("BreakDepthHr", "Hр"),
                ("DestructionDegreeEr", "Eр"), ("BreachThresholdP", "P"), ("WaterLevelZv", "Zв")
            };
            for (var i = 0; i < fields.Length; i++)
                AddCalculationInput(grid, i / 2, fields[i].Item2, fields[i].Item1, i % 2);
            box.Controls.Add(grid);
            return box;
        }

        private void AddCalculationInput(TableLayoutPanel grid, int row, string label, string key, int column)
        {
            var cell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(2) };
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            cell.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            var input = new TextBox { Dock = DockStyle.Fill };
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

            var imageBox = new GroupBox { Text = "Изображение объекта", Dock = DockStyle.Fill };
            _image.Dock = DockStyle.Fill;
            _image.SizeMode = PictureBoxSizeMode.Zoom;
            _image.BackColor = Color.WhiteSmoke;
            _image.BorderStyle = BorderStyle.FixedSingle;
            _image.DoubleClick += (s, e) => ChooseImage();
            imageBox.Controls.Add(_image);
            panel.Controls.Add(imageBox, 0, 0);

            var location = new GroupBox { Text = "Местоположение", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var locationGrid = NewGrid(1, 5);
            AddText(locationGrid, 0, "Субъект РФ", "SubjectRf");
            AddText(locationGrid, 1, "Район", "District");
            AddText(locationGrid, 2, "Адрес", "Address", true);
            AddText(locationGrid, 3, "Широта", "Latitude");
            AddText(locationGrid, 4, "Долгота", "Longitude");
            location.Controls.Add(locationGrid);
            panel.Controls.Add(location, 0, 1);

            var notes = new GroupBox { Text = "Примечания", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var remarks = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
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
            var cell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(2) };
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            Control input;
            if (DateFields.Contains(key))
            {
                input = new DateTimePicker
                {
                    Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom,
                    CustomFormat = "dd.MM.yyyy", ShowCheckBox = true, Checked = false
                };
            }
            else
            {
                input = new TextBox { Dock = DockStyle.Fill, Multiline = multiline, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None };
            }
            cell.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true
            }, 0, 0);
            cell.Controls.Add(input, 1, 0);
            _fields[key] = input;
            grid.Controls.Add(cell, column, row);
        }

        private void AddCombo(TableLayoutPanel grid, int row, string label, string key, IEnumerable<string> items, int column = 0)
        {
            var cell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(2) };
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
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
                AutoEllipsis = true
            }, 0, 0);
            cell.Controls.Add(combo, 1, 0);
            _fields[key] = combo;
            grid.Controls.Add(cell, column, row);
        }

        private static Button MakeButton(string text, EventHandler handler)
        {
            var button = new Button { Text = text, AutoSize = true, Height = 32, Padding = new Padding(8, 0, 8, 0) };
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
            var p = new ObjectProfile();
            foreach (var property in typeof(ObjectProfile).GetProperties())
                if (_fields.TryGetValue(property.Name, out var control))
                {
                    var value = GetControlText(control);
                    property.SetValue(p, control is ComboBox ? NormalizeValue(value) : value);
                }
            p.ImagePath = _imagePath;
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

        private void ApplyProfile(ObjectProfile profile)
        {
            ClearImage();
            foreach (var property in typeof(ObjectProfile).GetProperties())
                if (_fields.TryGetValue(property.Name, out var control))
                {
                    if (control is ComboBox combo)
                        AddComboValue(property.Name,
                            Convert.ToString(property.GetValue(profile)) ?? "");
                    SetControlText(control, Convert.ToString(property.GetValue(profile)) ?? "");
                }
            if (!string.IsNullOrWhiteSpace(profile.ImagePath) && File.Exists(profile.ImagePath))
            {
                SetImage(profile.ImagePath);
                _imagePath = profile.ImagePath;
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
            SetField("TypeCode", ObjectTypeNames["hydro_node"]);
            SetField("Status", Statuses[0]);
            ClearImage();
            _imagePath = "";
            _calculationButton.Enabled = false;
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
        }

        private bool ValidateFields(bool clearInvalid)
            {
                var valid = true;
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
                    if (error.Length > 0)
                    {
                        valid = false;
                        if (clearInvalid && !(key == "TypeCode" || key == "Name"))
                            SetControlText(control, "");
                    }
                }
                return valid;
            }

        private void OpenCalculationClick(object? sender, EventArgs e)
        {
            using var calculation = new CalculationForm(_selectedObjectId, _database);
            Hide();
            try { calculation.ShowDialog(this); } finally { Show(); }
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





