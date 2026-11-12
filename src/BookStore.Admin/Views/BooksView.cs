using BookStore.Admin.Infrastructure;
using BookStore.Business.Common;
using BookStore.Business.Services;

namespace BookStore.Admin.Views;

/// <summary>Quản lý sách: tìm kiếm, lọc, thêm, sửa, ẩn/hiện, xóa, ảnh bìa.</summary>
public class BooksView : ViewBase
{
    public override string Title => "Quản lý sách";

    private readonly TextBox _keyword = Ui.TextBox("Tên sách, tác giả, NXB, ISBN...", 260);
    private readonly ComboBox _category = Ui.Combo(180);
    private readonly CheckBox _lowOnly = new() { Text = "Chỉ sách sắp hết", AutoSize = true, Margin = new Padding(0, 8, 12, 0) };
    private readonly DataGridView _grid = Ui.Grid();
    private readonly Label _count = Ui.Label("", Ui.SmallFont, Ui.Muted);

    public BooksView(ServiceRunner services, AppSession session) : base(services, session)
    {
        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(_keyword);
        toolbar.Controls.Add(_category);
        toolbar.Controls.Add(_lowOnly);
        toolbar.Controls.Add(Ui.Button("🔍 Tìm", async (_, _) => await Safe(LoadBooksAsync)));
        toolbar.Controls.Add(Ui.Button("+ Thêm sách", async (_, _) => await EditAsync(0), ButtonKind.Primary));
        toolbar.Controls.Add(Ui.Button("✎ Sửa", async (_, _) => await EditSelectedAsync()));
        toolbar.Controls.Add(Ui.Button("👁 Ẩn / Hiện", async (_, _) => await ToggleHiddenAsync()));
        toolbar.Controls.Add(Ui.Button("🗑 Xóa", async (_, _) => await DeleteAsync(), ButtonKind.Danger));
        toolbar.Controls.Add(_count);

        _keyword.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await Safe(LoadBooksAsync); } };
        _category.SelectedIndexChanged += async (_, _) => { if (_category.Focused) await Safe(LoadBooksAsync); };
        _lowOnly.CheckedChanged += async (_, _) => await Safe(LoadBooksAsync);

        _grid.Col("Id", "Mã", 40, "int");
        _grid.Col("Title", "Tên sách", 220);
        _grid.Col("Authors", "Tác giả", 140);
        _grid.Col("CategoryName", "Thể loại", 100);
        _grid.Col("PublisherName", "NXB", 110);
        _grid.Col("Isbn", "ISBN", 100);
        _grid.Col("Price", "Giá bán", 80, "money");
        _grid.Col("Stock", "Tồn", 50, "int");
        _grid.Col("SoldQuantity", "Đã bán", 55, "int");
        _grid.CheckCol("IsFeatured", "Nổi bật", 50);
        _grid.CheckCol("IsHidden", "Ẩn", 40);
        _grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await EditSelectedAsync(); };
        // Tô đỏ nhạt sách sắp hết hàng, chữ xám sách đang ẩn
        _grid.RowPrePaint += (_, e) =>
        {
            if (_grid.Rows[e.RowIndex].DataBoundItem is not BookListItem b) return;
            var style = _grid.Rows[e.RowIndex].DefaultCellStyle;
            style.BackColor = b.IsLowStock ? Ui.LowStockBack : Color.Empty;
            style.ForeColor = b.IsHidden ? Ui.Muted : Color.Empty;
        };

        var legend = Ui.Label("Dòng tô đỏ: tồn kho ≤ ngưỡng cảnh báo · Tồn kho thay đổi qua phân hệ Kho (phiếu nhập/điều chỉnh) · Nháy đúp để sửa",
            Ui.SmallFont, Ui.Muted);
        legend.Dock = DockStyle.Bottom;

        Controls.Add(_grid);
        Controls.Add(legend);
        Controls.Add(toolbar);
    }

    public override async Task LoadDataAsync()
    {
        var categories = await Run<CategoryService, List<LookupItem>>(s => s.GetLookupAsync());
        _category.DisplayMember = "Name";
        _category.ValueMember = "Id";
        _category.DataSource = new[] { new LookupItem(0, "Tất cả thể loại") }.Concat(categories).ToList();
        await LoadBooksAsync();
    }

    private async Task LoadBooksAsync()
    {
        var query = new BookQuery
        {
            Keyword = _keyword.Text, CategoryId = _category.SelectedValue as int? ?? 0, LowStockOnly = _lowOnly.Checked,
            IncludeHidden = true, Sort = BookSort.Newest, PageSize = 500
        };
        var result = await Run<BookService, PagedResult<BookListItem>>(s => s.SearchAsync(query));
        _grid.Bind(result.Items.ToList(), b => b.Id);
        _count.Text = $"{result.TotalCount} đầu sách";
    }

    private async Task EditSelectedAsync()
    {
        if (_grid.Selected<BookListItem>() is { } b) await EditAsync(b.Id);
        else Ui.Info("Vui lòng chọn một đầu sách.");
    }

    private async Task EditAsync(int id) => await Safe(async () =>
    {
        using var form = new BookEditForm(Services, Session, id);
        await form.LoadAsync();
        if (form.ShowDialog(this) == DialogResult.OK) await LoadBooksAsync();
    });

    private async Task ToggleHiddenAsync()
    {
        if (_grid.Selected<BookListItem>() is not { } b) { Ui.Info("Vui lòng chọn một đầu sách."); return; }
        await Safe(async () =>
        {
            if (Ui.Show(await Run<BookService, Result>(s => s.SetHiddenAsync(Actor, b.Id, !b.IsHidden)))) await LoadBooksAsync();
        });
    }

    private async Task DeleteAsync()
    {
        if (_grid.Selected<BookListItem>() is not { } b) { Ui.Info("Vui lòng chọn một đầu sách."); return; }
        if (!Ui.Confirm($"Xóa sách \"{b.Title}\"?\n\nNếu sách đã có giao dịch, hệ thống sẽ chuyển sang trạng thái Ẩn để giữ lịch sử.")) return;
        await Safe(async () =>
        {
            if (Ui.Show(await Run<BookService, Result>(s => s.DeleteAsync(Actor, b.Id)))) await LoadBooksAsync();
        });
    }
}

