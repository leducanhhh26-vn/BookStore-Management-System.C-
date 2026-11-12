using BookStore.Business.Common;
using BookStore.Business.Services;
using BookStore.Web.Infrastructure;
using BookStore.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

[Authorize(Policy = "Customer")]
public class CheckoutController : Controller
{
    private readonly CartService _cart;
    private readonly CustomerService _customers;
    private readonly CouponService _coupons;
    private readonly OrderService _orders;

    public CheckoutController(CartService cart, CustomerService customers, CouponService coupons, OrderService orders)
    {
        _cart = cart;
        _customers = customers;
        _coupons = coupons;
        _orders = orders;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var vm = new CheckoutViewModel();
        await FillAsync(vm);
        if (vm.Cart.Lines.Count == 0)
        {
            this.Toast("Giỏ hàng đang trống.", false);
            return RedirectToAction("Index", "Cart");
        }
        var defaultAddress = vm.Addresses.FirstOrDefault(a => a.IsDefault) ?? vm.Addresses.FirstOrDefault();
        vm.AddressId = defaultAddress?.Id ?? 0;
        return View(vm);
    }

    /// <summary>command = "apply" để áp mã giảm giá (tính lại tiền), "place" để đặt hàng.</summary>
    [HttpPost]
    public async Task<IActionResult> Index(CheckoutViewModel vm, string command)
    {
        await FillAsync(vm);
        if (command == "apply") return View(vm);

        if (!vm.Cart.CanCheckout)
        {
            ModelState.AddModelError("", "Giỏ hàng có sản phẩm không đủ hàng. Vui lòng cập nhật giỏ hàng trước khi đặt.");
            return View(vm);
        }
        if (!string.IsNullOrWhiteSpace(vm.CouponCode) && !vm.CouponValid)
        {
            ModelState.AddModelError("", vm.CouponMessage ?? "Mã giảm giá không hợp lệ.");
            return View(vm);
        }

        var userId = User.GetUserId()!.Value;
        string name, phone, address;
        var saved = vm.Addresses.FirstOrDefault(a => a.Id == vm.AddressId);
        if (saved != null)
        {
            (name, phone, address) = (saved.ReceiverName, saved.Phone, saved.FullAddress);
        }
        else
        {
            // Giao tới một địa chỉ khác (nhập một lần, không lưu vào sổ địa chỉ)
            (name, phone, address) = (vm.ReceiverName ?? "", vm.ReceiverPhone ?? "", vm.ShippingAddress ?? "");
        }

        var result = await _orders.PlaceOrderAsync(userId, new PlaceOrderDto
        {
            ReceiverName = name, ReceiverPhone = phone, ShippingAddress = address,
            Note = vm.Note, PaymentMethod = vm.PaymentMethod, CouponCode = vm.CouponCode
        });
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Message);
            await FillAsync(vm);
            return View(vm);
        }

        this.Toast(result);
        return RedirectToAction("Details", "Orders", new { id = result.Value, placed = true });
    }

    /// <summary>Nạp giỏ hàng, sổ địa chỉ và tính tiền (giảm giá, phí ship) phía máy chủ.</summary>
    private async Task FillAsync(CheckoutViewModel vm)
    {
        var userId = User.GetUserId()!.Value;
        vm.Cart = await _cart.GetCartAsync(userId);
        vm.Addresses = await _customers.GetAddressesAsync(userId);
        vm.Discount = 0;
        vm.CouponValid = false;
        vm.CouponMessage = null;
        if (!string.IsNullOrWhiteSpace(vm.CouponCode))
        {
            var preview = await _coupons.PreviewAsync(vm.CouponCode, vm.Cart.Subtotal);
            vm.CouponValid = preview.Success;
            vm.CouponMessage = preview.Message;
            vm.Discount = preview.Success ? preview.Value : 0;
        }
        vm.ShippingFee = vm.Cart.Lines.Count == 0 ? 0 : ShippingPolicy.FeeFor(vm.Cart.Subtotal - vm.Discount);
    }
}
