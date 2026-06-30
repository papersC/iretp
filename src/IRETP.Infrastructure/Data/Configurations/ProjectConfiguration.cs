using IRETP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IRETP.Infrastructure.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.DeveloperId);
        builder.HasIndex(p => p.ZoneId);
        builder.HasIndex(p => p.Status);

        builder.Property(p => p.CompletionPercentage).HasPrecision(5, 2);
        builder.Property(p => p.TotalProjectCost).HasPrecision(18, 2);

        builder.HasOne(p => p.Developer)
            .WithMany(d => d.Projects)
            .HasForeignKey(p => p.DeveloperId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Zone)
            .WithMany(z => z.Projects)
            .HasForeignKey(p => p.ZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.EscrowAccount)
            .WithOne(e => e.Project)
            .HasForeignKey<EscrowAccount>(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