/// <summary>Form thêm / sửa sách.</summary>
public class BookEditForm : DialogBase
{
    private readonly ServiceRunner _services;
    private readonly AppSession _session;
    private readonly int _id;
    private BookEditDto _dto = new();

    private readonly TextBox _title = new();
    private readonly TextBox _isbn = new();
    private readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _publisher = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _authorFilter = new() { PlaceholderText = "Lọc tác giả..." };
    private readonly CheckedListBox _authors = new() { CheckOnClick = true, IntegralHeight = false };
    private readonly NumericUpDown _year = Number(1900, DateTime.Now.Year + 1, DateTime.Now.Year);
    private readonly NumericUpDown _pages = Number(1, 10000, 200);
    private readonly ComboBox _coverType = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _price = Number(0, 100_000_000, 100000, 0, 1000);
    private readonly NumericUpDown _threshold = Number(0, 10000, 10);
    private readonly NumericUpDown _initialStock = Number(0, 100000, 0);
    private readonly CheckBox _featured = new() { Text = "Hiển thị ở mục Sách nổi bật" };
    private readonly CheckBox _hidden = new() { Text = "Ẩn khỏi website" };
    private readonly TextBox _description = new() { Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly PictureBox _cover = new() { SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(239, 233, 222) };
    private readonly Label _coverInfo = Ui.Label("Chưa có ảnh bìa", Ui.SmallFont, Ui.Muted);
    private List<LookupItem> _allAuthors = new();
    private readonly HashSet<int> _checkedAuthors = new();
    private (byte[] Data, string ContentType)? _newImage;
    private bool _removeImage;

    public BookEditForm(ServiceRunner services, AppSession session, int id) : base(id == 0 ? "Thêm đầu sách mới" : "Sửa thông tin sách", 820)
    {
        _services = services;
        _session = session;
        _id = id;
        _coverType.Items.AddRange(new object[] { "Bìa mềm", "Bìa cứng" });

        AddField("Tên sách *", _title);
        AddField("Mã ISBN *", _isbn);
        AddField("Thể loại *", _category);
        AddField("Nhà xuất bản *", _publisher);

        var authorPanel = new TableLayoutPanel { ColumnCount = 2, RowCount = 2, Height = 150 };
        authorPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        authorPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        authorPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        authorPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _authorFilter.Dock = DockStyle.Fill;
        _authors.Dock = DockStyle.Fill;
        authorPanel.Controls.Add(_authorFilter, 0, 0);
        authorPanel.Controls.Add(Ui.Button("+ Tác giả mới", async (_, _) => await AddAuthorAsync()), 1, 0);
        authorPanel.Controls.Add(_authors, 0, 1);
        authorPanel.SetColumnSpan(_authors, 2);
        AddField("Tác giả * (chọn ≥ 1)", authorPanel, 150);
        _authorFilter.TextChanged += (_, _) => FillAuthors();
        _authors.ItemCheck += (_, e) =>
        {
            if (_authors.Items[e.Index] is LookupItem a)
            {
                if (e.NewValue == CheckState.Checked) _checkedAuthors.Add(a.Id); else _checkedAuthors.Remove(a.Id);
            }
        };

        var numbers = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        numbers.Controls.Add(Ui.Label("Năm XB"));
        _year.Width = 80;
        numbers.Controls.Add(_year);
        numbers.Controls.Add(Ui.Label("  Số trang"));
        _pages.Width = 80;
        numbers.Controls.Add(_pages);
        numbers.Controls.Add(Ui.Label("  Hình thức"));
        _coverType.Width = 110;
        numbers.Controls.Add(_coverType);
        AddField("Thông tin xuất bản", numbers, 36);

        var stock = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        _price.Width = 140;
        stock.Controls.Add(_price);
        stock.Controls.Add(Ui.Label("₫     Ngưỡng cảnh báo tồn"));
        _threshold.Width = 80;
        stock.Controls.Add(_threshold);
        AddField("Giá bán *", stock, 36);
        AddField(id == 0 ? "Tồn kho ban đầu" : "Tồn kho hiện tại", _initialStock);

        var flags = new FlowLayoutPanel { AutoSize = true };
        _featured.AutoSize = true;
        _hidden.AutoSize = true;
        flags.Controls.Add(_featured);
        flags.Controls.Add(_hidden);
        AddField("Hiển thị", flags, 32);
        AddField("Mô tả / tóm tắt", _description, 90);

        var coverPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        _cover.Size = new Size(90, 120);
        var coverButtons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
        coverButtons.Controls.Add(Ui.Button("Chọn ảnh...", (_, _) => PickImage()));
        coverButtons.Controls.Add(Ui.Button("Xóa ảnh", (_, _) => { _cover.Image = null; _newImage = null; _removeImage = true; _coverInfo.Text = "Sẽ xóa ảnh khi lưu"; }));
        coverButtons.Controls.Add(_coverInfo);
        coverPanel.Controls.Add(_cover);
        coverPanel.Controls.Add(coverButtons);
        AddField("Ảnh bìa (JPG/PNG ≤ 2MB)", coverPanel, 128);

        if (id != 0)
        {
            _initialStock.Enabled = false; // tồn kho chỉ đổi qua phiếu nhập / điều chỉnh kho
        }

        OnSave(SaveAsync);
    }

    public async Task LoadAsync()
    {
        _category.DisplayMember = _publisher.DisplayMember = "Name";
        _category.ValueMember = _publisher.ValueMember = "Id";
        _category.DataSource = await _services.Run<CategoryService, List<LookupItem>>(s => s.GetLookupAsync());
        _publisher.DataSource = await _services.Run<PublisherService, List<LookupItem>>(s => s.GetLookupAsync());
        _allAuthors = await _services.Run<AuthorService, List<LookupItem>>(s => s.GetLookupAsync());

        if (_id != 0)
        {
            _dto = await _services.Run<BookService, BookEditDto?>(s => s.GetForEditAsync(_id))
                   ?? throw new InvalidOperationException("Sách không còn tồn tại.");
            _title.Text = _dto.Title;
            _isbn.Text = _dto.Isbn;
            _category.SelectedValue = _dto.CategoryId;
            _publisher.SelectedValue = _dto.PublisherId;
            _year.Value = Math.Clamp(_dto.PublishYear, (int)_year.Minimum, (int)_year.Maximum);
            _pages.Value = Math.Clamp(_dto.Pages, 1, 10000);
            _coverType.SelectedItem = _dto.CoverType;
            _price.Value = Math.Min(_dto.Price, _price.Maximum);
            _threshold.Value = _dto.LowStockThreshold;
            _initialStock.Value = _dto.CurrentStock;
            _featured.Checked = _dto.IsFeatured;
            _hidden.Checked = _dto.IsHidden;
            _description.Text = _dto.Description;
            foreach (var a in _dto.AuthorIds) _checkedAuthors.Add(a);

            var image = await _services.Run<BookService, (byte[] Data, string ContentType)?>(s => s.GetImageAsync(_id));
            if (image != null) ShowImage(image.Value.Data, "Ảnh bìa hiện tại");
        }
        if (_coverType.SelectedIndex < 0) _coverType.SelectedIndex = 0;
        FillAuthors();
    }

    private void FillAuthors()
    {
        var filter = _authorFilter.Text.Trim();
        _authors.BeginUpdate();
        _authors.Items.Clear();
        foreach (var a in _allAuthors.Where(a => filter.Length == 0 || a.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) || _checkedAuthors.Contains(a.Id))
                     .OrderByDescending(a => _checkedAuthors.Contains(a.Id)).ThenBy(a => a.Name))
        {
            _authors.Items.Add(a, _checkedAuthors.Contains(a.Id));
        }
        _authors.DisplayMember = "Name";
        _authors.EndUpdate();
    }

