namespace ScrumPulse.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScrumPulse.Domain.Entities;

public class PullRequestReviewLogConfiguration : IEntityTypeConfiguration<PullRequestReviewLog>
{
    public void Configure(EntityTypeBuilder<PullRequestReviewLog> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.PrTitle)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(p => p.PrNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Reviewer)
            .WithMany()
            .HasForeignKey(p => p.ReviewerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.Sprint)
            .WithMany()
            .HasForeignKey(p => p.SprintId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.WorkItem)
            .WithMany()
            .HasForeignKey(p => p.WorkItemId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
