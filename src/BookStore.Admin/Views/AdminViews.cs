using BookStore.Admin.Infrastructure;
using BookStore.Business.Common;
using BookStore.Business.Services;
using BookStore.Domain;

namespace BookStore.Admin.Views;

/// <summary>Quản lý tài khoản nội bộ và phân quyền (chỉ Quản trị viên).</summary>
public class AccountsView : ViewBase
{
    public override string Title => "Tài khoản & phân quyền";

    private readonly TextBox _keyword = Ui.TextBox("Tên, email, số điện thoại...", 240);
    private readonly DataGridView _grid = Ui.Grid();

    public AccountsView(ServiceRunner services, AppSession session) : base(services, session)
    {
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(_keyword);
        toolbar.Controls.Add(Ui.Button("🔍 Tìm", async (_, _) => await Safe(LoadDataAsync)));
        toolbar.Controls.Add(Ui.Button("+ Tạo tài khoản", async (_, _) => await EditAsync(null), ButtonKind.Primary));
        toolbar.Controls.Add(Ui.Button("✎ Sửa / Phân quyền", async (_, _) => await EditAsync(_grid.Selected<StaffListItem>())));
        toolbar.Controls.Add(Ui.Button("🔑 Đặt lại mật khẩu", async (_, _) => await ResetPasswordAsync()));
        toolbar.Controls.Add(Ui.Button("🔒 Khóa / Mở khóa", async (_, _) => await ToggleAsync(), ButtonKind.Danger));
        _keyword.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Safe(LoadDataAsync); } };

        _grid.Col("Id", "Mã", 40, "int");
        _grid.Col("Username", "Tên đăng nhập", 110);
        _grid.Col("FullName", "Họ tên", 160);
        _grid.Col("Email", "Email", 180);
        _grid.Col("Phone", "Điện thoại", 100);
        _grid.Col("Role", "Vai trò", 100);
        _grid.Col("IsActive", "Trạng thái", 80);
        _grid.Col("CreatedAt", "Ngày tạo", 90, "dd/MM/yyyy");
        _grid.CellFormatting += (_, e) =>
        {
            var name = _grid.Columns[e.ColumnIndex].Name;
            if (name == "Role" && e.Value is UserRole role) e.Value = role.ToVietnamese();
            if (name == "IsActive" && e.Value is bool active)
            {
                e.Value = active ? "Hoạt động" : "Đã khóa";
                e.CellStyle!.ForeColor = active ? Ui.Success : Ui.Danger;
            }
        };
        _grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditAsync(_grid.Selected<StaffListItem>()); };

        var note = Ui.Label("Quản trị viên: toàn quyền. Nhân viên: không truy cập được Tài khoản & phân quyền, Mã giảm giá. Không thể tự khóa hoặc tự hạ quyền chính mình.",
            Ui.SmallFont, Ui.Muted);
        note.Dock = DockStyle.Bottom;
        Controls.Add(_grid);
        Controls.Add(note);
        Controls.Add(toolbar);
    }

    public override async Task LoadDataAsync() =>
        _grid.Bind(await Run<AccountService, List<StaffListItem>>(s => s.GetStaffAsync(_keyword.Text)), a => a.Id);

    private async Task EditAsync(StaffListItem? item)
    {
        await Safe(async () =>
        {
            var dto = item == null ? new StaffEditDto() : await Run<AccountService, StaffEditDto?>(s => s.GetAsync(item.Id));
            if (dto == null) return;
            using var form = new AccountEditForm(Services, Session, dto);
            if (form.ShowDialog(this) == DialogResult.OK) await LoadDataAsync();
        });
    }

    private async Task ResetPasswordAsync()
    {
        if (_grid.Selected<StaffListItem>() is not { } a) { Ui.Info("Vui lòng chọn một tài khoản."); return; }
        var pw = InputDialog.Ask(this, "Đặt lại mật khẩu cho " + a.Username, "Mật khẩu mới:", password: true);
        if (pw == null) return;
        await Safe(async () => Ui.Show(await Run<AccountService, Result>(s => s.ResetPasswordAsync(Actor, a.Id, pw))));
    }

    private async Task ToggleAsync()
    {
        if (_grid.Selected<StaffListItem>() is not { } a) { Ui.Info("Vui lòng chọn một tài khoản."); return; }
        if (!Ui.Confirm($"{(a.IsActive ? "Khóa" : "Mở khóa")} tài khoản {a.Username}?")) return;
        await Safe(async () =>
        {
            if (Ui.Show(await Run<AccountService, Result>(s => s.SetActiveAsync(Actor, a.Id, !a.IsActive)))) await LoadDataAsync();
        });
    }
}

