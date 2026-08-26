namespace ScrumPulse.Application.DTOs;

using System.ComponentModel.DataAnnotations;

public record TeamLeaveDto(
    Guid Id,
    Guid TeamMemberId,
    string TeamMemberName,
    DateTime StartDate,
    DateTime EndDate,
    string Reason,
    string LeaveType,
    string Location,
    bool IsApproved,
    double TotalDays,
    string LeaveSlot = "FullDay"
);

public record SubmitLeaveRequest(
    [Required] Guid TeamMemberId,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    string? Reason = "Planned Leave",
    string? LeaveType = "Privilege Leave",
    string? Location = "Offshore",
    string LeaveSlot = "FullDay"
);
