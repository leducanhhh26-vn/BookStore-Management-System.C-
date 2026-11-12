using BookStore.Business.Common;
using BookStore.Data;
using BookStore.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Business.Services;

public enum BookSort
{
    Newest,
    BestSelling,
    PriceAsc,
    PriceDesc,
    TitleAsc
}

public class BookQuery
{
    /// <summary>Tìm theo tên sách, tác giả, nhà xuất bản hoặc ISBN (không phân biệt dấu).</summary>
    public string? Keyword { get; set; }
    public int? CategoryId { get; set; }
    public int? PublisherId { get; set; }
    public int? AuthorId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool InStockOnly { get; set; }
    public bool LowStockOnly { get; set; }
    /// <summary>true cho Desktop (thấy cả sách đã ẩn), false cho Web.</summary>
    public bool IncludeHidden { get; set; }
    public BookSort Sort { get; set; } = BookSort.Newest;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

public class BookListItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Isbn { get; set; } = "";
    public string Authors { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string PublisherName { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int LowStockThreshold { get; set; }
    public bool IsHidden { get; set; }
    public bool IsFeatured { get; set; }
    public double? AverageRating { get; set; }
    public int SoldQuantity { get; set; }
    public bool IsLowStock => Stock <= LowStockThreshold;
}

public record ReviewItem(int UserId, string UserName, int Rating, string? Comment, DateTime CreatedAt);

public class BookDetailDto : BookListItem
{
    public int CategoryId { get; set; }
    public int PublisherId { get; set; }
    public int PublishYear { get; set; }
    public int Pages { get; set; }
    public string CoverType { get; set; } = "";
    public string? Description { get; set; }
    public List<LookupItem> AuthorList { get; set; } = new();
    public List<ReviewItem> Reviews { get; set; } = new();
    public List<BookListItem> RelatedBooks { get; set; } = new();
}

public class BookEditDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Isbn { get; set; } = "";
    public int CategoryId { get; set; }
    public int PublisherId { get; set; }
    public List<int> AuthorIds { get; set; } = new();
    public int PublishYear { get; set; } = DateTime.Now.Year;
    public int Pages { get; set; }
    public string CoverType { get; set; } = "Bìa mềm";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int LowStockThreshold { get; set; } = 10;
    public bool IsFeatured { get; set; }
    public bool IsHidden { get; set; }
    /// <summary>Chỉ dùng khi thêm mới: số lượng tồn ban đầu (ghi sổ kho).</summary>
    public int InitialStock { get; set; }
    /// <summary>Chỉ đọc khi sửa: tồn kho thay đổi qua phân hệ Kho, không sửa trực tiếp.</summary>
    public int CurrentStock { get; set; }
    public byte[]? RowVersion { get; set; }
}

public class BookService
{
    public const int MaxImageBytes = 2 * 1024 * 1024;
    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp", "image/gif" };

    private readonly BookStoreDbContext _db;
    public BookService(BookStoreDbContext db) => _db = db;

    // ===================== Tìm kiếm / danh sách =====================

    public async Task<PagedResult<BookListItem>> SearchAsync(BookQuery q)
    {
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 500);

        var query = _db.Books.AsNoTracking().AsQueryable();
        if (!q.IncludeHidden)
            query = query.Where(b => !b.IsHidden && !b.Category!.IsHidden);

        var keyword = Validation.Clean(q.Keyword);
        if (keyword.Length > 0)
        {
            var isbn = Validation.NormalizeIsbn(keyword);
            if (_db.Database.IsSqlServer())
            {
                // So khớp không phân biệt dấu: "mat biec" tìm được "Mắt Biếc"
                const string ai = "Vietnamese_CI_AI";
                query = query.Where(b =>
                    EF.Functions.Collate(b.Title, ai).Contains(keyword)
                    || b.BookAuthors.Any(ba => EF.Functions.Collate(ba.Author!.Name, ai).Contains(keyword))
                    || EF.Functions.Collate(b.Publisher!.Name, ai).Contains(keyword)
                    || (isbn.Length >= 6 && b.Isbn.Contains(isbn)));
            }
            else
            {
                query = query.Where(b => b.Title.Contains(keyword)
                    || b.BookAuthors.Any(ba => ba.Author!.Name.Contains(keyword))
                    || b.Publisher!.Name.Contains(keyword)
                    || (isbn.Length >= 6 && b.Isbn.Contains(isbn)));
            }
        }

        if (q.CategoryId is > 0) query = query.Where(b => b.CategoryId == q.CategoryId);
        if (q.PublisherId is > 0) query = query.Where(b => b.PublisherId == q.PublisherId);
        if (q.AuthorId is > 0) query = query.Where(b => b.BookAuthors.Any(ba => ba.AuthorId == q.AuthorId));
        if (q.MinPrice is > 0) query = query.Where(b => b.Price >= q.MinPrice);
        if (q.MaxPrice is > 0) query = query.Where(b => b.Price <= q.MaxPrice);
        if (q.InStockOnly) query = query.Where(b => b.Stock > 0);
        if (q.LowStockOnly) query = query.Where(b => b.Stock <= b.LowStockThreshold);

