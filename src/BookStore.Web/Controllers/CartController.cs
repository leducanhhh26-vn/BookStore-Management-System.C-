using BookStore.Business.Common;
using BookStore.Business.Services;
using BookStore.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

/// <summary>
/// Giỏ hàng: khách đã đăng nhập dùng bảng CartItems; khách vãng lai dùng Session.
/// Mọi kiểm tra tồn kho đều do CartService thực hiện.
/// </summary>
public class CartController : Controller
{
    private readonly CartService _cart;
    private readonly BookService _books;

    public CartController(CartService cart, BookService books)
    {
        _cart = cart;
        _books = books;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.GetUserId();
        var cart = userId != null
            ? await _cart.GetCartAsync(userId.Value)
            : await _cart.BuildCartAsync(GuestCart.Items(HttpContext.Session));
        return View(cart);
    }

    [HttpPost]
    public async Task<IActionResult> Add(int bookId, int quantity = 1, bool buyNow = false, string? returnUrl = null)
    {
        Result result;
        var userId = User.GetUserId();
        if (userId != null)
        {
            result = await _cart.AddAsync(userId.Value, bookId, quantity);
        }
        else
        {
            result = await AddToGuestCartAsync(bookId, quantity);
        }

        this.Toast(result);
        if (result.Success && buyNow) return RedirectToAction(nameof(Index));
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    private async Task<Result> AddToGuestCartAsync(int bookId, int quantity)
    {
        if (quantity <= 0) return Result.Fail("Số lượng phải lớn hơn 0.");
        var book = await _books.GetDetailAsync(bookId);
        if (book == null) return Result.Fail("Sản phẩm không còn tồn tại.");

        var cart = GuestCart.Get(HttpContext.Session);
        var newQty = cart.GetValueOrDefault(bookId) + quantity;
        if (newQty > book.Stock)
            return Result.Fail(book.Stock == 0 ? "Sản phẩm đã hết hàng." : $"Số lượng vượt quá tồn kho hiện có (còn {book.Stock} cuốn).");
        cart[bookId] = Math.Min(newQty, CartService.MaxQuantityPerBook);
        GuestCart.Save(HttpContext.Session, cart);
        return Result.Ok("Đã thêm sách vào giỏ hàng.");
    }

    [HttpPost]
    public async Task<IActionResult> Update(int bookId, int quantity)
    {
        var userId = User.GetUserId();
        if (userId != null)
        {
            this.Toast(await _cart.UpdateAsync(userId.Value, bookId, quantity));
        }
        else
        {
            var cart = GuestCart.Get(HttpContext.Session);
            if (quantity <= 0) cart.Remove(bookId);
            else
            {
                var stock = (await _books.GetDetailAsync(bookId))?.Stock ?? 0;
                cart[bookId] = Math.Clamp(quantity, 1, Math.Max(1, Math.Min(stock, CartService.MaxQuantityPerBook)));
                if (quantity > stock) this.Toast($"Số lượng vượt quá tồn kho hiện có, đã điều chỉnh về {cart[bookId]}.", false);
            }
            GuestCart.Save(HttpContext.Session, cart);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Remove(int bookId)
    {
        var userId = User.GetUserId();
        if (userId != null) await _cart.RemoveAsync(userId.Value, bookId);
        else
        {
            var cart = GuestCart.Get(HttpContext.Session);
            cart.Remove(bookId);
            GuestCart.Save(HttpContext.Session, cart);
        }
        this.Toast("Đã xóa sản phẩm khỏi giỏ hàng.");
        return RedirectToAction(nameof(Index));
    }
}
