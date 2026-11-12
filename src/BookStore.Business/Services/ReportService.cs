using BookStore.Data;
using BookStore.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Business.Services;

public record RevenuePoint(string Label, DateTime Start, int OrderCount, decimal BookRevenue, decimal ShippingFee)
{
    public decimal Total => BookRevenue + ShippingFee;
}

public record TopBookItem(int BookId, string Title, int Quantity, decimal Revenue);
public record StatusCount(OrderStatus Status, int Count)
{
    public string StatusText => Status.ToVietnamese();
}
public record CategoryStockItem(string CategoryName, int BookCount, int TotalStock, decimal StockValue, int LowStockCount);

public class DashboardDto
{
    public int TotalBooks { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int LowStockBooks { get; set; }
    public decimal RevenueToday { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public int OrdersThisMonth { get; set; }
    public List<RevenuePoint> Last7Days { get; set; } = new();
    public List<TopBookItem> TopBooks { get; set; } = new();
    public List<OrderListItem> RecentOrders { get; set; } = new();
}

public enum RevenueGrouping { Day, Month }

/// <summary>
/// Thống kê – báo cáo. Quy ước doanh thu:
///  - Chỉ tính đơn HOÀN TẤT, theo ngày hoàn tất (CompletedAt).
///  - Doanh thu sách = Tiền sách − Giảm giá; phí vận chuyển trình bày riêng.
/// </summary>
public class ReportService
{
    private readonly BookStoreDbContext _db;
    public ReportService(BookStoreDbContext db) => _db = db;

    private record CompletedRow(DateTime CompletedAt, decimal BookRevenue, decimal ShippingFee);

    private async Task<List<CompletedRow>> CompletedOrdersAsync(DateTime from, DateTime toExclusive) =>
        await _db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed && o.CompletedAt >= from && o.CompletedAt < toExclusive)
            .Select(o => new CompletedRow(o.CompletedAt!.Value, o.Subtotal - o.DiscountAmount, o.ShippingFee))
            .ToListAsync();

    public async Task<DashboardDto> GetDashboardAsync()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var monthRows = await CompletedOrdersAsync(monthStart, today.AddDays(1));
        return new DashboardDto
        {
            TotalBooks = await _db.Books.CountAsync(b => !b.IsHidden),
            TotalCustomers = await _db.Users.CountAsync(u => u.Role == UserRole.Customer),
            TotalOrders = await _db.Orders.CountAsync(),
            PendingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending),
            LowStockBooks = await _db.Books.CountAsync(b => !b.IsHidden && b.Stock <= b.LowStockThreshold),
            RevenueToday = monthRows.Where(r => r.CompletedAt >= today).Sum(r => r.BookRevenue),
            RevenueThisMonth = monthRows.Sum(r => r.BookRevenue),
            OrdersThisMonth = monthRows.Count,
            Last7Days = await GetRevenueAsync(today.AddDays(-6), today, RevenueGrouping.Day),
            TopBooks = await GetTopBooksAsync(today.AddDays(-29), today, 5),
            RecentOrders = await _db.Orders.AsNoTracking().OrderByDescending(o => o.OrderDate).Take(8)
                .Select(OrderService.ToListItem).ToListAsync()
        };
    }

    /// <summary>Doanh thu theo ngày hoặc tháng trong khoảng [from, to] (bao gồm cả ngày to). Mốc không có đơn vẫn hiển thị 0.</summary>
    public async Task<List<RevenuePoint>> GetRevenueAsync(DateTime from, DateTime to, RevenueGrouping grouping)
    {
        from = from.Date;
        to = to.Date;
        if (grouping == RevenueGrouping.Month)
        {
            from = new DateTime(from.Year, from.Month, 1);
            to = new DateTime(to.Year, to.Month, 1).AddMonths(1).AddDays(-1);
        }
        var rows = await CompletedOrdersAsync(from, to.AddDays(1));

        var points = new List<RevenuePoint>();
        if (grouping == RevenueGrouping.Day)
        {
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                var day = rows.Where(r => r.CompletedAt.Date == d).ToList();
                points.Add(new RevenuePoint(d.ToString("dd/MM"), d, day.Count, day.Sum(r => r.BookRevenue), day.Sum(r => r.ShippingFee)));
            }
        }
        else
        {
            for (var m = from; m <= to; m = m.AddMonths(1))
            {
                var month = rows.Where(r => r.CompletedAt.Year == m.Year && r.CompletedAt.Month == m.Month).ToList();
                points.Add(new RevenuePoint(m.ToString("MM/yyyy"), m, month.Count, month.Sum(r => r.BookRevenue), month.Sum(r => r.ShippingFee)));
            }
        }
        return points;
    }

    /// <summary>Sách bán chạy theo đơn hoàn tất trong khoảng thời gian.</summary>
    public async Task<List<TopBookItem>> GetTopBooksAsync(DateTime from, DateTime to, int top = 10)
    {
        var end = to.Date.AddDays(1);
        var rows = await _db.OrderDetails.AsNoTracking()
            .Where(d => d.Order!.Status == OrderStatus.Completed && d.Order.CompletedAt >= from.Date && d.Order.CompletedAt < end)
            .Select(d => new { d.BookId, d.Book!.Title, d.Quantity, d.UnitPrice })
            .ToListAsync();
        return rows.GroupBy(r => new { r.BookId, r.Title })
            .Select(g => new TopBookItem(g.Key.BookId, g.Key.Title, g.Sum(x => x.Quantity), g.Sum(x => x.Quantity * x.UnitPrice)))
            .OrderByDescending(x => x.Quantity).ThenByDescending(x => x.Revenue)
            .Take(top).ToList();
    }

    /// <summary>Số đơn theo trạng thái (theo ngày đặt).</summary>
    public async Task<List<StatusCount>> GetOrderStatusStatsAsync(DateTime from, DateTime to)
    {
        var end = to.Date.AddDays(1);
        var statuses = await _db.Orders.AsNoTracking()
            .Where(o => o.OrderDate >= from.Date && o.OrderDate < end)
            .Select(o => o.Status).ToListAsync();
        return Enum.GetValues<OrderStatus>()
            .Select(s => new StatusCount(s, statuses.Count(x => x == s))).ToList();
    }

    public async Task<List<CategoryStockItem>> GetStockByCategoryAsync()
    {
        var rows = await _db.Books.AsNoTracking().Where(b => !b.IsHidden)
            .Select(b => new { Category = b.Category!.Name, b.Stock, b.Price, b.LowStockThreshold }).ToListAsync();
        return rows.GroupBy(r => r.Category)
            .Select(g => new CategoryStockItem(g.Key, g.Count(), g.Sum(x => x.Stock), g.Sum(x => x.Stock * x.Price),
                g.Count(x => x.Stock <= x.LowStockThreshold)))
            .OrderBy(x => x.CategoryName).ToList();
    }
}
