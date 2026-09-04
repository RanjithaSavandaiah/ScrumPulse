namespace ScrumPulse.Infrastructure.Services;

using ScrumPulse.Application.Common.Interfaces;

/// <summary>
/// Scoped implementation of ITenantContext holding the active tenant team context for the current request.
/// </summary>
public class TenantContext : ITenantContext
{
    public Guid? CurrentTeamId { get; set; }
}
