namespace ScrumPulse.Application.CQRS.Teams;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Entities;

// ── Queries ──────────────────────────────────────────────────────────────

public record GetTeamsQuery : IQuery<IEnumerable<TeamDto>>;

public class GetTeamsQueryHandler(IAppDbContext db) : IQueryHandler<GetTeamsQuery, IEnumerable<TeamDto>>
{
    public async Task<IEnumerable<TeamDto>> HandleAsync(GetTeamsQuery query, CancellationToken ct = default)
    {
        var teams = await db.Teams
            .Where(team => team.IsActive)
            .OrderBy(team => team.Name)
            .AsNoTracking()
            .ToListAsync(ct);

        return teams.ToDtos();
    }
}

public record GetTeamByIdQuery(Guid Id) : IQuery<TeamDto?>;

public class GetTeamByIdQueryHandler(IAppDbContext db) : IQueryHandler<GetTeamByIdQuery, TeamDto?>
{
    public async Task<TeamDto?> HandleAsync(GetTeamByIdQuery query, CancellationToken ct = default)
    {
        var team = await db.Teams.FirstOrDefaultAsync(teamEntity => teamEntity.Id == query.Id, ct);
        return team?.ToDto();
    }
}

public record GetQualityGatesQuery(Guid TeamId) : IQuery<ConfigureTeamGatesRequest?>;

public class GetQualityGatesQueryHandler(IAppDbContext db) : IQueryHandler<GetQualityGatesQuery, ConfigureTeamGatesRequest?>
{
    public async Task<ConfigureTeamGatesRequest?> HandleAsync(GetQualityGatesQuery query, CancellationToken ct = default)
    {
        var team = await db.Teams.FirstOrDefaultAsync(teamEntity => teamEntity.Id == query.TeamId, ct);
        if (team == null) return null;

        return new ConfigureTeamGatesRequest(
            MappingExtensions.GetTeamDorCriteria(team),
            MappingExtensions.GetTeamDodCriteria(team)
        );
    }
}

// ── Commands ─────────────────────────────────────────────────────────────

public record CreateTeamCommand(CreateTeamRequest Request) : ICommand<Result<TeamDto>>;

public class CreateTeamCommandHandler(IAppDbContext db) : ICommandHandler<CreateTeamCommand, Result<TeamDto>>
{
    private const int MinSlugRandomSuffix = 100;
    private const int MaxSlugRandomSuffix = 1000;
    private const int MinFallbackSlugRandom = 1000;
    private const int MaxFallbackSlugRandom = 10000;
    private const int JoinCodeLength = 6;

    public async Task<Result<TeamDto>> HandleAsync(CreateTeamCommand command, CancellationToken ct = default)
    {
        var request = command.Request;
        var trimmedName = request.Name.Trim();
        var existingActiveTeam = await db.Teams
            .FirstOrDefaultAsync(team => team.IsActive && team.Name.ToLower() == trimmedName.ToLower(), ct);
        if (existingActiveTeam != null)
        {
            return Result<TeamDto>.Failure($"A squad named '{trimmedName}' already exists.", "CONFLICT");
        }

        var slug = GenerateSlug(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug);
        var existingSlug = await db.Teams.AnyAsync(team => team.Slug == slug, ct);
        if (existingSlug)
        {
            slug = $"{slug}-{RandomNumberGenerator.GetInt32(MinSlugRandomSuffix, MaxSlugRandomSuffix)}";
        }

        var joinCode = GenerateJoinCode();

        var team = new Team
        {
            Name = trimmedName,
            Slug = slug,
            Description = request.Description?.Trim() ?? string.Empty,
            JoinCode = joinCode,
            IsActive = true
        };

        db.Teams.Add(team);
        await db.SaveChangesAsync(ct);

        return Result<TeamDto>.Success(team.ToDto());
    }

