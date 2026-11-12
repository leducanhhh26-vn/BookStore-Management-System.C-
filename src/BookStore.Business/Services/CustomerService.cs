using BookStore.Business.Common;
using BookStore.Data;
using BookStore.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Business.Services;

public record CustomerListItem(int Id, string Username, string FullName, string Email, string Phone,
    bool IsActive, DateTime CreatedAt, int OrderCount, decimal TotalSpent);

public class ProfileDto
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class AddressDto
{
    public int Id { get; set; }
    public string ReceiverName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string AddressLine { get; set; } = "";
    public string Ward { get; set; } = "";
    public string District { get; set; } = "";
    public string Province { get; set; } = "";
    public bool IsDefault { get; set; }

    public string FullAddress => string.Join(", ",
        new[] { AddressLine, Ward, District, Province }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public class CustomerDetailDto
{
    public ProfileDto Profile { get; set; } = new();
    public List<AddressDto> Addresses { get; set; } = new();
    public List<OrderListItem> Orders { get; set; } = new();
    public decimal TotalSpent { get; set; }
}

/// <summary>
/// Quản lý khách hàng: phía Desktop (tra cứu, xem lịch sử, khóa/mở) và phía Web (hồ sơ, sổ địa chỉ).
/// </summary>
public class CustomerService
{
    private readonly BookStoreDbContext _db;
    private readonly AuthService _auth;

    public CustomerService(BookStoreDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    // ===================== Desktop =====================

    public async Task<List<CustomerListItem>> SearchAsync(string? keyword)
    {
        keyword = Validation.Clean(keyword);
        return await _db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Customer)
            .Where(u => keyword == "" || u.FullName.Contains(keyword) || u.Email.Contains(keyword)
                        || u.Phone.Contains(keyword) || u.Username.Contains(keyword))
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new CustomerListItem(u.Id, u.Username, u.FullName, u.Email, u.Phone, u.IsActive, u.CreatedAt,
                u.Orders.Count,
                u.Orders.Where(o => o.Status == OrderStatus.Completed)
                    .Sum(o => (decimal?)(o.Subtotal - o.DiscountAmount + o.ShippingFee)) ?? 0))
            .ToListAsync();
    }

    public async Task<CustomerDetailDto?> GetDetailAsync(int customerId)
    {
        var profile = await GetProfileAsync(customerId);
        if (profile == null) return null;

        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.OrderDate)
            .Select(OrderService.ToListItem)
            .ToListAsync();

        return new CustomerDetailDto
        {
            Profile = profile,
            Addresses = await GetAddressesAsync(customerId),
            Orders = orders,
            TotalSpent = orders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.Total)
        };
    }

    public async Task<Result> SetActiveAsync(UserSession actor, int customerId, bool isActive)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == customerId && u.Role == UserRole.Customer);
        if (user == null) return Result.Fail("Khách hàng không tồn tại.");
        user.IsActive = isActive;
        await _db.SaveChangesAsync();
        return Result.Ok(isActive ? "Đã mở khóa tài khoản khách hàng." : "Đã khóa tài khoản khách hàng.");
    }

    /// <summary>Nhân viên cập nhật thông tin liên hệ của khách (khi khách gọi điện yêu cầu).</summary>
    public async Task<Result> UpdateByStaffAsync(UserSession actor, ProfileDto dto)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        return await UpdateProfileAsync(dto.Id, dto);
    }

    // ===================== Web: hồ sơ cá nhân =====================

    public async Task<ProfileDto?> GetProfileAsync(int userId) =>
        await _db.Users.AsNoTracking().Where(u => u.Id == userId && u.Role == UserRole.Customer)
            .Select(u => new ProfileDto
            {
                Id = u.Id, Username = u.Username, FullName = u.FullName, Email = u.Email, Phone = u.Phone,
                CreatedAt = u.CreatedAt, IsActive = u.IsActive
            }).FirstOrDefaultAsync();

    public async Task<Result> UpdateProfileAsync(int userId, ProfileDto dto)
    {
        var fullName = Validation.Clean(dto.FullName);
        var email = Validation.Clean(dto.Email).ToLowerInvariant();
        var phone = Validation.Clean(dto.Phone);
        if (fullName.Length == 0) return Result.Fail("Vui lòng nhập họ tên.");
        if (!Validation.IsEmail(email)) return Result.Fail("Email không hợp lệ.");
        if (!Validation.IsPhone(phone)) return Result.Fail("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Role == UserRole.Customer);
        if (user == null) return Result.Fail("Tài khoản không tồn tại.");

        var duplicate = await _auth.CheckDuplicateAsync(user.Username, email, phone, userId);
        if (duplicate != null) return Result.Fail(duplicate);

        user.FullName = fullName;
        user.Email = email;
        user.Phone = phone;
        await _db.SaveChangesAsync();
        return Result.Ok("Cập nhật thông tin thành công.");
    }

    // ===================== Web: sổ địa chỉ =====================

    public async Task<List<AddressDto>> GetAddressesAsync(int userId) =>
        await _db.CustomerAddresses.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.Id)
            .Select(a => new AddressDto
            {
                Id = a.Id, ReceiverName = a.ReceiverName, Phone = a.Phone, AddressLine = a.AddressLine,
                Ward = a.Ward, District = a.District, Province = a.Province, IsDefault = a.IsDefault
            }).ToListAsync();

    public async Task<AddressDto?> GetAddressAsync(int userId, int addressId) =>
        (await GetAddressesAsync(userId)).FirstOrDefault(a => a.Id == addressId);

    public async Task<Result> SaveAddressAsync(int userId, AddressDto dto)
    {
        if (Validation.Clean(dto.ReceiverName).Length == 0) return Result.Fail("Vui lòng nhập tên người nhận.");
        if (!Validation.IsPhone(dto.Phone)) return Result.Fail("Số điện thoại người nhận không hợp lệ.");
        if (Validation.Clean(dto.AddressLine).Length == 0 || Validation.Clean(dto.Province).Length == 0)
            return Result.Fail("Vui lòng nhập địa chỉ và Tỉnh/Thành phố.");

        CustomerAddress? address;
        if (dto.Id == 0)
        {
            address = new CustomerAddress { UserId = userId };
            _db.CustomerAddresses.Add(address);
        }
        else
        {
            address = await _db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == dto.Id && a.UserId == userId);
            if (address == null) return Result.Fail("Địa chỉ không tồn tại.");
        }

        address.ReceiverName = Validation.Clean(dto.ReceiverName);
        address.Phone = Validation.Clean(dto.Phone);
        address.AddressLine = Validation.Clean(dto.AddressLine);
        address.Ward = Validation.Clean(dto.Ward);
        address.District = Validation.Clean(dto.District);
        address.Province = Validation.Clean(dto.Province);

        // Địa chỉ đầu tiên luôn là mặc định; chỉ có một địa chỉ mặc định
        bool hasOther = await _db.CustomerAddresses.AnyAsync(a => a.UserId == userId && a.Id != dto.Id);
        address.IsDefault = dto.IsDefault || !hasOther;
        if (address.IsDefault)
        {
            var others = await _db.CustomerAddresses.Where(a => a.UserId == userId && a.Id != dto.Id && a.IsDefault).ToListAsync();
            others.ForEach(a => a.IsDefault = false);
        }

        await _db.SaveChangesAsync();
        return Result.Ok("Đã lưu địa chỉ.");
    }

    public async Task<Result> DeleteAddressAsync(int userId, int addressId)
    {
        var address = await _db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId);
        if (address == null) return Result.Fail("Địa chỉ không tồn tại.");
        _db.CustomerAddresses.Remove(address);
        await _db.SaveChangesAsync();

        if (address.IsDefault)
        {
            var first = await _db.CustomerAddresses.Where(a => a.UserId == userId).OrderBy(a => a.Id).FirstOrDefaultAsync();
            if (first != null)
            {
                first.IsDefault = true;
                await _db.SaveChangesAsync();
            }
        }
        return Result.Ok("Đã xóa địa chỉ.");
    }
}
