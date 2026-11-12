using BookStore.Business.Services;
using BookStore.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

/// <summary>Lịch sử mua hàng và theo dõi trạng thái đơn của khách hàng.</summary>
[Authorize(Policy = "Customer")]
public class OrdersController : Controller
{
    private readonly OrderService _orders;
    private readonly ReviewService _reviews;

    public OrdersController(OrderService orders, ReviewService reviews)
    {
        _orders = orders;
        _reviews = reviews;
    }

    public async Task<IActionResult> Index(OrderStatus? status = null)
    {
        var orders = await _orders.GetCustomerOrdersAsync(User.GetUserId()!.Value);
        ViewBag.Status = status;
        ViewBag.Counts = orders.GroupBy(o => o.Status).ToDictionary(g => g.Key, g => g.Count());
        return View(status == null ? orders : orders.Where(o => o.Status == status).ToList());
    }

    public async Task<IActionResult> Details(int id, bool placed = false)
    {
        var userId = User.GetUserId()!.Value;
        var order = await _orders.GetDetailAsync(id, userId);
        if (order == null) return NotFound();

        var vm = new OrderDetailViewModel { Order = order };
        if (order.Status == OrderStatus.Completed)
            foreach (var line in order.Lines)
                if (await _reviews.GetMyReviewAsync(userId, line.BookId) != null) vm.ReviewedBookIds.Add(line.BookId);
        ViewBag.JustPlaced = placed;
        return View(vm);
    }

    /// <summary>Trả về trạng thái hiện tại để trang chi tiết tự làm mới khi nhân viên cập nhật trên Desktop.</summary>
    [HttpGet]
    public async Task<IActionResult> Status(int id)
    {
        var order = await _orders.GetDetailAsync(id, User.GetUserId()!.Value);
        if (order == null) return NotFound();
        return Json(new { status = order.Status.ToString(), payment = order.PaymentStatus.ToString() });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, string? reason)
    {
        this.Toast(await _orders.CancelByCustomerAsync(User.GetUserId()!.Value, id, reason));
        return RedirectToAction(nameof(Details), new { id });
    }
}
