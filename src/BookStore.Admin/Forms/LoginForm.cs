using BookStore.Admin.Infrastructure;
using BookStore.Business.Services;

namespace BookStore.Admin.Forms;

/// <summary>Màn hình đăng nhập: chỉ Quản trị viên và Nhân viên đang hoạt động được vào.</summary>
public class LoginForm : Form
{
    private readonly ServiceRunner _services;
    private readonly AppSession _session;
    private readonly TextBox _username;
    private readonly TextBox _password;
    private readonly Label _error;
    private readonly Button _login;

    public LoginForm(ServiceRunner services, AppSession session)
    {
        _services = services;
        _session = session;

        Text = "BookStore – Đăng nhập quản trị";
        Font = Ui.BaseFont;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(820, 480);
        BackColor = Ui.Surface;
        AutoScaleMode = AutoScaleMode.Font;

        // Bên trái: khối giới thiệu
        var brand = new Panel { Dock = DockStyle.Left, Width = 360, BackColor = Ui.Primary, Padding = new Padding(36) };
        brand.Controls.Add(new Label
        {
            Text = "Hệ thống quản lý nhà sách\n\n• Quản lý sách, danh mục, tác giả, NXB\n• Xử lý đơn hàng từ website\n• Nhập kho, kiểm kê, cảnh báo hết hàng\n• Thống kê doanh thu\n\nDữ liệu dùng chung với website bán sách.",
            Dock = DockStyle.Fill, ForeColor = Color.FromArgb(220, 230, 240), Font = new Font("Segoe UI", 10.5F)
        });
        brand.Controls.Add(new Label
        {
            Text = "📚 BookStore", Dock = DockStyle.Top, Height = 70, ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 24F)
        });

        // Bên phải: form đăng nhập
        var form = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(48, 60, 48, 30), ColumnCount = 1 };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.Controls.Add(new Label { Text = "Đăng nhập", Font = Ui.TitleFont, AutoSize = true, ForeColor = Ui.PrimaryDark });
        form.Controls.Add(new Label { Text = "Dành cho Quản trị viên và Nhân viên", AutoSize = true, ForeColor = Ui.Muted, Margin = new Padding(3, 0, 3, 24) });
        form.Controls.Add(new Label { Text = "Tên đăng nhập hoặc Email", AutoSize = true });
        _username = new TextBox { Dock = DockStyle.Top, Font = new Font("Segoe UI", 11F), Margin = new Padding(3, 4, 3, 14) };
        form.Controls.Add(_username);
        form.Controls.Add(new Label { Text = "Mật khẩu", AutoSize = true });
        _password = new TextBox { Dock = DockStyle.Top, Font = new Font("Segoe UI", 11F), UseSystemPasswordChar = true, Margin = new Padding(3, 4, 3, 6) };
        form.Controls.Add(_password);
        var show = new CheckBox { Text = "Hiện mật khẩu", AutoSize = true, ForeColor = Ui.Muted };
        show.CheckedChanged += (_, _) => _password.UseSystemPasswordChar = !show.Checked;
        form.Controls.Add(show);
        _error = new Label { AutoSize = true, ForeColor = Ui.Danger, MaximumSize = new Size(360, 0), Margin = new Padding(3, 8, 3, 8) };
        form.Controls.Add(_error);
        _login = Ui.Button("Đăng nhập", async (_, _) => await LoginAsync(), ButtonKind.Primary, 360);
        _login.Height = 42;
        _login.AutoSize = false;
        _login.Dock = DockStyle.Top;
        form.Controls.Add(_login);
        form.Controls.Add(new Label
        {
            Text = "Tài khoản mẫu: admin / Admin@123 · nhanvien / Nhanvien@123", AutoSize = true, ForeColor = Ui.Muted,
            Font = Ui.SmallFont, Margin = new Padding(3, 16, 3, 0)
        });

        Controls.Add(form);
        Controls.Add(brand);
        AcceptButton = _login;
    }

    private async Task LoginAsync()
    {
        _error.Text = "";
        _login.Enabled = false;
        UseWaitCursor = true;
        try
        {
            var result = await _services.Run<AuthService, Business.Common.Result<Business.Common.UserSession>>(
                s => s.LoginAsync(_username.Text, _password.Text, LoginChannel.Desktop));
            if (!result.Success)
            {
                _error.Text = result.Message;
                _password.SelectAll();
                _password.Focus();
                return;
            }
            _session.SignIn(result.Value!);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            Ui.ShowException(ex);
        }
        finally
        {
            UseWaitCursor = false;
            _login.Enabled = true;
        }
    }
}
