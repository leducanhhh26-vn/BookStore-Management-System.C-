using BookStore.Business.Common;
using BookStore.Data;
using BookStore.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Business.Services;

public record StockItem(int BookId, string Isbn, string Title, string CategoryName, decimal Price, int Stock,
    int LowStockThreshold, bool IsHidden)
{
    public bool IsLowStock => Stock <= LowStockThreshold;
    public decimal StockValue => Stock * Price;
}

public record ImportLine(int BookId, int Quantity, decimal UnitCost);

public class ImportReceiptDto
{
    public int SupplierId { get; set; }
    public string? Note { get; set; }
    public List<ImportLine> Lines { get; set; } = new();
}

public record ImportReceiptItem(int Id, DateTime ImportDate, string SupplierName, string StaffName,
    int TotalQuantity, decimal TotalCost, string? Note);

public record ImportReceiptLineItem(int BookId, string Title, int Quantity, decimal UnitCost)
{
    public decimal LineTotal => Quantity * UnitCost;
}

public record StockTransactionItem(int Id, DateTime CreatedAt, int BookId, string BookTitle,
    StockTransactionType Type, int QuantityChange, int StockAfter, int? RefId, string? StaffName, string? Reason)
{
    public string TypeText => Type.ToVietnamese();
}

/// <summary>Quản lý kho: theo dõi tồn, lập phiếu nhập, điều chỉnh kiểm kê, nhật ký nhập/xuất, cảnh báo sắp hết.</summary>
public class InventoryService
{
    private readonly BookStoreDbContext _db;
    public InventoryService(BookStoreDbContext db) => _db = db;

    public async Task<List<StockItem>> GetStockAsync(string? keyword = null, int? categoryId = null, bool lowStockOnly = false)
    {
        keyword = Validation.Clean(keyword);
        var query = _db.Books.AsNoTracking().AsQueryable();
        if (keyword.Length > 0) query = query.Where(b => b.Title.Contains(keyword) || b.Isbn.Contains(keyword));
        if (categoryId is > 0) query = query.Where(b => b.CategoryId == categoryId);
        if (lowStockOnly) query = query.Where(b => b.Stock <= b.LowStockThreshold);
        return await query.OrderBy(b => b.Stock).ThenBy(b => b.Title)
            .Select(b => new StockItem(b.Id, b.Isbn, b.Title, b.Category!.Name, b.Price, b.Stock, b.LowStockThreshold, b.IsHidden))
            .ToListAsync();
    }

    public Task<List<StockItem>> GetLowStockAsync() => GetStockAsync(lowStockOnly: true);

    public Task<int> CountLowStockAsync() => _db.Books.CountAsync(b => !b.IsHidden && b.Stock <= b.LowStockThreshold);

    // ===================== Phiếu nhập =====================

    public async Task<Result<int>> CreateImportReceiptAsync(UserSession actor, ImportReceiptDto dto)
    {
        if (!Permission.IsInternal(actor)) return Result<int>.Fail(Permission.Denied);
        if (!await _db.Suppliers.AnyAsync(s => s.Id == dto.SupplierId)) return Result<int>.Fail("Vui lòng chọn nhà cung cấp.");
        if (dto.Lines.Count == 0) return Result<int>.Fail("Phiếu nhập phải có ít nhất một đầu sách.");
        if (dto.Lines.Any(l => l.Quantity <= 0 || l.UnitCost <= 0))
            return Result<int>.Fail("Số lượng nhập và đơn giá nhập phải lớn hơn 0.");
        if (dto.Lines.Select(l => l.BookId).Distinct().Count() != dto.Lines.Count)
            return Result<int>.Fail("Một đầu sách chỉ được xuất hiện một lần trong phiếu nhập.");
        var ids = dto.Lines.Select(l => l.BookId).ToList();
        if (await _db.Books.CountAsync(b => ids.Contains(b.Id)) != ids.Count)
            return Result<int>.Fail("Có đầu sách không tồn tại.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        var receipt = new ImportReceipt
        {
            SupplierId = dto.SupplierId, StaffId = actor.Id, ImportDate = DateTime.Now, Note = Validation.CleanOrNull(dto.Note)
        };
        foreach (var l in dto.Lines)
            receipt.Details.Add(new ImportReceiptDetail { BookId = l.BookId, Quantity = l.Quantity, UnitCost = l.UnitCost });
        _db.ImportReceipts.Add(receipt);
        await _db.SaveChangesAsync();

        foreach (var l in dto.Lines)
        {
            int qty = l.Quantity;
            await _db.Books.Where(b => b.Id == l.BookId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Stock, b => b.Stock + qty));
            var stock = await _db.Books.Where(b => b.Id == l.BookId).Select(b => b.Stock).FirstAsync();
            _db.StockTransactions.Add(new StockTransaction
            {
                BookId = l.BookId, StaffId = actor.Id, Type = StockTransactionType.Import, QuantityChange = qty,
                StockAfter = stock, RefId = receipt.Id, Reason = $"Phiếu nhập #{receipt.Id}"
            });
        }
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Result<int>.Ok(receipt.Id, $"Đã lập phiếu nhập #{receipt.Id} và cập nhật tồn kho.");
    }

