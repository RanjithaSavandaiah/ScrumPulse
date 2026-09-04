namespace ScrumPulse.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScrumPulse.Domain.Entities;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(t => t.Slug)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(t => t.JoinCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(t => t.Slug).IsUnique();
        builder.HasIndex(t => t.JoinCode).IsUnique();
        builder.HasIndex(t => t.IsActive);
    }
}
