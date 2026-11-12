using BookStore.Admin.Infrastructure;
using BookStore.Business.Common;
using BookStore.Business.Services;

namespace BookStore.Admin.Views;

/// <summary>Quản lý khách hàng đăng ký trên website: tra cứu, xem lịch sử mua, cập nhật, khóa/mở khóa.</summary>
public class CustomersView : ViewBase
{
    public override string Title => "Quản lý khách hàng";

    private readonly TextBox _keyword = Ui.TextBox("Tên, email, số điện thoại...", 260);
    private readonly DataGridView _grid = Ui.Grid();
    private readonly Label _count = Ui.Label("", Ui.SmallFont, Ui.Muted);

    public CustomersView(ServiceRunner services, AppSession session) : base(services, session)
    {
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(_keyword);
        toolbar.Controls.Add(Ui.Button("🔍 Tìm", async (_, _) => await Safe(LoadDataAsync)));
        toolbar.Controls.Add(Ui.Button("📄 Chi tiết & lịch sử mua", async (_, _) => await OpenAsync(), ButtonKind.Primary));
        toolbar.Controls.Add(Ui.Button("✎ Sửa thông tin", async (_, _) => await EditAsync()));
        toolbar.Controls.Add(Ui.Button("🔒 Khóa / Mở khóa", async (_, _) => await ToggleAsync(), ButtonKind.Danger));
        toolbar.Controls.Add(_count);
        _keyword.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Safe(LoadDataAsync); } };

        _grid.Col("Id", "Mã", 40, "int");
        _grid.Col("FullName", "Họ tên", 150);
        _grid.Col("Username", "Tên đăng nhập", 110);
        _grid.Col("Email", "Email", 170);
        _grid.Col("Phone", "Điện thoại", 100);
        _grid.Col("OrderCount", "Số đơn", 55, "int");
        _grid.Col("TotalSpent", "Đã mua (hoàn tất)", 110, "money");
        _grid.Col("CreatedAt", "Ngày đăng ký", 100, "dd/MM/yyyy");
        _grid.Col("IsActive", "Trạng thái", 80);
        _grid.CellFormatting += (_, e) =>
        {
            if (_grid.Columns[e.ColumnIndex].Name != "IsActive" || e.Value is not bool active) return;
            e.Value = active ? "Hoạt động" : "Đã khóa";
            e.CellStyle!.ForeColor = active ? Ui.Success : Ui.Danger;
        };
        _grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await OpenAsync(); };

        Controls.Add(_grid);
        Controls.Add(toolbar);
    }

    public override async Task LoadDataAsync()
    {
        var items = await Run<CustomerService, List<CustomerListItem>>(s => s.SearchAsync(_keyword.Text));
        _grid.Bind(items, c => c.Id);
        _count.Text = $"{items.Count} khách hàng";
    }

    private CustomerListItem? Selected()
    {
        var c = _grid.Selected<CustomerListItem>();
        if (c == null) Ui.Info("Vui lòng chọn một khách hàng.");
        return c;
    }

    private async Task OpenAsync()
    {
        if (Selected() is not { } c) return;
        await Safe(async () =>
        {
            var detail = await Run<CustomerService, CustomerDetailDto?>(s => s.GetDetailAsync(c.Id));
            if (detail == null) { Ui.Error("Khách hàng không tồn tại."); return; }
            using var form = new CustomerDetailForm(Services, Session, detail);
            form.ShowDialog(this);
            await LoadDataAsync();
        });
    }

    private async Task EditAsync()
    {
        if (Selected() is not { } c) return;
        var fields = new[] { new FieldDef("FullName", "Họ tên", true), new FieldDef("Email", "Email", true), new FieldDef("Phone", "Số điện thoại", true) };
        var values = new Dictionary<string, string?> { ["FullName"] = c.FullName, ["Email"] = c.Email, ["Phone"] = c.Phone };
        using var dlg = new SimpleEditDialog("Sửa thông tin khách hàng", fields, values, v =>
            Run<CustomerService, Result>(s => s.UpdateByStaffAsync(Actor, new ProfileDto
            {
                Id = c.Id, FullName = v["FullName"], Email = v["Email"], Phone = v["Phone"]
            })));
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            Ui.Info(dlg.ResultMessage);
            await Safe(LoadDataAsync);
        }
    }

    private async Task ToggleAsync()
    {
        if (Selected() is not { } c) return;
        var action = c.IsActive ? "KHÓA" : "MỞ KHÓA";
        if (!Ui.Confirm($"{action} tài khoản của {c.FullName}?" + (c.IsActive ? "\n\nKhách sẽ bị đăng xuất khỏi website và không thể đăng nhập." : ""))) return;
        await Safe(async () =>
        {
            if (Ui.Show(await Run<CustomerService, Result>(s => s.SetActiveAsync(Actor, c.Id, !c.IsActive)))) await LoadDataAsync();
        });
    }
}

/// <summary>Thông tin khách hàng, sổ địa chỉ và lịch sử đơn hàng.</summary>
public class CustomerDetailForm : Form
{
    public CustomerDetailForm(ServiceRunner services, AppSession session, CustomerDetailDto d)
    {
        Text = "Khách hàng: " + d.Profile.FullName;
        Font = Ui.BaseFont;
        Size = new Size(960, 640);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Ui.Surface;
        Padding = new Padding(20);

        var p = d.Profile;
        var info = new Label
        {
            Dock = DockStyle.Top, Height = 110,
            Text = $"{p.FullName}   ({(p.IsActive ? "Hoạt động" : "Đã khóa")})\n" +
                   $"Tên đăng nhập: {p.Username}    Email: {p.Email}    Điện thoại: {p.Phone}\n" +
                   $"Ngày đăng ký: {p.CreatedAt:dd/MM/yyyy}    Số đơn: {d.Orders.Count}    Tổng đã mua (đơn hoàn tất): {Ui.Money(d.TotalSpent)}\n" +
                   "Địa chỉ: " + (d.Addresses.Count == 0 ? "chưa có" : string.Join("  |  ", d.Addresses.Select(a => (a.IsDefault ? "[Mặc định] " : "") + a.FullAddress)))
        };

        var orders = Ui.Grid();
        orders.Col("Id", "Mã đơn", 50, "int");
        orders.Col("OrderDate", "Ngày đặt", 100, "date");
        orders.Col("ItemCount", "SL", 40, "int");
        orders.Col("Total", "Tổng tiền", 90, "money");
        orders.Col("StatusText", "Trạng thái", 90);
        orders.Bind(d.Orders);
        orders.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex < 0 || orders.Selected<OrderListItem>() is not { } o) return;
            try { await OrderDetailForm.ShowAsync(this, services, session, o.Id); }
            catch (Exception ex) { Ui.ShowException(ex); }
        };

        Controls.Add(orders);
        Controls.Add(new Label { Text = "Lịch sử mua sách (nháy đúp để xem chi tiết đơn)", Dock = DockStyle.Top, Height = 30, Font = Ui.HeaderFont });
        Controls.Add(info);
    }
}