    private static string GenerateSlug(string input)
    {
        var slug = input.ToLowerInvariant().Trim();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? $"team-{RandomNumberGenerator.GetInt32(MinFallbackSlugRandom, MaxFallbackSlugRandom)}" : slug;
    }

    private static string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(JoinCodeLength);
        var result = new char[JoinCodeLength];
        for (int characterIndex = 0; characterIndex < JoinCodeLength; characterIndex++)
        {
            result[characterIndex] = chars[bytes[characterIndex] % chars.Length];
        }
        return new string(result);
    }
}

public record JoinTeamCommand(JoinTeamRequest Request) : ICommand<Result<TeamDto>>;

public class JoinTeamCommandHandler(IAppDbContext db) : ICommandHandler<JoinTeamCommand, Result<TeamDto>>
{
    public async Task<Result<TeamDto>> HandleAsync(JoinTeamCommand command, CancellationToken ct = default)
    {
        var code = command.Request.JoinCode.Trim().ToUpperInvariant();
        var team = await db.Teams.FirstOrDefaultAsync(teamEntity => teamEntity.JoinCode == code && teamEntity.IsActive, ct);
        if (team == null)
        {
            return Result<TeamDto>.Failure("No active team found with the specified join code.", "NOT_FOUND");
        }

        return Result<TeamDto>.Success(team.ToDto());
    }
}

public record UpdateTeamCommand(Guid Id, CreateTeamRequest Request) : ICommand<TeamDto?>;

public class UpdateTeamCommandHandler(IAppDbContext db) : ICommandHandler<UpdateTeamCommand, TeamDto?>
{
    public async Task<TeamDto?> HandleAsync(UpdateTeamCommand command, CancellationToken ct = default)
    {
        var team = await db.Teams.FirstOrDefaultAsync(teamEntity => teamEntity.Id == command.Id, ct);
        if (team == null) return null;

        team.Name = command.Request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(command.Request.Description))
            team.Description = command.Request.Description.Trim();

        await db.SaveChangesAsync(ct);
        return team.ToDto();
    }
}

public record ConfigureQualityGatesCommand(Guid Id, ConfigureTeamGatesRequest Request) : ICommand<Result<TeamDto>>;

public class ConfigureQualityGatesCommandHandler(IAppDbContext db) : ICommandHandler<ConfigureQualityGatesCommand, Result<TeamDto>>
{
    public async Task<Result<TeamDto>> HandleAsync(ConfigureQualityGatesCommand command, CancellationToken ct = default)
    {
        var request = command.Request;
        if (request.DorCriteria == null || request.DorCriteria.Count == 0)
        {
            return Result<TeamDto>.Failure("At least one Definition of Ready (DoR) criterion is required.", "BAD_REQUEST");
        }

        if (request.DodCriteria == null || request.DodCriteria.Count == 0)
        {
            return Result<TeamDto>.Failure("At least one Definition of Done (DoD) criterion is required.", "BAD_REQUEST");
        }

        var team = await db.Teams.FirstOrDefaultAsync(teamEntity => teamEntity.Id == command.Id, ct);
        if (team == null)
        {
            return Result<TeamDto>.Failure("Team not found", "NOT_FOUND");
        }

        var sanitizedDor = request.DorCriteria.Select(c =>
            c with { Id = string.IsNullOrWhiteSpace(c.Id) ? $"dor-{Guid.NewGuid():N}" : c.Id.Trim(), Label = c.Label.Trim() }).ToList();
        var sanitizedDod = request.DodCriteria.Select(c =>
            c with { Id = string.IsNullOrWhiteSpace(c.Id) ? $"dod-{Guid.NewGuid():N}" : c.Id.Trim(), Label = c.Label.Trim() }).ToList();

        team.DorChecklistJson = JsonSerializer.Serialize(sanitizedDor);
        team.DodChecklistJson = JsonSerializer.Serialize(sanitizedDod);

        await db.SaveChangesAsync(ct);
        return Result<TeamDto>.Success(team.ToDto());
    }
}
