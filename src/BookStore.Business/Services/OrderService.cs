using System.Linq.Expressions;
using BookStore.Business.Common;
using BookStore.Data;
using BookStore.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Business.Services;

public record OrderListItem(int Id, DateTime OrderDate, int CustomerId, string CustomerName, string ReceiverName,
    string ReceiverPhone, OrderStatus Status, PaymentMethod PaymentMethod, PaymentStatus PaymentStatus,
    int ItemCount, decimal Subtotal, decimal DiscountAmount, decimal ShippingFee, string? HandledBy)
{
    public decimal Total => Subtotal - DiscountAmount + ShippingFee;
    public string StatusText => Status.ToVietnamese();
}

public record OrderLine(int BookId, string Title, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => UnitPrice * Quantity;
}

public record PaymentItem(int Id, decimal Amount, PaymentStatus Status, DateTime? PaidAt, string? ReferenceCode);

public class OrderDetailDto
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string ReceiverName { get; set; } = "";
    public string ReceiverPhone { get; set; } = "";
    public string ShippingAddress { get; set; } = "";
    public string? Note { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public OrderStatus Status { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public string? CouponCode { get; set; }
    public string? CancelReason { get; set; }
    public string? HandledBy { get; set; }
    public byte[]? RowVersion { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
    public List<PaymentItem> Payments { get; set; } = new();

    public decimal Total => Subtotal - DiscountAmount + ShippingFee;
    public PaymentStatus PaymentStatus => Payments.Count == 0 ? PaymentStatus.Pending : Payments[^1].Status;
    public IReadOnlyList<OrderStatus> NextStatuses => OrderStatusRules.NextStatuses(Status);
}

public class PlaceOrderDto
{
    public string ReceiverName { get; set; } = "";
    public string ReceiverPhone { get; set; } = "";
    public string ShippingAddress { get; set; } = "";
    public string? Note { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;
    public string? CouponCode { get; set; }
}

public class OrderQuery
{
    public OrderStatus? Status { get; set; }
    /// <summary>Mã đơn, tên người nhận, số điện thoại hoặc tên khách.</summary>
    public string? Keyword { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

/// <summary>
/// Nghiệp vụ đơn hàng – dùng chung cho Web (đặt hàng, theo dõi, tự hủy) và Desktop (xử lý đơn).
///
/// Quy tắc tồn kho: trừ kho NGAY khi đặt hàng (trong transaction, bằng câu UPDATE có điều kiện
/// "Stock >= số lượng" nên hai khách đặt cùng lúc cũng không bán quá tồn). Hủy đơn thì hoàn kho.
/// Mọi biến động kho đều ghi vào StockTransactions.
/// </summary>
public class OrderService
{
    private readonly BookStoreDbContext _db;
    public OrderService(BookStoreDbContext db) => _db = db;

    internal static readonly Expression<Func<Order, OrderListItem>> ToListItem = o => new OrderListItem(
        o.Id, o.OrderDate, o.CustomerId, o.Customer!.FullName, o.ReceiverName, o.ReceiverPhone, o.Status,
        o.PaymentMethod,
        o.Payments.OrderByDescending(p => p.Id).Select(p => (PaymentStatus?)p.Status).FirstOrDefault() ?? PaymentStatus.Pending,
        o.Details.Sum(d => d.Quantity), o.Subtotal, o.DiscountAmount, o.ShippingFee,
        o.HandledBy == null ? null : o.HandledBy.FullName);

    // ===================== Đặt hàng (Web) =====================

    public async Task<Result<int>> PlaceOrderAsync(int customerId, PlaceOrderDto dto)
    {
        var receiverName = Validation.Clean(dto.ReceiverName);
        var receiverPhone = Validation.Clean(dto.ReceiverPhone);
        var address = Validation.Clean(dto.ShippingAddress);
        if (receiverName.Length == 0 || address.Length == 0)
            return Result<int>.Fail("Vui lòng nhập đầy đủ họ tên người nhận và địa chỉ giao hàng.");
        if (!Validation.IsPhone(receiverPhone))
            return Result<int>.Fail("Số điện thoại người nhận không hợp lệ.");
        if (!Enum.IsDefined(dto.PaymentMethod))
            return Result<int>.Fail("Phương thức thanh toán không hợp lệ.");

        var customer = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == customerId && u.Role == UserRole.Customer);
        if (customer == null || !customer.IsActive)
            return Result<int>.Fail("Tài khoản không hợp lệ hoặc đã bị khóa.");

        var cartItems = await _db.CartItems.Where(c => c.UserId == customerId).ToListAsync();
        if (cartItems.Count == 0) return Result<int>.Fail("Giỏ hàng đang trống.");

        // Nếu return trước CommitAsync thì transaction tự rollback khi dispose
        await using var tx = await _db.Database.BeginTransactionAsync();

        var ids = cartItems.Select(c => c.BookId).ToList();
        // Giá luôn lấy từ CSDL phía máy chủ, không tin giá gửi lên từ trình duyệt
        var books = await _db.Books.AsNoTracking().Where(b => ids.Contains(b.Id))
            .Select(b => new { b.Id, b.Title, b.Price, Hidden = b.IsHidden || b.Category!.IsHidden })
            .ToDictionaryAsync(b => b.Id);

        var order = new Order
        {
            CustomerId = customerId,
            OrderDate = DateTime.Now,
            ReceiverName = receiverName,
            ReceiverPhone = receiverPhone,
            ShippingAddress = address,
            Note = Validation.CleanOrNull(dto.Note),
            PaymentMethod = dto.PaymentMethod,
            Status = OrderStatus.Pending
        };

        foreach (var item in cartItems)
        {
            if (!books.TryGetValue(item.BookId, out var book) || book.Hidden)
                return Result<int>.Fail("Có sản phẩm trong giỏ đã ngừng kinh doanh, vui lòng xóa khỏi giỏ hàng.");

            int qty = item.Quantity;
            // Trừ kho nguyên tử: chỉ trừ khi còn đủ hàng
            int affected = await _db.Books
                .Where(b => b.Id == item.BookId && b.Stock >= qty)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Stock, b => b.Stock - qty));
            if (affected == 0)
            {
                var stock = await _db.Books.Where(b => b.Id == item.BookId).Select(b => b.Stock).FirstAsync();
                return Result<int>.Fail(stock == 0
                    ? $"Sách \"{book.Title}\" đã hết hàng, vui lòng cập nhật giỏ hàng."
                    : $"Sách \"{book.Title}\" chỉ còn {stock} cuốn, vui lòng cập nhật giỏ hàng.");
            }
            order.Details.Add(new OrderDetail { BookId = item.BookId, Quantity = qty, UnitPrice = book.Price });
        }

        order.Subtotal = order.Details.Sum(d => d.UnitPrice * d.Quantity);

        // Mã giảm giá: kiểm tra điều kiện rồi tăng lượt dùng có điều kiện (tránh vượt giới hạn)
        var code = Validation.Clean(dto.CouponCode).ToUpperInvariant();
        if (code.Length > 0)
        {
            var coupon = await _db.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code);
            if (coupon == null) return Result<int>.Fail("Mã giảm giá không hợp lệ hoặc đã hết hạn.");
            var discount = CouponService.CalculateDiscount(coupon, order.Subtotal, DateTime.Now);
            if (!discount.Success) return Result<int>.Fail(discount.Message);

            int used = await _db.Coupons
                .Where(c => c.Id == coupon.Id && c.IsActive && c.UsedCount < c.UsageLimit)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedCount, c => c.UsedCount + 1));
            if (used == 0) return Result<int>.Fail("Mã giảm giá đã hết lượt sử dụng.");

            order.CouponId = coupon.Id;
            order.DiscountAmount = discount.Value;
        }

        order.ShippingFee = ShippingPolicy.FeeFor(order.Subtotal - order.DiscountAmount);
        order.Payments.Add(new Payment { Amount = order.Total, Status = PaymentStatus.Pending });
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Ghi sổ kho (xuất bán) với tồn sau khi trừ
        var stockAfter = await _db.Books.Where(b => ids.Contains(b.Id)).Select(b => new { b.Id, b.Stock })
            .ToDictionaryAsync(b => b.Id, b => b.Stock);
        foreach (var d in order.Details)
            _db.StockTransactions.Add(new StockTransaction
            {
                BookId = d.BookId, Type = StockTransactionType.Sale, QuantityChange = -d.Quantity,
                StockAfter = stockAfter[d.BookId], RefId = order.Id, Reason = $"Đơn hàng #{order.Id}"
            });

        // Xóa các dòng giỏ đã mua
        _db.CartItems.RemoveRange(cartItems);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Result<int>.Ok(order.Id, $"Đặt hàng thành công! Mã đơn hàng của bạn là #{order.Id}.");
    }

    // ===================== Truy vấn =====================

    public async Task<List<OrderListItem>> GetCustomerOrdersAsync(int customerId) =>
        await _db.Orders.AsNoTracking().Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.OrderDate).Select(ToListItem).ToListAsync();

    public async Task<List<OrderListItem>> SearchAsync(OrderQuery q)
    {
        var query = _db.Orders.AsNoTracking().AsQueryable();
        if (q.Status.HasValue) query = query.Where(o => o.Status == q.Status);
        if (q.FromDate.HasValue) query = query.Where(o => o.OrderDate >= q.FromDate.Value.Date);
        if (q.ToDate.HasValue)
        {
            var end = q.ToDate.Value.Date.AddDays(1);
            query = query.Where(o => o.OrderDate < end);
        }

        var keyword = Validation.Clean(q.Keyword).TrimStart('#');
        if (keyword.Length > 0)
        {
            int.TryParse(keyword, out var id);
            query = query.Where(o => o.Id == id || o.ReceiverName.Contains(keyword) || o.ReceiverPhone.Contains(keyword)
                                     || o.Customer!.FullName.Contains(keyword) || o.Customer.Email.Contains(keyword));
        }

        return await query.OrderByDescending(o => o.OrderDate).Take(1000).Select(ToListItem).ToListAsync();
    }

    /// <summary>Chi tiết đơn. Nếu truyền customerId thì chỉ trả về khi đơn thuộc khách đó (bảo vệ dữ liệu trên Web).</summary>
    public async Task<OrderDetailDto?> GetDetailAsync(int orderId, int? customerId = null)
    {
        var o = await _db.Orders.AsNoTracking()
            .Include(x => x.Customer).Include(x => x.HandledBy).Include(x => x.Coupon)
            .Include(x => x.Details).ThenInclude(d => d.Book)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == orderId && (customerId == null || x.CustomerId == customerId));
        if (o == null) return null;

        return new OrderDetailDto
        {
            Id = o.Id, OrderDate = o.OrderDate, CompletedAt = o.CompletedAt,
            CustomerId = o.CustomerId, CustomerName = o.Customer!.FullName, CustomerEmail = o.Customer.Email,
            ReceiverName = o.ReceiverName, ReceiverPhone = o.ReceiverPhone, ShippingAddress = o.ShippingAddress,
            Note = o.Note, PaymentMethod = o.PaymentMethod, Status = o.Status,
            Subtotal = o.Subtotal, DiscountAmount = o.DiscountAmount, ShippingFee = o.ShippingFee,
            CouponCode = o.Coupon?.Code, CancelReason = o.CancelReason, HandledBy = o.HandledBy?.FullName,
            RowVersion = o.RowVersion,
            Lines = o.Details.Select(d => new OrderLine(d.BookId, d.Book!.Title, d.Quantity, d.UnitPrice)).ToList(),
            Payments = o.Payments.OrderBy(p => p.Id)
                .Select(p => new PaymentItem(p.Id, p.Amount, p.Status, p.PaidAt, p.ReferenceCode)).ToList()
        };
    }

    public Task<int> CountPendingAsync() => _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending);

    public async Task<int> GetLatestOrderIdAsync() => await _db.Orders.MaxAsync(o => (int?)o.Id) ?? 0;

    // ===================== Xử lý đơn (Desktop) =====================

    /// <summary>Chuyển trạng thái đơn theo luồng Pending → Confirmed → Shipping → Completed.</summary>
    public async Task<Result> ChangeStatusAsync(UserSession actor, int orderId, OrderStatus newStatus, byte[]? rowVersion = null)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        if (newStatus == OrderStatus.Cancelled) return Result.Fail("Vui lòng dùng chức năng Hủy đơn (cần nhập lý do).");

        var order = await _db.Orders.Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return Result.Fail("Đơn hàng không tồn tại.");
        if (rowVersion != null) _db.Entry(order).Property(o => o.RowVersion).OriginalValue = rowVersion;

        if (!OrderStatusRules.CanChange(order.Status, newStatus))
            return Result.Fail($"Không thể chuyển đơn từ \"{order.Status.ToVietnamese()}\" sang \"{newStatus.ToVietnamese()}\".");

        var payment = order.Payments.OrderBy(p => p.Id).LastOrDefault();
        if (newStatus == OrderStatus.Shipping && order.PaymentMethod == PaymentMethod.BankTransfer
            && payment?.Status != PaymentStatus.Paid)
            return Result.Fail("Đơn chuyển khoản chưa được xác nhận thanh toán, chưa thể giao hàng.");

        order.Status = newStatus;
        order.HandledById ??= actor.Id;
        if (newStatus == OrderStatus.Completed)
        {
            order.CompletedAt = DateTime.Now;
            // COD: shipper thu tiền khi giao thành công
            if (payment != null && payment.Status == PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Paid;
                payment.PaidAt = DateTime.Now;
            }
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail("Đơn hàng vừa được người khác cập nhật. Vui lòng tải lại danh sách.");
        }
        return Result.Ok($"Đơn #{orderId} đã chuyển sang \"{newStatus.ToVietnamese()}\".");
    }

    /// <summary>Nhân viên xác nhận đã nhận tiền chuyển khoản.</summary>
    public async Task<Result> ConfirmPaymentAsync(UserSession actor, int orderId, string referenceCode)
    {
        if (!Permission.IsInternal(actor)) return Result.Fail(Permission.Denied);
        referenceCode = Validation.Clean(referenceCode).ToUpperInvariant();
        if (referenceCode.Length == 0) return Result.Fail("Vui lòng nhập mã giao dịch ngân hàng.");

        var order = await _db.Orders.Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return Result.Fail("Đơn hàng không tồn tại.");
        if (order.PaymentMethod != PaymentMethod.BankTransfer) return Result.Fail("Đơn này thanh toán khi nhận hàng (COD).");
        if (order.Status == OrderStatus.Cancelled) return Result.Fail("Đơn đã hủy.");
        var payment = order.Payments.OrderBy(p => p.Id).LastOrDefault();
        if (payment == null || payment.Status == PaymentStatus.Paid) return Result.Fail("Đơn đã được thanh toán.");
        if (await _db.Payments.AnyAsync(p => p.ReferenceCode == referenceCode))
            return Result.Fail("Mã giao dịch này đã được ghi nhận cho một đơn khác.");

        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = DateTime.Now;
        payment.ReferenceCode = referenceCode;
        order.HandledById ??= actor.Id;
        await _db.SaveChangesAsync();
        return Result.Ok("Đã xác nhận thanh toán chuyển khoản.");
    }

    public Task<Result> CancelByStaffAsync(UserSession actor, int orderId, string reason) =>
        Permission.IsInternal(actor)
            ? CancelInternalAsync(orderId, reason, staffId: actor.Id, customerId: null)
            : Task.FromResult(Result.Fail(Permission.Denied));

    public Task<Result> CancelByCustomerAsync(int customerId, int orderId, string? reason) =>
        CancelInternalAsync(orderId, string.IsNullOrWhiteSpace(reason) ? "Khách hàng tự hủy đơn" : reason!,
            staffId: null, customerId: customerId);

    /// <summary>
    /// Hủy đơn và hoàn kho trong cùng một transaction. Câu UPDATE có điều kiện "Status = trạng thái cũ"
    /// bảo đảm hai người hủy cùng lúc thì chỉ một lần thành công → kho chỉ được hoàn đúng một lần.
    /// </summary>
    private async Task<Result> CancelInternalAsync(int orderId, string reason, int? staffId, int? customerId)
    {
        reason = Validation.Clean(reason);
        if (reason.Length == 0) return Result.Fail("Vui lòng nhập lý do hủy đơn.");

        var order = await _db.Orders.AsNoTracking().Include(o => o.Details)
            .FirstOrDefaultAsync(o => o.Id == orderId && (customerId == null || o.CustomerId == customerId));
        if (order == null) return Result.Fail("Đơn hàng không tồn tại.");

        if (customerId != null && !OrderStatusRules.CustomerCanCancel(order.Status))
            return Result.Fail("Đơn hàng đã được xác nhận, vui lòng liên hệ cửa hàng để được hỗ trợ hủy.");
        if (!OrderStatusRules.CanChange(order.Status, OrderStatus.Cancelled))
            return Result.Fail($"Không thể hủy đơn đang ở trạng thái \"{order.Status.ToVietnamese()}\".");

        await using var tx = await _db.Database.BeginTransactionAsync();

        var oldStatus = order.Status;
        int affected = await _db.Orders
            .Where(o => o.Id == orderId && o.Status == oldStatus)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Cancelled)
                .SetProperty(o => o.CancelReason, reason)
                .SetProperty(o => o.HandledById, o => staffId ?? o.HandledById));
        if (affected == 0) return Result.Fail("Đơn hàng vừa được cập nhật bởi người khác. Vui lòng tải lại.");

        foreach (var d in order.Details)
        {
            int qty = d.Quantity;
            await _db.Books.Where(b => b.Id == d.BookId)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Stock, b => b.Stock + qty));
            var stock = await _db.Books.Where(b => b.Id == d.BookId).Select(b => b.Stock).FirstAsync();
            _db.StockTransactions.Add(new StockTransaction
            {
                BookId = d.BookId, StaffId = staffId, Type = StockTransactionType.Cancel, QuantityChange = qty,
                StockAfter = stock, RefId = orderId, Reason = $"Hủy đơn #{orderId}: {reason}"
            });
        }

        await _db.Payments.Where(p => p.OrderId == orderId && p.Status == PaymentStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Failed));

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Result.Ok($"Đã hủy đơn #{orderId} và hoàn lại tồn kho.");
    }
}
