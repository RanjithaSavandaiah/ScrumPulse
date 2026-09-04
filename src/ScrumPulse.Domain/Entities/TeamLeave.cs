namespace ScrumPulse.Domain.Entities;

using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Enums;

/// <summary>
/// Tracks team member leave/PTO with automatic capacity calculation support.
/// </summary>
public class TeamLeave : BaseEntity
{
    public Guid TeamMemberId { get; set; }
    public TeamMember? TeamMember { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public LeaveCategory LeaveType { get; set; } = LeaveCategory.PrivilegeLeave;
    public LeaveSlotType LeaveSlot { get; set; } = LeaveSlotType.FullDay;
    public string Location { get; set; } = "Offshore";
    public bool IsApproved { get; set; } = true;

    public double TotalDays
    {
        get
        {
            if (LeaveSlot == LeaveSlotType.FirstHalf || LeaveSlot == LeaveSlotType.SecondHalf)
                return 0.5;

            int businessDays = 0;
            var cur = StartDate.Date;
            var end = EndDate.Date;
            while (cur <= end)
            {
                if (cur.DayOfWeek != DayOfWeek.Saturday && cur.DayOfWeek != DayOfWeek.Sunday)
                {
                    businessDays++;
                }
                cur = cur.AddDays(1);
            }
            return Math.Max(1, businessDays);
        }
    }
}
