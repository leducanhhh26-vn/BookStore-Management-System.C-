using BookStore.Admin.Infrastructure;
using BookStore.Business.Common;
using BookStore.Business.Services;

namespace BookStore.Admin.Views;

/// <summary>Mô tả một ô nhập trên form Thêm/Sửa của màn hình CRUD đơn giản.</summary>
public record FieldDef(string Key, string Label, bool Required = false, bool Multiline = false);

/// <summary>
/// Màn hình CRUD dùng chung (Danh mục, Tác giả, NXB, Nhà cung cấp): thanh tìm kiếm, lưới, Thêm/Sửa/Xóa.
/// Lớp con chỉ cần khai báo cột, ô nhập và gọi Service tương ứng → các màn hình thống nhất giao diện.
/// </summary>
public abstract class SimpleCrudView<TItem> : ViewBase where TItem : class
{
    private readonly TextBox _keyword = Ui.TextBox("Tìm kiếm...", 260);
    protected readonly DataGridView Grid = Ui.Grid();
    protected readonly FlowLayoutPanel ToolbarPanel = Ui.Toolbar();
    private readonly Label _count = Ui.Label("", Ui.SmallFont, Ui.Muted);

    protected abstract string EntityName { get; }
    protected abstract FieldDef[] Fields { get; }
    protected abstract void ConfigureColumns(DataGridView grid);
    protected abstract Task<List<TItem>> LoadAsync(string keyword);
    protected abstract int GetId(TItem item);
    protected abstract Dictionary<string, string?> GetValues(TItem item);
    protected abstract Task<Result> SaveAsync(int id, Dictionary<string, string> values);
    protected abstract Task<Result> DeleteAsync(int id);

    protected SimpleCrudView(ServiceRunner services, AppSession session) : base(services, session)
    {
        ToolbarPanel.Controls.Add(_keyword);
        ToolbarPanel.Controls.Add(Ui.Button("🔍 Tìm", async (_, _) => await Safe(LoadDataAsync)));
        ToolbarPanel.Controls.Add(Ui.Button("+ Thêm", async (_, _) => await EditAsync(null), ButtonKind.Primary));
        ToolbarPanel.Controls.Add(Ui.Button("✎ Sửa", async (_, _) => await EditSelectedAsync()));
        ToolbarPanel.Controls.Add(Ui.Button("🗑 Xóa", async (_, _) => await DeleteSelectedAsync(), ButtonKind.Danger));
        _keyword.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            await Safe(LoadDataAsync);
        };
        Grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditSelectedAsync(); };

        Controls.Add(Grid);
        Controls.Add(ToolbarPanel);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (Grid.Columns.Count == 0) ConfigureColumns(Grid);
        if (!ToolbarPanel.Controls.Contains(_count)) ToolbarPanel.Controls.Add(_count);
    }

    public override async Task LoadDataAsync()
    {
        if (Grid.Columns.Count == 0) ConfigureColumns(Grid);
        var items = await LoadAsync(_keyword.Text);
        Grid.Bind(items, i => GetId(i));
        _count.Text = $"{items.Count} {EntityName.ToLower()}";
    }

    protected TItem? Selected()
    {
        var item = Grid.Selected<TItem>();
        if (item == null) Ui.Info($"Vui lòng chọn một {EntityName.ToLower()}.");
        return item;
    }

    private async Task EditSelectedAsync()
    {
        if (Selected() is { } item) await EditAsync(item);
    }

    private async Task EditAsync(TItem? item)
    {
        using var dlg = new SimpleEditDialog((item == null ? "Thêm " : "Sửa ") + EntityName.ToLower(), Fields,
            item == null ? new() : GetValues(item), values => SaveAsync(item == null ? 0 : GetId(item), values));
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            Ui.Info(dlg.ResultMessage);
            await Safe(LoadDataAsync);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (Selected() is not { } item) return;
        if (!Ui.Confirm($"Bạn chắc chắn muốn xóa {EntityName.ToLower()} này?")) return;
        await Safe(async () =>
        {
            if (Ui.Show(await DeleteAsync(GetId(item)))) await LoadDataAsync();
        });
    }
}

