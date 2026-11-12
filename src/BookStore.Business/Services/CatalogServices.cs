using BookStore.Business.Common;
using BookStore.Data;
using BookStore.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Business.Services;

// =====================================================================
//  DTO dùng chung cho các danh mục đơn giản
// =====================================================================

public record CategoryItem(int Id, string Name, string? Description, bool IsHidden, int BookCount);
public record AuthorItem(int Id, string Name, string? Bio, int BookCount);
public record PublisherItem(int Id, string Name, string? Address, string? Phone, int BookCount);
public record SupplierItem(int Id, string Name, string? Phone, string? Email, string? Address, int ReceiptCount);
public record LookupItem(int Id, string Name);

// =====================================================================
//  DANH MỤC THỂ LOẠI
// =====================================================================

public class CategoryService
{
    private readonly BookStoreDbContext _db;
    public CategoryService(BookStoreDbContext db) => _db = db;

    public async Task<List<CategoryItem>> GetAllAsync(string? keyword = null, bool includeHidden = true)
    {
        keyword = Validation.Clean(keyword);
        return await _db.Categories.AsNoTracking()
            .Where(c => includeHidden || !c.IsHidden)
            .Where(c => keyword == "" || c.Name.Contains(keyword))
            .OrderBy(c => c.Name)
            .Select(c => new CategoryItem(c.Id, c.Name, c.Description, c.IsHidden, c.Books.Count(b => !b.IsHidden)))
            .ToListAsync();
    }

    public async Task<List<LookupItem>> GetLookupAsync() =>
        await _db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => new LookupItem(c.Id, c.Name)).ToListAsync();

    public async Task<Result> SaveAsync(UserSession actor, int id, string name, string? description)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        name = Validation.Clean(name);
        if (name.Length == 0) return Result.Fail("Tên thể loại không được để trống.");
        if (await _db.Categories.AnyAsync(c => c.Name == name && c.Id != id))
            return Result.Fail("Tên thể loại đã tồn tại.");

        var category = id == 0 ? new Category() : await _db.Categories.FindAsync(id);
        if (category == null) return Result.Fail("Thể loại không tồn tại.");
        category.Name = name;
        category.Description = Validation.CleanOrNull(description);
        if (id == 0) _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return Result.Ok(id == 0 ? "Thêm thể loại thành công." : "Cập nhật thể loại thành công.");
    }

    public async Task<Result> SetHiddenAsync(UserSession actor, int id, bool hidden)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        var category = await _db.Categories.FindAsync(id);
        if (category == null) return Result.Fail("Thể loại không tồn tại.");
        category.IsHidden = hidden;
        await _db.SaveChangesAsync();
        return Result.Ok(hidden ? "Đã ẩn thể loại khỏi trang Web." : "Đã hiển thị lại thể loại.");
    }

    public async Task<Result> DeleteAsync(UserSession actor, int id)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        var category = await _db.Categories.FindAsync(id);
        if (category == null) return Result.Fail("Thể loại không tồn tại.");
        if (await _db.Books.AnyAsync(b => b.CategoryId == id))
            return Result.Fail("Không thể xóa thể loại đang có sản phẩm, vui lòng chuyển sang chế độ Ẩn.");
        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return Result.Ok("Đã xóa thể loại.");
    }
}

// =====================================================================
//  TÁC GIẢ
// =====================================================================

public class AuthorService
{
    private readonly BookStoreDbContext _db;
    public AuthorService(BookStoreDbContext db) => _db = db;

    public async Task<List<AuthorItem>> GetAllAsync(string? keyword = null)
    {
        keyword = Validation.Clean(keyword);
        return await _db.Authors.AsNoTracking()
            .Where(a => keyword == "" || a.Name.Contains(keyword))
            .OrderBy(a => a.Name)
            .Select(a => new AuthorItem(a.Id, a.Name, a.Bio, a.BookAuthors.Count))
            .ToListAsync();
    }

    public async Task<List<LookupItem>> GetLookupAsync() =>
        await _db.Authors.AsNoTracking().OrderBy(a => a.Name).Select(a => new LookupItem(a.Id, a.Name)).ToListAsync();

    public async Task<Result> SaveAsync(UserSession actor, int id, string name, string? bio)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        name = Validation.Clean(name);
        if (name.Length == 0) return Result.Fail("Tên tác giả không được để trống.");

        var author = id == 0 ? new Author() : await _db.Authors.FindAsync(id);
        if (author == null) return Result.Fail("Tác giả không tồn tại.");
        author.Name = name;
        author.Bio = Validation.CleanOrNull(bio);
        if (id == 0) _db.Authors.Add(author);
        await _db.SaveChangesAsync();
        return Result.Ok(id == 0 ? "Thêm tác giả thành công." : "Cập nhật tác giả thành công.");
    }

    public async Task<Result> DeleteAsync(UserSession actor, int id)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        var author = await _db.Authors.FindAsync(id);
        if (author == null) return Result.Fail("Tác giả không tồn tại.");
        if (await _db.BookAuthors.AnyAsync(ba => ba.AuthorId == id))
            return Result.Fail("Không thể xóa tác giả đã gắn với sách. Hãy gỡ tác giả khỏi các sách trước.");
        _db.Authors.Remove(author);
        await _db.SaveChangesAsync();
        return Result.Ok("Đã xóa tác giả.");
    }
}

