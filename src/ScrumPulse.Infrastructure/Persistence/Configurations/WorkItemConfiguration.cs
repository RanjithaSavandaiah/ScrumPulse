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

        builder.Property(w => w.ExternalId)
            .HasMaxLength(64);

        builder.Property(w => w.BoardState)
            .HasMaxLength(100);

        builder.Property(w => w.ExternalUrl)
            .HasMaxLength(500);

        // Performance & Query Optimization Indexes
        builder.HasIndex(w => new { w.SprintId, w.Status });
        builder.HasIndex(w => w.AssigneeId);
        builder.HasIndex(w => w.CreatedAtUtc);

        // Every sync looks each item up by its board identifier before deciding to insert or
        // update, so without this the import degrades to a table scan per item. Scoped by team
        // because two squads may connect to the same project and see the same item numbers.
        // Not unique: soft delete means a removed item can still hold the same identifier.
        builder.HasIndex(w => new { w.TeamId, w.Origin, w.ExternalId });

        // Explicitly ignore non persisted computed properties
        builder.Ignore(w => w.IsReadOnlyFromBoard);
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
