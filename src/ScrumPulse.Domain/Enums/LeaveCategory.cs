namespace ScrumPulse.Domain.Enums;

/// <summary>
/// Categorizes the type of team member leave.
/// </summary>
public enum LeaveCategory
{
    PrivilegeLeave = 0,
    SickLeave = 1,
    PlannedTimeOff = 2,
    CompensatoryOff = 3,
    PublicHoliday = 4,
    Unpaid = 5
}