// =====================================================================
//  NHÀ XUẤT BẢN
// =====================================================================

public class PublisherService
{
    private readonly BookStoreDbContext _db;
    public PublisherService(BookStoreDbContext db) => _db = db;

    public async Task<List<PublisherItem>> GetAllAsync(string? keyword = null)
    {
        keyword = Validation.Clean(keyword);
        return await _db.Publishers.AsNoTracking()
            .Where(p => keyword == "" || p.Name.Contains(keyword))
            .OrderBy(p => p.Name)
            .Select(p => new PublisherItem(p.Id, p.Name, p.Address, p.Phone, p.Books.Count))
            .ToListAsync();
    }

    public async Task<List<LookupItem>> GetLookupAsync() =>
        await _db.Publishers.AsNoTracking().OrderBy(p => p.Name).Select(p => new LookupItem(p.Id, p.Name)).ToListAsync();

    public async Task<Result> SaveAsync(UserSession actor, int id, string name, string? address, string? phone)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        name = Validation.Clean(name);
        if (name.Length == 0) return Result.Fail("Tên nhà xuất bản không được để trống.");
        if (!string.IsNullOrWhiteSpace(phone) && !Validation.Clean(phone).All(char.IsDigit))
            return Result.Fail("Số điện thoại chỉ gồm chữ số.");
        if (await _db.Publishers.AnyAsync(p => p.Name == name && p.Id != id))
            return Result.Fail("Nhà xuất bản đã tồn tại.");

        var publisher = id == 0 ? new Publisher() : await _db.Publishers.FindAsync(id);
        if (publisher == null) return Result.Fail("Nhà xuất bản không tồn tại.");
        publisher.Name = name;
        publisher.Address = Validation.CleanOrNull(address);
        publisher.Phone = Validation.CleanOrNull(phone);
        if (id == 0) _db.Publishers.Add(publisher);
        await _db.SaveChangesAsync();
        return Result.Ok(id == 0 ? "Thêm nhà xuất bản thành công." : "Cập nhật nhà xuất bản thành công.");
    }

    public async Task<Result> DeleteAsync(UserSession actor, int id)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        var publisher = await _db.Publishers.FindAsync(id);
        if (publisher == null) return Result.Fail("Nhà xuất bản không tồn tại.");
        if (await _db.Books.AnyAsync(b => b.PublisherId == id))
            return Result.Fail("Không thể xóa nhà xuất bản đang có sách.");
        _db.Publishers.Remove(publisher);
        await _db.SaveChangesAsync();
        return Result.Ok("Đã xóa nhà xuất bản.");
    }
}

// =====================================================================
//  NHÀ CUNG CẤP
// =====================================================================

public class SupplierService
{
    private readonly BookStoreDbContext _db;
    public SupplierService(BookStoreDbContext db) => _db = db;

    public async Task<List<SupplierItem>> GetAllAsync(string? keyword = null)
    {
        keyword = Validation.Clean(keyword);
        var suppliers = await _db.Suppliers.AsNoTracking()
            .Where(s => keyword == "" || s.Name.Contains(keyword) || (s.Phone != null && s.Phone.Contains(keyword)))
            .OrderBy(s => s.Name).ToListAsync();
        var counts = await _db.ImportReceipts.GroupBy(r => r.SupplierId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        return suppliers.Select(s => new SupplierItem(s.Id, s.Name, s.Phone, s.Email, s.Address,
            counts.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<List<LookupItem>> GetLookupAsync() =>
        await _db.Suppliers.AsNoTracking().OrderBy(s => s.Name).Select(s => new LookupItem(s.Id, s.Name)).ToListAsync();

    public async Task<Result> SaveAsync(UserSession actor, int id, string name, string? phone, string? email, string? address)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        name = Validation.Clean(name);
        if (name.Length == 0) return Result.Fail("Tên nhà cung cấp không được để trống.");
        if (!string.IsNullOrWhiteSpace(email) && !Validation.IsEmail(email)) return Result.Fail("Email không hợp lệ.");
        if (!string.IsNullOrWhiteSpace(phone) && !Validation.Clean(phone).All(char.IsDigit))
            return Result.Fail("Số điện thoại chỉ gồm chữ số.");

        var supplier = id == 0 ? new Supplier() : await _db.Suppliers.FindAsync(id);
        if (supplier == null) return Result.Fail("Nhà cung cấp không tồn tại.");
        supplier.Name = name;
        supplier.Phone = Validation.CleanOrNull(phone);
        supplier.Email = Validation.CleanOrNull(email);
        supplier.Address = Validation.CleanOrNull(address);
        if (id == 0) _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        return Result.Ok(id == 0 ? "Thêm nhà cung cấp thành công." : "Cập nhật nhà cung cấp thành công.");
    }

    public async Task<Result> DeleteAsync(UserSession actor, int id)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier == null) return Result.Fail("Nhà cung cấp không tồn tại.");
        if (await _db.ImportReceipts.AnyAsync(r => r.SupplierId == id))
            return Result.Fail("Không thể xóa nhà cung cấp đã có phiếu nhập (cần giữ lịch sử chứng từ).");
        _db.Suppliers.Remove(supplier);
        await _db.SaveChangesAsync();
        return Result.Ok("Đã xóa nhà cung cấp.");
    }
}
