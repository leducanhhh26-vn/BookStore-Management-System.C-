using System.Media;
using BookStore.Admin.Infrastructure;
using BookStore.Admin.Views;
using BookStore.Business.Services;
using BookStore.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.Admin.Forms;

/// <summary>
/// Cửa sổ chính: menu bên trái, thanh tiêu đề, vùng nội dung hiển thị từng màn hình chức năng.
/// Có bộ hẹn giờ kiểm tra đơn hàng mới từ website mỗi 15 giây.
/// </summary>
public class MainForm : Form
{
    private readonly IServiceProvider _provider;
    private readonly ServiceRunner _services;
    private readonly AppSession _session;
    private readonly Panel _content;
    private readonly Label _title;
    private readonly Label _newOrderBanner;
    private readonly Dictionary<Type, Button> _navButtons = new();
    private readonly System.Windows.Forms.Timer _orderTimer;
    private ViewBase? _current;
    private Button? _ordersButton;
    private int _lastOrderId;

    public bool LoggedOut { get; private set; }

    public MainForm(IServiceProvider provider, ServiceRunner services, AppSession session)
    {
        _provider = provider;
        _services = services;
        _session = session;

        Text = "BookStore – Quản trị nhà sách";
        Font = Ui.BaseFont;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1150, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Ui.Background;
        AutoScaleMode = AutoScaleMode.Font;

        // ---------------- Menu bên trái ----------------
        var sidebar = new FlowLayoutPanel
        {
            Dock = DockStyle.Left, Width = 230, BackColor = Ui.PrimaryDark, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(0, 0, 0, 12)
        };
        sidebar.Controls.Add(new Label
        {
            Text = "📚 BookStore", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 16F),
            Width = 230, Height = 64, TextAlign = ContentAlignment.MiddleCenter
        });

        AddSection(sidebar, "TỔNG QUAN");
        AddNav<DashboardView>(sidebar, "📊  Dashboard");
        AddSection(sidebar, "BÁN HÀNG");
        _ordersButton = AddNav<OrdersView>(sidebar, "🧾  Đơn hàng");
        AddNav<CustomersView>(sidebar, "👥  Khách hàng");
        AddSection(sidebar, "SẢN PHẨM");
        AddNav<BooksView>(sidebar, "📖  Sách");
        AddNav<CategoriesView>(sidebar, "🗂  Danh mục");
        AddNav<AuthorsView>(sidebar, "✍  Tác giả");
        AddNav<PublishersView>(sidebar, "🏢  Nhà xuất bản");
        AddSection(sidebar, "KHO HÀNG");
        AddNav<InventoryView>(sidebar, "📦  Quản lý kho");
        AddNav<SuppliersView>(sidebar, "🚚  Nhà cung cấp");
        AddSection(sidebar, "BÁO CÁO");
        AddNav<ReportsView>(sidebar, "📈  Thống kê");
        // Chức năng chỉ dành cho Quản trị viên: ẩn menu với Nhân viên (Service cũng kiểm tra quyền lần nữa)
        if (_session.IsAdmin)
        {
            AddSection(sidebar, "QUẢN TRỊ");
            AddNav<CouponsView>(sidebar, "🏷  Mã giảm giá");
            AddNav<AccountsView>(sidebar, "🔐  Tài khoản & phân quyền");
        }

