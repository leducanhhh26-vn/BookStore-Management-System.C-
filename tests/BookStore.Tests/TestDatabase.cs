using BookStore.Business.Common;
using BookStore.Business.Services;
using BookStore.Data;
using BookStore.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Tests;

/// <summary>
/// Database SQLite trong bộ nhớ cho kiểm thử. Mỗi test có database riêng, sạch.
/// Dữ liệu tối thiểu: 1 admin, 1 nhân viên, 2 khách, 2 sách (tồn 10 và 3).
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    public BookStoreDbContext Db { get; }

    public User Admin { get; private set; } = null!;
    public User Staff { get; private set; } = null!;
    public User Customer { get; private set; } = null!;
    public User Customer2 { get; private set; } = null!;
    public Book BookA { get; private set; } = null!;   // giá 100.000, tồn 10
    public Book BookB { get; private set; } = null!;   // giá 250.000, tồn 3

    public UserSession AdminSession => new(Admin.Id, Admin.Username, Admin.FullName, Admin.Role);
    public UserSession StaffSession => new(Staff.Id, Staff.Username, Staff.FullName, Staff.Role);

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        Db = CreateContext();
        Db.Database.EnsureCreated();
        Seed();
    }

    /// <summary>Tạo thêm một DbContext trên cùng database (giả lập request/màn hình khác).</summary>
    public BookStoreDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<BookStoreDbContext>().UseSqlite(_connection).Options);

    private void Seed()
    {
        // Băm mật khẩu với work factor thấp để test chạy nhanh
        string Hash(string pw) => BCrypt.Net.BCrypt.HashPassword(pw, 4);
        Admin = new User { Username = "admin", FullName = "Admin", Email = "admin@test.vn", Phone = "0900000001", Role = UserRole.Admin, PasswordHash = Hash("Admin@123") };
        Staff = new User { Username = "staff", FullName = "Nhân viên", Email = "staff@test.vn", Phone = "0900000002", Role = UserRole.Staff, PasswordHash = Hash("Staff@123") };
        Customer = new User { Username = "khach", FullName = "Khách A", Email = "khach@test.vn", Phone = "0900000003", Role = UserRole.Customer, PasswordHash = Hash("Khach@123") };
        Customer2 = new User { Username = "khach2", FullName = "Khách B", Email = "khach2@test.vn", Phone = "0900000004", Role = UserRole.Customer, PasswordHash = Hash("Khach@123") };
        Db.Users.AddRange(Admin, Staff, Customer, Customer2);

        var category = new Category { Name = "Văn học" };
        var publisher = new Publisher { Name = "NXB Trẻ" };
        var author = new Author { Name = "Nguyễn Nhật Ánh" };
        BookA = new Book { Title = "Mắt Biếc", Isbn = "9786040000017", Category = category, Publisher = publisher, Price = 100000, Pages = 300, PublishYear = 2019, Stock = 10 };
        BookB = new Book { Title = "Hoa Vàng", Isbn = "9786040000024", Category = category, Publisher = publisher, Price = 250000, Pages = 378, PublishYear = 2018, Stock = 3 };
        BookA.BookAuthors.Add(new BookAuthor { Author = author });
        BookB.BookAuthors.Add(new BookAuthor { Author = author });
        Db.Books.AddRange(BookA, BookB);

        Db.Coupons.Add(new Coupon
        {
            Code = "GIAM10", DiscountType = DiscountType.Percent, Value = 10, MaxDiscount = 20000, MinOrder = 100000,
            StartAt = DateTime.Now.AddDays(-1), EndAt = DateTime.Now.AddDays(30), UsageLimit = 1
        });
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    public PlaceOrderDto ValidShipping(string? coupon = null) => new()
    {
        ReceiverName = "Khách A", ReceiverPhone = "0900000003", ShippingAddress = "273 An Dương Vương, Q5, TP.HCM",
        PaymentMethod = PaymentMethod.COD, CouponCode = coupon
    };

    public int StockOf(int bookId)
    {
        using var ctx = CreateContext();
        return ctx.Books.Where(b => b.Id == bookId).Select(b => b.Stock).First();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
