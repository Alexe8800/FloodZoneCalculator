#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation;

/// <summary>
/// Диалог формирования отчёта: слева — выбор графиков, справа — выбор автоматических
/// разделов (таблицы/расчёты/выводы), внизу — формат (Word или PDF).
/// Всё содержимое генерируется из данных расчёта — вручную ничего не вводится.
/// </summary>
public sealed class ChartExportForm : Form
{
    private readonly CheckedListBox _chartsList;
    private readonly CheckedListBox _sectionsList;
    private readonly ComboBox _format;

    private static readonly string[] FormatItems =
    {
        "Word (.docx)",
        "PDF (через «Microsoft Print to PDF»)",
    };

    public ChartExportForm(IEnumerable<string> chartTitles, IEnumerable<string> sectionTitles)
    {
        ModernTheme.ApplyToForm(this);
        Text = "Формирование отчёта";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = true;
        Width = 900;
        Height = 640;
        MinimumSize = new Size(760, 540);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(14),
            BackColor = Color.Transparent,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // ---------------- Левая колонка — графики ----------------
        _chartsList = BuildCheckList();
        foreach (var title in chartTitles)
            _chartsList.Items.Add(title, false);
        root.Controls.Add(
            BuildColumn("Графики (рисунки в отчёте):", _chartsList), 0, 1);

        // ---------------- Правая колонка — разделы ----------------
        _sectionsList = BuildCheckList();
        foreach (var title in sectionTitles)
            _sectionsList.Items.Add(title, true); // разделы отмечаем по умолчанию
        root.Controls.Add(
            BuildColumn("Разделы отчёта (таблицы и расчёты):", _sectionsList), 1, 1);

        // Верхняя подпись-пояснение.
        var hint = new Label
        {
            Text = "Отметьте, что включить в отчёт. Титульный лист и выбранные разделы " +
                   "формируются автоматически по данным расчёта.",
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 40,
            Font = ModernTheme.FontMain,
            ForeColor = ModernTheme.TextSub,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 6),
        };
        root.Controls.Add(hint, 0, 0);
        root.SetColumnSpan(hint, 2);

        // ---------------- Низ — формат и кнопки ----------------
        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0),
            BackColor = Color.Transparent,
        };
        bottom.Controls.Add(new Label
        {
            Text = "Формат:",
            AutoSize = true,
            Padding = new Padding(0, 9, 6, 0),
            ForeColor = ModernTheme.TextSub,
            BackColor = Color.Transparent,
        });
        _format = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 280,
            Height = 32,
        };
        _format.Items.AddRange(FormatItems);
        _format.SelectedIndex = 0;
        ModernTheme.ApplyToCombo(_format);
        bottom.Controls.Add(_format);

        bottom.Controls.Add(new Label { AutoSize = true, Width = 80 });

        var export = ModernTheme.MakeButton("Сформировать отчёт…", primary: true, height: 36);
        export.Click += (_, _) =>
        {
            if (SelectedChartTitles.Count == 0 && SelectedSectionTitles.Count == 0)
            {
                MessageBox.Show(this, "Выберите хотя бы один график или раздел.", "Пустой отчёт",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        var selectAll = ModernTheme.MakeButton("Всё", primary: false, height: 36);
        selectAll.Click += (_, _) => { SetAllChecked(_chartsList, true); SetAllChecked(_sectionsList, true); };
        var selectNone = ModernTheme.MakeButton("Ничего", primary: false, height: 36);
        selectNone.Click += (_, _) => { SetAllChecked(_chartsList, false); SetAllChecked(_sectionsList, false); };
        var cancel = ModernTheme.MakeButton("Отмена", primary: false, height: 36);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        bottom.Controls.Add(selectAll);
        bottom.Controls.Add(selectNone);
        bottom.Controls.Add(export);
        bottom.Controls.Add(cancel);
        root.Controls.Add(bottom, 0, 2);
        root.SetColumnSpan(bottom, 2);

        Controls.Add(root);
    }

    private static CheckedListBox BuildCheckList() => new()
    {
        Dock = DockStyle.Fill,
        CheckOnClick = true,
        IntegralHeight = false,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = ModernTheme.Surface,
        ForeColor = ModernTheme.TextMain,
        Font = ModernTheme.FontMain,
    };

    // Колонка: подпись + список (+ отступы).
    private static Control BuildColumn(string caption, CheckedListBox list)
    {
        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        stack.Controls.Add(new Label
        {
            Text = caption,
            AutoSize = true,
            Font = ModernTheme.FontBold,
            ForeColor = ModernTheme.TextMain,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 4),
        }, 0, 0);
        stack.Controls.Add(list, 0, 1);
        return stack;
    }

    private static void SetAllChecked(CheckedListBox list, bool value)
    {
        for (var i = 0; i < list.Items.Count; i++)
            list.SetItemChecked(i, value);
    }

    /// <summary>Заголовки отмеченных графиков.</summary>
    public IReadOnlyList<string> SelectedChartTitles =>
        _chartsList.CheckedItems.Cast<string>().ToList();

    /// <summary>Заголовки отмеченных разделов.</summary>
    public IReadOnlyList<string> SelectedSectionTitles =>
        _sectionsList.CheckedItems.Cast<string>().ToList();

    /// <summary>Выбран ли PDF-формат (иначе Word).</summary>
    public bool ExportPdf => _format.SelectedIndex == 1;
}
