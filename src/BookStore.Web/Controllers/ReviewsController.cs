using BookStore.Business.Services;
using BookStore.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

[Authorize(Policy = "Customer")]
public class ReviewsController : Controller
{
    private readonly ReviewService _reviews;
    public ReviewsController(ReviewService reviews) => _reviews = reviews;

    [HttpPost]
    public async Task<IActionResult> Save(int bookId, int rating, string? comment)
    {
        this.Toast(await _reviews.SaveAsync(User.GetUserId()!.Value, bookId, rating, comment));
        return Redirect(Url.Action("Details", "Books", new { id = bookId }) + "#reviews");
    }
}
