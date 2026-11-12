using System.Drawing.Drawing2D;

namespace BookStore.Admin.Infrastructure;

/// <summary>
/// Biểu đồ cột đơn giản tự vẽ bằng GDI+ (không cần thư viện ngoài).
/// Dùng cho Dashboard và màn hình Thống kê.
/// </summary>
public class BarChart : Control
{
    private IReadOnlyList<(string Label, decimal Value)> _data = Array.Empty<(string, decimal)>();

    public string ChartTitle { get; set; } = "";
    public Func<decimal, string> ValueFormatter { get; set; } = v => v.ToString("#,##0");
    public Color BarColor { get; set; } = Ui.Primary;

    public BarChart()
    {
        DoubleBuffered = true;
        BackColor = Ui.Surface;
        Font = Ui.SmallFont;
        MinimumSize = new Size(200, 180);
        ResizeRedraw = true;
    }

    public void SetData(IEnumerable<(string Label, decimal Value)> data)
    {
        _data = data.ToList();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int top = 12;
        if (!string.IsNullOrEmpty(ChartTitle))
        {
            using var titleFont = new Font("Segoe UI Semibold", 10.5F);
            g.DrawString(ChartTitle, titleFont, Brushes.Black, 12, 8);
            top = 36;
        }

        if (_data.Count == 0)
        {
            TextRenderer.DrawText(g, "Chưa có dữ liệu", Font, ClientRectangle, Ui.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var plot = new Rectangle(64, top + 18, Width - 80, Height - top - 18 - 34);
        if (plot.Width < 20 || plot.Height < 20) return;

        decimal max = Math.Max(_data.Max(d => d.Value), 1);
        // Làm tròn giá trị lớn nhất cho trục dọc dễ đọc
        decimal magnitude = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)max)));
        max = Math.Ceiling(max / magnitude) * magnitude;

        using var gridPen = new Pen(Color.FromArgb(235, 231, 224));
        using var axisBrush = new SolidBrush(Ui.Muted);
        for (int i = 0; i <= 4; i++)
        {
            int y = plot.Bottom - plot.Height * i / 4;
            g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            var label = ShortNumber(max * i / 4);
            var size = g.MeasureString(label, Font);
            g.DrawString(label, Font, axisBrush, plot.Left - size.Width - 6, y - size.Height / 2);
        }

        float slot = plot.Width / (float)_data.Count;
        float barWidth = Math.Min(48, slot * 0.6f);
        using var barBrush = new SolidBrush(BarColor);
        using var valueBrush = new SolidBrush(Color.FromArgb(55, 60, 70));
        int labelEvery = Math.Max(1, (int)Math.Ceiling(_data.Count / (plot.Width / 46f)));

        for (int i = 0; i < _data.Count; i++)
        {
            var (label, value) = _data[i];
            float h = (float)(value / max) * plot.Height;
            float x = plot.Left + slot * i + (slot - barWidth) / 2;
            var rect = new RectangleF(x, plot.Bottom - h, barWidth, h);
            if (h > 0)
            {
                using var path = RoundedTop(rect, Math.Min(5, barWidth / 3));
                g.FillPath(barBrush, path);
            }

            if (value > 0 && _data.Count <= 12)
            {
                var text = ShortNumber(value);
                var size = g.MeasureString(text, Font);
                g.DrawString(text, Font, valueBrush, x + barWidth / 2 - size.Width / 2, rect.Top - size.Height - 1);
            }
            if (i % labelEvery == 0)
            {
                var size = g.MeasureString(label, Font);
                g.DrawString(label, Font, axisBrush, x + barWidth / 2 - size.Width / 2, plot.Bottom + 6);
            }
        }
    }

    private static string ShortNumber(decimal v) => v switch
    {
        >= 1_000_000_000 => (v / 1_000_000_000m).ToString("0.#") + " tỷ",
        >= 1_000_000 => (v / 1_000_000m).ToString("0.#") + "tr",
        >= 1_000 => (v / 1_000m).ToString("0.#") + "k",
        _ => v.ToString("0")
    };

    private static GraphicsPath RoundedTop(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        if (r.Height < radius * 2) radius = r.Height / 2;
        if (radius <= 0)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.Left, r.Top, radius * 2, radius * 2, 180, 90);
        path.AddArc(r.Right - radius * 2, r.Top, radius * 2, radius * 2, 270, 90);
        path.AddLine(r.Right, r.Bottom, r.Left, r.Bottom);
        path.CloseFigure();
        return path;
    }
}

/// <summary>Ô thống kê nhanh (KPI) trên Dashboard.</summary>
public class StatTile : Panel
{
    private readonly Label _value;
    private readonly Label _caption;

    public StatTile(string caption, Color accent)
    {
        Width = 220;
        Height = 92;
        BackColor = Ui.Surface;
        Margin = new Padding(0, 0, 12, 12);
        Padding = new Padding(16, 12, 12, 12);

        var bar = new Panel { Dock = DockStyle.Left, Width = 5, BackColor = accent };
        _caption = new Label { Text = caption, Dock = DockStyle.Top, Height = 24, ForeColor = Ui.Muted, Font = Ui.BaseFont, Padding = new Padding(10, 0, 0, 0) };
        _value = new Label { Text = "—", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 17F), ForeColor = Ui.PrimaryDark, Padding = new Padding(8, 0, 0, 0) };
        Controls.Add(_value);
        Controls.Add(_caption);
        Controls.Add(bar);
    }

    public string Value
    {
        set => _value.Text = value;
    }
}
