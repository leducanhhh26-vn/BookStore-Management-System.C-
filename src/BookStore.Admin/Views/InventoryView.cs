using BookStore.Admin.Infrastructure;
using BookStore.Business.Common;
using BookStore.Business.Services;
using BookStore.Domain;

namespace BookStore.Admin.Views;

/// <summary>
/// Quản lý kho gồm 3 thẻ: Tồn kho (cảnh báo sắp hết), Phiếu nhập, Nhật ký nhập/xuất kho.
/// Tồn kho chỉ thay đổi qua phiếu nhập, đơn hàng, hủy đơn hoặc điều chỉnh kiểm kê – luôn có dấu vết.
/// </summary>
public class InventoryView : ViewBase
{
    public override string Title => "Quản lý kho";

    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill, Font = Ui.BaseFont, Padding = new Point(16, 6) };

    // Thẻ tồn kho
    private readonly TextBox _stockKeyword = Ui.TextBox("Tên sách, ISBN...", 220);
    private readonly ComboBox _stockCategory = Ui.Combo(170);
    private readonly CheckBox _lowOnly = new() { Text = "Chỉ sách sắp hết hàng", AutoSize = true, Margin = new Padding(0, 8, 12, 0) };
    private readonly DataGridView _stockGrid = Ui.Grid();
    private readonly Label _stockSummary = Ui.Label("", Ui.SmallFont, Ui.Muted);

    // Thẻ phiếu nhập
    private readonly DataGridView _receiptGrid = Ui.Grid();
    private readonly DataGridView _receiptLines = Ui.Grid();

    // Thẻ nhật ký
    private readonly ComboBox _logType = Ui.Combo(180);
    private readonly DateTimePicker _logFrom = new() { Format = DateTimePickerFormat.Short, Width = 120, Margin = new Padding(0, 4, 8, 0) };
    private readonly DateTimePicker _logTo = new() { Format = DateTimePickerFormat.Short, Width = 120, Margin = new Padding(0, 4, 8, 0) };
    private readonly DataGridView _logGrid = Ui.Grid();

    public InventoryView(ServiceRunner services, AppSession session) : base(services, session)
    {
        _tabs.TabPages.Add(BuildStockTab());
        _tabs.TabPages.Add(BuildReceiptTab());
        _tabs.TabPages.Add(BuildLogTab());
        _tabs.SelectedIndexChanged += async (_, _) => await Safe(LoadCurrentTabAsync);
        Controls.Add(_tabs);
    }

    // ===================== Thẻ 1: Tồn kho =====================

    private TabPage BuildStockTab()
    {
        var page = new TabPage("📦 Tồn kho") { BackColor = Ui.Background, Padding = new Padding(10) };
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(_stockKeyword);
        toolbar.Controls.Add(_stockCategory);
        toolbar.Controls.Add(_lowOnly);
        toolbar.Controls.Add(Ui.Button("🔍 Tìm", async (_, _) => await Safe(LoadStockAsync)));
        toolbar.Controls.Add(Ui.Button("+ Lập phiếu nhập", async (_, _) => await CreateReceiptAsync(), ButtonKind.Primary));
        toolbar.Controls.Add(Ui.Button("⚖ Điều chỉnh tồn (kiểm kê)", async (_, _) => await AdjustAsync()));
        toolbar.Controls.Add(Ui.Button("📜 Lịch sử sách này", async (_, _) => await ShowBookHistoryAsync()));
        _lowOnly.CheckedChanged += async (_, _) => await Safe(LoadStockAsync);
        _stockKeyword.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Safe(LoadStockAsync); } };

        _stockGrid.Col("BookId", "Mã", 40, "int");
        _stockGrid.Col("Isbn", "ISBN", 100);
        _stockGrid.Col("Title", "Tên sách", 240);
        _stockGrid.Col("CategoryName", "Thể loại", 110);
        _stockGrid.Col("Price", "Giá bán", 80, "money");
        _stockGrid.Col("Stock", "Tồn kho", 60, "int");
        _stockGrid.Col("LowStockThreshold", "Ngưỡng cảnh báo", 70, "int");
        _stockGrid.Col("StockValue", "Giá trị tồn", 100, "money");
        _stockGrid.Col("IsLowStock", "Tình trạng", 90);
        _stockGrid.CellFormatting += (_, e) =>
        {
            if (_stockGrid.Columns[e.ColumnIndex].Name != "IsLowStock" || _stockGrid.Rows[e.RowIndex].DataBoundItem is not StockItem s) return;
            e.Value = s.Stock == 0 ? "Hết hàng" : s.IsLowStock ? "⚠ Sắp hết" : "Đủ hàng";
            e.CellStyle!.ForeColor = s.IsLowStock ? Ui.Danger : Ui.Success;
            e.CellStyle.Font = Ui.BoldFont;
        };
        _stockGrid.RowPrePaint += (_, e) =>
        {
            if (_stockGrid.Rows[e.RowIndex].DataBoundItem is StockItem s)
                _stockGrid.Rows[e.RowIndex].DefaultCellStyle.BackColor = s.IsLowStock ? Ui.LowStockBack : Color.Empty;
        };

        _stockSummary.Dock = DockStyle.Bottom;
        page.Controls.Add(_stockGrid);
        page.Controls.Add(_stockSummary);
        page.Controls.Add(toolbar);
        return page;
    }

    private async Task LoadStockAsync()
    {
        var categoryId = _stockCategory.SelectedValue as int?;
        var items = await Run<InventoryService, List<StockItem>>(s => s.GetStockAsync(_stockKeyword.Text, categoryId, _lowOnly.Checked));
        _stockGrid.Bind(items, i => i.BookId);
        int low = items.Count(i => i.IsLowStock);
        _stockSummary.Text = $"{items.Count} đầu sách · Tổng tồn {items.Sum(i => i.Stock):N0} cuốn · Giá trị tồn {Ui.Money(items.Sum(i => i.StockValue))}" +
                             (low > 0 ? $" · ⚠ {low} đầu sách sắp hết hàng cần nhập thêm" : "");
        _stockSummary.ForeColor = low > 0 ? Ui.Danger : Ui.Muted;
    }

    private async Task CreateReceiptAsync()
    {
        await Safe(async () =>
        {
            using var form = new ImportReceiptForm(Services, Session);
            // Gợi ý: nếu đang chọn một sách sắp hết thì thêm sẵn vào phiếu
            await form.LoadAsync(_stockGrid.Selected<StockItem>());
            if (form.ShowDialog(this) == DialogResult.OK) await LoadCurrentTabAsync();
        });
    }

    private async Task AdjustAsync()
    {
        if (_stockGrid.Selected<StockItem>() is not { } item) { Ui.Info("Vui lòng chọn đầu sách cần điều chỉnh."); return; }
        using var form = new AdjustStockForm(Services, Session, item);
        if (form.ShowDialog(this) == DialogResult.OK) await Safe(LoadStockAsync);
    }

    private async Task ShowBookHistoryAsync()
    {
        if (_stockGrid.Selected<StockItem>() is not { } item) { Ui.Info("Vui lòng chọn một đầu sách."); return; }
        await Safe(async () =>
        {
            var logs = await Run<InventoryService, List<StockTransactionItem>>(s => s.GetTransactionsAsync(item.BookId));
            using var form = new Form
            {
                Text = "Lịch sử nhập/xuất: " + item.Title, Size = new Size(900, 520), StartPosition = FormStartPosition.CenterParent,
                Font = Ui.BaseFont, Padding = new Padding(12)
            };
            var grid = Ui.Grid();
            ConfigureLogColumns(grid);
            form.Controls.Add(grid);
            grid.Bind(logs);
            form.ShowDialog(this);
        });
    }

    // ===================== Thẻ 2: Phiếu nhập =====================

    private TabPage BuildReceiptTab()
    {
        var page = new TabPage("🧾 Phiếu nhập") { BackColor = Ui.Background, Padding = new Padding(10) };
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(Ui.Button("+ Lập phiếu nhập", async (_, _) => await CreateReceiptAsync(), ButtonKind.Primary));
        toolbar.Controls.Add(Ui.Button("↻ Làm mới", async (_, _) => await Safe(LoadReceiptsAsync)));

        _receiptGrid.Col("Id", "Số phiếu", 50, "int");
        _receiptGrid.Col("ImportDate", "Ngày nhập", 100, "date");
        _receiptGrid.Col("SupplierName", "Nhà cung cấp", 200);
        _receiptGrid.Col("StaffName", "Người lập", 130);
        _receiptGrid.Col("TotalQuantity", "Tổng SL", 60, "int");
        _receiptGrid.Col("TotalCost", "Tổng tiền nhập", 110, "money");
        _receiptGrid.Col("Note", "Ghi chú", 160);
        _receiptGrid.SelectionChanged += async (_, _) => await Safe(LoadReceiptLinesAsync);

        _receiptLines.Col("BookId", "Mã sách", 50, "int");
        _receiptLines.Col("Title", "Tên sách", 260);
        _receiptLines.Col("Quantity", "Số lượng", 60, "int");
        _receiptLines.Col("UnitCost", "Đơn giá nhập", 90, "money");
        _receiptLines.Col("LineTotal", "Thành tiền", 100, "money");

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 260 };
        split.Panel1.Controls.Add(_receiptGrid);
        split.Panel2.Controls.Add(_receiptLines);
        split.Panel2.Controls.Add(new Label { Text = "Chi tiết phiếu nhập đang chọn", Dock = DockStyle.Top, Height = 28, Font = Ui.HeaderFont });

        page.Controls.Add(split);
        page.Controls.Add(toolbar);
        return page;
    }

    private async Task LoadReceiptsAsync()
    {
        var items = await Run<InventoryService, List<ImportReceiptItem>>(s => s.GetReceiptsAsync());
        _receiptGrid.Bind(items, r => r.Id);
        await LoadReceiptLinesAsync();
    }

    private async Task LoadReceiptLinesAsync()
    {
        if (_receiptGrid.Selected<ImportReceiptItem>() is not { } r)
        {
            _receiptLines.DataSource = null;
            return;
        }
        _receiptLines.Bind(await Run<InventoryService, List<ImportReceiptLineItem>>(s => s.GetReceiptLinesAsync(r.Id)));
    }

    // ===================== Thẻ 3: Nhật ký kho =====================

    private TabPage BuildLogTab()
    {
        var page = new TabPage("📜 Nhật ký nhập/xuất") { BackColor = Ui.Background, Padding = new Padding(10) };
        _logType.Items.Add("Tất cả loại");
        foreach (var t in Enum.GetValues<StockTransactionType>()) _logType.Items.Add(t.ToVietnamese());
        _logType.SelectedIndex = 0;
        _logFrom.Value = DateTime.Today.AddDays(-30);
        _logTo.Value = DateTime.Today;

        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(_logType);
        toolbar.Controls.Add(Ui.Label("Từ"));
        toolbar.Controls.Add(_logFrom);
        toolbar.Controls.Add(Ui.Label("Đến"));
        toolbar.Controls.Add(_logTo);
        toolbar.Controls.Add(Ui.Button("🔍 Xem", async (_, _) => await Safe(LoadLogAsync), ButtonKind.Primary));

        ConfigureLogColumns(_logGrid);
        page.Controls.Add(_logGrid);
        page.Controls.Add(toolbar);
        return page;
    }

    private static void ConfigureLogColumns(DataGridView g)
    {
        g.Col("CreatedAt", "Thời gian", 100, "date");
        g.Col("BookTitle", "Sách", 220);
        g.Col("TypeText", "Loại", 110);
        g.Col("QuantityChange", "Thay đổi", 60, "+#,##0;-#,##0;0", right: true);
        g.Col("StockAfter", "Tồn sau", 60, "int");
        g.Col("StaffName", "Người thực hiện", 120);
        g.Col("Reason", "Diễn giải", 220);
        g.CellFormatting += (_, e) =>
        {
            if (g.Columns[e.ColumnIndex].Name == "QuantityChange" && e.Value is int change)
                e.CellStyle!.ForeColor = change > 0 ? Ui.Success : Ui.Danger;
            if (g.Columns[e.ColumnIndex].Name == "StaffName" && e.Value == null) e.Value = "Khách đặt hàng (Web)";
        };
    }

    private async Task LoadLogAsync()
    {
        StockTransactionType? type = _logType.SelectedIndex <= 0 ? null : (StockTransactionType)_logType.SelectedIndex;
        var items = await Run<InventoryService, List<StockTransactionItem>>(s => s.GetTransactionsAsync(null, type, _logFrom.Value, _logTo.Value));
        _logGrid.Bind(items);
    }

    // ===================== Chung =====================

    public override async Task LoadDataAsync()
    {
        var categories = await Run<CategoryService, List<LookupItem>>(s => s.GetLookupAsync());
        _stockCategory.DisplayMember = "Name";
        _stockCategory.ValueMember = "Id";
        _stockCategory.DataSource = new[] { new LookupItem(0, "Tất cả thể loại") }.Concat(categories).ToList();
        _stockCategory.SelectedIndexChanged += async (_, _) => await Safe(LoadStockAsync);
        await LoadCurrentTabAsync();
    }

    private Task LoadCurrentTabAsync() => _tabs.SelectedIndex switch
    {
        1 => LoadReceiptsAsync(),
        2 => LoadLogAsync(),
        _ => LoadStockAsync()
    };
}

