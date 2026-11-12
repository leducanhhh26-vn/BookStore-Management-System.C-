using BookStore.Business.Common;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.Admin.Infrastructure;

/// <summary>Người dùng đang đăng nhập trên ứng dụng Desktop (singleton).</summary>
public class AppSession
{
    private UserSession? _user;

    public UserSession User => _user ?? throw new InvalidOperationException("Chưa đăng nhập.");
    public bool IsLoggedIn => _user != null;
    public bool IsAdmin => _user?.IsAdmin == true;

    public void SignIn(UserSession user) => _user = user;
    public void Clear() => _user = null;
}

/// <summary>
/// Chạy một thao tác nghiệp vụ trong một scope DI riêng → mỗi thao tác có DbContext mới
/// (giống mỗi request trên Web), dữ liệu luôn được đọc mới nhất từ SQL Server.
/// </summary>
public class ServiceRunner
{
    private readonly IServiceScopeFactory _scopes;
    public ServiceRunner(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task<TResult> Run<TService, TResult>(Func<TService, Task<TResult>> action) where TService : notnull
    {
        using var scope = _scopes.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }
}
