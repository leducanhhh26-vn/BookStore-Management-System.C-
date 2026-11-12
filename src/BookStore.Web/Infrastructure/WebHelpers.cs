using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using BookStore.Business.Common;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Infrastructure;

public static class Format
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>110000 → "110.000 ₫"</summary>
    public static string Money(decimal value) => value.ToString("#,##0", Vi) + " ₫";
    public static string Date(DateTime value) => value.ToString("dd/MM/yyyy HH:mm");
}

public static class UserExtensions
{
    public static int? GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

/// <summary>Thông báo dạng toast hiển thị sau khi chuyển trang (lưu qua TempData).</summary>
public static class ToastExtensions
{
    public const string Key = "Toast";

    public static void Toast(this Controller c, Result r) => c.Toast(r.Message, r.Success);

    public static void Toast(this Controller c, string message, bool success = true) =>
        c.TempData[Key] = JsonSerializer.Serialize(new ToastMessage(message, success));
}

public record ToastMessage(string Message, bool Success);

/// <summary>Giỏ hàng của khách chưa đăng nhập, lưu trong Session dạng {BookId: Quantity}.</summary>
public static class GuestCart
{
    private const string Key = "GuestCart";

    public static Dictionary<int, int> Get(ISession session)
    {
        var json = session.GetString(Key);
        return string.IsNullOrEmpty(json) ? new() : JsonSerializer.Deserialize<Dictionary<int, int>>(json) ?? new();
    }

    public static void Save(ISession session, Dictionary<int, int> cart) =>
        session.SetString(Key, JsonSerializer.Serialize(cart));

    public static void Clear(ISession session) => session.Remove(Key);

    public static IEnumerable<(int BookId, int Quantity)> Items(ISession session) =>
        Get(session).Select(kv => (kv.Key, kv.Value));
}
