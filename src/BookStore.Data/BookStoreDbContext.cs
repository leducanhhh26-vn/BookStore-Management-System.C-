using BookStore.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Data;

/// <summary>
/// DbContext duy nhất của hệ thống. Cả BookStore.Web và BookStore.Admin đều dùng lớp này
/// và trỏ tới CÙNG MỘT database SQL Server.
/// </summary>
public class BookStoreDbContext : DbContext
{
    public BookStoreDbContext(DbContextOptions<BookStoreDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();
    public DbSet<BookImage> BookImages => Set<BookImage>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ImportReceipt> ImportReceipts => Set<ImportReceipt>();
    public DbSet<ImportReceiptDetail> ImportReceiptDetails => Set<ImportReceiptDetail>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Các cấu hình bảng nằm trong thư mục Configurations (mỗi nhóm một file)
        b.ApplyConfigurationsFromAssembly(typeof(BookStoreDbContext).Assembly);

        // SQLite (chỉ dùng trong unit test) không tự sinh ROWVERSION như SQL Server
        // nên tắt cơ chế kiểm tra phiên bản khi chạy test.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            b.Entity<Book>().Property(x => x.RowVersion).IsConcurrencyToken(false).ValueGeneratedNever();
            b.Entity<Order>().Property(x => x.RowVersion).IsConcurrencyToken(false).ValueGeneratedNever();

            // SQLite lưu decimal dạng TEXT → so sánh trong CHECK sai; khi test thì lưu dạng số thực
            foreach (var p in b.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                         .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
                p.SetValueConverter(typeof(Microsoft.EntityFrameworkCore.Storage.ValueConversion.CastingConverter<,>)
                    .MakeGenericType(p.ClrType, p.ClrType == typeof(decimal) ? typeof(double) : typeof(double?))
                    .GetConstructor(Type.EmptyTypes)!.Invoke(null) as Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter);
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        // Tiền tệ: DECIMAL(18,2), không dùng số thực dấu phẩy động
        c.Properties<decimal>().HavePrecision(18, 2);
        // Enum lưu dạng chữ để đọc dễ trong SSMS (vd: 'Pending', 'Admin')
        c.Properties<UserRole>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<OrderStatus>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<PaymentMethod>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<PaymentStatus>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<StockTransactionType>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<DiscountType>().HaveConversion<string>().HaveMaxLength(20);
    }
}
