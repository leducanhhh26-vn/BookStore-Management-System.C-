using BookStore.Admin.Infrastructure;
using BookStore.Business.Common;
using BookStore.Business.Services;
using BookStore.Domain;

namespace BookStore.Admin.Views;

/// <summary>
/// Quản lý đơn hàng từ website: lọc theo trạng thái/ngày, xem chi tiết, xác nhận, giao hàng, hoàn tất, hủy.
/// Danh sách tự làm mới mỗi 15 giây để thấy ngay đơn khách vừa đặt.
/// </summary>
public class OrdersView : ViewBase
{
    public override string Title => "Quản lý đơn hàng";

    private readonly ComboBox _status = Ui.Combo(170);
    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Short, Width = 120, ShowCheckBox = true, Checked = false, Margin = new Padding(0, 4, 8, 0) };
    private readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Short, Width = 120, ShowCheckBox = true, Checked = false, Margin = new Padding(0, 4, 8, 0) };
    private readonly TextBox _keyword = Ui.TextBox("Mã đơn, tên, SĐT...", 200);
    private readonly DataGridView _grid = Ui.Grid();
    private readonly Label _summary = Ui.Label("", Ui.SmallFont, Ui.Muted);
    private readonly System.Windows.Forms.Timer _refresh = new() { Interval = 15000 };

    public OrdersView(ServiceRunner services, AppSession session) : base(services, session)
    {
        _status.Items.Add("Tất cả trạng thái");
        foreach (var s in Enum.GetValues<OrderStatus>()) _status.Items.Add(s.ToVietnamese());
        _status.SelectedIndex = 1; // mặc định: Chờ xác nhận
        _status.SelectedIndexChanged += async (_, _) => await Safe(LoadDataAsync);

        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(_status);
        toolbar.Controls.Add(Ui.Label("Từ"));
        toolbar.Controls.Add(_from);
        toolbar.Controls.Add(Ui.Label("Đến"));
        toolbar.Controls.Add(_to);
        toolbar.Controls.Add(_keyword);
        toolbar.Controls.Add(Ui.Button("🔍 Tìm", async (_, _) => await Safe(LoadDataAsync)));
        toolbar.Controls.Add(Ui.Button("📄 Xem / Xử lý", async (_, _) => await OpenSelectedAsync(), ButtonKind.Primary));
        toolbar.Controls.Add(Ui.Button("✔ Xác nhận nhanh", async (_, _) => await QuickConfirmAsync(), ButtonKind.Success));
        toolbar.Controls.Add(Ui.Button("↻ Làm mới", async (_, _) => await Safe(LoadDataAsync)));
        _keyword.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Safe(LoadDataAsync); } };

        _grid.Col("Id", "Mã đơn", 55, "int");
        _grid.Col("OrderDate", "Ngày đặt", 105, "date");
        _grid.Col("CustomerName", "Khách hàng", 130);
        _grid.Col("ReceiverPhone", "SĐT nhận", 90);
        _grid.Col("ItemCount", "SL sách", 55, "int");
        _grid.Col("Total", "Tổng tiền", 95, "money");
        _grid.Col("PaymentMethod", "Thanh toán", 90);
        _grid.Col("PaymentStatus", "TT thanh toán", 80);
        _grid.Col("StatusText", "Trạng thái", 95);
        _grid.Col("HandledBy", "NV xử lý", 110);
        _grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await OpenSelectedAsync(); };
        _grid.CellFormatting += (_, e) =>
        {
            if (_grid.Rows[e.RowIndex].DataBoundItem is not OrderListItem o) return;
            var name = _grid.Columns[e.ColumnIndex].Name;
            if (name == "StatusText")
            {
                e.CellStyle!.ForeColor = StatusColor(o.Status);
                e.CellStyle.Font = Ui.BoldFont;
            }
            else if (name == "PaymentMethod") e.Value = o.PaymentMethod == PaymentMethod.COD ? "COD" : "Chuyển khoản";
            else if (name == "PaymentStatus") e.Value = o.PaymentStatus.ToVietnamese();
            else if (name == "HandledBy" && e.Value == null) e.Value = "—";
        };

        _summary.Dock = DockStyle.Bottom;
        Controls.Add(_grid);
        Controls.Add(_summary);
        Controls.Add(toolbar);

        _refresh.Tick += async (_, _) =>
        {
            try { await LoadDataAsync(); } catch { /* lỗi tạm thời, thử lại lần sau */ }
        };
        _refresh.Start();
        Disposed += (_, _) => _refresh.Dispose();
    }

    public static Color StatusColor(OrderStatus s) => s switch
    {
        OrderStatus.Pending => Color.FromArgb(176, 120, 0),
        OrderStatus.Confirmed => Ui.Primary,
        OrderStatus.Shipping => Color.FromArgb(91, 63, 122),
        OrderStatus.Completed => Ui.Success,
        OrderStatus.Cancelled => Ui.Danger,
        _ => Color.Black
    };

    public override async Task LoadDataAsync()
    {
        var query = new OrderQuery
        {
            Status = _status.SelectedIndex <= 0 ? null : (OrderStatus)_status.SelectedIndex,
            FromDate = _from.Checked ? _from.Value : null,
            ToDate = _to.Checked ? _to.Value : null,
            Keyword = _keyword.Text
        };
        var orders = await Run<OrderService, List<OrderListItem>>(s => s.SearchAsync(query));
        _grid.Bind(orders, o => o.Id);
        _summary.Text = $"{orders.Count} đơn hàng · Tổng giá trị {Ui.Money(orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.Total))} · Tự làm mới mỗi 15 giây";
    }

    private async Task OpenSelectedAsync()
    {
        if (_grid.Selected<OrderListItem>() is not { } o) { Ui.Info("Vui lòng chọn một đơn hàng."); return; }
        await Safe(async () =>
        {
            if (await OrderDetailForm.ShowAsync(this, Services, Session, o.Id)) await LoadDataAsync();
        });
    }

    private async Task QuickConfirmAsync()
    {
        if (_grid.Selected<OrderListItem>() is not { } o) { Ui.Info("Vui lòng chọn một đơn hàng."); return; }
        if (o.Status != OrderStatus.Pending) { Ui.Info("Chỉ xác nhận nhanh được đơn đang chờ xác nhận."); return; }
        if (!Ui.Confirm($"Xác nhận đơn #{o.Id} của {o.CustomerName} ({Ui.Money(o.Total)})?")) return;
        await Safe(async () =>
        {
            if (Ui.Show(await Run<OrderService, Result>(s => s.ChangeStatusAsync(Actor, o.Id, OrderStatus.Confirmed)))) await LoadDataAsync();
        });
    }
}

