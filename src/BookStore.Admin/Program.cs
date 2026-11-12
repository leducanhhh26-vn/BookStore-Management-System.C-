using BookStore.Admin.Forms;
using BookStore.Admin.Infrastructure;
using BookStore.Admin.Views;
using BookStore.Business;
using BookStore.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BookStore.Admin;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Ui.ShowException(e.Exception);

        // ---------------- Cấu hình + Dependency Injection ----------------
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory
        });
        var connectionString = builder.Configuration.GetConnectionString("BookStore");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Ui.Error("Thiếu ConnectionStrings:BookStore trong file appsettings.json.");
            return;
        }

        // Cùng một hàm đăng ký với ứng dụng Web → cùng tầng nghiệp vụ, cùng database
        builder.Services.AddBookStore(connectionString);
        builder.Services.AddSingleton<AppSession>();
        builder.Services.AddSingleton<ServiceRunner>();
        builder.Services.AddTransient<LoginForm>();
        builder.Services.AddTransient<MainForm>();
        builder.Services.AddTransient<DashboardView>();
        builder.Services.AddTransient<BooksView>();
        builder.Services.AddTransient<CategoriesView>();
        builder.Services.AddTransient<AuthorsView>();
        builder.Services.AddTransient<PublishersView>();
        builder.Services.AddTransient<SuppliersView>();
        builder.Services.AddTransient<CustomersView>();
        builder.Services.AddTransient<OrdersView>();
        builder.Services.AddTransient<InventoryView>();
        builder.Services.AddTransient<AccountsView>();
        builder.Services.AddTransient<CouponsView>();
        builder.Services.AddTransient<ReportsView>();
        using var host = builder.Build();

        // ---------------- Kết nối & khởi tạo database ----------------
        try
        {
            Cursor.Current = Cursors.WaitCursor;
            DatabaseInitializer.InitializeAsync(host.Services).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Ui.Error("Không kết nối được SQL Server.\n\nKiểm tra SQL Server đã chạy và chuỗi kết nối trong appsettings.json.\n\nChi tiết: "
                     + ex.GetBaseException().Message);
            return;
        }
        finally
        {
            Cursor.Current = Cursors.Default;
        }

        // ---------------- Vòng đăng nhập → màn hình chính → đăng xuất ----------------
        var session = host.Services.GetRequiredService<AppSession>();
        while (true)
        {
            using (var login = host.Services.GetRequiredService<LoginForm>())
            {
                if (login.ShowDialog() != DialogResult.OK) break;
            }

            using var main = host.Services.GetRequiredService<MainForm>();
            Application.Run(main);
            if (!main.LoggedOut) break;
            session.Clear();
        }
    }
}
