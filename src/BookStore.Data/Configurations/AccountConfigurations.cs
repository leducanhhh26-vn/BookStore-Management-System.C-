using BookStore.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookStore.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.ToTable("Users");
        e.HasKey(x => x.Id);
        e.Property(x => x.Username).HasMaxLength(50).IsRequired();
        e.Property(x => x.PasswordHash).HasMaxLength(100).IsUnicode(false).IsRequired();
        e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
        e.Property(x => x.Email).HasMaxLength(100).IsRequired();
        e.Property(x => x.Phone).HasMaxLength(15).IsUnicode(false).IsRequired();
        e.HasIndex(x => x.Username).IsUnique();
        e.HasIndex(x => x.Email).IsUnique();
        e.HasIndex(x => x.Phone).IsUnique();
    }
}

public class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> e)
    {
        e.ToTable("CustomerAddresses");
        e.HasKey(x => x.Id);
        e.Property(x => x.ReceiverName).HasMaxLength(100).IsRequired();
        e.Property(x => x.Phone).HasMaxLength(15).IsUnicode(false).IsRequired();
        e.Property(x => x.AddressLine).HasMaxLength(200).IsRequired();
        e.Property(x => x.Ward).HasMaxLength(100);
        e.Property(x => x.District).HasMaxLength(100);
        e.Property(x => x.Province).HasMaxLength(100);
        e.Ignore(x => x.FullAddress);
        e.HasOne(x => x.User).WithMany(u => u.Addresses).HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