/// <summary>Form Thêm/Sửa sinh tự động từ danh sách FieldDef.</summary>
public class SimpleEditDialog : DialogBase
{
    private readonly Dictionary<string, TextBox> _inputs = new();
    public string ResultMessage { get; private set; } = "";

    public SimpleEditDialog(string title, FieldDef[] fields, Dictionary<string, string?> values,
        Func<Dictionary<string, string>, Task<Result>> save) : base(title)
    {
        foreach (var f in fields)
        {
            var box = new TextBox { Text = values.GetValueOrDefault(f.Key) ?? "", Multiline = f.Multiline, ScrollBars = f.Multiline ? ScrollBars.Vertical : ScrollBars.None };
            _inputs[f.Key] = AddField(f.Label + (f.Required ? " *" : ""), box, f.Multiline ? 80 : 0);
        }
        OnSave(async () =>
        {
            foreach (var f in fields.Where(f => f.Required))
                if (string.IsNullOrWhiteSpace(_inputs[f.Key].Text))
                {
                    _inputs[f.Key].Focus();
                    return Result.Fail($"Vui lòng nhập {f.Label.ToLower()}.");
                }
            var result = await save(_inputs.ToDictionary(kv => kv.Key, kv => kv.Value.Text));
            ResultMessage = result.Message;
            return result;
        });
    }
}

// =====================================================================
//  Các màn hình cụ thể
// =====================================================================

public class CategoriesView : SimpleCrudView<CategoryItem>
{
    public override string Title => "Danh mục thể loại";
    protected override string EntityName => "Thể loại";
    protected override FieldDef[] Fields => new[] { new FieldDef("Name", "Tên thể loại", true), new FieldDef("Description", "Mô tả", false, true) };

    public CategoriesView(ServiceRunner services, AppSession session) : base(services, session)
    {
        ToolbarPanel.Controls.Add(Ui.Button("👁 Ẩn / Hiện trên Web", async (_, _) =>
        {
            if (Selected() is not { } c) return;
            await Safe(async () =>
            {
                if (Ui.Show(await Run<CategoryService, Result>(s => s.SetHiddenAsync(Actor, c.Id, !c.IsHidden)))) await LoadDataAsync();
            });
        }));
    }

    protected override void ConfigureColumns(DataGridView g)
    {
        g.Col("Id", "Mã", 40, "int");
        g.Col("Name", "Tên thể loại", 160);
        g.Col("Description", "Mô tả", 300);
        g.Col("BookCount", "Số sách", 60, "int");
        g.CheckCol("IsHidden", "Đang ẩn", 60);
    }

    protected override Task<List<CategoryItem>> LoadAsync(string keyword) => Run<CategoryService, List<CategoryItem>>(s => s.GetAllAsync(keyword));
    protected override int GetId(CategoryItem item) => item.Id;
    protected override Dictionary<string, string?> GetValues(CategoryItem i) => new() { ["Name"] = i.Name, ["Description"] = i.Description };
    protected override Task<Result> SaveAsync(int id, Dictionary<string, string> v) => Run<CategoryService, Result>(s => s.SaveAsync(Actor, id, v["Name"], v["Description"]));
    protected override Task<Result> DeleteAsync(int id) => Run<CategoryService, Result>(s => s.DeleteAsync(Actor, id));
}

public class AuthorsView : SimpleCrudView<AuthorItem>
{
    public override string Title => "Quản lý tác giả";
    protected override string EntityName => "Tác giả";
    protected override FieldDef[] Fields => new[] { new FieldDef("Name", "Tên tác giả", true), new FieldDef("Bio", "Tiểu sử", false, true) };

    public AuthorsView(ServiceRunner services, AppSession session) : base(services, session) { }

    protected override void ConfigureColumns(DataGridView g)
    {
        g.Col("Id", "Mã", 40, "int");
        g.Col("Name", "Tên tác giả", 180);
        g.Col("Bio", "Tiểu sử", 320);
        g.Col("BookCount", "Số sách", 60, "int");
    }

