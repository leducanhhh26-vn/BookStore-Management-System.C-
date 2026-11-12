using BookStore.Domain;

namespace BookStore.Business.Common;

/// <summary>
/// Kết quả của một thao tác nghiệp vụ. Service trả về Result thay vì ném lỗi,
/// để Controller (Web) và Form (Desktop) chỉ việc hiển thị thông báo.
/// </summary>
public class Result
{
    public bool Success { get; protected init; }
    public string Message { get; protected init; } = "";

    public static Result Ok(string message = "Thao tác thành công.") => new() { Success = true, Message = message };
    public static Result Fail(string message) => new() { Success = false, Message = message };
}

public class Result<T> : Result
{
    public T? Value { get; private init; }

    public static Result<T> Ok(T value, string message = "Thao tác thành công.") =>
        new() { Success = true, Value = value, Message = message };

    public static new Result<T> Fail(string message) => new() { Success = false, Message = message };
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

/// <summary>Thông tin người dùng đang đăng nhập, truyền vào service để kiểm tra quyền.</summary>
public record UserSession(int Id, string Username, string FullName, UserRole Role)
{
    public bool IsAdmin => Role == UserRole.Admin;
    public bool IsStaffOrAdmin => Role is UserRole.Admin or UserRole.Staff;
}

/// <summary>Kiểm tra quyền ở tầng nghiệp vụ (không chỉ dựa vào việc ẩn nút trên giao diện).</summary>
public static class Permission
{
    public const string Denied = "Bạn không có quyền thực hiện chức năng này.";

    public static bool IsInternal(UserSession? actor) => actor is { IsStaffOrAdmin: true };
    public static bool IsAdmin(UserSession? actor) => actor is { IsAdmin: true };
}
