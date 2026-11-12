using BookStore.Business.Services;
using BookStore.Data;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.Business;

public static class BusinessServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký toàn bộ tầng dữ liệu + nghiệp vụ. Web và Admin đều gọi đúng hàm này,
    /// nên hai ứng dụng dùng chung một bộ quy tắc nghiệp vụ và một database.
    /// </summary>
    public static IServiceCollection AddBookStore(this IServiceCollection services, string connectionString)
    {
        services.AddBookStoreData(connectionString);

        // Scoped: mỗi request (Web) hoặc mỗi màn hình (Desktop) dùng một DbContext riêng
        services.AddScoped<AuthService>();
        services.AddScoped<AccountService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<AuthorService>();
        services.AddScoped<PublisherService>();
        services.AddScoped<SupplierService>();
        services.AddScoped<BookService>();
        services.AddScoped<CartService>();
        services.AddScoped<CouponService>();
        services.AddScoped<OrderService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<ReportService>();
        services.AddScoped<ReviewService>();
        return services;
    }
}
