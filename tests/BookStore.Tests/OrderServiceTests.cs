using BookStore.Business.Services;
using BookStore.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Tests;

public class OrderServiceTests
{
    private static async Task AddToCart(TestDatabase t, int userId, int bookId, int qty)
    {
        var r = await new CartService(t.Db).AddAsync(userId, bookId, qty);
        Assert.True(r.Success, r.Message);
    }

    [Fact]
    public async Task PlaceOrder_DeductsStock_LogsTransaction_AndClearsCart()
    {
        using var t = new TestDatabase();
        await AddToCart(t, t.Customer.Id, t.BookA.Id, 2);

        var result = await new OrderService(t.Db).PlaceOrderAsync(t.Customer.Id, t.ValidShipping());

        Assert.True(result.Success, result.Message);
        Assert.Equal(8, t.StockOf(t.BookA.Id));
        var order = await t.Db.Orders.Include(o => o.Details).Include(o => o.Payments).SingleAsync();
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(200000, order.Subtotal);
        Assert.Equal(25000, order.ShippingFee); // dưới 300.000đ tính phí ship
        Assert.Equal(225000, order.Payments.Single().Amount);
        var log = await t.Db.StockTransactions.SingleAsync();
        Assert.Equal(StockTransactionType.Sale, log.Type);
        Assert.Equal(-2, log.QuantityChange);
        Assert.Equal(8, log.StockAfter);
        Assert.False(await t.Db.CartItems.AnyAsync());
    }

    [Fact]
    public async Task PlaceOrder_FreeShipping_WhenSubtotalAtLeast300k()
    {
        using var t = new TestDatabase();
        await AddToCart(t, t.Customer.Id, t.BookA.Id, 3);

        var result = await new OrderService(t.Db).PlaceOrderAsync(t.Customer.Id, t.ValidShipping());

        Assert.True(result.Success, result.Message);
        Assert.Equal(0, (await t.Db.Orders.SingleAsync()).ShippingFee);
    }

    [Fact]
    public async Task PlaceOrder_Fails_WhenStockTakenByAnotherCustomer_AndNothingIsSaved()
    {
        using var t = new TestDatabase();
        // Hai khách cùng bỏ 2 cuốn BookB (tồn 3) vào giỏ
        await AddToCart(t, t.Customer.Id, t.BookB.Id, 2);
        await AddToCart(t, t.Customer2.Id, t.BookB.Id, 2);

        using var ctx2 = t.CreateContext();
        var first = await new OrderService(t.Db).PlaceOrderAsync(t.Customer2.Id, t.ValidShipping());
        var second = await new OrderService(ctx2).PlaceOrderAsync(t.Customer.Id, t.ValidShipping());

        Assert.True(first.Success, first.Message);
        Assert.False(second.Success);
        Assert.Contains("chỉ còn 1 cuốn", second.Message);
        Assert.Equal(1, t.StockOf(t.BookB.Id));                 // không bán vượt tồn
        Assert.Equal(1, await ctx2.Orders.CountAsync());         // đơn thứ hai bị rollback
        Assert.True(await ctx2.CartItems.AnyAsync(c => c.UserId == t.Customer.Id)); // giỏ của khách vẫn còn
    }

    [Fact]
    public async Task PlaceOrder_RollsBackEarlierLines_WhenALaterLineIsOutOfStock()
    {
        using var t = new TestDatabase();
        await AddToCart(t, t.Customer.Id, t.BookA.Id, 1);
        await AddToCart(t, t.Customer.Id, t.BookB.Id, 3);
        // Nhân viên kiểm kê giảm tồn BookB về 1 sau khi khách đã bỏ vào giỏ
        await new InventoryService(t.CreateContext()).AdjustStockAsync(t.StaffSession, t.BookB.Id, 1, "Hư hỏng");

        var result = await new OrderService(t.Db).PlaceOrderAsync(t.Customer.Id, t.ValidShipping());

        Assert.False(result.Success);
        Assert.Equal(10, t.StockOf(t.BookA.Id)); // dòng đầu đã trừ nhưng được hoàn tác
        Assert.Equal(0, await t.CreateContext().Orders.CountAsync());
    }

    [Fact]
    public async Task PlaceOrder_AppliesCoupon_WithMaxDiscount_AndConsumesUsage()
    {
        using var t = new TestDatabase();
        await AddToCart(t, t.Customer.Id, t.BookB.Id, 1); // 250.000 → 10% = 25.000, trần 20.000

        var result = await new OrderService(t.Db).PlaceOrderAsync(t.Customer.Id, t.ValidShipping("giam10"));

        Assert.True(result.Success, result.Message);
        var order = await t.CreateContext().Orders.SingleAsync();
        Assert.Equal(20000, order.DiscountAmount);
        Assert.Equal(1, (await t.CreateContext().Coupons.SingleAsync()).UsedCount);
    }

