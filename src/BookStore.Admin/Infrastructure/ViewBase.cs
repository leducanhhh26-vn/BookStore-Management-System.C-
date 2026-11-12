using BookStore.Business.Common;

namespace BookStore.Admin.Infrastructure;

/// <summary>
/// Lớp cơ sở cho các màn hình chức năng (hiển thị trong vùng nội dung của MainForm).
/// Cung cấp: tiêu đề, truy cập Service, người dùng hiện tại và hàm chạy an toàn (bắt lỗi, con trỏ chờ).
/// </summary>
public class ViewBase : UserControl
{
    protected ServiceRunner Services { get; }
    protected AppSession Session { get; }
    protected UserSession Actor => Session.User;

    public virtual string Title => "";

    protected ViewBase(ServiceRunner services, AppSession session)
    {
        Services = services;
        Session = session;
        Dock = DockStyle.Fill;
        BackColor = Ui.Background;
        Font = Ui.BaseFont;
        Padding = new Padding(20, 16, 20, 16);
    }

    /// <summary>Tải lại dữ liệu của màn hình. MainForm gọi khi mở màn hình.</summary>
    public virtual Task LoadDataAsync() => Task.CompletedTask;

    /// <summary>Chạy một thao tác, bắt mọi ngoại lệ và báo lỗi thân thiện thay vì làm sập ứng dụng.</summary>
    protected async Task Safe(Func<Task> action)
    {
        var form = FindForm();
        try
        {
            if (form != null) form.UseWaitCursor = true;
            await action();
        }
        catch (Exception ex)
        {
            Ui.ShowException(ex);
        }
        finally
        {
            if (form != null) form.UseWaitCursor = false;
        }
    }

    protected Task<TResult> Run<TService, TResult>(Func<TService, Task<TResult>> action) where TService : notnull =>
        Services.Run(action);
}

/// <summary>Hộp thoại nhập liệu dạng 2 cột (nhãn | ô nhập) dùng chung cho các form Thêm/Sửa.</summary>
public class DialogBase : Form
{
    protected readonly TableLayoutPanel Fields;
    protected readonly FlowLayoutPanel Buttons;
    protected readonly Button SaveButton;
    protected readonly Button CancelButtonControl;

    protected DialogBase(string title, int width = 520)
    {
        Text = title;
        Font = Ui.BaseFont;
        BackColor = Ui.Surface;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        Width = width;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        Fields = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2,
            Padding = new Padding(0, 0, 0, 8), Width = width - 40
        };
        Fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        Fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width - 190));

        SaveButton = Ui.Button("Lưu", null, ButtonKind.Primary, 100);
        CancelButtonControl = Ui.Button("Hủy", (_, _) => { DialogResult = DialogResult.Cancel; Close(); }, ButtonKind.Default, 100);
        Buttons = new FlowLayoutPanel
        {
            AutoSize = false, FlowDirection = FlowDirection.RightToLeft, Width = width - 40, Height = 52,
            Padding = new Padding(0, 10, 0, 0)
        };
        Buttons.Controls.Add(CancelButtonControl);
        Buttons.Controls.Add(SaveButton);

        var layout = new FlowLayoutPanel
        {
            Location = new Point(0, 0), FlowDirection = FlowDirection.TopDown, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Padding = new Padding(16)
        };
        layout.Controls.Add(Fields);
        layout.Controls.Add(Buttons);
        Controls.Add(layout);

        AcceptButton = SaveButton;
        CancelButton = CancelButtonControl;
    }

    /// <summary>Thêm một dòng "nhãn: điều khiển" vào form.</summary>
    protected T AddField<T>(string label, T control, int height = 0) where T : Control
    {
        var lbl = Ui.Label(label);
        lbl.Margin = new Padding(0, 10, 8, 0);
        // Anchor trái-phải: ô nhập giãn hết chiều ngang cột, giữ chiều cao riêng (ô mô tả nhiều dòng)
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        control.Margin = new Padding(0, 4, 0, 4);
        if (height > 0) control.Height = height;
        Fields.Controls.Add(lbl);
        Fields.Controls.Add(control);
        return control;
    }

    protected static NumericUpDown Number(decimal min, decimal max, decimal value = 0, int decimals = 0, decimal increment = 1) => new()
    {
        Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), DecimalPlaces = decimals,
        Increment = increment, ThousandsSeparator = true, TextAlign = HorizontalAlignment.Right
    };

    /// <summary>Gắn hàm lưu bất đồng bộ cho nút Lưu: chỉ đóng form khi lưu thành công.</summary>
    protected void OnSave(Func<Task<Result>> save)
    {
        SaveButton.Click += async (_, _) =>
        {
            SaveButton.Enabled = false;
            UseWaitCursor = true;
            try
            {
                var result = await save();
                if (result.Success)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    MessageBox.Show(result.Message, "Dữ liệu chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Ui.ShowException(ex);
            }
            finally
            {
                UseWaitCursor = false;
                SaveButton.Enabled = true;
            }
        };
    }
}

/// <summary>Hộp thoại nhập một giá trị (lý do hủy đơn, mật khẩu mới, mã giao dịch...).</summary>
public class InputDialog : DialogBase
{
    private readonly TextBox _input;
    public string Value => _input.Text.Trim();

    private InputDialog(string title, string label, string initial, bool multiline, bool password) : base(title, 480)
    {
        _input = AddField(label, new TextBox
        {
            Text = initial, Multiline = multiline, UseSystemPasswordChar = password,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None
        }, multiline ? 70 : 0);
        SaveButton.Text = "Đồng ý";
        SaveButton.Click += (_, _) =>
        {
            if (Value.Length == 0)
            {
                Ui.Error("Vui lòng nhập " + label.TrimEnd(':').ToLower() + ".");
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
    }

    public static string? Ask(IWin32Window? owner, string title, string label, string initial = "", bool multiline = false, bool password = false)
    {
        using var dlg = new InputDialog(title, label, initial, multiline, password);
        return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.Value : null;
    }
}
