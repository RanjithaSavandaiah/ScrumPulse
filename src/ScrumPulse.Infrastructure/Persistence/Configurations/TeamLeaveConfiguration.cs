namespace ScrumPulse.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;

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

        // Map enum properties to text columns with bidirectional int/string parsing
        builder.Property(l => l.LeaveType)
            .HasConversion(
                v => v.ToString(),
                v => ParseLeaveCategory(v));

        builder.Property(l => l.LeaveSlot)
            .HasConversion(
                v => v.ToString(),
                v => ParseLeaveSlotType(v));
    }

    private static LeaveCategory ParseLeaveCategory(string v)
    {
        if (int.TryParse(v, out var n) && Enum.IsDefined(typeof(LeaveCategory), n)) return (LeaveCategory)n;
        if (Enum.TryParse<LeaveCategory>(v, true, out var res)) return res;
        return LeaveCategory.PrivilegeLeave;
    }

    private static LeaveSlotType ParseLeaveSlotType(string v)
    {
        if (int.TryParse(v, out var n) && Enum.IsDefined(typeof(LeaveSlotType), n)) return (LeaveSlotType)n;
        if (Enum.TryParse<LeaveSlotType>(v, true, out var res)) return res;
        return LeaveSlotType.FullDay;
    }
}
