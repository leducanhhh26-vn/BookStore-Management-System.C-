namespace BookStore.Domain;

/// <summary>Vai trò của tài khoản. Mỗi tài khoản có đúng một vai trò.</summary>
public enum UserRole
{
    Admin = 1,
    Staff = 2,
    Customer = 3
}

/// <summary>Trạng thái đơn hàng. Luồng hợp lệ được định nghĩa trong <see cref="OrderStatusRules"/>.</summary>
public enum OrderStatus
{
    Pending = 1,     // Chờ xác nhận
    Confirmed = 2,   // Đã xác nhận (đang đóng gói)
    Shipping = 3,    // Đang giao
    Completed = 4,   // Hoàn tất
    Cancelled = 5    // Đã hủy
}

public enum PaymentMethod
{
    COD = 1,           // Thanh toán khi nhận hàng
    BankTransfer = 2   // Chuyển khoản ngân hàng (nhân viên xác nhận thủ công)
}

public enum PaymentStatus
{
    Pending = 1,
    Paid = 2,
    Failed = 3
}

/// <summary>Loại biến động kho, dùng cho sổ nhật ký nhập/xuất kho.</summary>
public enum StockTransactionType
{
    Import = 1,   // Nhập hàng từ nhà cung cấp
    Sale = 2,     // Xuất bán khi khách đặt đơn
    Cancel = 3,   // Hoàn kho khi hủy đơn
    Adjust = 4    // Điều chỉnh sau kiểm kê
}

public enum DiscountType
{
    Percent = 1,
    Amount = 2
}
