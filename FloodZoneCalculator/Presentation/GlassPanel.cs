#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation;

/// <summary>
/// Стеклянная панель: тонкая полупрозрачная подложка с внутренним свечением.
/// </summary>
public class GlassPanel : Panel
{
    public GlassPanel()
    {
        BackColor = Color.Transparent;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = ClientRectangle;
        var path = ModernTheme.RoundRect(rect, 10f);

        using var fill = new SolidBrush(Color.FromArgb(16, 30, 41, 59));
        g.FillPath(fill, path);

        using var border = new Pen(Color.FromArgb(60, 71, 85, 105), 1f);
        g.DrawPath(border, path);

        // Верхне-левое «стеклянное» свечение
        using var highlight = new SolidBrush(Color.FromArgb(20, 255, 255, 255));
        g.FillPath(highlight, path);
    }
}