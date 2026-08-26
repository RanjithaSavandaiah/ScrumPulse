namespace ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Enums;

public class TeamMember : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public RoleType Role { get; set; } = RoleType.Developer;
    public string Location { get; set; } = "Offshore";
    public string TimeZone { get; set; } = "Asia/Kolkata";
    public string Avatar { get; set; } = string.Empty;
    public int ActiveWipLimit { get; set; } = 3;
    public bool IsActive { get; set; } = true;
}