        query = q.Sort switch
        {
            BookSort.PriceAsc => query.OrderBy(b => b.Price).ThenBy(b => b.Id),
            BookSort.PriceDesc => query.OrderByDescending(b => b.Price).ThenBy(b => b.Id),
            BookSort.TitleAsc => query.OrderBy(b => b.Title),
            BookSort.BestSelling => query.OrderByDescending(b => _db.OrderDetails
                    .Where(d => d.BookId == b.Id && d.Order!.Status != OrderStatus.Cancelled).Sum(d => (int?)d.Quantity) ?? 0)
                .ThenBy(b => b.Id),
            _ => query.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id)
        };

        var total = await query.CountAsync();
        var items = await Project(query.Skip((page - 1) * pageSize).Take(pageSize));
        return new PagedResult<BookListItem> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    /// <summary>Chuyển truy vấn Book thành BookListItem (chạy trên SQL, chỉ lấy cột cần thiết).</summary>
    private async Task<List<BookListItem>> Project(IQueryable<Book> query)
    {
        var rows = await query.Select(b => new
        {
            b.Id, b.Title, b.Isbn, b.Price, b.Stock, b.LowStockThreshold, b.IsHidden, b.IsFeatured,
            Category = b.Category!.Name,
            Publisher = b.Publisher!.Name,
            Authors = b.BookAuthors.Select(ba => ba.Author!.Name).ToList(),
            Rating = b.Reviews.Average(r => (double?)r.Rating),
            Sold = _db.OrderDetails.Where(d => d.BookId == b.Id && d.Order!.Status != OrderStatus.Cancelled)
                .Sum(d => (int?)d.Quantity) ?? 0
        }).ToListAsync();

        return rows.Select(r => new BookListItem
        {
            Id = r.Id, Title = r.Title, Isbn = r.Isbn, Price = r.Price, Stock = r.Stock,
            LowStockThreshold = r.LowStockThreshold, IsHidden = r.IsHidden, IsFeatured = r.IsFeatured,
            CategoryName = r.Category, PublisherName = r.Publisher, Authors = string.Join(", ", r.Authors),
            AverageRating = r.Rating is null ? null : Math.Round(r.Rating.Value, 1), SoldQuantity = r.Sold
        }).ToList();
    }

    // ===================== Trang chủ =====================

    public Task<List<BookListItem>> GetFeaturedAsync(int take) =>
        Project(VisibleBooks().Where(b => b.IsFeatured).OrderByDescending(b => b.CreatedAt).Take(take));

