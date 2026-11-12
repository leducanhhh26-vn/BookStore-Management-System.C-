using System.Security.Claims;
using BookStore.Business.Common;
using BookStore.Business.Services;
using BookStore.Web.Infrastructure;
using BookStore.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

public class AccountController : Controller
{
    private readonly AuthService _auth;
    private readonly CustomerService _customers;
    private readonly CartService _cart;
    private readonly OrderService _orders;

    public AccountController(AuthService auth, CustomerService customers, CartService cart, OrderService orders)
    {
        _auth = auth;
        _customers = customers;
        _cart = cart;
        _orders = orders;
    }

    // ===================== Đăng nhập / Đăng ký / Đăng xuất =====================

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await _auth.LoginAsync(vm.Login, vm.Password, LoginChannel.Web);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Message);
            return View(vm);
        }

        await SignInAsync(result.Value!, vm.RememberMe);
        this.Toast($"Xin chào {result.Value!.FullName}!");
        return LocalRedirect(Url.IsLocalUrl(vm.ReturnUrl) ? vm.ReturnUrl! : "/");
    }

    [HttpGet]
    public IActionResult Register() =>
        User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home") : View(new RegisterViewModel());

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await _auth.RegisterCustomerAsync(new RegisterDto
        {
            FullName = vm.FullName, Username = vm.Username, Email = vm.Email, Phone = vm.Phone,
            Password = vm.Password, ConfirmPassword = vm.ConfirmPassword
        });
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Message);
            return View(vm);
        }

        await SignInAsync(result.Value!, true);
        this.Toast("Đăng ký tài khoản thành công. Chào mừng bạn đến với BookStore!");
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        this.Toast("Bạn đã đăng xuất.");
        return RedirectToAction("Index", "Home");
    }

    private async Task SignInAsync(UserSession user, bool remember)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new("username", user.Username),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = remember });

        // Gộp giỏ hàng lúc chưa đăng nhập vào giỏ của tài khoản
        var guestItems = GuestCart.Items(HttpContext.Session).ToList();
        if (guestItems.Count > 0)
        {
            await _cart.MergeAsync(user.Id, guestItems);
            GuestCart.Clear(HttpContext.Session);
        }
    }

    // ===================== Hồ sơ cá nhân =====================

    [Authorize(Policy = "Customer")]
    public async Task<IActionResult> Profile()
    {
        var userId = User.GetUserId()!.Value;
        var profile = await _customers.GetProfileAsync(userId);
        if (profile == null) return RedirectToAction(nameof(Login));
        ViewBag.ChangePassword = new ChangePasswordViewModel();
        return View(new ProfileViewModel
        {
            Username = profile.Username, CreatedAt = profile.CreatedAt, FullName = profile.FullName,
            Email = profile.Email, Phone = profile.Phone,
            OrderCount = (await _orders.GetCustomerOrdersAsync(userId)).Count,
            AddressCount = (await _customers.GetAddressesAsync(userId)).Count
        });
    }

    [HttpPost, Authorize(Policy = "Customer")]
    public async Task<IActionResult> Profile(ProfileViewModel vm)
    {
        var userId = User.GetUserId()!.Value;
        if (!ModelState.IsValid)
        {
            ViewBag.ChangePassword = new ChangePasswordViewModel();
            return View(vm);
        }

        var result = await _customers.UpdateProfileAsync(userId, new ProfileDto { FullName = vm.FullName, Email = vm.Email, Phone = vm.Phone });
        this.Toast(result);
        if (result.Success)
        {
            // Cập nhật tên hiển thị trên thanh menu
            var profile = (await _customers.GetProfileAsync(userId))!;
            await SignInAsync(new UserSession(userId, profile.Username, profile.FullName, Domain.UserRole.Customer), true);
        }
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, Authorize(Policy = "Customer")]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            this.Toast(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage, false);
            return RedirectToAction(nameof(Profile));
        }
        var result = await _auth.ChangePasswordAsync(User.GetUserId()!.Value, vm.CurrentPassword, vm.NewPassword, vm.ConfirmPassword);
        this.Toast(result);
        return RedirectToAction(nameof(Profile));
    }

    // ===================== Sổ địa chỉ =====================

    [Authorize(Policy = "Customer")]
    public async Task<IActionResult> Addresses() => View(await _customers.GetAddressesAsync(User.GetUserId()!.Value));

    [HttpGet, Authorize(Policy = "Customer")]
    public async Task<IActionResult> AddressForm(int id = 0, string? returnUrl = null)
    {
        if (id == 0) return View(new AddressViewModel { ReturnUrl = returnUrl, ReceiverName = User.Identity?.Name ?? "" });
        var address = await _customers.GetAddressAsync(User.GetUserId()!.Value, id);
        if (address == null) return NotFound();
        var vm = AddressViewModel.From(address);
        vm.ReturnUrl = returnUrl;
        return View(vm);
    }

    [HttpPost, Authorize(Policy = "Customer")]
    public async Task<IActionResult> AddressForm(AddressViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var result = await _customers.SaveAddressAsync(User.GetUserId()!.Value, vm.ToDto());
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Message);
            return View(vm);
        }
        this.Toast(result);
        return Url.IsLocalUrl(vm.ReturnUrl) ? LocalRedirect(vm.ReturnUrl!) : RedirectToAction(nameof(Addresses));
    }

    [HttpPost, Authorize(Policy = "Customer")]
    public async Task<IActionResult> DeleteAddress(int id)
    {
        this.Toast(await _customers.DeleteAddressAsync(User.GetUserId()!.Value, id));
        return RedirectToAction(nameof(Addresses));
    }

    [HttpPost, Authorize(Policy = "Customer")]
    public async Task<IActionResult> SetDefaultAddress(int id)
    {
        var userId = User.GetUserId()!.Value;
        var address = await _customers.GetAddressAsync(userId, id);
        if (address != null)
        {
            address.IsDefault = true;
            this.Toast(await _customers.SaveAddressAsync(userId, address));
        }
        return RedirectToAction(nameof(Addresses));
    }
}
