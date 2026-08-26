namespace ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Common;

public class TeamLeave : BaseEntity
{
    public Guid TeamMemberId { get; set; }
    public TeamMember? TeamMember { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string LeaveType { get; set; } = "PTO";
    public string LeaveSlot { get; set; } = "FullDay"; // "FullDay", "FirstHalf", "SecondHalf"
    public string Location { get; set; } = "Offshore";
    public bool IsApproved { get; set; } = true;

    public double TotalDays => LeaveSlot switch
    {
        "FirstHalf" => 0.5,
        "SecondHalf" => 0.5,
        _ => Math.Max(1, (int)(EndDate.Date - StartDate.Date).TotalDays + 1)
    };
}
