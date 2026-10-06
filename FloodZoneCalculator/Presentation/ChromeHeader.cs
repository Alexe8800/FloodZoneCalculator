#nullable enable
using System;
using System.Drawing;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation;

/// <summary>
/// Верхняя панель приложения: глубокий вертикальный градиент, акцентная полоса и заголовок.
/// </summary>
public class ChromeHeader : Panel
{
    public ChromeHeader()
    {
        Height = 56;
        BackColor = Color.Transparent;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    public string Title { get; set; } = "РАСЧЁТЫ ДАННЫХ";

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = ClientRectangle;
        using var grad = ModernTheme.Gradient(rect, Color.FromArgb(15, 23, 42), Color.FromArgb(22, 30, 48), LinearGradientOrientation.TopToBottom);
        g.FillPath(grad, ModernTheme.RoundRect(rect, 0));
        g.DrawLine(new Pen(Color.FromArgb(40, 6, 182, 212), 1f), rect.Left, rect.Top, rect.Right, rect.Top);
        g.DrawString(Title, new Font("Segoe UI", 14F, FontStyle.Bold), new SolidBrush(Color.FromArgb(241, 245, 249)),
            new Point(18, 0), new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Near });
    }
}