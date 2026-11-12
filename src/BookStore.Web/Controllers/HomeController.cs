using System.Diagnostics;
using BookStore.Business.Services;
using BookStore.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

public class HomeController : Controller
{
    private readonly BookService _books;
    private readonly CategoryService _categories;

    public HomeController(BookService books, CategoryService categories)
    {
        _books = books;
        _categories = categories;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new HomeViewModel
        {
            Categories = await _categories.GetAllAsync(includeHidden: false),
            Featured = await _books.GetFeaturedAsync(8),
            BestSellers = await _books.GetBestSellersAsync(8),
            Newest = await _books.GetNewestAsync(8)
        };
        return View(vm);
    }

    [Route("/Home/Error")]
    public IActionResult Error()
    {
        ViewBag.RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }

    [Route("/Home/NotFound")]
    public IActionResult NotFoundPage(int code = 404)
    {
        Response.StatusCode = code;
        return View("NotFound", code);
    }
}
