namespace ScrumPulse.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScrumPulse.Domain.Entities;

public class TeamLeaveConfiguration : IEntityTypeConfiguration<TeamLeave>
{
    public void Configure(EntityTypeBuilder<TeamLeave> builder)
    {
        builder.HasKey(l => l.Id);

        builder.HasOne(l => l.TeamMember)
            .WithMany()
            .HasForeignKey(l => l.TeamMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // Performance & Capacity Calculation Indexes
        builder.HasIndex(l => new { l.TeamMemberId, l.StartDate, l.EndDate });
        builder.HasIndex(l => new { l.IsApproved, l.StartDate, l.EndDate });
        builder.Ignore(l => l.TotalDays);
    }
}
