#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation;

/// <summary>
/// Нижняя панель статуса: градиентная, с акцентной полоской.
/// </summary>
public class StatusPanel : Panel
{
    private readonly Label _leftLabel;
    private readonly Label _rightLabel;
    private string _leftText = "Готово";
    private string _rightText = "06.10.2026 12:00";

    public StatusPanel()
    {
        Height = 30;
        Dock = DockStyle.Bottom;
        BackColor = Color.Transparent;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

        _leftLabel = new Label
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            Padding = new Padding(12, 0, 0, 0),
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = ModernTheme.TextSub,
        };
        _rightLabel = new Label
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            Padding = new Padding(0, 0, 12, 0),
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = ModernTheme.TextMuted,
        };
        Controls.Add(_leftLabel);
        Controls.Add(_rightLabel);
    }

    public string LeftText
    {
        get => _leftText;
        set { _leftText = value; _leftLabel.Text = value; Invalidate(); }
    }

    public string RightText
    {
        get => _rightText;
        set { _rightText = value; _rightLabel.Text = value; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = ClientRectangle;
        using var grad = ModernTheme.Gradient(rect, Color.FromArgb(11, 17, 32), Color.FromArgb(17, 24, 39), LinearGradientOrientation.LeftToRight);
        g.FillPath(grad, ModernTheme.RoundRect(rect, 0));
        g.DrawLine(new Pen(Color.FromArgb(40, 6, 182, 212), 1f), rect.Left, rect.Top, rect.Right, rect.Top);
    }
}