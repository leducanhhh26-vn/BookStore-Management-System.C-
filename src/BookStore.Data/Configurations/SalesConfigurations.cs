using BookStore.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookStore.Data.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> e)
    {
        e.ToTable("CartItems", t => t.HasCheckConstraint("CK_CartItems_Quantity", "Quantity > 0"));
        e.HasKey(x => new { x.UserId, x.BookId });
        e.HasOne(x => x.User).WithMany(u => u.CartItems).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Book).WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> e)
    {
        e.ToTable("Coupons", t =>
        {
            t.HasCheckConstraint("CK_Coupons_Value", "Value > 0");
            t.HasCheckConstraint("CK_Coupons_Dates", "EndAt > StartAt");
            t.HasCheckConstraint("CK_Coupons_Usage", "UsageLimit > 0 AND UsedCount >= 0");
            t.HasCheckConstraint("CK_Coupons_MinOrder", "MinOrder >= 0");
        });
        e.HasKey(x => x.Id);
        e.Property(x => x.Code).HasMaxLength(30).IsUnicode(false).IsRequired();
        e.HasIndex(x => x.Code).IsUnique();
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> e)
    {
        e.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_Money", "Subtotal >= 0 AND DiscountAmount >= 0 AND ShippingFee >= 0 AND DiscountAmount <= Subtotal");
        });
        e.HasKey(x => x.Id);
        e.Property(x => x.ReceiverName).HasMaxLength(100).IsRequired();
        e.Property(x => x.ReceiverPhone).HasMaxLength(15).IsUnicode(false).IsRequired();
        e.Property(x => x.ShippingAddress).HasMaxLength(300).IsRequired();
        e.Property(x => x.Note).HasMaxLength(500);
        e.Property(x => x.CancelReason).HasMaxLength(500);
        e.Property(x => x.RowVersion).IsRowVersion();
        e.Ignore(x => x.Total);
        e.HasIndex(x => new { x.CustomerId, x.OrderDate });
        e.HasIndex(x => new { x.Status, x.CompletedAt });

        e.HasOne(x => x.Customer).WithMany(u => u.Orders).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.HandledBy).WithMany().HasForeignKey(x => x.HandledById).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Coupon).WithMany().HasForeignKey(x => x.CouponId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(EntityTypeBuilder<OrderDetail> e)
    {
        e.ToTable("OrderDetails", t =>
        {
            t.HasCheckConstraint("CK_OrderDetails_Quantity", "Quantity > 0");
            t.HasCheckConstraint("CK_OrderDetails_UnitPrice", "UnitPrice >= 0");
        });
        e.HasKey(x => new { x.OrderId, x.BookId });
        e.Ignore(x => x.LineTotal);
        e.HasIndex(x => x.BookId);
        e.HasOne(x => x.Order).WithMany(o => o.Details).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Book).WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> e)
    {
        e.ToTable("Payments", t => t.HasCheckConstraint("CK_Payments_Amount", "Amount > 0"));
        e.HasKey(x => x.Id);
        e.Property(x => x.ReferenceCode).HasMaxLength(100).IsUnicode(false);
        e.HasIndex(x => x.ReferenceCode).IsUnique().HasFilter("ReferenceCode IS NOT NULL");
        e.HasOne(x => x.Order).WithMany(o => o.Payments).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