public class AccountEditForm : DialogBase
{
    public AccountEditForm(ServiceRunner services, AppSession session, StaffEditDto dto)
        : base(dto.Id == 0 ? "Tạo tài khoản nhân viên" : "Sửa tài khoản / phân quyền")
    {
        var username = AddField("Tên đăng nhập *", new TextBox { Text = dto.Username });
        var fullName = AddField("Họ tên *", new TextBox { Text = dto.FullName });
        var email = AddField("Email *", new TextBox { Text = dto.Email });
        var phone = AddField("Số điện thoại *", new TextBox { Text = dto.Phone });
        var role = AddField("Vai trò *", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList });
        role.Items.AddRange(new object[] { UserRole.Admin.ToVietnamese(), UserRole.Staff.ToVietnamese() });
        role.SelectedIndex = dto.Role == UserRole.Admin ? 0 : 1;
        TextBox? password = null;
        if (dto.Id == 0)
            password = AddField("Mật khẩu ban đầu *", new TextBox { UseSystemPasswordChar = true, PlaceholderText = "≥ 6 ký tự, có chữ và số" });

        OnSave(async () =>
        {
            dto.Username = username.Text;
            dto.FullName = fullName.Text;
            dto.Email = email.Text;
            dto.Phone = phone.Text;
            dto.Role = role.SelectedIndex == 0 ? UserRole.Admin : UserRole.Staff;
            dto.Password = password?.Text;
            var result = await services.Run<AccountService, Result>(s => s.SaveAsync(session.User, dto));
            if (result.Success) Ui.Info(result.Message);
            return result;
        });
    }
}

/// <summary>Quản lý mã giảm giá (chỉ Quản trị viên).</summary>
public class CouponsView : ViewBase
{
    public override string Title => "Mã giảm giá";
    private readonly DataGridView _grid = Ui.Grid();

