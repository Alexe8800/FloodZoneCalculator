#nullable enable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation;

/// <summary>
/// Современное текстовое поле: скруглённое, без стандартной рамки, с подсветкой фокуса.
/// </summary>
public class ModernTextBox : TextBox
{
    private bool _focused;

    public ModernTextBox()
    {
        BackColor = Color.FromArgb(30, 41, 59);
        ForeColor = Color.FromArgb(241, 245, 249);
        Font = ModernTheme.FontMain;
        Padding = new Padding(10, 7, 10, 7);
        BorderStyle = BorderStyle.None;
        Cursor = Cursors.IBeam;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnEnter(EventArgs e) { base.OnEnter(e); _focused = true; Invalidate(); }
    protected override void OnLeave(EventArgs e) { base.OnLeave(e); _focused = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var r = ClientRectangle;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        var path = ModernTheme.RoundRect(r, 7f);
        using var fill = new SolidBrush(Enabled ? Color.FromArgb(30, 41, 59) : Color.FromArgb(45, 55, 75));
        g.FillPath(fill, path);

        if (_focused)
        {
            using var glow = new Pen(Color.FromArgb(60, 6, 182, 212), 1.5f);
            g.DrawPath(glow, path);
        }
        else
        {
            using var border = new Pen(Color.FromArgb(51, 65, 85), 1f);
            g.DrawPath(border, path);
        }

        // Отрисовка текста поверх хрома
        base.OnPaint(e);
    }
}