    public async Task<List<ImportReceiptItem>> GetReceiptsAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = _db.ImportReceipts.AsNoTracking().AsQueryable();
        if (from.HasValue) query = query.Where(r => r.ImportDate >= from.Value.Date);
        if (to.HasValue)
        {
            var end = to.Value.Date.AddDays(1);
            query = query.Where(r => r.ImportDate < end);
        }
        return await query.OrderByDescending(r => r.ImportDate)
            .Select(r => new ImportReceiptItem(r.Id, r.ImportDate, r.Supplier!.Name, r.Staff!.FullName,
                r.Details.Sum(d => d.Quantity), r.Details.Sum(d => d.Quantity * d.UnitCost), r.Note))
            .ToListAsync();
    }

    public async Task<List<ImportReceiptLineItem>> GetReceiptLinesAsync(int receiptId) =>
        await _db.ImportReceiptDetails.AsNoTracking().Where(d => d.ReceiptId == receiptId)
            .Select(d => new ImportReceiptLineItem(d.BookId, d.Book!.Title, d.Quantity, d.UnitCost))
            .ToListAsync();

    // ===================== Điều chỉnh kiểm kê =====================

    public async Task<Result> AdjustStockAsync(UserSession actor, int bookId, int newStock, string reason)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        reason = Validation.Clean(reason);
        if (newStock < 0) return Result.Fail("Số lượng tồn kho không thể nhỏ hơn 0.");
        if (reason.Length == 0) return Result.Fail("Vui lòng nhập lý do điều chỉnh.");

        var oldStock = await _db.Books.Where(b => b.Id == bookId).Select(b => (int?)b.Stock).FirstOrDefaultAsync();
        if (oldStock == null) return Result.Fail("Sản phẩm không tồn tại.");
        if (oldStock == newStock) return Result.Fail("Số lượng mới bằng số lượng hiện tại, không có gì thay đổi.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        // Chỉ cập nhật nếu tồn chưa bị thay đổi bởi giao dịch khác (vd: khách vừa đặt hàng)
        int affected = await _db.Books.Where(b => b.Id == bookId && b.Stock == oldStock)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Stock, newStock));
        if (affected == 0) return Result.Fail("Tồn kho vừa thay đổi do có giao dịch khác. Vui lòng tải lại và thực hiện lại.");

        _db.StockTransactions.Add(new StockTransaction
        {
            BookId = bookId, StaffId = actor.Id, Type = StockTransactionType.Adjust,
            QuantityChange = newStock - oldStock.Value, StockAfter = newStock, Reason = reason
        });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Result.Ok($"Đã điều chỉnh tồn kho từ {oldStock} thành {newStock}.");
    }

    // ===================== Nhật ký nhập/xuất =====================

    public async Task<List<StockTransactionItem>> GetTransactionsAsync(int? bookId = null, StockTransactionType? type = null,
        DateTime? from = null, DateTime? to = null)
    {
        var query = _db.StockTransactions.AsNoTracking().AsQueryable();
        if (bookId is > 0) query = query.Where(t => t.BookId == bookId);
        if (type.HasValue) query = query.Where(t => t.Type == type);
        if (from.HasValue) query = query.Where(t => t.CreatedAt >= from.Value.Date);
        if (to.HasValue)
        {
            var end = to.Value.Date.AddDays(1);
            query = query.Where(t => t.CreatedAt < end);
        }
        return await query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).Take(2000)
            .Select(t => new StockTransactionItem(t.Id, t.CreatedAt, t.BookId, t.Book!.Title, t.Type, t.QuantityChange,
                t.StockAfter, t.RefId, t.Staff == null ? null : t.Staff.FullName, t.Reason))
            .ToListAsync();
    }
}
