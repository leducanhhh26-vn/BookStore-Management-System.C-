using System.ComponentModel.DataAnnotations;
using BookStore.Business.Common;
using BookStore.Business.Services;
using BookStore.Domain;

namespace BookStore.Web.ViewModels;

public class HomeViewModel
{
    public List<CategoryItem> Categories { get; set; } = new();
    public List<BookListItem> Featured { get; set; } = new();
    public List<BookListItem> BestSellers { get; set; } = new();
    public List<BookListItem> Newest { get; set; } = new();
}

public class BookListViewModel
{
    public PagedResult<BookListItem> Result { get; set; } = new();
    public List<CategoryItem> Categories { get; set; } = new();
    public List<LookupItem> Publishers { get; set; } = new();
    // Bộ lọc đang áp dụng (đổ lại vào form)
    public string? Q { get; set; }
    public int? CategoryId { get; set; }
    public int? PublisherId { get; set; }
    public int? AuthorId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool InStock { get; set; }
    public BookSort Sort { get; set; }
    public string Heading { get; set; } = "Tất cả sách";

    /// <summary>Tạo bộ tham số URL giữ nguyên bộ lọc, chỉ đổi trang.</summary>
    public Dictionary<string, string?> RouteFor(int page) => new()
    {
        ["q"] = Q, ["categoryId"] = CategoryId?.ToString(), ["publisherId"] = PublisherId?.ToString(),
        ["authorId"] = AuthorId?.ToString(), ["minPrice"] = MinPrice?.ToString("0"), ["maxPrice"] = MaxPrice?.ToString("0"),
        ["inStock"] = InStock ? "true" : null, ["sort"] = Sort.ToString(), ["page"] = page.ToString()
    };
}

public class BookDetailViewModel
{
    public BookDetailDto Book { get; set; } = new();
    public bool CanReview { get; set; }
    public ReviewItem? MyReview { get; set; }
    public int InCart { get; set; }
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email")]
    [Display(Name = "Tên đăng nhập hoặc Email")]
    public string Login { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; } = true;
    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(100)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
    [RegularExpression(@"^[a-zA-Z0-9_.]{4,50}$", ErrorMessage = "4–50 ký tự: chữ không dấu, số, dấu chấm, gạch dưới")]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại gồm 10 chữ số, bắt đầu bằng 0")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu"), MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu"), Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không trùng khớp")]
    [DataType(DataType.Password)]
    [Display(Name = "Nhập lại mật khẩu")]
    public string ConfirmPassword { get; set; } = "";
}

public class ProfileViewModel
{
    public string Username { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên"), Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại"), RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại gồm 10 chữ số, bắt đầu bằng 0")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = "";

    public int OrderCount { get; set; }
    public int AddressCount { get; set; }
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại"), DataType(DataType.Password), Display(Name = "Mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới"), MinLength(6, ErrorMessage = "Tối thiểu 6 ký tự"), DataType(DataType.Password), Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới"), Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không trùng khớp")]
    [DataType(DataType.Password), Display(Name = "Nhập lại mật khẩu mới")]
    public string ConfirmPassword { get; set; } = "";
}

public class AddressViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên người nhận"), Display(Name = "Họ tên người nhận")]
    public string ReceiverName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại"), RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại gồm 10 chữ số, bắt đầu bằng 0")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập số nhà, tên đường"), Display(Name = "Số nhà, tên đường")]
    public string AddressLine { get; set; } = "";

    [Display(Name = "Phường/Xã")]
    public string? Ward { get; set; }

    [Display(Name = "Quận/Huyện")]
    public string? District { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập Tỉnh/Thành phố"), Display(Name = "Tỉnh/Thành phố")]
    public string Province { get; set; } = "";

    [Display(Name = "Đặt làm địa chỉ mặc định")]
    public bool IsDefault { get; set; }

    public string? ReturnUrl { get; set; }

    public AddressDto ToDto() => new()
    {
        Id = Id, ReceiverName = ReceiverName, Phone = Phone, AddressLine = AddressLine, Ward = Ward ?? "",
        District = District ?? "", Province = Province, IsDefault = IsDefault
    };

    public static AddressViewModel From(AddressDto a) => new()
    {
        Id = a.Id, ReceiverName = a.ReceiverName, Phone = a.Phone, AddressLine = a.AddressLine, Ward = a.Ward,
        District = a.District, Province = a.Province, IsDefault = a.IsDefault
    };
}

public class CheckoutViewModel
{
    public CartDto Cart { get; set; } = new();
    public List<AddressDto> Addresses { get; set; } = new();

    /// <summary>Id địa chỉ trong sổ; 0 = nhập địa chỉ mới.</summary>
    public int AddressId { get; set; }
    public string? ReceiverName { get; set; }
    public string? ReceiverPhone { get; set; }
    public string? ShippingAddress { get; set; }

    public string? Note { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;
    public string? CouponCode { get; set; }

    // Tóm tắt tiền (tính phía máy chủ)
    public decimal Discount { get; set; }
    public string? CouponMessage { get; set; }
    public bool CouponValid { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total => Cart.Subtotal - Discount + ShippingFee;
}

public class OrderDetailViewModel
{
    public OrderDetailDto Order { get; set; } = new();
    public HashSet<int> ReviewedBookIds { get; set; } = new();
}
