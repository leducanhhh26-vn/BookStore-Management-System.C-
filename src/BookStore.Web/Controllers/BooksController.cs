using System.Net;
using System.Text;
using BookStore.Business.Services;
using BookStore.Web.Infrastructure;
using BookStore.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

public class BooksController : Controller
{
    private readonly BookService _books;
    private readonly CategoryService _categories;
    private readonly PublisherService _publishers;
    private readonly ReviewService _reviews;
    private readonly CartService _cart;

    public BooksController(BookService books, CategoryService categories, PublisherService publishers,
        ReviewService reviews, CartService cart)
    {
        _books = books;
        _categories = categories;
        _publishers = publishers;
        _reviews = reviews;
        _cart = cart;
    }

    /// <summary>Danh sách sách: duyệt theo thể loại, tìm kiếm, lọc giá/NXB, sắp xếp, phân trang.</summary>
    public async Task<IActionResult> Index(string? q, int? categoryId, int? publisherId, int? authorId,
        decimal? minPrice, decimal? maxPrice, bool inStock = false, BookSort sort = BookSort.Newest, int page = 1)
    {
        var result = await _books.SearchAsync(new BookQuery
        {
            Keyword = q, CategoryId = categoryId, PublisherId = publisherId, AuthorId = authorId,
            MinPrice = minPrice, MaxPrice = maxPrice, InStockOnly = inStock, Sort = sort, Page = page, PageSize = 12
        });

        var categories = await _categories.GetAllAsync(includeHidden: false);
        var heading = !string.IsNullOrWhiteSpace(q) ? $"Kết quả tìm kiếm cho \"{q}\""
            : categories.FirstOrDefault(c => c.Id == categoryId)?.Name ?? "Tất cả sách";

        return View(new BookListViewModel
        {
            Result = result, Categories = categories, Publishers = await _publishers.GetLookupAsync(),
            Q = q, CategoryId = categoryId, PublisherId = publisherId, AuthorId = authorId,
            MinPrice = minPrice, MaxPrice = maxPrice, InStock = inStock, Sort = sort, Heading = heading
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var book = await _books.GetDetailAsync(id);
        if (book == null)
        {
            this.Toast("Sản phẩm không còn tồn tại.", false);
            return RedirectToAction("Index", "Home");
        }

        var vm = new BookDetailViewModel { Book = book };
        var userId = User.GetUserId();
        if (userId != null)
        {
            vm.CanReview = await _reviews.CanReviewAsync(userId.Value, id);
            vm.MyReview = await _reviews.GetMyReviewAsync(userId.Value, id);
            vm.InCart = (await _cart.GetCartAsync(userId.Value)).Lines.FirstOrDefault(l => l.BookId == id)?.Quantity ?? 0;
        }
        else
        {
            vm.InCart = GuestCart.Get(HttpContext.Session).GetValueOrDefault(id);
        }
        return View(vm);
    }

    /// <summary>
    /// Ảnh bìa lấy từ bảng BookImages (do Desktop tải lên). Chưa có ảnh thì sinh ảnh bìa SVG tạm từ tên sách.
    /// </summary>
    [ResponseCache(Duration = 60)]
    public async Task<IActionResult> Cover(int id)
    {
        var image = await _books.GetImageAsync(id);
        if (image != null) return File(image.Value.Data, image.Value.ContentType);

        var book = await _books.GetDetailAsync(id, includeHidden: true);
        return Content(PlaceholderSvg(book?.Title ?? "BookStore", book?.Authors ?? "", id), "image/svg+xml", Encoding.UTF8);
    }

    private static string PlaceholderSvg(string title, string author, int seed)
    {
        string[][] palettes =
        {
            new[] { "#1e3a5f", "#3d6a99" }, new[] { "#7a2e3a", "#b5525f" }, new[] { "#2f5d50", "#4e9a83" },
            new[] { "#5b3f7a", "#8a68b0" }, new[] { "#8a5a1c", "#c98d3a" }, new[] { "#33475b", "#5f7f9c" }
        };
        var p = palettes[seed % palettes.Length];

        // Ngắt dòng tên sách khoảng 14 ký tự/dòng, tối đa 5 dòng
        var lines = new List<string>();
        var current = "";
        foreach (var word in title.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if ((current + " " + word).Trim().Length > 14 && current.Length > 0)
            {
                lines.Add(current);
                current = word;
            }
            else current = (current + " " + word).Trim();
        }
        if (current.Length > 0) lines.Add(current);
        lines = lines.Take(5).ToList();

        var sb = new StringBuilder();
        sb.Append($"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 300 440" width="300" height="440">""");
        sb.Append($"""<defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="{p[0]}"/><stop offset="1" stop-color="{p[1]}"/></linearGradient></defs>""");
        sb.Append("""<rect width="300" height="440" fill="url(#g)"/>""");
        sb.Append("""<rect x="0" y="0" width="14" height="440" fill="#000" opacity=".18"/>""");
        sb.Append("""<rect x="34" y="40" width="232" height="2" fill="#fff" opacity=".6"/>""");
        int y = 110;
        foreach (var line in lines)
        {
            sb.Append($"""<text x="150" y="{y}" text-anchor="middle" font-family="Georgia,serif" font-size="26" font-weight="bold" fill="#fff">{WebUtility.HtmlEncode(line)}</text>""");
            y += 36;
        }
        sb.Append("""<rect x="34" y="360" width="232" height="2" fill="#fff" opacity=".6"/>""");
        sb.Append($"""<text x="150" y="392" text-anchor="middle" font-family="Arial,sans-serif" font-size="15" fill="#fff" opacity=".9">{WebUtility.HtmlEncode(author.Length > 30 ? author[..30] + "…" : author)}</text>""");
        sb.Append("</svg>");
        return sb.ToString();
    }
}