    private async Task AddAuthorAsync()
    {
        var name = InputDialog.Ask(this, "Thêm tác giả", "Tên tác giả:");
        if (name == null) return;
        var result = await _services.Run<AuthorService, Result>(s => s.SaveAsync(_session.User, 0, name, null));
        if (!Ui.Show(result)) return;
        _allAuthors = await _services.Run<AuthorService, List<LookupItem>>(s => s.GetLookupAsync());
        var added = _allAuthors.Where(a => a.Name == name.Trim()).MaxBy(a => a.Id);
        if (added != null) _checkedAuthors.Add(added.Id);
        _authorFilter.Text = "";
        FillAuthors();
    }

    private void PickImage()
    {
        using var dlg = new OpenFileDialog { Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.webp;*.gif)|*.jpg;*.jpeg;*.png;*.webp;*.gif", Title = "Chọn ảnh bìa" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var info = new FileInfo(dlg.FileName);
        if (info.Length > BookService.MaxImageBytes)
        {
            Ui.Error("File ảnh không hợp lệ (tối đa 2 MB).");
            return;
        }
        var data = File.ReadAllBytes(dlg.FileName);
        var type = info.Extension.ToLowerInvariant() switch
        {
            ".png" => "image/png", ".webp" => "image/webp", ".gif" => "image/gif", _ => "image/jpeg"
        };
        _newImage = (data, type);
        _removeImage = false;
        ShowImage(data, $"{info.Name} ({info.Length / 1024} KB)");
    }

