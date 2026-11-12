using BookStore.Business.Common;
using BookStore.Data;
using BookStore.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Business.Services;

/// <summary>Đánh giá sách: chỉ khách đã mua và nhận hàng thành công mới được đánh giá (chống đánh giá ảo).</summary>
public class ReviewService
{
    private readonly BookStoreDbContext _db;
    public ReviewService(BookStoreDbContext db) => _db = db;

    public Task<bool> CanReviewAsync(int userId, int bookId) =>
        _db.OrderDetails.AnyAsync(d => d.BookId == bookId && d.Order!.CustomerId == userId
                                       && d.Order.Status == OrderStatus.Completed);

    public async Task<ReviewItem?> GetMyReviewAsync(int userId, int bookId) =>
        await _db.Reviews.AsNoTracking().Where(r => r.UserId == userId && r.BookId == bookId)
            .Select(r => new ReviewItem(r.UserId, r.User!.FullName, r.Rating, r.Comment, r.CreatedAt))
            .FirstOrDefaultAsync();

    public async Task<Result> SaveAsync(int userId, int bookId, int rating, string? comment)
    {
        if (rating < 1 || rating > 5) return Result.Fail("Vui lòng chọn số sao từ 1 đến 5.");
        if (comment?.Length > 1000) return Result.Fail("Nội dung đánh giá tối đa 1000 ký tự.");
        if (!await CanReviewAsync(userId, bookId))
            return Result.Fail("Bạn chỉ có thể đánh giá sách đã mua và nhận hàng thành công.");

        var review = await _db.Reviews.FindAsync(userId, bookId);
        if (review == null)
        {
            _db.Reviews.Add(new Review { UserId = userId, BookId = bookId, Rating = rating, Comment = Validation.CleanOrNull(comment) });
        }
        else
        {
            review.Rating = rating;
            review.Comment = Validation.CleanOrNull(comment);
            review.CreatedAt = DateTime.Now;
        }
        await _db.SaveChangesAsync();
        return Result.Ok("Cảm ơn bạn đã đánh giá!");
    }
}
