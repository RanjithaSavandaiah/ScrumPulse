namespace ScrumPulse.Application.Specifications;

using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;

public class WorkItemsFilterSpecification : BaseSpecification<WorkItem>
{
    public WorkItemsFilterSpecification(Guid? sprintId, WorkItemStatus? status)
        : base(item => (!sprintId.HasValue || item.SprintId == sprintId.Value) &&
                       (!status.HasValue || item.Status == status.Value))
    {
        AddInclude(item => item.Assignee!);
        AddInclude(item => item.PrReviewer!);
        ApplyOrderByDescending(item => item.CreatedAtUtc);
    }
}

public class WorkItemWithRelationsByIdSpecification : BaseSpecification<WorkItem>
{
    public WorkItemWithRelationsByIdSpecification(Guid id)
        : base(item => item.Id == id)
    {
        AddInclude(item => item.Assignee!);
        AddInclude(item => item.PrReviewer!);
        AddInclude(item => item.Sprint!);
    }
}

public class ActiveBlockersSpecification : BaseSpecification<Blocker>
{
    public ActiveBlockersSpecification(Guid? sprintId)
        : base(blocker => (!sprintId.HasValue || blocker.SprintId == sprintId.Value))
    {
        AddInclude(blocker => blocker.RaisedBy!);
        AddInclude(blocker => blocker.WorkItem!);
        ApplyOrderByDescending(blocker => blocker.RaisedAtUtc);
    }
}

public class ActiveTeamMembersSpecification : BaseSpecification<TeamMember>
{
    public ActiveTeamMembersSpecification()
        : base(member => member.IsActive)
    {
        ApplyOrderBy(member => member.Name);
    }
}
