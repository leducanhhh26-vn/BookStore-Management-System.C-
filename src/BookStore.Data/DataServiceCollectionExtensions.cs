using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>Đăng ký DbContext dùng SQL Server. Web và Admin gọi cùng hàm này với cùng connection string.</summary>
    public static IServiceCollection AddBookStoreData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<BookStoreDbContext>(options =>
            options.UseSqlServer(connectionString));
        return services;
    }
}

/// <summary>
/// Dùng cho lệnh "dotnet ef migrations add ..." lúc thiết kế (không cần chạy Web/Admin).
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BookStoreDbContext>
{
    public BookStoreDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("BOOKSTORE_CONNECTION")
                 ?? @"Server=(localdb)\MSSQLLocalDB;Database=BookStoreDb;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<BookStoreDbContext>().UseSqlServer(cs).Options;
        return new BookStoreDbContext(options);
    }
}
