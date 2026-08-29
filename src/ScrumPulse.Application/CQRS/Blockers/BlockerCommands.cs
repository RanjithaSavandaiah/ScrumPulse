namespace ScrumPulse.Application.CQRS.Blockers;

using ScrumPulse.Application.CQRS;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Application.Specifications;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Events;

public record GetBlockersQuery(Guid? SprintId) : IQuery<IEnumerable<BlockerDto>>;

public class GetBlockersQueryHandler(IUnitOfWork unitOfWork) : IQueryHandler<GetBlockersQuery, IEnumerable<BlockerDto>>
{
    public async Task<IEnumerable<BlockerDto>> HandleAsync(GetBlockersQuery query, CancellationToken ct = default)
    {
        var repo = unitOfWork.Repository<Blocker>();
        var blockers = await repo.ListAsync(new ActiveBlockersSpecification(query.SprintId), ct);
        return blockers.ToDtos();
    }
}

public record CreateBlockerCommand(CreateBlockerRequest Request) : ICommand<BlockerDto>;

public class CreateBlockerCommandHandler(IUnitOfWork unitOfWork) : ICommandHandler<CreateBlockerCommand, BlockerDto>
{
    public async Task<BlockerDto> HandleAsync(CreateBlockerCommand command, CancellationToken ct = default)
    {
        var repo = unitOfWork.Repository<Blocker>();
        var blocker = new Blocker
        {
            Title = command.Request.Title,
            Description = command.Request.Description,
            Category = command.Request.Category,
            SlaHoursLimit = command.Request.SlaHoursLimit,
            WorkItemId = command.Request.WorkItemId,
            RaisedById = command.Request.RaisedById,
            SprintId = command.Request.SprintId
        };

        blocker.AddDomainEvent(new BlockerRaisedEvent(
            blocker.Id, blocker.Title, blocker.Category, blocker.SlaHoursLimit, blocker.SprintId, blocker.RaisedById
        ));

        await repo.AddAsync(blocker, ct);
        await unitOfWork.CommitAsync(ct);

        return blocker.ToDto();
    }
}

public record ResolveBlockerCommand(Guid BlockerId, ResolveBlockerRequest Request) : ICommand<BlockerDto?>;

public class ResolveBlockerCommandHandler(IUnitOfWork unitOfWork) : ICommandHandler<ResolveBlockerCommand, BlockerDto?>
{
    public async Task<BlockerDto?> HandleAsync(ResolveBlockerCommand command, CancellationToken ct = default)
    {
        var repo = unitOfWork.Repository<Blocker>();
        var blocker = await repo.GetByIdAsync(command.BlockerId, ct);
        if (blocker == null) return null;

        // Capture SLA breach state BEFORE resolving (once resolved, dynamic calc changes)
        blocker.WasSlaBreachedOnResolution = blocker.HoursWaiting > blocker.SlaHoursLimit;
        blocker.ResolvedAtUtc = DateTime.UtcNow;
        blocker.ResolutionNotes = command.Request.ResolutionNotes;

        blocker.AddDomainEvent(new BlockerResolvedEvent(
            blocker.Id, blocker.Title, blocker.ResolutionNotes, blocker.HoursWaiting, blocker.WasSlaBreachedOnResolution
        ));

        await unitOfWork.CommitAsync(ct);

        return blocker.ToDto();
    }
}

public record UpdateBlockerCommand(Guid BlockerId, CreateBlockerRequest Request) : ICommand<BlockerDto?>;

public class UpdateBlockerCommandHandler(IUnitOfWork unitOfWork) : ICommandHandler<UpdateBlockerCommand, BlockerDto?>
{
    public async Task<BlockerDto?> HandleAsync(UpdateBlockerCommand command, CancellationToken ct = default)
    {
        var repo = unitOfWork.Repository<Blocker>();
        var blocker = await repo.GetByIdAsync(command.BlockerId, ct);
        if (blocker == null) return null;

        blocker.Title = command.Request.Title;
        blocker.Description = command.Request.Description;
        blocker.Category = command.Request.Category;
        blocker.SlaHoursLimit = command.Request.SlaHoursLimit;
        if (command.Request.WorkItemId.HasValue) blocker.WorkItemId = command.Request.WorkItemId;
        if (command.Request.RaisedById != Guid.Empty) blocker.RaisedById = command.Request.RaisedById;
        if (command.Request.SprintId.HasValue) blocker.SprintId = command.Request.SprintId;

        await unitOfWork.CommitAsync(ct);

        return blocker.ToDto();
    }
}

public record DeleteBlockerCommand(Guid BlockerId) : ICommand<bool>;

public class DeleteBlockerCommandHandler(IUnitOfWork unitOfWork) : ICommandHandler<DeleteBlockerCommand, bool>
{
    public async Task<bool> HandleAsync(DeleteBlockerCommand command, CancellationToken ct = default)
    {
        var repo = unitOfWork.Repository<Blocker>();
        var blocker = await repo.GetByIdAsync(command.BlockerId, ct);
        if (blocker == null) return false;

        await repo.DeleteAsync(blocker, ct);
        await unitOfWork.CommitAsync(ct);
        return true;
    }
}