    public CouponsView(ServiceRunner services, AppSession session) : base(services, session)
    {
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(Ui.Button("+ Tạo mã", async (_, _) => await EditAsync(new CouponDto()), ButtonKind.Primary));
        toolbar.Controls.Add(Ui.Button("✎ Sửa", async (_, _) => { if (_grid.Selected<CouponDto>() is { } c) await EditAsync(c); }));
        toolbar.Controls.Add(Ui.Button("🗑 Xóa / Ngừng", async (_, _) => await DeleteAsync(), ButtonKind.Danger));

        _grid.Col("Code", "Mã", 90);
        _grid.Col("Description", "Ưu đãi", 260);
        _grid.Col("StartAt", "Bắt đầu", 90, "dd/MM/yyyy");
        _grid.Col("EndAt", "Kết thúc", 90, "dd/MM/yyyy");
        _grid.Col("UsedCount", "Đã dùng", 60, "int");
        _grid.Col("UsageLimit", "Giới hạn", 60, "int");
        _grid.Col("IsActive", "Trạng thái", 90);
        _grid.CellFormatting += (_, e) =>
        {
            if (_grid.Columns[e.ColumnIndex].Name != "IsActive" || _grid.Rows[e.RowIndex].DataBoundItem is not CouponDto c) return;
            bool expired = c.EndAt < DateTime.Now, used = c.UsedCount >= c.UsageLimit;
            e.Value = !c.IsActive ? "Ngừng" : expired ? "Hết hạn" : used ? "Hết lượt" : "Đang áp dụng";
            e.CellStyle!.ForeColor = c.IsActive && !expired && !used ? Ui.Success : Ui.Muted;
        };
        _grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0 && _grid.Selected<CouponDto>() is { } c) await EditAsync(c); };

        Controls.Add(_grid);
        Controls.Add(toolbar);
    }

    public override async Task LoadDataAsync() => _grid.Bind(await Run<CouponService, List<CouponDto>>(s => s.GetAllAsync()), c => c.Id);

    private async Task EditAsync(CouponDto dto)
    {
        using var form = new CouponEditForm(Services, Session, dto);
        if (form.ShowDialog(this) == DialogResult.OK) await Safe(LoadDataAsync);
    }

    private async Task DeleteAsync()
    {
        if (_grid.Selected<CouponDto>() is not { } c) { Ui.Info("Vui lòng chọn một mã."); return; }
        if (!Ui.Confirm($"Xóa mã {c.Code}? (Mã đã được dùng sẽ chỉ bị ngừng kích hoạt)")) return;
        await Safe(async () =>
        {
            if (Ui.Show(await Run<CouponService, Result>(s => s.DeleteAsync(Actor, c.Id)))) await LoadDataAsync();
        });
    }
}

public class CouponEditForm : DialogBase
{
    public CouponEditForm(ServiceRunner services, AppSession session, CouponDto dto) : base(dto.Id == 0 ? "Tạo mã giảm giá" : "Sửa mã giảm giá")
    {
        var code = AddField("Mã *", new TextBox { Text = dto.Code, CharacterCasing = CharacterCasing.Upper });
        var type = AddField("Loại giảm", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList });
        type.Items.AddRange(new object[] { "Theo phần trăm (%)", "Số tiền cố định (₫)" });
        type.SelectedIndex = dto.DiscountType == DiscountType.Percent ? 0 : 1;
        var value = AddField("Giá trị giảm *", Number(0, 100_000_000, dto.Value));
        var max = AddField("Giảm tối đa (₫, 0 = không giới hạn)", Number(0, 100_000_000, dto.MaxDiscount ?? 0, 0, 1000));
        var min = AddField("Đơn tối thiểu (₫)", Number(0, 100_000_000, dto.MinOrder, 0, 1000));
        var start = AddField("Bắt đầu", new DateTimePicker { Value = dto.StartAt, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm" });
        var end = AddField("Kết thúc", new DateTimePicker { Value = dto.EndAt, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm" });
        var limit = AddField("Giới hạn lượt dùng", Number(1, 1_000_000, Math.Max(1, dto.UsageLimit)));
        var active = AddField("Kích hoạt", new CheckBox { Checked = dto.IsActive, Text = "Đang áp dụng" });
        if (dto.Id != 0) AddField("Đã dùng", new Label { Text = $"{dto.UsedCount} lượt", AutoSize = true });

        type.SelectedIndexChanged += (_, _) => max.Enabled = type.SelectedIndex == 0;
        max.Enabled = type.SelectedIndex == 0;

        OnSave(async () =>
        {
            dto.Code = code.Text;
            dto.DiscountType = type.SelectedIndex == 0 ? DiscountType.Percent : DiscountType.Amount;
            dto.Value = value.Value;
            dto.MaxDiscount = max.Value > 0 ? max.Value : null;
            dto.MinOrder = min.Value;
            dto.StartAt = start.Value;
            dto.EndAt = end.Value;
            dto.UsageLimit = (int)limit.Value;
            dto.IsActive = active.Checked;
            var result = await services.Run<CouponService, Result>(s => s.SaveAsync(session.User, dto));
            if (result.Success) Ui.Info(result.Message);
            return result;
        });
    }
}
