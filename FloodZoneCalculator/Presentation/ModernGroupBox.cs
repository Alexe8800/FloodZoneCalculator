#nullable enable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation;

/// <summary>
/// Группа со скруглёнными углами и градиентной шапкой с названием.
/// </summary>
public class ModernGroupBox : GroupBox
{
    public ModernGroupBox()
    {
        FlatStyle = FlatStyle.Flat;
        BackColor = ModernTheme.Panel;
        Font = ModernTheme.FontSubHeading;
        ForeColor = ModernTheme.PrimaryLight;
        Padding = new Padding(12, 34, 12, 12);
        Margin = new Padding(4, 6, 4, 4);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = ClientRectangle;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        // Скруглённая рамка
        var path = ModernTheme.RoundRect(rect, 10f);
        using var border = new Pen(Color.FromArgb(71, 85, 105), 1.2f);
        g.DrawPath(border, path);

        // Градиентная шапка
        var headerRect = new Rectangle(rect.Left + 10, rect.Top + 6, Math.Max(180, rect.Width - 20), 20);
        var headerPath = ModernTheme.RoundRect(headerRect, 5f);
        using var grad = ModernTheme.Gradient(headerRect, Color.FromArgb(8, 145, 178), Color.FromArgb(6, 182, 212), LinearGradientOrientation.LeftToRight);
        g.FillPath(grad, headerPath);

        // Тонкая светлая обводка шапки
        using var hp = new Pen(Color.FromArgb(70, 255, 255, 255), 0.7f);
        g.DrawPath(hp, headerPath);

        // Заголовок по центру шапки
        var tf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(Text, Font, new SolidBrush(Color.White), headerRect, tf);

        // Верхняя акцентная полоска
        g.DrawLine(new Pen(Color.FromArgb(50, 6, 182, 212), 1.5f), rect.Left, rect.Top, rect.Right, rect.Top);
    }
}