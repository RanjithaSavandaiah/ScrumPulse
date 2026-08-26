namespace ScrumPulse.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScrumPulse.Domain.Entities;

public class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Title)
            .IsRequired()
            .HasMaxLength(300);

        builder.HasOne(w => w.Assignee)
            .WithMany()
            .HasForeignKey(w => w.AssigneeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(w => w.Sprint)
            .WithMany(s => s.WorkItems)
            .HasForeignKey(w => w.SprintId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(w => w.PrReviewer)
            .WithMany()
            .HasForeignKey(w => w.PrReviewerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
