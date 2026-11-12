using BookStore.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookStore.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> e)
    {
        e.ToTable("Suppliers");
        e.HasKey(x => x.Id);
        e.Property(x => x.Name).HasMaxLength(150).IsRequired();
        e.Property(x => x.Phone).HasMaxLength(15).IsUnicode(false);
        e.Property(x => x.Email).HasMaxLength(100);
        e.Property(x => x.Address).HasMaxLength(200);
    }
}

public class ImportReceiptConfiguration : IEntityTypeConfiguration<ImportReceipt>
{
    public void Configure(EntityTypeBuilder<ImportReceipt> e)
    {
        e.ToTable("ImportReceipts");
        e.HasKey(x => x.Id);
        e.Property(x => x.Note).HasMaxLength(500);
        e.HasIndex(x => x.ImportDate);
        e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ImportReceiptDetailConfiguration : IEntityTypeConfiguration<ImportReceiptDetail>
{
    public void Configure(EntityTypeBuilder<ImportReceiptDetail> e)
    {
        e.ToTable("ImportReceiptDetails", t =>
        {
            t.HasCheckConstraint("CK_ImportReceiptDetails_Quantity", "Quantity > 0");
            t.HasCheckConstraint("CK_ImportReceiptDetails_UnitCost", "UnitCost >= 0");
        });
        e.HasKey(x => new { x.ReceiptId, x.BookId });
        e.HasOne(x => x.Receipt).WithMany(r => r.Details).HasForeignKey(x => x.ReceiptId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Book).WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> e)
    {
        e.ToTable("StockTransactions", t =>
        {
            t.HasCheckConstraint("CK_StockTransactions_StockAfter", "StockAfter >= 0");
            t.HasCheckConstraint("CK_StockTransactions_QuantityChange", "QuantityChange <> 0");
        });
        e.HasKey(x => x.Id);
        e.Property(x => x.Reason).HasMaxLength(500);
        e.HasIndex(x => new { x.BookId, x.CreatedAt });
        e.HasOne(x => x.Book).WithMany().HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Restrict);
    }
}