    protected override Task<List<AuthorItem>> LoadAsync(string keyword) => Run<AuthorService, List<AuthorItem>>(s => s.GetAllAsync(keyword));
    protected override int GetId(AuthorItem item) => item.Id;
    protected override Dictionary<string, string?> GetValues(AuthorItem i) => new() { ["Name"] = i.Name, ["Bio"] = i.Bio };
    protected override Task<Result> SaveAsync(int id, Dictionary<string, string> v) => Run<AuthorService, Result>(s => s.SaveAsync(Actor, id, v["Name"], v["Bio"]));
    protected override Task<Result> DeleteAsync(int id) => Run<AuthorService, Result>(s => s.DeleteAsync(Actor, id));
}

public class PublishersView : SimpleCrudView<PublisherItem>
{
    public override string Title => "Quản lý nhà xuất bản";
    protected override string EntityName => "Nhà xuất bản";
    protected override FieldDef[] Fields => new[]
    {
        new FieldDef("Name", "Tên nhà xuất bản", true), new FieldDef("Address", "Địa chỉ"), new FieldDef("Phone", "Số điện thoại")
    };

    public PublishersView(ServiceRunner services, AppSession session) : base(services, session) { }

    protected override void ConfigureColumns(DataGridView g)
    {
        g.Col("Id", "Mã", 40, "int");
        g.Col("Name", "Tên nhà xuất bản", 180);
        g.Col("Address", "Địa chỉ", 280);
        g.Col("Phone", "Điện thoại", 100);
        g.Col("BookCount", "Số sách", 60, "int");
    }

    protected override Task<List<PublisherItem>> LoadAsync(string keyword) => Run<PublisherService, List<PublisherItem>>(s => s.GetAllAsync(keyword));
    protected override int GetId(PublisherItem item) => item.Id;
    protected override Dictionary<string, string?> GetValues(PublisherItem i) => new() { ["Name"] = i.Name, ["Address"] = i.Address, ["Phone"] = i.Phone };
    protected override Task<Result> SaveAsync(int id, Dictionary<string, string> v) => Run<PublisherService, Result>(s => s.SaveAsync(Actor, id, v["Name"], v["Address"], v["Phone"]));
    protected override Task<Result> DeleteAsync(int id) => Run<PublisherService, Result>(s => s.DeleteAsync(Actor, id));
}

public class SuppliersView : SimpleCrudView<SupplierItem>
{
    public override string Title => "Nhà cung cấp";
    protected override string EntityName => "Nhà cung cấp";
    protected override FieldDef[] Fields => new[]
    {
        new FieldDef("Name", "Tên nhà cung cấp", true), new FieldDef("Phone", "Số điện thoại"),
        new FieldDef("Email", "Email"), new FieldDef("Address", "Địa chỉ")
    };

    public SuppliersView(ServiceRunner services, AppSession session) : base(services, session) { }

    protected override void ConfigureColumns(DataGridView g)
    {
        g.Col("Id", "Mã", 40, "int");
        g.Col("Name", "Tên nhà cung cấp", 200);
        g.Col("Phone", "Điện thoại", 100);
        g.Col("Email", "Email", 160);
        g.Col("Address", "Địa chỉ", 240);
        g.Col("ReceiptCount", "Số phiếu nhập", 80, "int");
    }

    protected override Task<List<SupplierItem>> LoadAsync(string keyword) => Run<SupplierService, List<SupplierItem>>(s => s.GetAllAsync(keyword));
    protected override int GetId(SupplierItem item) => item.Id;
    protected override Dictionary<string, string?> GetValues(SupplierItem i) => new() { ["Name"] = i.Name, ["Phone"] = i.Phone, ["Email"] = i.Email, ["Address"] = i.Address };
    protected override Task<Result> SaveAsync(int id, Dictionary<string, string> v) => Run<SupplierService, Result>(s => s.SaveAsync(Actor, id, v["Name"], v["Phone"], v["Email"], v["Address"]));
    protected override Task<Result> DeleteAsync(int id) => Run<SupplierService, Result>(s => s.DeleteAsync(Actor, id));
}
