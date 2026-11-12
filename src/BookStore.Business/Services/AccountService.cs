using BookStore.Business.Common;
using BookStore.Data;
using BookStore.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Business.Services;

public record StaffListItem(int Id, string Username, string FullName, string Email, string Phone,
    UserRole Role, bool IsActive, DateTime CreatedAt);

public class StaffEditDto
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Staff;
    /// <summary>Chỉ dùng khi tạo mới.</summary>
    public string? Password { get; set; }
}

/// <summary>Quản lý tài khoản nội bộ (Admin/Nhân viên). Mọi thao tác yêu cầu quyền Admin.</summary>
public class AccountService
{
    private readonly BookStoreDbContext _db;
    private readonly AuthService _auth;

    public AccountService(BookStoreDbContext db, AuthService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<List<StaffListItem>> GetStaffAsync(string? keyword)
    {
        keyword = Validation.Clean(keyword);
        return await _db.Users.AsNoTracking()
            .Where(u => u.Role != UserRole.Customer)
            .Where(u => keyword == "" || u.FullName.Contains(keyword) || u.Username.Contains(keyword)
                        || u.Email.Contains(keyword) || u.Phone.Contains(keyword))
            .OrderBy(u => u.Role).ThenBy(u => u.FullName)
            .Select(u => new StaffListItem(u.Id, u.Username, u.FullName, u.Email, u.Phone, u.Role, u.IsActive, u.CreatedAt))
            .ToListAsync();
    }

    public async Task<StaffEditDto?> GetAsync(int id) =>
        await _db.Users.AsNoTracking().Where(u => u.Id == id && u.Role != UserRole.Customer)
            .Select(u => new StaffEditDto
            {
                Id = u.Id, Username = u.Username, FullName = u.FullName, Email = u.Email, Phone = u.Phone, Role = u.Role
            }).FirstOrDefaultAsync();

    public async Task<Result> SaveAsync(UserSession actor, StaffEditDto dto)
    {
        if (!Permission.IsAdmin(actor)) return Result.Fail(Permission.Denied);

        var username = Validation.Clean(dto.Username);
        var fullName = Validation.Clean(dto.FullName);
        var email = Validation.Clean(dto.Email).ToLowerInvariant();
        var phone = Validation.Clean(dto.Phone);

        if (fullName.Length == 0) return Result.Fail("Vui lòng nhập họ tên.");
        if (!Validation.IsUsername(username)) return Result.Fail("Tên đăng nhập 4–50 ký tự, chỉ gồm chữ không dấu, số, dấu chấm hoặc gạch dưới.");
        if (!Validation.IsEmail(email)) return Result.Fail("Email không hợp lệ.");
        if (!Validation.IsPhone(phone)) return Result.Fail("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.");
        if (dto.Role == UserRole.Customer) return Result.Fail("Vai trò phải là Quản trị viên hoặc Nhân viên.");

        var duplicate = await _auth.CheckDuplicateAsync(username, email, phone, dto.Id == 0 ? null : dto.Id);
        if (duplicate != null) return Result.Fail(duplicate);

        if (dto.Id == 0)
        {
            var pwError = Validation.CheckPassword(dto.Password);
            if (pwError != null) return Result.Fail(pwError);
            _db.Users.Add(new User
            {
                Username = username, FullName = fullName, Email = email, Phone = phone, Role = dto.Role,
                PasswordHash = AuthService.HashPassword(dto.Password!), IsActive = true
            });
            await _db.SaveChangesAsync();
            return Result.Ok("Tạo tài khoản nhân viên thành công.");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == dto.Id && u.Role != UserRole.Customer);
        if (user == null) return Result.Fail("Tài khoản không tồn tại.");
        if (user.Id == actor.Id && dto.Role != UserRole.Admin)
            return Result.Fail("Không thể tự hạ quyền Quản trị viên của tài khoản đang đăng nhập.");

        user.Username = username;
        user.FullName = fullName;
        user.Email = email;
        user.Phone = phone;
        user.Role = dto.Role;
        await _db.SaveChangesAsync();
        return Result.Ok("Cập nhật tài khoản thành công.");
    }

    public async Task<Result> SetActiveAsync(UserSession actor, int userId, bool isActive)
    {
        if (!Permission.IsAdmin(actor)) return Result.Fail(Permission.Denied);
        if (userId == actor.Id && !isActive) return Result.Fail("Không thể khóa tài khoản đang sử dụng.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Role != UserRole.Customer);
        if (user == null) return Result.Fail("Tài khoản không tồn tại.");
        user.IsActive = isActive;
        await _db.SaveChangesAsync();
        return Result.Ok(isActive ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản.");
    }

    public async Task<Result> ResetPasswordAsync(UserSession actor, int userId, string newPassword)
    {
        if (!Permission.IsAdmin(actor)) return Result.Fail(Permission.Denied);
        var pwError = Validation.CheckPassword(newPassword);
        if (pwError != null) return Result.Fail(pwError);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Role != UserRole.Customer);
        if (user == null) return Result.Fail("Tài khoản không tồn tại.");
        user.PasswordHash = AuthService.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return Result.Ok("Đặt lại mật khẩu thành công.");
    }
}
