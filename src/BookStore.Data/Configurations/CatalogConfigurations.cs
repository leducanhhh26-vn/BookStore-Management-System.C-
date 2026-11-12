using BookStore.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookStore.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> e)
    {
        e.ToTable("Categories");
        e.HasKey(x => x.Id);
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.Property(x => x.Description).HasMaxLength(500);
        e.HasIndex(x => x.Name).IsUnique();
    }
}

public class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> e)
    {
        e.ToTable("Authors");
        e.HasKey(x => x.Id);
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.Property(x => x.Bio).HasMaxLength(1000);
        e.HasIndex(x => x.Name);
    }
}

public class PublisherConfiguration : IEntityTypeConfiguration<Publisher>
{
    public void Configure(EntityTypeBuilder<Publisher> e)
    {
        e.ToTable("Publishers");
        e.HasKey(x => x.Id);
        e.Property(x => x.Name).HasMaxLength(150).IsRequired();
        e.Property(x => x.Address).HasMaxLength(200);
        e.Property(x => x.Phone).HasMaxLength(15).IsUnicode(false);
        e.HasIndex(x => x.Name).IsUnique();
    }
}

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> e)
    {
        e.ToTable("Books", t =>
        {
            t.HasCheckConstraint("CK_Books_Price", "Price > 0");
            t.HasCheckConstraint("CK_Books_Stock", "Stock >= 0");
            t.HasCheckConstraint("CK_Books_Pages", "Pages > 0");
            t.HasCheckConstraint("CK_Books_LowStockThreshold", "LowStockThreshold >= 0");
        });
        e.HasKey(x => x.Id);
        e.Property(x => x.Isbn).HasMaxLength(20).IsUnicode(false).IsRequired();
        e.Property(x => x.Title).HasMaxLength(250).IsRequired();
        e.Property(x => x.CoverType).HasMaxLength(50).IsRequired();
        e.Property(x => x.Description).HasMaxLength(4000);
        e.Property(x => x.RowVersion).IsRowVersion();
        e.Ignore(x => x.IsLowStock);
        e.HasIndex(x => x.Isbn).IsUnique();
        e.HasIndex(x => new { x.CategoryId, x.IsHidden });
        e.HasIndex(x => x.Title);

        e.HasOne(x => x.Category).WithMany(c => c.Books).HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Publisher).WithMany(p => p.Books).HasForeignKey(x => x.PublisherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BookAuthorConfiguration : IEntityTypeConfiguration<BookAuthor>
{
    public void Configure(EntityTypeBuilder<BookAuthor> e)
    {
        e.ToTable("BookAuthors");
        e.HasKey(x => new { x.BookId, x.AuthorId });
        e.HasOne(x => x.Book).WithMany(b => b.BookAuthors).HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Author).WithMany(a => a.BookAuthors).HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class BookImageConfiguration : IEntityTypeConfiguration<BookImage>
{
    public void Configure(EntityTypeBuilder<BookImage> e)
    {
        e.ToTable("BookImages");
        e.HasKey(x => x.BookId);
        e.Property(x => x.Data).IsRequired();
        e.Property(x => x.ContentType).HasMaxLength(50).IsUnicode(false).IsRequired();
        e.HasOne(x => x.Book).WithOne(b => b.Image).HasForeignKey<BookImage>(x => x.BookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> e)
    {
        e.ToTable("Reviews", t => t.HasCheckConstraint("CK_Reviews_Rating", "Rating BETWEEN 1 AND 5"));
        e.HasKey(x => new { x.UserId, x.BookId });
        e.Property(x => x.Comment).HasMaxLength(1000);
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Book).WithMany(b => b.Reviews).HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
    }
}
