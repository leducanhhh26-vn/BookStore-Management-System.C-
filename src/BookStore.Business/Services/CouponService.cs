using BookStore.Business.Common;
using BookStore.Data;
using BookStore.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Business.Services;

public class CouponDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public DiscountType DiscountType { get; set; } = DiscountType.Percent;
    public decimal Value { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal MinOrder { get; set; }
    public DateTime StartAt { get; set; } = DateTime.Today;
    public DateTime EndAt { get; set; } = DateTime.Today.AddMonths(1);
    public int UsageLimit { get; set; } = 100;
    public int UsedCount { get; set; }
    public bool IsActive { get; set; } = true;

    public string Description => DiscountType == DiscountType.Percent
        ? $"Giảm {Value:0.##}%" + (MaxDiscount is > 0 ? $" (tối đa {MaxDiscount:N0}đ)" : "") + $", đơn từ {MinOrder:N0}đ"
        : $"Giảm {Value:N0}đ, đơn từ {MinOrder:N0}đ";
}

public class CouponService
{
    private readonly BookStoreDbContext _db;
    public CouponService(BookStoreDbContext db) => _db = db;

    /// <summary>
    /// Tính số tiền giảm của một mã cho tiền sách. Hàm thuần (không truy cập CSDL) để dễ kiểm thử.
    /// </summary>
    public static Result<decimal> CalculateDiscount(Coupon coupon, decimal subtotal, DateTime now)
    {
        if (!coupon.IsActive || now < coupon.StartAt || now > coupon.EndAt)
            return Result<decimal>.Fail("Mã giảm giá không hợp lệ hoặc đã hết hạn.");
        if (coupon.UsedCount >= coupon.UsageLimit)
            return Result<decimal>.Fail("Mã giảm giá đã hết lượt sử dụng.");
        if (subtotal < coupon.MinOrder)
            return Result<decimal>.Fail($"Mã chỉ áp dụng cho đơn hàng từ {coupon.MinOrder:N0}đ.");

        decimal discount = coupon.DiscountType == DiscountType.Percent
            ? Math.Round(subtotal * coupon.Value / 100, 0)
            : coupon.Value;
        if (coupon.MaxDiscount is > 0) discount = Math.Min(discount, coupon.MaxDiscount.Value);
        discount = Math.Min(discount, subtotal); // không giảm quá tiền sách
        return Result<decimal>.Ok(discount, $"Áp dụng mã {coupon.Code} thành công, giảm {discount:N0}đ.");
    }

    /// <summary>Xem trước số tiền giảm trên trang thanh toán (chưa trừ lượt dùng).</summary>
    public async Task<Result<decimal>> PreviewAsync(string? code, decimal subtotal)
    {
        code = Validation.Clean(code).ToUpperInvariant();
        if (code.Length == 0) return Result<decimal>.Fail("Vui lòng nhập mã giảm giá.");
        var coupon = await _db.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code);
        if (coupon == null) return Result<decimal>.Fail("Mã giảm giá không hợp lệ hoặc đã hết hạn.");
        return CalculateDiscount(coupon, subtotal, DateTime.Now);
    }

    // ===================== Quản lý mã (Desktop – Admin) =====================

    public async Task<List<CouponDto>> GetAllAsync() =>
        await _db.Coupons.AsNoTracking().OrderByDescending(c => c.EndAt).Select(c => new CouponDto
        {
            Id = c.Id, Code = c.Code, DiscountType = c.DiscountType, Value = c.Value, MaxDiscount = c.MaxDiscount,
            MinOrder = c.MinOrder, StartAt = c.StartAt, EndAt = c.EndAt, UsageLimit = c.UsageLimit,
            UsedCount = c.UsedCount, IsActive = c.IsActive
        }).ToListAsync();

    public async Task<Result> SaveAsync(UserSession actor, CouponDto dto)
    {
        if (!Permission.IsAdmin(actor)) return Result.Fail(Permission.Denied);
        var code = Validation.Clean(dto.Code).ToUpperInvariant();
        if (code.Length < 3 || code.Any(char.IsWhiteSpace)) return Result.Fail("Mã phải có ít nhất 3 ký tự và không chứa khoảng trắng.");
        if (dto.Value <= 0) return Result.Fail("Giá trị giảm phải lớn hơn 0.");
        if (dto.DiscountType == DiscountType.Percent && dto.Value > 100) return Result.Fail("Giảm theo phần trăm không được vượt quá 100%.");
        if (dto.MinOrder < 0) return Result.Fail("Giá trị đơn tối thiểu không được âm.");
        if (dto.EndAt <= dto.StartAt) return Result.Fail("Ngày kết thúc phải sau ngày bắt đầu.");
        if (dto.UsageLimit <= 0) return Result.Fail("Giới hạn lượt dùng phải lớn hơn 0.");
        if (await _db.Coupons.AnyAsync(c => c.Code == code && c.Id != dto.Id)) return Result.Fail("Mã giảm giá đã tồn tại.");

        var coupon = dto.Id == 0 ? new Coupon() : await _db.Coupons.FindAsync(dto.Id);
        if (coupon == null) return Result.Fail("Mã giảm giá không tồn tại.");
        if (dto.UsageLimit < coupon.UsedCount) return Result.Fail($"Mã đã được dùng {coupon.UsedCount} lần, giới hạn không được nhỏ hơn số này.");

        coupon.Code = code;
        coupon.DiscountType = dto.DiscountType;
        coupon.Value = dto.Value;
        coupon.MaxDiscount = dto.DiscountType == DiscountType.Percent && dto.MaxDiscount is > 0 ? dto.MaxDiscount : null;
        coupon.MinOrder = dto.MinOrder;
        coupon.StartAt = dto.StartAt;
        coupon.EndAt = dto.EndAt;
        coupon.UsageLimit = dto.UsageLimit;
        coupon.IsActive = dto.IsActive;
        if (dto.Id == 0) _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync();
        return Result.Ok(dto.Id == 0 ? "Tạo mã giảm giá thành công." : "Cập nhật mã giảm giá thành công.");
    }

    public async Task<Result> DeleteAsync(UserSession actor, int id)
    {
        if (!Permission.IsAdmin(actor)) return Result.Fail(Permission.Denied);
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon == null) return Result.Fail("Mã giảm giá không tồn tại.");
        if (await _db.Orders.AnyAsync(o => o.CouponId == id))
        {
            coupon.IsActive = false;
            await _db.SaveChangesAsync();
            return Result.Ok("Mã đã được dùng trong đơn hàng nên chỉ có thể ngừng kích hoạt. Đã ngừng kích hoạt mã.");
        }
        _db.Coupons.Remove(coupon);
        await _db.SaveChangesAsync();
        return Result.Ok("Đã xóa mã giảm giá.");
    }
}
