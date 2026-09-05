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

        // Performance & Query Optimization Indexes
        builder.HasIndex(w => new { w.SprintId, w.Status });
        builder.HasIndex(w => w.AssigneeId);
        builder.HasIndex(w => w.CreatedAtUtc);

        // Explicitly ignore non persisted computed properties
        builder.Ignore(w => w.StatusEnteredAtUtc);
        builder.Ignore(w => w.DaysInCurrentStatus);
        builder.Ignore(w => w.PickupLatencyHours);
        builder.Ignore(w => w.DevCycleTimeHours);
        builder.Ignore(w => w.PrReviewLatencyHours);
        builder.Ignore(w => w.PrMergeLatencyHours);
        builder.Ignore(w => w.QaTestingLatencyHours);
        builder.Ignore(w => w.TotalCycleTimeHours);
    }
}
