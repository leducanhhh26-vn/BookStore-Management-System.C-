using BookStore.Business.Common;
using BookStore.Business.Services;
using BookStore.Data;
using BookStore.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task Login_ChecksPassword_LockedAccount_AndChannel()
    {
        using var t = new TestDatabase();
        var auth = new AuthService(t.Db);

        Assert.True((await auth.LoginAsync("admin", "Admin@123", LoginChannel.Desktop)).Success);
        Assert.False((await auth.LoginAsync("admin", "sai-mat-khau", LoginChannel.Desktop)).Success);
        Assert.False((await auth.LoginAsync("khach", "Khach@123", LoginChannel.Desktop)).Success); // khách không vào Desktop
        Assert.False((await auth.LoginAsync("admin", "Admin@123", LoginChannel.Web)).Success);     // admin không mua hàng trên Web
        Assert.True((await auth.LoginAsync("khach@test.vn", "Khach@123", LoginChannel.Web)).Success); // đăng nhập bằng email

        await new CustomerService(t.Db, auth).SetActiveAsync(t.StaffSession, t.Customer.Id, false);
        var locked = await auth.LoginAsync("khach", "Khach@123", LoginChannel.Web);
        Assert.False(locked.Success);
        Assert.Contains("khóa", locked.Message);
    }

    [Fact]
    public async Task Register_ValidatesInput_AndRejectsDuplicates()
    {
        using var t = new TestDatabase();
        var auth = new AuthService(t.Db);
        RegisterDto Dto(string user, string email, string phone) => new()
        {
            FullName = "Người Mới", Username = user, Email = email, Phone = phone, Password = "Matkhau1", ConfirmPassword = "Matkhau1"
        };

        Assert.True((await auth.RegisterCustomerAsync(Dto("nguoimoi", "moi@test.vn", "0911111111"))).Success);
        Assert.False((await auth.RegisterCustomerAsync(Dto("khac", "moi@test.vn", "0922222222"))).Success);  // trùng email
        Assert.False((await auth.RegisterCustomerAsync(Dto("khac", "khac@test.vn", "0911111111"))).Success); // trùng SĐT
        Assert.False((await auth.RegisterCustomerAsync(Dto("khac", "email-sai", "0933333333"))).Success);    // email sai

        var mismatch = Dto("khac2", "khac2@test.vn", "0944444444");
        mismatch.ConfirmPassword = "khac";
        Assert.False((await auth.RegisterCustomerAsync(mismatch)).Success);

        var stored = await t.Db.Users.SingleAsync(u => u.Username == "nguoimoi");
        Assert.NotEqual("Matkhau1", stored.PasswordHash); // không lưu mật khẩu gốc
        Assert.Equal(UserRole.Customer, stored.Role);
    }
}

public class AccountServiceTests
{
    [Fact]
    public async Task OnlyAdmin_CanManageStaff_AndAdminCannotLockOrDemoteSelf()
    {
        using var t = new TestDatabase();
        var service = new AccountService(t.Db, new AuthService(t.Db));
        var newStaff = new StaffEditDto { Username = "nhanvien3", FullName = "NV 3", Email = "nv3@test.vn", Phone = "0955555555", Role = UserRole.Staff, Password = "Staff123" };

        Assert.False((await service.SaveAsync(t.StaffSession, newStaff)).Success);   // nhân viên không có quyền
        Assert.True((await service.SaveAsync(t.AdminSession, newStaff)).Success);
        Assert.False((await service.SetActiveAsync(t.AdminSession, t.Admin.Id, false)).Success);

        var self = (await service.GetAsync(t.Admin.Id))!;
        self.Role = UserRole.Staff;
        Assert.False((await service.SaveAsync(t.AdminSession, self)).Success);
    }
}

public class InventoryServiceTests
{
    [Fact]
    public async Task ImportReceipt_IncreasesStock_AndValidatesLines()
    {
        using var t = new TestDatabase();
        t.Db.Suppliers.Add(new Supplier { Name = "Fahasa" });
        await t.Db.SaveChangesAsync();
        var supplierId = (await t.Db.Suppliers.SingleAsync()).Id;
        var service = new InventoryService(t.Db);

        var invalid = await service.CreateImportReceiptAsync(t.StaffSession, new ImportReceiptDto
        {
            SupplierId = supplierId, Lines = { new ImportLine(t.BookA.Id, 0, 50000) }
        });
        Assert.False(invalid.Success);

        var ok = await service.CreateImportReceiptAsync(t.StaffSession, new ImportReceiptDto
        {
            SupplierId = supplierId, Lines = { new ImportLine(t.BookA.Id, 20, 60000), new ImportLine(t.BookB.Id, 5, 150000) }
        });
        Assert.True(ok.Success, ok.Message);
        Assert.Equal(30, t.StockOf(t.BookA.Id));
        Assert.Equal(8, t.StockOf(t.BookB.Id));
        Assert.Equal(2, await t.CreateContext().StockTransactions.CountAsync(x => x.Type == StockTransactionType.Import));
    }

