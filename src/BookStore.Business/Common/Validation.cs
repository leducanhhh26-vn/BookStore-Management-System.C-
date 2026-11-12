using System.Text.RegularExpressions;

namespace BookStore.Business.Common;

/// <summary>Các hàm kiểm tra dữ liệu dùng chung cho Web và Desktop.</summary>
public static partial class Validation
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    // Số điện thoại Việt Nam: 10 chữ số, bắt đầu bằng 0
    [GeneratedRegex(@"^0\d{9}$")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9_.]{4,50}$")]
    private static partial Regex UsernameRegex();

    public static bool IsEmail(string? s) => !string.IsNullOrWhiteSpace(s) && EmailRegex().IsMatch(s.Trim());
    public static bool IsPhone(string? s) => !string.IsNullOrWhiteSpace(s) && PhoneRegex().IsMatch(s.Trim());
    public static bool IsUsername(string? s) => !string.IsNullOrWhiteSpace(s) && UsernameRegex().IsMatch(s.Trim());

    /// <summary>Mật khẩu tối thiểu 6 ký tự, có cả chữ và số.</summary>
    public static string? CheckPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 6)
            return "Mật khẩu phải có ít nhất 6 ký tự.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "Mật khẩu phải có cả chữ và số.";
        return null;
    }

    /// <summary>Chuẩn hóa ISBN (bỏ dấu gạch, khoảng trắng) và kiểm tra ISBN-10 hoặc ISBN-13.</summary>
    public static string NormalizeIsbn(string? isbn) =>
        new string((isbn ?? "").Where(c => char.IsDigit(c) || c == 'X' || c == 'x').ToArray()).ToUpperInvariant();

    public static bool IsIsbn(string normalized)
    {
        if (normalized.Length == 13 && normalized.All(char.IsDigit))
        {
            int sum = 0;
            for (int i = 0; i < 12; i++) sum += (normalized[i] - '0') * (i % 2 == 0 ? 1 : 3);
            return (10 - sum % 10) % 10 == normalized[12] - '0';
        }
        if (normalized.Length == 10 && normalized[..9].All(char.IsDigit))
        {
            int sum = 0;
            for (int i = 0; i < 9; i++) sum += (normalized[i] - '0') * (10 - i);
            int last = normalized[9] == 'X' ? 10 : normalized[9] - '0';
            return (sum + last) % 11 == 0;
        }
        return false;
    }

    public static string Clean(string? s) => (s ?? "").Trim();
    public static string? CleanOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>Chính sách phí vận chuyển – một nơi duy nhất để Web hiển thị và Service tính tiền.</summary>
public static class ShippingPolicy
{
    public const decimal StandardFee = 25000;
    public const decimal FreeShippingThreshold = 300000;

    public static decimal FeeFor(decimal subtotalAfterDiscount) =>
        subtotalAfterDiscount >= FreeShippingThreshold ? 0 : StandardFee;
}