        // ---------------- Thanh tiêu đề ----------------
        var header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Ui.Surface, Padding = new Padding(20, 0, 16, 0) };
        _title = new Label { Dock = DockStyle.Left, AutoSize = false, Width = 420, Font = Ui.TitleFont, ForeColor = Ui.PrimaryDark, TextAlign = ContentAlignment.MiddleLeft };
        var user = _session.User;
        var userPanel = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 12, 0, 0), WrapContents = false };
        userPanel.Controls.Add(new Label
        {
            Text = $"{user.FullName}  ·  {user.Role.ToVietnamese()}", AutoSize = true, ForeColor = Ui.Muted, Margin = new Padding(0, 8, 12, 0)
        });
        userPanel.Controls.Add(Ui.Button("Đổi mật khẩu", (_, _) => ChangePassword()));
        userPanel.Controls.Add(Ui.Button("Đăng xuất", (_, _) => Logout(), ButtonKind.Default));
        _newOrderBanner = new Label
        {
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, BackColor = Ui.Accent,
            Font = Ui.BoldFont, Visible = false, Cursor = Cursors.Hand, Margin = new Padding(10)
        };
        _newOrderBanner.Click += (_, _) =>
        {
            _newOrderBanner.Visible = false;
            _ = ShowView<OrdersView>();
        };
        header.Controls.Add(_newOrderBanner);
        header.Controls.Add(userPanel);
        header.Controls.Add(_title);
        var headerLine = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Ui.Border };

        _content = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Background };

        Controls.Add(_content);
        Controls.Add(headerLine);
        Controls.Add(header);
        Controls.Add(sidebar);

        _orderTimer = new System.Windows.Forms.Timer { Interval = 15000 };
        _orderTimer.Tick += async (_, _) => await CheckNewOrdersAsync(notify: true);

        Load += async (_, _) =>
        {
            await CheckNewOrdersAsync(notify: false);
            _orderTimer.Start();
            await ShowView<DashboardView>();
        };
        FormClosed += (_, _) => _orderTimer.Stop();
    }

    private static void AddSection(FlowLayoutPanel sidebar, string text) =>
        sidebar.Controls.Add(new Label
        {
            Text = text, ForeColor = Color.FromArgb(130, 150, 170), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Width = 230, Height = 30, Padding = new Padding(22, 12, 0, 0)
        });

    private Button AddNav<TView>(FlowLayoutPanel sidebar, string text) where TView : ViewBase
    {
        var b = new Button
        {
            Text = text, Width = 230, Height = 42, FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(220, 228, 236),
            BackColor = Ui.PrimaryDark, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(18, 0, 0, 0),
            Font = new Font("Segoe UI", 10.5F), Cursor = Cursors.Hand, Margin = new Padding(0)
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(36, 60, 84);
        b.Click += async (_, _) => await ShowView<TView>();
        _navButtons[typeof(TView)] = b;
        sidebar.Controls.Add(b);
        return b;
    }

    /// <summary>Tạo màn hình mới qua DI rồi hiển thị trong vùng nội dung.</summary>
    public async Task ShowView<TView>() where TView : ViewBase
    {
        foreach (var (type, button) in _navButtons)
            button.BackColor = type == typeof(TView) ? Ui.Primary : Ui.PrimaryDark;

        _content.SuspendLayout();
        _current?.Dispose();
        _content.Controls.Clear();
        _current = _provider.GetRequiredService<TView>();
        _title.Text = _current.Title;
        _content.Controls.Add(_current);
        _content.ResumeLayout();
        if (typeof(TView) == typeof(OrdersView)) _newOrderBanner.Visible = false;

        try
        {
            UseWaitCursor = true;
            await _current.LoadDataAsync();
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

    /// <summary>Đếm đơn chờ xử lý và phát hiện đơn mới khách vừa đặt trên website.</summary>
    private async Task CheckNewOrdersAsync(bool notify)
    {
        try
        {
            var pending = await _services.Run<OrderService, int>(s => s.CountPendingAsync());
            var latest = await _services.Run<OrderService, int>(s => s.GetLatestOrderIdAsync());
            if (_ordersButton != null) _ordersButton.Text = pending > 0 ? $"🧾  Đơn hàng  ({pending} chờ)" : "🧾  Đơn hàng";

            if (notify && latest > _lastOrderId && _lastOrderId > 0)
            {
                _newOrderBanner.Text = $"🔔 Có đơn hàng mới #{latest} từ website – bấm để xem";
                _newOrderBanner.Visible = true;
                SystemSounds.Asterisk.Play();
                if (_current is OrdersView orders) await orders.LoadDataAsync();
            }
            _lastOrderId = latest;
        }
        catch
        {
            // Mất kết nối tạm thời: bỏ qua, lần kiểm tra sau sẽ thử lại
        }
    }

    private async void ChangePassword()
    {
        var current = InputDialog.Ask(this, "Đổi mật khẩu", "Mật khẩu hiện tại:", password: true);
        if (current == null) return;
        var newPw = InputDialog.Ask(this, "Đổi mật khẩu", "Mật khẩu mới:", password: true);
        if (newPw == null) return;
        var confirm = InputDialog.Ask(this, "Đổi mật khẩu", "Nhập lại mật khẩu mới:", password: true);
        if (confirm == null) return;
        try
        {
            Ui.Show(await _services.Run<AuthService, Business.Common.Result>(
                s => s.ChangePasswordAsync(_session.User.Id, current, newPw, confirm)));
        }
        catch (Exception ex)
        {
            Ui.ShowException(ex);
        }
    }

    private void Logout()
    {
        if (!Ui.Confirm("Bạn muốn đăng xuất khỏi hệ thống?")) return;
        LoggedOut = true;
        Close();
    }
}
