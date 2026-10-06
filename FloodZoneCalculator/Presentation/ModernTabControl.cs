#nullable enable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation;

/// <summary>
/// Вкладка с кастомной отрисовкой: «плавающие» скруглённые табы на градиентной подложке.
/// </summary>
public class ModernTabControl : TabControl
{
    public ModernTabControl()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        Font = ModernTheme.FontBold;
    }

    public Color ActiveColor { get; set; } = ModernTheme.Primary;
    public Color InactiveColor { get; set; } = ModernTheme.Surface;
    public Color TabBarColor { get; set; } = ModernTheme.Panel;

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= TabPages.Count) return;

        var tab = TabPages[e.Index];
        var rect = e.Bounds;
        var g = e.Graphics;

        var textWidth = (int)Math.Ceiling(g.MeasureString(tab.Text, Font).Width) + 26;
        var tabWidth = Math.Max(94, Math.Min(rect.Width - 4, textWidth));
        var tabRect = new Rectangle(e.Index == 0 ? rect.Left : rect.X - 3, rect.Top, tabWidth, rect.Height);
        if (e.Index == TabPages.Count - 1) tabRect = rect; // последняя вкладка — без наложения

        var selected = (e.State & DrawItemState.Selected) != 0;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        if (selected)
        {
            using var grad = ModernTheme.Gradient(tabRect, ActiveColor, ModernTheme.ShadeColor(ActiveColor, 12), LinearGradientOrientation.LeftToRight);
            g.FillPath(grad, ModernTheme.RoundRect(tabRect, 8f));
            using var pen = new Pen(Color.FromArgb(30, 0, 0, 0), 1f);
            g.DrawPath(pen, ModernTheme.RoundRect(tabRect, 8f));
            using var top = new Pen(Color.FromArgb(90, 255, 255, 255), 1f);
            g.DrawLine(top, tabRect.Left + 8, tabRect.Top + 4, tabRect.Right - 8, tabRect.Top + 4);
            g.DrawString(tab.Text, Font, new SolidBrush(Color.White), tabRect,
                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }
        else
        {
            using var grad = ModernTheme.Gradient(tabRect, InactiveColor, TabBarColor, LinearGradientOrientation.LeftToRight);
            g.FillPath(grad, ModernTheme.RoundRect(tabRect, 8f));
            g.DrawString(tab.Text, Font, new SolidBrush(ModernTheme.TextSub), tabRect,
                new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rowHeight = GetTabRect(0).Height;
        var rowRect = new Rectangle(Bounds.Left, Bounds.Top, Bounds.Width, rowHeight);
        using var bar = new SolidBrush(TabBarColor);
        e.Graphics.FillRectangle(bar, rowRect);
        e.Graphics.DrawLine(new Pen(ModernTheme.BorderLight, 1f), rowRect.Left, rowRect.Bottom, rowRect.Right, rowRect.Bottom);
        base.OnPaint(e);
    }
}