    private void ShowImage(byte[] data, string caption)
    {
        try
        {
            using var ms = new MemoryStream(data);
            _cover.Image = Image.FromStream(ms);
        }
        catch
        {
            _cover.Image = null; // WEBP không xem trước được trên WinForms nhưng vẫn hiển thị trên Web
        }
        _coverInfo.Text = caption;
    }

    private async Task<Result> SaveAsync()
    {
        _dto.Title = _title.Text;
        _dto.Isbn = _isbn.Text;
        _dto.CategoryId = _category.SelectedValue as int? ?? 0;
        _dto.PublisherId = _publisher.SelectedValue as int? ?? 0;
        _dto.AuthorIds = _checkedAuthors.ToList();
        _dto.PublishYear = (int)_year.Value;
        _dto.Pages = (int)_pages.Value;
        _dto.CoverType = _coverType.SelectedItem?.ToString() ?? "Bìa mềm";
        _dto.Price = _price.Value;
        _dto.LowStockThreshold = (int)_threshold.Value;
        _dto.InitialStock = (int)_initialStock.Value;
        _dto.IsFeatured = _featured.Checked;
        _dto.IsHidden = _hidden.Checked;
        _dto.Description = _description.Text;

        int bookId = _id;
        Result result;
        if (_id == 0)
        {
            var created = await _services.Run<BookService, Result<int>>(s => s.CreateAsync(_session.User, _dto));
            if (!created.Success) return created;
            bookId = created.Value;
            result = created;
        }
        else
        {
            result = await _services.Run<BookService, Result>(s => s.UpdateAsync(_session.User, _dto));
            if (!result.Success) return result;
        }

        if (_newImage != null)
        {
            var img = _newImage.Value;
            var imageResult = await _services.Run<BookService, Result>(s => s.SetImageAsync(_session.User, bookId, img.Data, img.ContentType));
            if (!imageResult.Success) Ui.Show(imageResult);
        }
        else if (_removeImage)
        {
            await _services.Run<BookService, Result>(s => s.RemoveImageAsync(_session.User, bookId));
        }

        Ui.Info(result.Message);
        return result;
    }
}
