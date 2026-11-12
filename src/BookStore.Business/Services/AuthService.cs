using BookStore.Business.Common;
using BookStore.Data;
using BookStore.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Business.Services;

public enum LoginChannel
{
    Web,      // Chỉ khách hàng
    Desktop   // Chỉ quản trị viên và nhân viên
}

public class RegisterDto
{
    public string FullName { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Password { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}

/// <summary>Đăng nhập, đăng ký, đổi mật khẩu. Mật khẩu được băm bằng BCrypt.</summary>
public class AuthService
{
    private readonly BookStoreDbContext _db;

    public AuthService(BookStoreDbContext db) => _db = db;

    public static string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);
    public static bool VerifyPassword(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);

    public async Task<Result<UserSession>> LoginAsync(string login, string password, LoginChannel channel)
    {
        login = Validation.Clean(login);
        if (login.Length == 0 || string.IsNullOrEmpty(password))
            return Result<UserSession>.Fail("Vui lòng nhập tên đăng nhập và mật khẩu.");

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == login || u.Email == login);

        // Thông báo chung cho cả 2 trường hợp sai tài khoản / sai mật khẩu để tránh dò tài khoản
        if (user == null || !VerifyPassword(password, user.PasswordHash))
            return Result<UserSession>.Fail("Tên đăng nhập hoặc mật khẩu không đúng.");

        if (!user.IsActive)
            return Result<UserSession>.Fail("Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");

        bool allowed = channel == LoginChannel.Web
            ? user.Role == UserRole.Customer
            : user.Role is UserRole.Admin or UserRole.Staff;
        if (!allowed)
            return Result<UserSession>.Fail(channel == LoginChannel.Web
                ? "Tài khoản nội bộ vui lòng đăng nhập trên ứng dụng quản trị."
                : "Tài khoản khách hàng không được phép truy cập ứng dụng quản trị.");

        return Result<UserSession>.Ok(new UserSession(user.Id, user.Username, user.FullName, user.Role), "Đăng nhập thành công.");
    }

    public async Task<Result<UserSession>> RegisterCustomerAsync(RegisterDto dto)
    {
        var fullName = Validation.Clean(dto.FullName);
        var username = Validation.Clean(dto.Username);
        var email = Validation.Clean(dto.Email).ToLowerInvariant();
        var phone = Validation.Clean(dto.Phone);

        if (fullName.Length == 0 || username.Length == 0 || email.Length == 0 || phone.Length == 0)
            return Result<UserSession>.Fail("Vui lòng điền đầy đủ các trường thông tin.");
        if (!Validation.IsUsername(username))
            return Result<UserSession>.Fail("Tên đăng nhập 4–50 ký tự, chỉ gồm chữ không dấu, số, dấu chấm hoặc gạch dưới.");
        if (!Validation.IsEmail(email))
            return Result<UserSession>.Fail("Định dạng Email không hợp lệ.");
        if (!Validation.IsPhone(phone))
            return Result<UserSession>.Fail("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.");
        var pwError = Validation.CheckPassword(dto.Password);
        if (pwError != null) return Result<UserSession>.Fail(pwError);
        if (dto.Password != dto.ConfirmPassword)
            return Result<UserSession>.Fail("Mật khẩu nhập lại không trùng khớp.");

        var duplicate = await CheckDuplicateAsync(username, email, phone, excludeUserId: null);
        if (duplicate != null) return Result<UserSession>.Fail(duplicate);

        var user = new User
        {
            FullName = fullName, Username = username, Email = email, Phone = phone,
            PasswordHash = HashPassword(dto.Password), Role = UserRole.Customer, IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Result<UserSession>.Ok(new UserSession(user.Id, user.Username, user.FullName, user.Role), "Đăng ký tài khoản thành công.");
    }

    public async Task<Result> ChangePasswordAsync(int userId, string currentPassword, string newPassword, string confirmPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Result.Fail("Tài khoản không tồn tại.");
        if (!VerifyPassword(currentPassword ?? "", user.PasswordHash)) return Result.Fail("Mật khẩu hiện tại không đúng.");
        var pwError = Validation.CheckPassword(newPassword);
        if (pwError != null) return Result.Fail(pwError);
        if (newPassword != confirmPassword) return Result.Fail("Mật khẩu nhập lại không trùng khớp.");

        user.PasswordHash = HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return Result.Ok("Đổi mật khẩu thành công.");
    }

    /// <summary>Trả về thông báo lỗi nếu Username / Email / Số điện thoại đã được dùng, ngược lại trả null.</summary>
    internal async Task<string?> CheckDuplicateAsync(string username, string email, string phone, int? excludeUserId)
    {
        var others = _db.Users.Where(u => excludeUserId == null || u.Id != excludeUserId);
        if (await others.AnyAsync(u => u.Username == username)) return "Tên đăng nhập đã tồn tại.";
        if (await others.AnyAsync(u => u.Email == email)) return "Email này đã được đăng ký tài khoản trên hệ thống.";
        if (await others.AnyAsync(u => u.Phone == phone)) return "Số điện thoại này đã được đăng ký.";
        return null;
    }
}
