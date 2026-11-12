namespace BookStore.Domain;

/// <summary>
/// Quy tắc chuyển trạng thái đơn hàng – dùng chung cho Web, Desktop và kiểm thử.
///   Pending → Confirmed → Shipping → Completed
///   Pending / Confirmed → Cancelled
/// </summary>
public static class OrderStatusRules
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Pending] = new[] { OrderStatus.Confirmed, OrderStatus.Cancelled },
        [OrderStatus.Confirmed] = new[] { OrderStatus.Shipping, OrderStatus.Cancelled },
        [OrderStatus.Shipping] = new[] { OrderStatus.Completed },
        [OrderStatus.Completed] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>()
    };

    public static bool CanChange(OrderStatus from, OrderStatus to) => Allowed[from].Contains(to);

    public static IReadOnlyList<OrderStatus> NextStatuses(OrderStatus from) => Allowed[from];

    /// <summary>Khách hàng chỉ được tự hủy khi đơn còn chờ xác nhận.</summary>
    public static bool CustomerCanCancel(OrderStatus status) => status == OrderStatus.Pending;

    public static string ToVietnamese(this OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Chờ xác nhận",
        OrderStatus.Confirmed => "Đã xác nhận",
        OrderStatus.Shipping => "Đang giao",
        OrderStatus.Completed => "Hoàn tất",
        OrderStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };

    public static string ToVietnamese(this PaymentMethod method) => method switch
    {
        PaymentMethod.COD => "Thanh toán khi nhận hàng (COD)",
        PaymentMethod.BankTransfer => "Chuyển khoản ngân hàng",
        _ => method.ToString()
    };

    public static string ToVietnamese(this PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "Chưa thanh toán",
        PaymentStatus.Paid => "Đã thanh toán",
        PaymentStatus.Failed => "Thất bại",
        _ => status.ToString()
    };

    public static string ToVietnamese(this UserRole role) => role switch
    {
        UserRole.Admin => "Quản trị viên",
        UserRole.Staff => "Nhân viên",
        UserRole.Customer => "Khách hàng",
        _ => role.ToString()
    };

    public static string ToVietnamese(this StockTransactionType type) => type switch
    {
        StockTransactionType.Import => "Nhập hàng",
        StockTransactionType.Sale => "Xuất bán",
        StockTransactionType.Cancel => "Hoàn kho (hủy đơn)",
        StockTransactionType.Adjust => "Điều chỉnh kiểm kê",
        _ => type.ToString()
    };
}
