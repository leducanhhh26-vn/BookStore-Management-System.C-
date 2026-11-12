using System.Globalization;
using BookStore.Business.Common;

namespace BookStore.Admin.Infrastructure;

/// <summary>Giao diện thống nhất cho mọi màn hình: màu, font, nút, lưới dữ liệu, hộp thoại.</summary>
public static class Ui
{
    public static readonly Color Primary = Color.FromArgb(31, 78, 121);
    public static readonly Color PrimaryDark = Color.FromArgb(23, 42, 61);
    public static readonly Color Accent = Color.FromArgb(196, 98, 45);
    public static readonly Color Success = Color.FromArgb(47, 138, 90);
    public static readonly Color Danger = Color.FromArgb(185, 56, 56);
    public static readonly Color Warning = Color.FromArgb(214, 158, 46);
    public static readonly Color Background = Color.FromArgb(244, 241, 235);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(225, 219, 208);
    public static readonly Color Muted = Color.FromArgb(107, 114, 128);
    public static readonly Color LowStockBack = Color.FromArgb(253, 236, 234);

    public static readonly Font BaseFont = new("Segoe UI", 10F);
    public static readonly Font BoldFont = new("Segoe UI", 10F, FontStyle.Bold);
    public static readonly Font TitleFont = new("Segoe UI Semibold", 16F);
    public static readonly Font HeaderFont = new("Segoe UI Semibold", 12F);
    public static readonly Font SmallFont = new("Segoe UI", 9F);

    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static string Money(decimal value) => value.ToString("#,##0", Vi) + " ₫";
    public const string MoneyFormat = "#,##0 ₫";
    public const string DateTimeFormat = "dd/MM/yyyy HH:mm";

    // ===================== Hộp thoại =====================

    public static void Info(string message) =>
        MessageBox.Show(message, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Error(string message) =>
        MessageBox.Show(message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static bool Confirm(string message, string title = "Xác nhận") =>
        MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    /// <summary>Hiển thị kết quả nghiệp vụ; trả về true nếu thành công.</summary>
    public static bool Show(Result result)
    {
        if (result.Success) Info(result.Message);
        else MessageBox.Show(result.Message, "Không thực hiện được", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return result.Success;
    }

    public static void ShowException(Exception ex)
    {
        var root = ex.GetBaseException();
        var message = root is Microsoft.Data.SqlClient.SqlException
            ? "Lỗi kết nối hoặc truy vấn cơ sở dữ liệu. Vui lòng kiểm tra SQL Server và thử lại.\n\nChi tiết: " + root.Message
            : "Đã xảy ra lỗi không mong muốn.\n\nChi tiết: " + root.Message;
        Error(message);
    }

    // ===================== Điều khiển =====================

    public static Button Button(string text, EventHandler? onClick = null, ButtonKind kind = ButtonKind.Default, int width = 0)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = width == 0,
            Width = width == 0 ? 90 : width,
            Height = 34,
            Padding = new Padding(10, 0, 10, 0),
            FlatStyle = FlatStyle.Flat,
            Font = BaseFont,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0),
            UseVisualStyleBackColor = false
        };
        var (back, fore, border) = kind switch
        {
            ButtonKind.Primary => (Primary, Color.White, Primary),
            ButtonKind.Danger => (Danger, Color.White, Danger),
            ButtonKind.Success => (Success, Color.White, Success),
            _ => (Surface, Color.FromArgb(35, 39, 47), Border)
        };
        b.BackColor = back;
        b.ForeColor = fore;
        b.FlatAppearance.BorderColor = border;
        if (onClick != null) b.Click += onClick;
        return b;
    }

    public static Label Label(string text, Font? font = null, Color? color = null) => new()
    {
        Text = text, AutoSize = true, Font = font ?? BaseFont, ForeColor = color ?? Color.FromArgb(35, 39, 47),
        Margin = new Padding(0, 8, 8, 0)
    };

    public static TextBox TextBox(string placeholder = "", int width = 220) => new()
    {
        PlaceholderText = placeholder, Width = width, Font = BaseFont, Margin = new Padding(0, 4, 8, 0)
    };

    public static ComboBox Combo(int width = 200) => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, Width = width, Font = BaseFont, Margin = new Padding(0, 4, 8, 0)
    };

    /// <summary>Thanh công cụ nằm trên đầu mỗi màn hình (tìm kiếm + nút chức năng).</summary>
    public static FlowLayoutPanel Toolbar() => new()
    {
        Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(0, 0, 0, 10), WrapContents = true, BackColor = Background
    };

    // ===================== Lưới dữ liệu =====================

    public static DataGridView Grid()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            BackgroundColor = Surface,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Border,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 38,
            EnableHeadersVisualStyles = false,
            Font = BaseFont
        };
        g.RowTemplate.Height = 34;
        g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(236, 241, 246);
        g.ColumnHeadersDefaultCellStyle.ForeColor = PrimaryDark;
        g.ColumnHeadersDefaultCellStyle.Font = BoldFont;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(236, 241, 246);
        g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 228, 242);
        g.DefaultCellStyle.SelectionForeColor = Color.Black;
        g.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 249, 246);
        g.DataError += (_, e) => e.ThrowException = false;
        return g;
    }

    /// <summary>Thêm cột vào lưới. fill = tỉ lệ chiếm chỗ; format: "money", "date", "int" hoặc chuỗi định dạng .NET.</summary>
    public static DataGridViewColumn Col(this DataGridView g, string property, string header, int fill = 100,
        string? format = null, bool right = false)
    {
        var col = new DataGridViewTextBoxColumn
        {
            DataPropertyName = property, HeaderText = header, FillWeight = fill, Name = property,
            SortMode = DataGridViewColumnSortMode.NotSortable, MinimumWidth = 40
        };
        switch (format)
        {
            case "money":
                col.DefaultCellStyle.Format = MoneyFormat;
                col.DefaultCellStyle.FormatProvider = Vi;
                right = true;
                break;
            case "date":
                col.DefaultCellStyle.Format = DateTimeFormat;
                break;
            case "int":
                right = true;
                break;
            case not null:
                col.DefaultCellStyle.Format = format;
                break;
        }
        if (right) col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        g.Columns.Add(col);
        return col;
    }

    public static DataGridViewCheckBoxColumn CheckCol(this DataGridView g, string property, string header, int fill = 50)
    {
        var col = new DataGridViewCheckBoxColumn { DataPropertyName = property, HeaderText = header, FillWeight = fill, Name = property };
        g.Columns.Add(col);
        return col;
    }

    public static T? Selected<T>(this DataGridView g) where T : class => g.CurrentRow?.DataBoundItem as T;

    /// <summary>Gán dữ liệu cho lưới và cố gắng giữ dòng đang chọn (theo khóa).</summary>
    public static void Bind<T>(this DataGridView g, IList<T> items, Func<T, object>? key = null) where T : class
    {
        object? selectedKey = key != null && g.Selected<T>() is { } cur ? key(cur) : null;
        g.DataSource = new System.ComponentModel.BindingList<T>(items);
        if (selectedKey == null || key == null) return;
        foreach (DataGridViewRow row in g.Rows)
            if (row.DataBoundItem is T item && Equals(key(item), selectedKey))
            {
                g.CurrentCell = row.Cells.Cast<DataGridViewCell>().First(c => c.Visible);
                break;
            }
    }
}

public enum ButtonKind { Default, Primary, Danger, Success }
