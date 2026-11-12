using BookStore.Business.Common;
using BookStore.Data;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Business.Services;

public class CartLine
{
    public int BookId { get; set; }
    public string Title { get; set; } = "";
    public string Authors { get; set; } = "";
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int Stock { get; set; }
    public bool IsAvailable { get; set; }
    public decimal LineTotal => Price * Quantity;
    /// <summary>Cảnh báo hiển thị cho dòng này (hết hàng, vượt tồn...).</summary>
    public string? Warning { get; set; }
}

public class CartDto
{
    public List<CartLine> Lines { get; set; } = new();
    public decimal Subtotal => Lines.Where(l => l.IsAvailable).Sum(l => l.LineTotal);
    public int TotalQuantity => Lines.Sum(l => l.Quantity);
    public bool CanCheckout => Lines.Count > 0 && Lines.All(l => l.IsAvailable && l.Quantity <= l.Stock);
}

/// <summary>
/// Giỏ hàng. Khách đã đăng nhập: lưu ở bảng CartItems (giữ giỏ giữa các lần đăng nhập).
/// Khách vãng lai: Web giữ danh sách (BookId, Quantity) trong Session, gọi BuildCartAsync để hiển thị,
/// và gộp vào DB bằng MergeAsync khi khách đăng nhập.
/// </summary>
public class CartService
{
    public const int MaxQuantityPerBook = 99;
    private readonly BookStoreDbContext _db;

    public CartService(BookStoreDbContext db) => _db = db;

    public async Task<CartDto> GetCartAsync(int userId)
    {
        var items = await _db.CartItems.AsNoTracking().Where(c => c.UserId == userId)
            .OrderBy(c => c.AddedAt).Select(c => new { c.BookId, c.Quantity }).ToListAsync();
        return await BuildCartAsync(items.Select(i => (i.BookId, i.Quantity)));
    }

    public async Task<int> CountAsync(int userId) =>
        await _db.CartItems.Where(c => c.UserId == userId).SumAsync(c => (int?)c.Quantity) ?? 0;

    /// <summary>Dựng giỏ hàng từ danh sách (BookId, Quantity) với giá và tồn kho hiện tại trong CSDL.</summary>
    public async Task<CartDto> BuildCartAsync(IEnumerable<(int BookId, int Quantity)> items)
    {
        var list = items.ToList();
        var ids = list.Select(i => i.BookId).ToList();
        var books = await _db.Books.AsNoTracking().Where(b => ids.Contains(b.Id))
            .Select(b => new
            {
                b.Id, b.Title, b.Price, b.Stock, Hidden = b.IsHidden || b.Category!.IsHidden,
                Authors = b.BookAuthors.Select(a => a.Author!.Name).ToList()
            })
            .ToDictionaryAsync(b => b.Id);

        var cart = new CartDto();
        foreach (var (bookId, qty) in list)
        {
            if (!books.TryGetValue(bookId, out var b)) continue;
            var line = new CartLine
            {
                BookId = b.Id, Title = b.Title, Price = b.Price, Quantity = qty, Stock = b.Stock,
                Authors = string.Join(", ", b.Authors), IsAvailable = !b.Hidden && b.Stock > 0
            };
            if (b.Hidden) line.Warning = "Sản phẩm đã ngừng kinh doanh.";
            else if (b.Stock == 0) line.Warning = "Sản phẩm đã hết hàng.";
            else if (qty > b.Stock) line.Warning = $"Chỉ còn {b.Stock} cuốn trong kho.";
            cart.Lines.Add(line);
        }
        return cart;
    }

    public async Task<Result> AddAsync(int userId, int bookId, int quantity)
    {
        if (quantity <= 0) return Result.Fail("Số lượng phải lớn hơn 0.");
        var book = await _db.Books.AsNoTracking().Where(b => b.Id == bookId)
            .Select(b => new { b.Stock, Hidden = b.IsHidden || b.Category!.IsHidden }).FirstOrDefaultAsync();
        if (book == null || book.Hidden) return Result.Fail("Sản phẩm không còn tồn tại.");

        var item = await _db.CartItems.FindAsync(userId, bookId);
        int newQty = (item?.Quantity ?? 0) + quantity;
        if (newQty > book.Stock)
            return Result.Fail(book.Stock == 0 ? "Sản phẩm đã hết hàng." : $"Số lượng vượt quá tồn kho hiện có (còn {book.Stock} cuốn).");
        if (newQty > MaxQuantityPerBook) return Result.Fail($"Mỗi sản phẩm tối đa {MaxQuantityPerBook} cuốn.");

        if (item == null) _db.CartItems.Add(new Domain.CartItem { UserId = userId, BookId = bookId, Quantity = newQty });
        else item.Quantity = newQty;
        await _db.SaveChangesAsync();
        return Result.Ok("Đã thêm sách vào giỏ hàng.");
    }

    /// <summary>Cập nhật số lượng. Số lượng ≤ 0 thì xóa dòng; vượt tồn thì tự điều chỉnh về tối đa.</summary>
    public async Task<Result> UpdateAsync(int userId, int bookId, int quantity)
    {
        var item = await _db.CartItems.FindAsync(userId, bookId);
        if (item == null) return Result.Fail("Sản phẩm không có trong giỏ.");
        if (quantity <= 0)
        {
            _db.CartItems.Remove(item);
            await _db.SaveChangesAsync();
            return Result.Ok("Đã xóa sản phẩm khỏi giỏ hàng.");
        }

        var stock = await _db.Books.Where(b => b.Id == bookId).Select(b => b.Stock).FirstAsync();
        var message = "Đã cập nhật giỏ hàng.";
        if (quantity > stock)
        {
            quantity = Math.Max(stock, 1);
            message = $"Số lượng vượt quá tồn kho hiện có, đã điều chỉnh về {quantity}.";
        }
        item.Quantity = Math.Min(quantity, MaxQuantityPerBook);
        await _db.SaveChangesAsync();
        return Result.Ok(message);
    }

    public async Task<Result> RemoveAsync(int userId, int bookId)
    {
        var item = await _db.CartItems.FindAsync(userId, bookId);
        if (item != null)
        {
            _db.CartItems.Remove(item);
            await _db.SaveChangesAsync();
        }
        return Result.Ok("Đã xóa sản phẩm khỏi giỏ hàng.");
    }

    /// <summary>Gộp giỏ hàng của khách vãng lai (trong Session) vào giỏ của tài khoản khi đăng nhập.</summary>
    public async Task MergeAsync(int userId, IEnumerable<(int BookId, int Quantity)> guestItems)
    {
        foreach (var (bookId, qty) in guestItems)
        {
            var stock = await _db.Books.Where(b => b.Id == bookId && !b.IsHidden).Select(b => (int?)b.Stock).FirstOrDefaultAsync();
            if (stock is null or 0) continue;
            var item = await _db.CartItems.FindAsync(userId, bookId);
            int merged = Math.Min((item?.Quantity ?? 0) + qty, Math.Min(stock.Value, MaxQuantityPerBook));
            if (item == null) _db.CartItems.Add(new Domain.CartItem { UserId = userId, BookId = bookId, Quantity = merged });
            else item.Quantity = merged;
        }
        await _db.SaveChangesAsync();
    }
}
