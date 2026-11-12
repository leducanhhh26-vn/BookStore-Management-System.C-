namespace BookStore.Domain;

// =====================================================================
//  TÀI KHOẢN
// =====================================================================

/// <summary>Tài khoản dùng chung cho Admin, Nhân viên (Desktop) và Khách hàng (Web).</summary>
public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<CustomerAddress> Addresses { get; set; } = new List<CustomerAddress>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}

/// <summary>Sổ địa chỉ nhận hàng của khách hàng.</summary>
public class CustomerAddress
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ReceiverName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string AddressLine { get; set; } = "";
    public string Ward { get; set; } = "";
    public string District { get; set; } = "";
    public string Province { get; set; } = "";
    public bool IsDefault { get; set; }

    public User? User { get; set; }

    public string FullAddress => string.Join(", ",
        new[] { AddressLine, Ward, District, Province }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

// =====================================================================
//  SÁCH
// =====================================================================

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsHidden { get; set; }

    public ICollection<Book> Books { get; set; } = new List<Book>();
}

public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Bio { get; set; }

    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
}

public class Publisher
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }

    public ICollection<Book> Books { get; set; } = new List<Book>();
}

/// <summary>Một đầu sách (một phiên bản xác định bởi ISBN).</summary>
public class Book
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public int PublisherId { get; set; }
    public string Isbn { get; set; } = "";
    public string Title { get; set; } = "";
    public int PublishYear { get; set; }
    public int Pages { get; set; }
    public string CoverType { get; set; } = "Bìa mềm";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    /// <summary>Tồn kho khả dụng. Mọi thay đổi đều ghi kèm một dòng StockTransaction.</summary>
    public int Stock { get; set; }
    public int LowStockThreshold { get; set; } = 10;
    public bool IsFeatured { get; set; }
    public bool IsHidden { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    /// <summary>Dấu phiên bản do SQL Server tự sinh, chống ghi đè khi hai người cùng sửa.</summary>
    public byte[]? RowVersion { get; set; }

    public Category? Category { get; set; }
    public Publisher? Publisher { get; set; }
    public BookImage? Image { get; set; }
    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();

    public bool IsLowStock => Stock <= LowStockThreshold;
}

/// <summary>Bảng liên kết N–N giữa Sách và Tác giả.</summary>
public class BookAuthor
{
    public int BookId { get; set; }
    public int AuthorId { get; set; }

    public Book? Book { get; set; }
    public Author? Author { get; set; }
}

/// <summary>Ảnh bìa lưu trong CSDL để Web và Desktop (có thể ở 2 máy khác nhau) cùng thấy.</summary>
public class BookImage
{
    public int BookId { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "image/jpeg";

    public Book? Book { get; set; }
}

// =====================================================================
//  KHO
// =====================================================================

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
}

/// <summary>Phiếu nhập hàng từ nhà cung cấp.</summary>
public class ImportReceipt
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public int StaffId { get; set; }
    public DateTime ImportDate { get; set; } = DateTime.Now;
    public string? Note { get; set; }

    public Supplier? Supplier { get; set; }
    public User? Staff { get; set; }
    public ICollection<ImportReceiptDetail> Details { get; set; } = new List<ImportReceiptDetail>();
}

public class ImportReceiptDetail
{
    public int ReceiptId { get; set; }
    public int BookId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }

    public ImportReceipt? Receipt { get; set; }
    public Book? Book { get; set; }
}

/// <summary>Sổ nhật ký biến động kho: nhập, bán, hoàn, điều chỉnh.</summary>
public class StockTransaction
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int? StaffId { get; set; }
    public StockTransactionType Type { get; set; }
    /// <summary>Số lượng thay đổi: dương là tăng, âm là giảm.</summary>
    public int QuantityChange { get; set; }
    public int StockAfter { get; set; }
    /// <summary>Mã chứng từ liên quan (mã phiếu nhập hoặc mã đơn hàng).</summary>
    public int? RefId { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Book? Book { get; set; }
    public User? Staff { get; set; }
}

// =====================================================================
//  GIỎ HÀNG – ĐƠN HÀNG – THANH TOÁN
// =====================================================================

/// <summary>Một dòng trong giỏ hàng của khách đã đăng nhập. Mỗi khách có một giỏ nên không cần bảng Carts.</summary>
public class CartItem
{
    public int UserId { get; set; }
    public int BookId { get; set; }
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.Now;

    public User? User { get; set; }
    public Book? Book { get; set; }
}

public class Coupon
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public DiscountType DiscountType { get; set; }
    public decimal Value { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal MinOrder { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int? HandledById { get; set; }
    public int? CouponId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    // Thông tin giao hàng được chốt tại thời điểm đặt (không đọc lại sổ địa chỉ)
    public string ReceiverName { get; set; } = "";
    public string ReceiverPhone { get; set; } = "";
    public string ShippingAddress { get; set; } = "";
    public string? Note { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    /// <summary>Tổng tiền sách (đã chốt khi đặt).</summary>
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public string? CancelReason { get; set; }
    public byte[]? RowVersion { get; set; }

    public User? Customer { get; set; }
    public User? HandledBy { get; set; }
    public Coupon? Coupon { get; set; }
    public ICollection<OrderDetail> Details { get; set; } = new List<OrderDetail>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    /// <summary>Tổng thanh toán = Tiền sách − Giảm giá + Phí vận chuyển (giá trị suy ra, không lưu).</summary>
    public decimal Total => Subtotal - DiscountAmount + ShippingFee;
}

public class OrderDetail
{
    public int OrderId { get; set; }
    public int BookId { get; set; }
    public int Quantity { get; set; }
    /// <summary>Giá bán một cuốn tại thời điểm đặt.</summary>
    public decimal UnitPrice { get; set; }

    public Order? Order { get; set; }
    public Book? Book { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}

public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime? PaidAt { get; set; }
    public string? ReferenceCode { get; set; }

    public Order? Order { get; set; }
}

/// <summary>Đánh giá sách. Chỉ khách đã mua và nhận hàng mới được đánh giá; mỗi khách 1 đánh giá / sách.</summary>
public class Review
{
    public int UserId { get; set; }
    public int BookId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public User? User { get; set; }
    public Book? Book { get; set; }
}
