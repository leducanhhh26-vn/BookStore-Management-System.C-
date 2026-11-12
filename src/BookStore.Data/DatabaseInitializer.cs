using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.Data;

/// <summary>
/// Tạo/cập nhật database bằng Migrations và nạp dữ liệu mẫu nếu database còn trống.
/// Được gọi khi khởi động cả Web và Admin, nên khởi động app nào trước cũng được.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);
    }
}