/// <summary>Lập phiếu nhập hàng nhiều dòng sách.</summary>
public class ImportReceiptForm : DialogBase
{
    private readonly ServiceRunner _services;
    private readonly AppSession _session;
    private readonly ComboBox _supplier = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _book = new() { DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
    private readonly NumericUpDown _qty = Number(1, 100000, 10);
    private readonly NumericUpDown _cost = Number(0, 100_000_000, 50000, 0, 1000);
    private readonly TextBox _note = new();
    private readonly DataGridView _lines = Ui.Grid();
    private readonly Label _total = Ui.Label("Tổng: 0 ₫", Ui.BoldFont);
    private readonly List<Line> _items = new();

    public class Line
    {
        public int BookId { get; init; }
        public string Title { get; init; } = "";
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal LineTotal => Quantity * UnitCost;
    }

    public ImportReceiptForm(ServiceRunner services, AppSession session) : base("Lập phiếu nhập hàng", 860)
    {
        _services = services;
        _session = session;

        AddField("Nhà cung cấp *", _supplier);
        AddField("Ghi chú", _note);

        var addPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        _book.Width = 320;
        _qty.Width = 80;
        _cost.Width = 120;
        addPanel.Controls.Add(_book);
        addPanel.Controls.Add(Ui.Label("SL"));
        addPanel.Controls.Add(_qty);
        addPanel.Controls.Add(Ui.Label("Giá nhập"));
        addPanel.Controls.Add(_cost);
        addPanel.Controls.Add(Ui.Button("+ Thêm dòng", (_, _) => AddLine(), ButtonKind.Primary));
        AddField("Sách nhập", addPanel, 40);

        _lines.ReadOnly = false;
        _lines.Col("Title", "Tên sách", 260).ReadOnly = true;
        _lines.Col("Quantity", "Số lượng", 70, "int");
        _lines.Col("UnitCost", "Đơn giá nhập", 100, "money");
        _lines.Col("LineTotal", "Thành tiền", 100, "money").ReadOnly = true;
        _lines.CellEndEdit += (_, _) => { _lines.Refresh(); UpdateTotal(); };
        var linesPanel = new Panel { Height = 220 };
        _lines.Dock = DockStyle.Fill;
        linesPanel.Controls.Add(_lines);
        AddField("Danh sách dòng\n(sửa trực tiếp SL/giá)", linesPanel, 220);

        var bottom = new FlowLayoutPanel { AutoSize = true };
        bottom.Controls.Add(Ui.Button("Xóa dòng đang chọn", (_, _) => RemoveLine(), ButtonKind.Danger));
        bottom.Controls.Add(_total);
        AddField("", bottom, 40);

        SaveButton.Text = "Hoàn tất nhập hàng";
        SaveButton.AutoSize = true;
        OnSave(SaveAsync);
    }

    public async Task LoadAsync(StockItem? suggested)
    {
        _supplier.DisplayMember = "Name";
        _supplier.ValueMember = "Id";
        _supplier.DataSource = await _services.Run<SupplierService, List<LookupItem>>(s => s.GetLookupAsync());

        var books = await _services.Run<InventoryService, List<StockItem>>(s => s.GetStockAsync());
        _book.DisplayMember = "Title";
        _book.ValueMember = "BookId";
        _book.DataSource = books.OrderBy(b => b.Title).ToList();
        if (suggested != null)
        {
            _book.SelectedValue = suggested.BookId;
            _cost.Value = Math.Round(suggested.Price * 0.6m / 1000) * 1000;
        }
        BindLines();
    }

    private void AddLine()
    {
        if (_book.SelectedItem is not StockItem book) { Ui.Info("Vui lòng chọn sách trong danh sách."); return; }
        if (_cost.Value <= 0) { Ui.Info("Đơn giá nhập phải lớn hơn 0."); return; }
        var existing = _items.FirstOrDefault(i => i.BookId == book.BookId);
        if (existing != null)
        {
            existing.Quantity += (int)_qty.Value;
            existing.UnitCost = _cost.Value;
        }
        else
        {
            _items.Add(new Line { BookId = book.BookId, Title = book.Title, Quantity = (int)_qty.Value, UnitCost = _cost.Value });
        }
        BindLines();
    }

    private void RemoveLine()
    {
        if (_lines.Selected<Line>() is { } line)
        {
            _items.Remove(line);
            BindLines();
        }
    }

    private void BindLines()
    {
        _lines.DataSource = null;
        _lines.DataSource = new System.ComponentModel.BindingList<Line>(_items);
        UpdateTotal();
    }

    private void UpdateTotal() => _total.Text = $"   {_items.Count} dòng · {_items.Sum(i => i.Quantity):N0} cuốn · Tổng: {Ui.Money(_items.Sum(i => i.LineTotal))}";

    private async Task<Result> SaveAsync()
    {
        var dto = new ImportReceiptDto
        {
            SupplierId = _supplier.SelectedValue as int? ?? 0,
            Note = _note.Text,
            Lines = _items.Select(i => new ImportLine(i.BookId, i.Quantity, i.UnitCost)).ToList()
        };
        var result = await _services.Run<InventoryService, Result<int>>(s => s.CreateImportReceiptAsync(_session.User, dto));
        if (result.Success) Ui.Info(result.Message);
        return result;
    }
}

/// <summary>Điều chỉnh tồn kho sau kiểm kê (bắt buộc ghi lý do).</summary>
public class AdjustStockForm : DialogBase
{
    public AdjustStockForm(ServiceRunner services, AppSession session, StockItem item) : base("Điều chỉnh tồn kho")
    {
        AddField("Sách", new Label { Text = item.Title, AutoSize = true, Font = Ui.BoldFont });
        AddField("Tồn hiện tại", new Label { Text = item.Stock.ToString("N0"), AutoSize = true });
        var newStock = AddField("Tồn thực tế *", Number(0, 100000, item.Stock));
        var reason = AddField("Lý do *", new TextBox { Multiline = true, PlaceholderText = "VD: Kiểm kê phát hiện 2 cuốn rách bìa" }, 70);
        OnSave(async () =>
        {
            var result = await services.Run<InventoryService, Result>(s => s.AdjustStockAsync(session.User, item.BookId, (int)newStock.Value, reason.Text));
            if (result.Success) Ui.Info(result.Message);
            return result;
        });
    }
}
