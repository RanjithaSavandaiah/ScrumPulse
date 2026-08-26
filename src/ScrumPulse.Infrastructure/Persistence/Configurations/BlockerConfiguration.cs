namespace ScrumPulse.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScrumPulse.Domain.Entities;

public class BlockerConfiguration : IEntityTypeConfiguration<Blocker>
{
    public void Configure(EntityTypeBuilder<Blocker> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.HasOne(b => b.Sprint)
            .WithMany(s => s.Blockers)
            .HasForeignKey(b => b.SprintId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(b => b.WorkItem)
            .WithMany()
            .HasForeignKey(b => b.WorkItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(b => b.RaisedBy)
            .WithMany()
            .HasForeignKey(b => b.RaisedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
