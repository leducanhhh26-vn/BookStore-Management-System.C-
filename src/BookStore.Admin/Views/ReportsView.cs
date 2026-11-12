using BookStore.Admin.Infrastructure;
using BookStore.Business.Services;
using ClosedXML.Excel;

namespace BookStore.Admin.Views;

/// <summary>
/// Thống kê – báo cáo: doanh thu theo ngày/tháng, sách bán chạy, đơn theo trạng thái, tồn kho theo thể loại.
/// Xuất toàn bộ báo cáo ra file Excel.
/// </summary>
public class ReportsView : ViewBase
{
    public override string Title => "Thống kê & báo cáo";

    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Short, Width = 120, Margin = new Padding(0, 4, 8, 0) };
    private readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Short, Width = 120, Margin = new Padding(0, 4, 8, 0) };
    private readonly ComboBox _grouping = Ui.Combo(140);
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill, Padding = new Point(16, 6) };

    private readonly BarChart _revenueChart = new() { Dock = DockStyle.Top, Height = 280, ChartTitle = "Doanh thu sách (đơn hoàn tất)" };
    private readonly DataGridView _revenueGrid = Ui.Grid();
    private readonly Label _revenueSummary = new() { Dock = DockStyle.Bottom, Height = 30, Font = Ui.BoldFont, TextAlign = ContentAlignment.MiddleRight };
    private readonly DataGridView _topGrid = Ui.Grid();
    private readonly BarChart _statusChart = new() { Dock = DockStyle.Top, Height = 260, ChartTitle = "Số đơn theo trạng thái (theo ngày đặt)", BarColor = Ui.Accent };
    private readonly DataGridView _statusGrid = Ui.Grid();
    private readonly DataGridView _stockGrid = Ui.Grid();

    private List<RevenuePoint> _revenue = new();
    private List<TopBookItem> _top = new();
    private List<StatusCount> _status = new();
    private List<CategoryStockItem> _stock = new();

    public ReportsView(ServiceRunner services, AppSession session) : base(services, session)
    {
        _from.Value = DateTime.Today.AddDays(-29);
        _to.Value = DateTime.Today;
        _grouping.Items.AddRange(new object[] { "Theo ngày", "Theo tháng" });
        _grouping.SelectedIndex = 0;

        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(Ui.Label("Từ ngày"));
        toolbar.Controls.Add(_from);
        toolbar.Controls.Add(Ui.Label("Đến ngày"));
        toolbar.Controls.Add(_to);
        toolbar.Controls.Add(_grouping);
        toolbar.Controls.Add(Ui.Button("📊 Thống kê", async (_, _) => await Safe(LoadDataAsync), ButtonKind.Primary));
        toolbar.Controls.Add(Ui.Button("7 ngày", async (_, _) => await QuickRange(-6, 0)));
        toolbar.Controls.Add(Ui.Button("30 ngày", async (_, _) => await QuickRange(-29, 0)));
        toolbar.Controls.Add(Ui.Button("12 tháng", async (_, _) => await QuickRange(-364, 1)));
        toolbar.Controls.Add(Ui.Button("⬇ Xuất Excel", (_, _) => ExportExcel(), ButtonKind.Success));

        // Doanh thu
        _revenueGrid.Col("Label", "Thời gian", 80);
        _revenueGrid.Col("OrderCount", "Số đơn hoàn tất", 80, "int");
        _revenueGrid.Col("BookRevenue", "Doanh thu sách", 110, "money");
        _revenueGrid.Col("ShippingFee", "Phí vận chuyển", 100, "money");
        _revenueGrid.Col("Total", "Tổng thu", 110, "money");
        var revenuePage = new TabPage("💰 Doanh thu") { BackColor = Ui.Surface, Padding = new Padding(10) };
        revenuePage.Controls.Add(_revenueGrid);
        revenuePage.Controls.Add(_revenueSummary);
        revenuePage.Controls.Add(_revenueChart);

        // Sách bán chạy
        _topGrid.Col("Rank", "Hạng", 40, "int");
        _topGrid.Col("Title", "Tên sách", 300);
        _topGrid.Col("Quantity", "Số lượng bán", 90, "int");
        _topGrid.Col("Revenue", "Doanh thu", 110, "money");
        var topPage = new TabPage("🔥 Sách bán chạy") { BackColor = Ui.Surface, Padding = new Padding(10) };
        topPage.Controls.Add(_topGrid);

        // Đơn theo trạng thái
        _statusGrid.Col("StatusText", "Trạng thái", 160);
        _statusGrid.Col("Count", "Số đơn", 80, "int");
        var statusPage = new TabPage("🧾 Đơn hàng") { BackColor = Ui.Surface, Padding = new Padding(10) };
        statusPage.Controls.Add(_statusGrid);
        statusPage.Controls.Add(_statusChart);

        // Tồn kho theo thể loại
        _stockGrid.Col("CategoryName", "Thể loại", 160);
        _stockGrid.Col("BookCount", "Số đầu sách", 80, "int");
        _stockGrid.Col("TotalStock", "Tổng tồn (cuốn)", 90, "int");
        _stockGrid.Col("StockValue", "Giá trị tồn (giá bán)", 120, "money");
        _stockGrid.Col("LowStockCount", "Sắp hết hàng", 80, "int");
        var stockPage = new TabPage("📦 Tồn kho") { BackColor = Ui.Surface, Padding = new Padding(10) };
        stockPage.Controls.Add(_stockGrid);

        _tabs.TabPages.AddRange(new[] { revenuePage, topPage, statusPage, stockPage });
        Controls.Add(_tabs);
        Controls.Add(toolbar);
    }

    private async Task QuickRange(int fromDays, int grouping)
    {
        _from.Value = DateTime.Today.AddDays(fromDays);
        _to.Value = DateTime.Today;
        _grouping.SelectedIndex = grouping;
        await Safe(LoadDataAsync);
    }

    public record RankedBook(int Rank, string Title, int Quantity, decimal Revenue);

    public override async Task LoadDataAsync()
    {
        if (_from.Value.Date > _to.Value.Date)
        {
            Ui.Error("Khoảng thời gian không hợp lệ: ngày bắt đầu lớn hơn ngày kết thúc.");
            return;
        }
        var from = _from.Value.Date;
        var to = _to.Value.Date;
        var grouping = _grouping.SelectedIndex == 1 ? RevenueGrouping.Month : RevenueGrouping.Day;

        _revenue = await Run<ReportService, List<RevenuePoint>>(s => s.GetRevenueAsync(from, to, grouping));
        _top = await Run<ReportService, List<TopBookItem>>(s => s.GetTopBooksAsync(from, to, 20));
        _status = await Run<ReportService, List<StatusCount>>(s => s.GetOrderStatusStatsAsync(from, to));
        _stock = await Run<ReportService, List<CategoryStockItem>>(s => s.GetStockByCategoryAsync());

        _revenueChart.ValueFormatter = Ui.Money;
        _revenueChart.SetData(_revenue.Select(r => (r.Label, r.BookRevenue)));
        _revenueGrid.Bind(_revenue);
        _revenueSummary.Text = $"Tổng: {_revenue.Sum(r => r.OrderCount)} đơn hoàn tất · Doanh thu sách {Ui.Money(_revenue.Sum(r => r.BookRevenue))} · Phí vận chuyển {Ui.Money(_revenue.Sum(r => r.ShippingFee))}";

        _topGrid.Bind(_top.Select((t, i) => new RankedBook(i + 1, t.Title, t.Quantity, t.Revenue)).ToList());
        _statusChart.SetData(_status.Select(s => (s.StatusText, (decimal)s.Count)));
        _statusGrid.Bind(_status);
        _stockGrid.Bind(_stock);
    }

    private void ExportExcel()
    {
        if (_revenue.Count == 0 && _stock.Count == 0)
        {
            Ui.Info("Chưa có dữ liệu. Vui lòng bấm Thống kê trước.");
            return;
        }
        using var dlg = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = $"BaoCao_BookStore_{_from.Value:yyyyMMdd}_{_to.Value:yyyyMMdd}.xlsx"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            using var wb = new XLWorkbook();
            var period = $"Từ {_from.Value:dd/MM/yyyy} đến {_to.Value:dd/MM/yyyy}";

            var ws = Sheet(wb, "Doanh thu", "BÁO CÁO DOANH THU", period,
                new[] { "Thời gian", "Số đơn hoàn tất", "Doanh thu sách", "Phí vận chuyển", "Tổng thu" });
            int row = 5;
            foreach (var r in _revenue)
            {
                ws.Cell(row, 1).Value = r.Label;
                ws.Cell(row, 2).Value = r.OrderCount;
                ws.Cell(row, 3).Value = r.BookRevenue;
                ws.Cell(row, 4).Value = r.ShippingFee;
                ws.Cell(row, 5).Value = r.Total;
                row++;
            }
            ws.Cell(row, 1).Value = "TỔNG CỘNG";
            ws.Cell(row, 2).FormulaA1 = $"SUM(B5:B{row - 1})";
            ws.Cell(row, 3).FormulaA1 = $"SUM(C5:C{row - 1})";
            ws.Cell(row, 4).FormulaA1 = $"SUM(D5:D{row - 1})";
            ws.Cell(row, 5).FormulaA1 = $"SUM(E5:E{row - 1})";
            ws.Row(row).Style.Font.Bold = true;
            ws.Range(5, 3, row, 5).Style.NumberFormat.Format = "#,##0";
            ws.Columns().AdjustToContents();

            ws = Sheet(wb, "Sách bán chạy", "SÁCH BÁN CHẠY", period, new[] { "Hạng", "Tên sách", "Số lượng bán", "Doanh thu" });
            row = 5;
            for (int i = 0; i < _top.Count; i++, row++)
            {
                ws.Cell(row, 1).Value = i + 1;
                ws.Cell(row, 2).Value = _top[i].Title;
                ws.Cell(row, 3).Value = _top[i].Quantity;
                ws.Cell(row, 4).Value = _top[i].Revenue;
            }
            ws.Range(5, 4, Math.Max(5, row), 4).Style.NumberFormat.Format = "#,##0";
            ws.Columns().AdjustToContents();

            ws = Sheet(wb, "Đơn hàng", "ĐƠN HÀNG THEO TRẠNG THÁI", period, new[] { "Trạng thái", "Số đơn" });
            row = 5;
            foreach (var s in _status)
            {
                ws.Cell(row, 1).Value = s.StatusText;
                ws.Cell(row++, 2).Value = s.Count;
            }
            ws.Columns().AdjustToContents();

            ws = Sheet(wb, "Tồn kho", "TỒN KHO THEO THỂ LOẠI", $"Tại thời điểm {DateTime.Now:dd/MM/yyyy HH:mm}",
                new[] { "Thể loại", "Số đầu sách", "Tổng tồn", "Giá trị tồn", "Sắp hết hàng" });
            row = 5;
            foreach (var s in _stock)
            {
                ws.Cell(row, 1).Value = s.CategoryName;
                ws.Cell(row, 2).Value = s.BookCount;
                ws.Cell(row, 3).Value = s.TotalStock;
                ws.Cell(row, 4).Value = s.StockValue;
                ws.Cell(row++, 5).Value = s.LowStockCount;
            }
            ws.Range(5, 4, Math.Max(5, row), 4).Style.NumberFormat.Format = "#,##0";
            ws.Columns().AdjustToContents();

            wb.SaveAs(dlg.FileName);
            if (Ui.Confirm("Đã xuất báo cáo Excel thành công. Mở file ngay?", "Xuất Excel"))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch (IOException)
        {
            Ui.Error("Không ghi được file. Có thể file đang được mở trong Excel, hãy đóng file và thử lại.");
        }
        catch (Exception ex)
        {
            Ui.ShowException(ex);
        }
    }

    private IXLWorksheet Sheet(XLWorkbook wb, string name, string title, string period, string[] headers)
    {
        var ws = wb.Worksheets.Add(name);
        ws.Cell(1, 1).Value = "NHÀ SÁCH BOOKSTORE – " + title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = period + $" · Người lập: {Actor.FullName} · Ngày lập: {DateTime.Now:dd/MM/yyyy HH:mm}";
        for (int i = 0; i < headers.Length; i++)
        {
            var c = ws.Cell(4, i + 1);
            c.Value = headers[i];
            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor = XLColor.FromArgb(31, 78, 121);
            c.Style.Font.FontColor = XLColor.White;
        }
        return ws;
    }
}
