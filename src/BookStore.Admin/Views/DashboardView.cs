using BookStore.Admin.Infrastructure;
using BookStore.Business.Services;

namespace BookStore.Admin.Views;

/// <summary>Tổng quan: số liệu chính, doanh thu 7 ngày, sách bán chạy, đơn hàng gần đây.</summary>
public class DashboardView : ViewBase
{
    public override string Title => "Dashboard";

    private readonly StatTile _books = new("Đầu sách đang bán", Ui.Primary);
    private readonly StatTile _customers = new("Khách hàng", Ui.Success);
    private readonly StatTile _orders = new("Tổng đơn hàng", Color.FromArgb(91, 63, 122));
    private readonly StatTile _pending = new("Đơn chờ xác nhận", Ui.Warning);
    private readonly StatTile _revenueMonth = new("Doanh thu tháng này", Ui.Accent);
    private readonly StatTile _revenueToday = new("Doanh thu hôm nay", Ui.Accent);
    private readonly StatTile _lowStock = new("Sách sắp hết hàng", Ui.Danger);
    private readonly BarChart _chart = new() { Dock = DockStyle.Fill, ChartTitle = "Doanh thu 7 ngày gần nhất (đơn hoàn tất)" };
    private readonly DataGridView _topBooks = Ui.Grid();
    private readonly DataGridView _recent = Ui.Grid();

    public DashboardView(ServiceRunner services, AppSession session) : base(services, session)
    {
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(Ui.Button("↻ Làm mới", async (_, _) => await Safe(LoadDataAsync)));
        toolbar.Controls.Add(Ui.Label($"Xin chào {session.User.FullName}! Số liệu được lấy trực tiếp từ cơ sở dữ liệu dùng chung với website.", Ui.BaseFont, Ui.Muted));

        var tiles = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, BackColor = Ui.Background };
        tiles.Controls.AddRange(new Control[] { _books, _customers, _orders, _pending, _revenueToday, _revenueMonth, _lowStock });

        _topBooks.Col("Title", "Sách bán chạy (30 ngày)", 200);
        _topBooks.Col("Quantity", "SL bán", 60, "int");
        _topBooks.Col("Revenue", "Doanh thu", 90, "money");

        _recent.Col("Id", "Mã đơn", 50, "int");
        _recent.Col("OrderDate", "Ngày đặt", 90, "date");
        _recent.Col("CustomerName", "Khách hàng", 120);
        _recent.Col("Total", "Tổng tiền", 90, "money");
        _recent.Col("StatusText", "Trạng thái", 90);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Ui.Background };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        grid.Controls.Add(Card(_chart, null), 0, 0);
        grid.Controls.Add(Card(_topBooks, null), 1, 0);
        grid.Controls.Add(Card(_recent, "Đơn hàng gần đây"), 0, 1);
        grid.SetColumnSpan(grid.GetControlFromPosition(0, 1)!, 2);

        Controls.Add(grid);
        Controls.Add(tiles);
        Controls.Add(toolbar);
    }

    private static Control Card(Control inner, string? title)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Surface, Padding = new Padding(10), Margin = new Padding(0, 0, 12, 12) };
        panel.Controls.Add(inner);
        if (title != null) panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 28, Font = Ui.HeaderFont });
        return panel;
    }

    public override async Task LoadDataAsync()
    {
        var d = await Run<ReportService, DashboardDto>(s => s.GetDashboardAsync());
        _books.Value = d.TotalBooks.ToString("N0");
        _customers.Value = d.TotalCustomers.ToString("N0");
        _orders.Value = d.TotalOrders.ToString("N0");
        _pending.Value = d.PendingOrders.ToString("N0");
        _revenueMonth.Value = Ui.Money(d.RevenueThisMonth);
        _revenueToday.Value = Ui.Money(d.RevenueToday);
        _lowStock.Value = d.LowStockBooks.ToString("N0");
        _chart.ValueFormatter = Ui.Money;
        _chart.SetData(d.Last7Days.Select(p => (p.Label, p.BookRevenue)));
        _topBooks.Bind(d.TopBooks);
        _recent.Bind(d.RecentOrders);
    }
}
