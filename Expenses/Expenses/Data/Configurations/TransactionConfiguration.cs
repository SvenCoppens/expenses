using Expenses.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Expenses.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(t => t.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.Description)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(t => t.Notes)
            .HasMaxLength(1000);

        builder.Property(t => t.Type)
            .IsRequired();

        builder.HasIndex(t => new { t.UserId, t.Date });

        builder.HasOne(t => t.Category)
            .WithMany(c => c.Transactions)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Subcategory)
            .WithMany()
            .HasForeignKey(t => t.SubcategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
