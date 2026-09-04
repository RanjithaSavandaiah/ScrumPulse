namespace ScrumPulse.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScrumPulse.Domain.Entities;

public class DailyStandupConfiguration : IEntityTypeConfiguration<DailyStandup>
{
    public void Configure(EntityTypeBuilder<DailyStandup> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasOne(s => s.TeamMember)
            .WithMany()
            .HasForeignKey(s => s.TeamMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // Performance & Standup History Indexes
        builder.HasIndex(s => new { s.SprintId, s.StandupDate });
        builder.HasIndex(s => new { s.TeamMemberId, s.StandupDate });
    }
}
