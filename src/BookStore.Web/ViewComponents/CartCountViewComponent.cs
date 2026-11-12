using BookStore.Business.Services;
using BookStore.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.ViewComponents;

/// <summary>Số lượng sách trong giỏ, hiển thị trên thanh menu.</summary>
public class CartCountViewComponent : ViewComponent
{
    private readonly CartService _cart;
    public CartCountViewComponent(CartService cart) => _cart = cart;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = UserClaimsPrincipal.GetUserId();
        int count = userId != null
            ? await _cart.CountAsync(userId.Value)
            : GuestCart.Get(HttpContext.Session).Values.Sum();
        return Content(count > 0 ? count.ToString() : "");
    }
}

/// <summary>Danh sách thể loại cho menu.</summary>
public class CategoryMenuViewComponent : ViewComponent
{
    private readonly CategoryService _categories;
    public CategoryMenuViewComponent(CategoryService categories) => _categories = categories;

    public async Task<IViewComponentResult> InvokeAsync() =>
        View(await _categories.GetAllAsync(includeHidden: false));
}
