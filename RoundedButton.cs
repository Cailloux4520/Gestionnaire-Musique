using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace MusicOrganizer;

public class RoundedButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BorderRadius { get; set; } = 10;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SelectedBackColor { get; set; } = Color.FromArgb(21, 101, 192);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color NormalBackColor { get; set; } = Color.FromArgb(230, 234, 238);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color NormalForeColor { get; set; } = Color.FromArgb(32, 36, 40);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SelectedMode { get; set; }

    public RoundedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var path = CreateRoundRectangle(ClientRectangle, BorderRadius);
        Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        pevent.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

        using var path = CreateRoundRectangle(bounds, BorderRadius);
        var fillColor = Enabled
            ? SelectedMode ? SelectedBackColor : NormalBackColor
            : Color.FromArgb(205, 210, 214);
        var textColor = Enabled
            ? SelectedMode ? Color.White : NormalForeColor
            : Color.FromArgb(120, 124, 128);

        using var brush = new SolidBrush(fillColor);
        pevent.Graphics.FillPath(brush, path);

        using var pen = new Pen(Color.FromArgb(35, Color.White), 1f);
        pevent.Graphics.DrawPath(pen, path);

        TextRenderer.DrawText(pevent.Graphics, Text, Font, ClientRectangle, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath CreateRoundRectangle(Rectangle bounds, int radius)
    {
        radius = Math.Max(2, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}