    public Task<List<BookListItem>> GetNewestAsync(int take) =>
        Project(VisibleBooks().OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id).Take(take));

    public async Task<List<BookListItem>> GetBestSellersAsync(int take) =>
        (await SearchAsync(new BookQuery { Sort = BookSort.BestSelling, PageSize = take })).Items.ToList();

    private IQueryable<Book> VisibleBooks() =>
        _db.Books.AsNoTracking().Where(b => !b.IsHidden && !b.Category!.IsHidden);

    // ===================== Chi tiết =====================

    public async Task<BookDetailDto?> GetDetailAsync(int id, bool includeHidden = false)
    {
        var book = await _db.Books.AsNoTracking()
            .Include(b => b.Category).Include(b => b.Publisher)
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (book == null) return null;
        if (!includeHidden && (book.IsHidden || book.Category!.IsHidden)) return null;

        var reviews = await _db.Reviews.AsNoTracking().Where(r => r.BookId == id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewItem(r.UserId, r.User!.FullName, r.Rating, r.Comment, r.CreatedAt))
            .ToListAsync();

        var related = await Project(VisibleBooks()
            .Where(b => b.CategoryId == book.CategoryId && b.Id != id)
            .OrderByDescending(b => b.CreatedAt).Take(4));

        var sold = await _db.OrderDetails.Where(d => d.BookId == id && d.Order!.Status != OrderStatus.Cancelled)
            .SumAsync(d => (int?)d.Quantity) ?? 0;

        return new BookDetailDto
        {
            Id = book.Id, Title = book.Title, Isbn = book.Isbn, Price = book.Price, Stock = book.Stock,
            LowStockThreshold = book.LowStockThreshold, IsHidden = book.IsHidden, IsFeatured = book.IsFeatured,
            CategoryId = book.CategoryId, CategoryName = book.Category!.Name,
            PublisherId = book.PublisherId, PublisherName = book.Publisher!.Name,
            PublishYear = book.PublishYear, Pages = book.Pages, CoverType = book.CoverType, Description = book.Description,
            AuthorList = book.BookAuthors.Select(ba => new LookupItem(ba.AuthorId, ba.Author!.Name)).ToList(),
            Authors = string.Join(", ", book.BookAuthors.Select(ba => ba.Author!.Name)),
            Reviews = reviews,
            AverageRating = reviews.Count == 0 ? null : Math.Round(reviews.Average(r => r.Rating), 1),
            SoldQuantity = sold,
            RelatedBooks = related
        };
    }

    // ===================== Thêm / sửa / xóa (Desktop) =====================

    public async Task<BookEditDto?> GetForEditAsync(int id) =>
        await _db.Books.AsNoTracking().Where(b => b.Id == id).Select(b => new BookEditDto
        {
            Id = b.Id, Title = b.Title, Isbn = b.Isbn, CategoryId = b.CategoryId, PublisherId = b.PublisherId,
            AuthorIds = b.BookAuthors.Select(ba => ba.AuthorId).ToList(),
            PublishYear = b.PublishYear, Pages = b.Pages, CoverType = b.CoverType, Description = b.Description,
            Price = b.Price, LowStockThreshold = b.LowStockThreshold, IsFeatured = b.IsFeatured, IsHidden = b.IsHidden,
            CurrentStock = b.Stock, RowVersion = b.RowVersion
        }).FirstOrDefaultAsync();

    private async Task<string?> ValidateAsync(BookEditDto dto, string isbn)
    {
        if (Validation.Clean(dto.Title).Length == 0) return "Vui lòng nhập tên sách.";
        if (!Validation.IsIsbn(isbn)) return "Mã ISBN không hợp lệ (cần 10 hoặc 13 chữ số, đúng chữ số kiểm tra).";
        if (dto.Price <= 0) return "Giá bán phải lớn hơn 0.";
        if (dto.Pages <= 0) return "Số trang phải là số dương.";
        if (dto.PublishYear < 1900 || dto.PublishYear > DateTime.Now.Year + 1) return "Năm xuất bản không hợp lệ.";
        if (dto.LowStockThreshold < 0) return "Ngưỡng cảnh báo không được âm.";
        if (dto.AuthorIds.Count == 0) return "Sách phải có ít nhất một tác giả.";
        if (!await _db.Categories.AnyAsync(c => c.Id == dto.CategoryId)) return "Vui lòng chọn thể loại.";
        if (!await _db.Publishers.AnyAsync(p => p.Id == dto.PublisherId)) return "Vui lòng chọn nhà xuất bản.";
        if (await _db.Books.AnyAsync(b => b.Isbn == isbn && b.Id != dto.Id)) return "Mã ISBN đã tồn tại trong hệ thống.";
        return null;
    }

    public async Task<Result<int>> CreateAsync(UserSession actor, BookEditDto dto)
    {
        if (!Permission.IsInternal(actor)) return Result<int>.Fail(Permission.Denied);
        var isbn = Validation.NormalizeIsbn(dto.Isbn);
        dto.Id = 0;
        var error = await ValidateAsync(dto, isbn);
        if (error != null) return Result<int>.Fail(error);
        if (dto.InitialStock < 0) return Result<int>.Fail("Tồn kho ban đầu không được âm.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        var book = new Book { Isbn = isbn, Stock = dto.InitialStock, CreatedAt = DateTime.Now };
        Apply(book, dto);
        foreach (var authorId in dto.AuthorIds.Distinct()) book.BookAuthors.Add(new BookAuthor { AuthorId = authorId });
        _db.Books.Add(book);
        await _db.SaveChangesAsync();

        if (dto.InitialStock > 0)
        {
            _db.StockTransactions.Add(new StockTransaction
            {
                BookId = book.Id, StaffId = actor.Id, Type = StockTransactionType.Adjust,
                QuantityChange = dto.InitialStock, StockAfter = dto.InitialStock, Reason = "Tồn kho ban đầu khi tạo đầu sách"
            });
            await _db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return Result<int>.Ok(book.Id, "Thêm đầu sách mới thành công.");
    }

    public async Task<Result> UpdateAsync(UserSession actor, BookEditDto dto)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        var isbn = Validation.NormalizeIsbn(dto.Isbn);
        var error = await ValidateAsync(dto, isbn);
        if (error != null) return Result.Fail(error);

        var book = await _db.Books.Include(b => b.BookAuthors).FirstOrDefaultAsync(b => b.Id == dto.Id);
        if (book == null) return Result.Fail("Sản phẩm không tồn tại.");

        // Gắn phiên bản người dùng đã mở để phát hiện người khác sửa trong lúc đó
        if (dto.RowVersion != null) _db.Entry(book).Property(b => b.RowVersion).OriginalValue = dto.RowVersion;

        book.Isbn = isbn;
        Apply(book, dto);
        var newIds = dto.AuthorIds.Distinct().ToHashSet();
        foreach (var ba in book.BookAuthors.Where(ba => !newIds.Contains(ba.AuthorId)).ToList()) book.BookAuthors.Remove(ba);
        foreach (var id in newIds.Where(id => book.BookAuthors.All(ba => ba.AuthorId != id)))
            book.BookAuthors.Add(new BookAuthor { AuthorId = id });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail("Sách này vừa được người khác cập nhật. Vui lòng tải lại dữ liệu rồi thực hiện lại.");
        }
        return Result.Ok("Cập nhật thông tin sách thành công.");
    }

    private static void Apply(Book book, BookEditDto dto)
    {
        book.Title = Validation.Clean(dto.Title);
        book.CategoryId = dto.CategoryId;
        book.PublisherId = dto.PublisherId;
        book.PublishYear = dto.PublishYear;
        book.Pages = dto.Pages;
        book.CoverType = string.IsNullOrWhiteSpace(dto.CoverType) ? "Bìa mềm" : dto.CoverType.Trim();
        book.Description = Validation.CleanOrNull(dto.Description);
        book.Price = dto.Price;
        book.LowStockThreshold = dto.LowStockThreshold;
        book.IsFeatured = dto.IsFeatured;
        book.IsHidden = dto.IsHidden;
    }

    public async Task<Result> SetHiddenAsync(UserSession actor, int id, bool hidden)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        var book = await _db.Books.FindAsync(id);
        if (book == null) return Result.Fail("Sản phẩm không tồn tại.");
        book.IsHidden = hidden;
        await _db.SaveChangesAsync();
        return Result.Ok(hidden ? "Đã ẩn sách khỏi trang Web." : "Sách đã được hiển thị trên trang Web.");
    }

    /// <summary>
    /// Xóa sách. Nếu sách đã phát sinh giao dịch (đơn hàng, phiếu nhập, sổ kho) thì chỉ ẩn để bảo toàn lịch sử.
    /// </summary>
    public async Task<Result> DeleteAsync(UserSession actor, int id)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        var book = await _db.Books.FindAsync(id);
        if (book == null) return Result.Fail("Sản phẩm không tồn tại.");

        bool hasHistory = await _db.OrderDetails.AnyAsync(d => d.BookId == id)
                          || await _db.ImportReceiptDetails.AnyAsync(d => d.BookId == id)
                          || await _db.StockTransactions.AnyAsync(t => t.BookId == id);
        if (hasHistory)
        {
            book.IsHidden = true;
            await _db.SaveChangesAsync();
            return Result.Ok("Sách đã có giao dịch nên không thể xóa vĩnh viễn. Hệ thống đã chuyển sách sang trạng thái Ẩn.");
        }

        _db.Books.Remove(book);
        await _db.SaveChangesAsync();
        return Result.Ok("Đã xóa sách.");
    }

    // ===================== Ảnh bìa =====================

    public async Task<(byte[] Data, string ContentType)?> GetImageAsync(int bookId)
    {
        var img = await _db.BookImages.AsNoTracking().FirstOrDefaultAsync(i => i.BookId == bookId);
        return img == null ? null : (img.Data, img.ContentType);
    }

    public async Task<HashSet<int>> GetBookIdsWithImageAsync() =>
        (await _db.BookImages.Select(i => i.BookId).ToListAsync()).ToHashSet();

    public async Task<Result> SetImageAsync(UserSession actor, int bookId, byte[] data, string contentType)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        if (data.Length == 0 || data.Length > MaxImageBytes) return Result.Fail("File ảnh không hợp lệ (tối đa 2 MB).");
        contentType = contentType.ToLowerInvariant();
        if (!AllowedImageTypes.Contains(contentType)) return Result.Fail("File ảnh không hợp lệ (chỉ nhận JPG, PNG, WEBP, GIF).");
        if (!await _db.Books.AnyAsync(b => b.Id == bookId)) return Result.Fail("Sản phẩm không tồn tại.");

        var img = await _db.BookImages.FindAsync(bookId);
        if (img == null) _db.BookImages.Add(new BookImage { BookId = bookId, Data = data, ContentType = contentType });
        else
        {
            img.Data = data;
            img.ContentType = contentType;
        }
        await _db.SaveChangesAsync();
        return Result.Ok("Đã cập nhật ảnh bìa.");
    }

    public async Task<Result> RemoveImageAsync(UserSession actor, int bookId)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        var img = await _db.BookImages.FindAsync(bookId);
        if (img != null)
        {
            _db.BookImages.Remove(img);
            await _db.SaveChangesAsync();
        }
        return Result.Ok("Đã xóa ảnh bìa.");
    }
}
