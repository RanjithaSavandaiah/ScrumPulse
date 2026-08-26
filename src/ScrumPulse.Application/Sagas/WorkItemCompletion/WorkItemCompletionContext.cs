namespace ScrumPulse.Application.Sagas.WorkItemCompletion;

using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;

public class WorkItemCompletionContext
{
    public Guid WorkItemId { get; set; }
    public DateTime? CustomTimestampUtc { get; set; }
    public Guid? ReviewerId { get; set; }
    
    // State tracked across saga steps
    public WorkItem? WorkItem { get; set; }
    public WorkItemStatus OriginalStatus { get; set; }
    public DateTime? OriginalCompletedAt { get; set; }
    public bool OriginalDodStagingVerified { get; set; }
    public int DeliveredPointsAdded { get; set; }
    public Sprint? Sprint { get; set; }
    public WorkItemDto? OutputDto { get; set; }
    public bool QualityGatesPassed { get; set; }
    public bool AiEvaluationTriggered { get; set; }
}
