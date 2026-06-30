using IRETP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IRETP.Infrastructure.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);

        builder.HasIndex(t => t.TransactionDate);
        builder.HasIndex(t => t.ZoneId);
        builder.HasIndex(t => t.PropertyType);
        builder.HasIndex(t => t.TransactionType);
        builder.HasIndex(t => new { t.ZoneId, t.TransactionDate });

        builder.Property(t => t.AreaSqft).HasPrecision(18, 2);
        builder.Property(t => t.AreaSqm).HasPrecision(18, 2);
        builder.Property(t => t.TransactionValue).HasPrecision(18, 2);
        builder.Property(t => t.PricePerSqft).HasPrecision(18, 2);

        builder.HasOne(t => t.Zone)
            .WithMany(z => z.Transactions)
            .HasForeignKey(t => t.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Project)
            .WithMany(p => p.Transactions)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