    [Fact]
    public async Task AdjustStock_RequiresReason_AndRejectsNegative()
    {
        using var t = new TestDatabase();
        var service = new InventoryService(t.Db);

        Assert.False((await service.AdjustStockAsync(t.StaffSession, t.BookA.Id, -1, "Sai")).Success);
        Assert.False((await service.AdjustStockAsync(t.StaffSession, t.BookA.Id, 7, "")).Success);
        Assert.True((await service.AdjustStockAsync(t.StaffSession, t.BookA.Id, 7, "Kiểm kê phát hiện 3 cuốn hư")).Success);

        Assert.Equal(7, t.StockOf(t.BookA.Id));
        var log = await t.CreateContext().StockTransactions.SingleAsync();
        Assert.Equal(-3, log.QuantityChange);
    }
}

public class CouponTests
{
    private static Coupon Percent(decimal value, decimal? max, decimal min) => new()
    {
        Code = "X", DiscountType = DiscountType.Percent, Value = value, MaxDiscount = max, MinOrder = min,
        StartAt = DateTime.Now.AddDays(-1), EndAt = DateTime.Now.AddDays(1), UsageLimit = 10
    };

    [Theory]
    [InlineData(200000, 20000)]   // 10% của 200.000
    [InlineData(1000000, 50000)]  // bị chặn bởi mức giảm tối đa
    public void PercentCoupon_RespectsMaxDiscount(decimal subtotal, decimal expected)
    {
        var r = CouponService.CalculateDiscount(Percent(10, 50000, 100000), subtotal, DateTime.Now);
        Assert.True(r.Success);
        Assert.Equal(expected, r.Value);
    }

    [Fact]
    public void Coupon_Rejected_WhenBelowMinOrder_Expired_OrUsedUp()
    {
        var c = Percent(10, null, 100000);
        Assert.False(CouponService.CalculateDiscount(c, 50000, DateTime.Now).Success);
        Assert.False(CouponService.CalculateDiscount(c, 200000, DateTime.Now.AddDays(5)).Success);
        c.UsedCount = c.UsageLimit;
        Assert.False(CouponService.CalculateDiscount(c, 200000, DateTime.Now).Success);
    }

    [Fact]
    public void AmountCoupon_NeverExceedsSubtotal()
    {
        var c = new Coupon { Code = "Y", DiscountType = DiscountType.Amount, Value = 500000, MinOrder = 0,
            StartAt = DateTime.Now.AddDays(-1), EndAt = DateTime.Now.AddDays(1), UsageLimit = 1 };
        Assert.Equal(80000, CouponService.CalculateDiscount(c, 80000, DateTime.Now).Value);
    }
}

public class ValidationTests
{
    [Theory]
    [InlineData("978-0-306-40615-7", true)]
    [InlineData("978-0-306-40615-8", false)]  // sai chữ số kiểm tra
    [InlineData("9786040000019", true)]
    [InlineData("0306406152", true)]
    [InlineData("12345", false)]
    public void Isbn_IsValidated_WithCheckDigit(string input, bool expected) =>
        Assert.Equal(expected, Validation.IsIsbn(Validation.NormalizeIsbn(input)));

    [Theory]
    [InlineData("0912345678", true)]
    [InlineData("912345678", false)]
    [InlineData("09123456789", false)]
    public void Phone_MustBeVietnamese10Digits(string phone, bool expected) => Assert.Equal(expected, Validation.IsPhone(phone));

    [Fact]
    public void OrderStatusRules_MatchSpecification()
    {
        Assert.True(OrderStatusRules.CanChange(OrderStatus.Pending, OrderStatus.Confirmed));
        Assert.True(OrderStatusRules.CanChange(OrderStatus.Confirmed, OrderStatus.Cancelled));
        Assert.False(OrderStatusRules.CanChange(OrderStatus.Shipping, OrderStatus.Cancelled));
        Assert.False(OrderStatusRules.CanChange(OrderStatus.Completed, OrderStatus.Pending));
    }
}

public class SeederTests
{
    [Fact]
    public async Task SeedData_StockMatchesStockTransactionLog()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        await using var db = new BookStoreDbContext(new DbContextOptionsBuilder<BookStoreDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();

        await DbSeeder.SeedAsync(db);

        var books = await db.Books.Select(b => new { b.Id, b.Stock }).ToListAsync();
        var logs = await db.StockTransactions.GroupBy(x => x.BookId)
            .Select(g => new { g.Key, Sum = g.Sum(x => x.QuantityChange) }).ToDictionaryAsync(x => x.Key, x => x.Sum);
        Assert.NotEmpty(books);
        Assert.All(books, b => Assert.Equal(b.Stock, logs.GetValueOrDefault(b.Id)));
        Assert.True(await db.Orders.AnyAsync(o => o.Status == OrderStatus.Pending));
        Assert.True(await db.Orders.AnyAsync(o => o.Status == OrderStatus.Completed));
    }
}