    [Fact]
    public async Task PlaceOrder_RejectsCoupon_WhenUsageLimitReached()
    {
        using var t = new TestDatabase();
        await AddToCart(t, t.Customer.Id, t.BookA.Id, 1);
        await AddToCart(t, t.Customer2.Id, t.BookA.Id, 1);
        Assert.True((await new OrderService(t.Db).PlaceOrderAsync(t.Customer.Id, t.ValidShipping("GIAM10"))).Success);

        var second = await new OrderService(t.CreateContext()).PlaceOrderAsync(t.Customer2.Id, t.ValidShipping("GIAM10"));

        Assert.False(second.Success);
        Assert.Equal(9, t.StockOf(t.BookA.Id)); // đơn thứ hai không trừ kho
    }

    [Fact]
    public async Task PlaceOrder_Fails_WhenCartEmpty_OrPhoneInvalid()
    {
        using var t = new TestDatabase();
        var service = new OrderService(t.Db);
        Assert.False((await service.PlaceOrderAsync(t.Customer.Id, t.ValidShipping())).Success);

        await AddToCart(t, t.Customer.Id, t.BookA.Id, 1);
        var dto = t.ValidShipping();
        dto.ReceiverPhone = "12345";
        Assert.False((await service.PlaceOrderAsync(t.Customer.Id, dto)).Success);
    }

    [Fact]
    public async Task StatusFlow_FollowsRules_AndCompletingCodMarksPaid()
    {
        using var t = new TestDatabase();
        await AddToCart(t, t.Customer.Id, t.BookA.Id, 1);
        var orderId = (await new OrderService(t.Db).PlaceOrderAsync(t.Customer.Id, t.ValidShipping())).Value;

        var service = new OrderService(t.CreateContext());
        Assert.False((await service.ChangeStatusAsync(t.StaffSession, orderId, OrderStatus.Completed)).Success); // không nhảy cóc
        Assert.True((await service.ChangeStatusAsync(t.StaffSession, orderId, OrderStatus.Confirmed)).Success);
        Assert.True((await service.ChangeStatusAsync(t.StaffSession, orderId, OrderStatus.Shipping)).Success);
        Assert.True((await service.ChangeStatusAsync(t.StaffSession, orderId, OrderStatus.Completed)).Success);
        Assert.False((await service.ChangeStatusAsync(t.StaffSession, orderId, OrderStatus.Pending)).Success);  // không quay ngược

        var order = await t.CreateContext().Orders.Include(o => o.Payments).SingleAsync();
        Assert.NotNull(order.CompletedAt);
        Assert.Equal(t.Staff.Id, order.HandledById);
        Assert.Equal(PaymentStatus.Paid, order.Payments.Single().Status);
    }

    [Fact]
    public async Task Cancel_RestoresStock_ExactlyOnce()
    {
        using var t = new TestDatabase();
        await AddToCart(t, t.Customer.Id, t.BookA.Id, 4);
        var orderId = (await new OrderService(t.Db).PlaceOrderAsync(t.Customer.Id, t.ValidShipping())).Value;
        Assert.Equal(6, t.StockOf(t.BookA.Id));

        var first = await new OrderService(t.CreateContext()).CancelByStaffAsync(t.StaffSession, orderId, "Sai địa chỉ");
        var second = await new OrderService(t.CreateContext()).CancelByStaffAsync(t.StaffSession, orderId, "Bấm lần 2");

        Assert.True(first.Success, first.Message);
        Assert.False(second.Success);
        Assert.Equal(10, t.StockOf(t.BookA.Id));
        var ctx = t.CreateContext();
        Assert.Equal(OrderStatus.Cancelled, (await ctx.Orders.SingleAsync()).Status);
        Assert.Equal(1, await ctx.StockTransactions.CountAsync(x => x.Type == StockTransactionType.Cancel));
        Assert.Equal(PaymentStatus.Failed, (await ctx.Payments.SingleAsync()).Status);
    }

    [Fact]
    public async Task Customer_CanCancelOnlyOwnPendingOrder()
    {
        using var t = new TestDatabase();
        await AddToCart(t, t.Customer.Id, t.BookA.Id, 1);
        var orderId = (await new OrderService(t.Db).PlaceOrderAsync(t.Customer.Id, t.ValidShipping())).Value;

        // Khách khác không hủy được đơn của người khác
        Assert.False((await new OrderService(t.CreateContext()).CancelByCustomerAsync(t.Customer2.Id, orderId, null)).Success);

        await new OrderService(t.CreateContext()).ChangeStatusAsync(t.StaffSession, orderId, OrderStatus.Confirmed);
        // Đơn đã xác nhận thì khách không tự hủy được
        Assert.False((await new OrderService(t.CreateContext()).CancelByCustomerAsync(t.Customer.Id, orderId, null)).Success);
        Assert.Equal(9, t.StockOf(t.BookA.Id));
    }

    [Fact]
    public async Task Customer_CannotChangeOrderStatus()
    {
        using var t = new TestDatabase();
        await AddToCart(t, t.Customer.Id, t.BookA.Id, 1);
        var orderId = (await new OrderService(t.Db).PlaceOrderAsync(t.Customer.Id, t.ValidShipping())).Value;
        var customerSession = new Business.Common.UserSession(t.Customer.Id, "khach", "Khách A", UserRole.Customer);

        var result = await new OrderService(t.CreateContext()).ChangeStatusAsync(customerSession, orderId, OrderStatus.Confirmed);

        Assert.False(result.Success);
    }
}