/// <summary>Chi tiết đơn hàng và các nút xử lý theo đúng luồng trạng thái.</summary>
public class OrderDetailForm : Form
{
    private readonly ServiceRunner _services;
    private readonly AppSession _session;
    private OrderDetailDto _order = new();
    private bool _changed;

    private readonly Label _header = new() { Dock = DockStyle.Top, Height = 40, Font = Ui.TitleFont, ForeColor = Ui.PrimaryDark };
    private readonly Label _info = new() { Dock = DockStyle.Top, AutoSize = false, Height = 150, Font = Ui.BaseFont };
    private readonly DataGridView _lines = Ui.Grid();
    private readonly Label _totals = new() { Dock = DockStyle.Bottom, Height = 100, Font = Ui.BaseFont, TextAlign = ContentAlignment.TopRight };
    private readonly FlowLayoutPanel _actions = new() { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(0, 10, 0, 0), FlowDirection = FlowDirection.RightToLeft };

    private OrderDetailForm(ServiceRunner services, AppSession session)
    {
        _services = services;
        _session = session;
        Text = "Chi tiết đơn hàng";
        Font = Ui.BaseFont;
        Size = new Size(920, 700);
        MinimumSize = new Size(800, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Ui.Surface;
        Padding = new Padding(20);
        AutoScaleMode = AutoScaleMode.Font;

        _lines.Col("BookId", "Mã sách", 50, "int");
        _lines.Col("Title", "Tên sách", 260);
        _lines.Col("Quantity", "SL", 40, "int");
        _lines.Col("UnitPrice", "Đơn giá", 90, "money");
        _lines.Col("LineTotal", "Thành tiền", 100, "money");

        Controls.Add(_lines);
        Controls.Add(_totals);
        Controls.Add(_actions);
        Controls.Add(_info);
        Controls.Add(_header);
    }

    /// <summary>Mở form; trả về true nếu đơn hàng đã được thay đổi.</summary>
    public static async Task<bool> ShowAsync(IWin32Window owner, ServiceRunner services, AppSession session, int orderId)
    {
        using var form = new OrderDetailForm(services, session);
        if (!await form.ReloadAsync(orderId)) return false;
        form.ShowDialog(owner);
        return form._changed;
    }

    private async Task<bool> ReloadAsync(int orderId)
    {
        var order = await _services.Run<OrderService, OrderDetailDto?>(s => s.GetDetailAsync(orderId));
        if (order == null)
        {
            Ui.Error("Đơn hàng không tồn tại.");
            return false;
        }
        _order = order;
        Render();
        return true;
    }

    private void Render()
    {
        var o = _order;
        _header.Text = $"Đơn hàng #{o.Id}  ·  {o.Status.ToVietnamese()}";
        _header.ForeColor = OrdersView.StatusColor(o.Status);
        _info.Text =
            $"Ngày đặt: {o.OrderDate:dd/MM/yyyy HH:mm}" + (o.CompletedAt != null ? $"     Hoàn tất: {o.CompletedAt:dd/MM/yyyy HH:mm}" : "") + "\n" +
            $"Khách hàng: {o.CustomerName} ({o.CustomerEmail})\n" +
            $"Người nhận: {o.ReceiverName} – {o.ReceiverPhone}\n" +
            $"Địa chỉ: {o.ShippingAddress}\n" +
            $"Thanh toán: {o.PaymentMethod.ToVietnamese()} – {o.PaymentStatus.ToVietnamese()}" +
            (o.Payments.LastOrDefault()?.ReferenceCode is { } refCode ? $" (Mã GD: {refCode})" : "") + "\n" +
            $"Nhân viên xử lý: {o.HandledBy ?? "chưa có"}" +
            (string.IsNullOrEmpty(o.Note) ? "" : $"\nGhi chú của khách: {o.Note}") +
            (o.Status == OrderStatus.Cancelled ? $"\nLý do hủy: {o.CancelReason}" : "");
        _lines.Bind(o.Lines);
        _totals.Text =
            $"Tạm tính: {Ui.Money(o.Subtotal)}\n" +
            (o.DiscountAmount > 0 ? $"Giảm giá ({o.CouponCode}): −{Ui.Money(o.DiscountAmount)}\n" : "") +
            $"Phí vận chuyển: {(o.ShippingFee == 0 ? "Miễn phí" : Ui.Money(o.ShippingFee))}\n" +
            $"TỔNG THANH TOÁN: {Ui.Money(o.Total)}";
        _totals.Font = Ui.BoldFont;

        // Chỉ hiện các nút hợp lệ với trạng thái hiện tại
        _actions.Controls.Clear();
        _actions.Controls.Add(Ui.Button("Đóng", (_, _) => Close()));
        foreach (var next in o.NextStatuses.Reverse())
        {
            if (next == OrderStatus.Cancelled)
                _actions.Controls.Add(Ui.Button("✖ Hủy đơn", async (_, _) => await CancelAsync(), ButtonKind.Danger));
            else
                _actions.Controls.Add(Ui.Button(next switch
                {
                    OrderStatus.Confirmed => "✔ Xác nhận đơn",
                    OrderStatus.Shipping => "🚚 Bắt đầu giao hàng",
                    OrderStatus.Completed => "🏁 Giao thành công",
                    _ => next.ToVietnamese()
                }, async (_, _) => await ChangeStatusAsync(next), ButtonKind.Primary));
        }
        if (o.PaymentMethod == PaymentMethod.BankTransfer && o.PaymentStatus != PaymentStatus.Paid && o.Status != OrderStatus.Cancelled)
            _actions.Controls.Add(Ui.Button("💳 Xác nhận đã nhận chuyển khoản", async (_, _) => await ConfirmPaymentAsync(), ButtonKind.Success));
    }

    private async Task ChangeStatusAsync(OrderStatus next)
    {
        if (!Ui.Confirm($"Chuyển đơn #{_order.Id} sang \"{next.ToVietnamese()}\"?")) return;
        await Execute(s => s.ChangeStatusAsync(_session.User, _order.Id, next, _order.RowVersion));
    }

    private async Task CancelAsync()
    {
        var reason = InputDialog.Ask(this, $"Hủy đơn #{_order.Id}", "Lý do hủy:", "Hết hàng đột xuất", multiline: true);
        if (reason == null) return;
        if (_order.PaymentStatus == PaymentStatus.Paid)
            Ui.Info("Lưu ý: đơn này khách đã thanh toán chuyển khoản. Cần hoàn tiền cho khách theo quy trình riêng.");
        await Execute(s => s.CancelByStaffAsync(_session.User, _order.Id, reason));
    }

    private async Task ConfirmPaymentAsync()
    {
        var code = InputDialog.Ask(this, "Xác nhận chuyển khoản", "Mã giao dịch ngân hàng:");
        if (code == null) return;
        await Execute(s => s.ConfirmPaymentAsync(_session.User, _order.Id, code));
    }

    private async Task Execute(Func<OrderService, Task<Result>> action)
    {
        try
        {
            UseWaitCursor = true;
            var result = await _services.Run(action);
            Ui.Show(result);
            if (result.Success) _changed = true;
            await ReloadAsync(_order.Id);
        }
        catch (Exception ex)
        {
            Ui.ShowException(ex);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }
}
