namespace ScrumPulse.Tests.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScrumPulse.Api.Controllers;
using ScrumPulse.Application.CQRS;
using ScrumPulse.Application.CQRS.Sprints;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Services;
using Xunit;

public class ExtendedControllerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ScrumPulse_ExtControllerTestDb_{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private static IMediator CreateMediatorForSprints(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IQueryHandler<GetSprintsQuery, IEnumerable<SprintDto>>>(new GetSprintsQueryHandler(db));
        services.AddSingleton<ICommandHandler<CreateSprintCommand, SprintDto>>(new CreateSprintCommandHandler(db));
        services.AddSingleton<ICommandHandler<UpdateSprintCommand, SprintDto?>>(new UpdateSprintCommandHandler(db));
        services.AddSingleton<ICommandHandler<DeleteSprintCommand, bool>>(new DeleteSprintCommandHandler(db));
        services.AddSingleton<ICommandHandler<ActivateSprintCommand, SprintDto?>>(new ActivateSprintCommandHandler(db));
        services.AddSingleton<ICommandHandler<UpdateConfidenceCommand, SprintDto?>>(new UpdateConfidenceCommandHandler(db));
        return new AppMediator(services.BuildServiceProvider());
    }

    private static T ExtractValue<T>(ActionResult<T> actionResult) where T : class
    {
        if (actionResult.Value != null) return actionResult.Value;
        if (actionResult.Result is OkObjectResult okResult && okResult.Value is T directValue) return directValue;
        if (actionResult.Result is CreatedAtActionResult createdResult && createdResult.Value is T createdValue) return createdValue;
        throw new InvalidOperationException($"Could not extract value of type {typeof(T).Name} from ActionResult");
    }

    [Fact]
    public async Task TechHubController_TechDebt_And_TechTalks_FullLifecycle()
    {
        using var db = CreateInMemoryDb();
        var controller = new TechHubController(db);

        var presenter = new TeamMember { Id = Guid.NewGuid(), Name = "Dev Presenter", IsActive = true };
        db.TeamMembers.Add(presenter);
        await db.SaveChangesAsync();

        // 1. Create Tech Debt
        var createDebtRequest = new CreateTechDebtRequest(
            Title: "Upgrade Entity Framework Core to 10.0.1",
            Description: "Utilize compiled query performance enhancements",
            Severity: TechDebtSeverity.High,
            EstimatedHours: 8,
            PayoffSprintId: null,
            AssigneeId: presenter.Id
        );
        var createDebtResult = await controller.CreateTechDebt(createDebtRequest, Ct);
        var debtDto = ExtractValue(createDebtResult);
        Assert.Equal("Upgrade Entity Framework Core to 10.0.1", debtDto.Title);
        Assert.Equal(TechDebtSeverity.High, debtDto.Severity);

        // 2. Update Tech Debt
        var updateDebtRequest = new UpdateTechDebtRequest(
            Title: "Upgrade Entity Framework Core to 10.0.1 & Benchmark",
            Description: "Benchmarked memory allocations across endpoints",
            Severity: TechDebtSeverity.Critical,
            EstimatedHours: 12,
            Status: TechDebtStatus.InProgress,
            PayoffSprintId: null,
            AssigneeId: presenter.Id
        );
        var updateDebtResult = await controller.UpdateTechDebt(debtDto.Id, updateDebtRequest, Ct);
        var updatedDebtDto = ExtractValue(updateDebtResult);
        Assert.Equal(TechDebtSeverity.Critical, updatedDebtDto.Severity);
        Assert.Equal(12, updatedDebtDto.EstimatedHours);

        // 3. Resolve Tech Debt
        var resolveResult = await controller.ResolveTechDebt(debtDto.Id, new ResolveTechDebtRequest(TechDebtStatus.Resolved), Ct);
        var resolvedDebtDto = ExtractValue(resolveResult);
        Assert.Equal(TechDebtStatus.Resolved, resolvedDebtDto.Status);

        // 4. Delete Tech Debt
        var deleteDebtResult = await controller.DeleteTechDebt(debtDto.Id, Ct);
        Assert.IsType<NoContentResult>(deleteDebtResult);

        // 5. Schedule Tech Talk
        var createTalkRequest = new CreateTechTalkRequest(
            Topic: "Playwright End-to-End Automation Strategies",
            PresenterId: presenter.Id,
            TalkDate: DateTime.UtcNow.AddDays(7),
            DurationMinutes: 45,
            SlidesUrl: "https://slides.example.com/playwright",
            KeyTakeaways: "Deterministic locators, auto-waiting, and headless execution."
        );
        var createTalkResult = await controller.CreateTechTalk(createTalkRequest, Ct);
        var talkDto = ExtractValue(createTalkResult);
        Assert.Equal("Playwright End-to-End Automation Strategies", talkDto.Topic);
        Assert.Equal(45, talkDto.DurationMinutes);

        // 6. Update Tech Talk
        var updateTalkRequest = new UpdateTechTalkRequest(
            Topic: "Playwright E2E & Browser Console Sentinels",
            PresenterId: presenter.Id,
            TalkDate: DateTime.UtcNow.AddDays(7),
            DurationMinutes: 60,
            SlidesUrl: "https://slides.example.com/playwright-sentinel",
            KeyTakeaways: "Zero console error enforcement in CD pipeline."
        );
        var updateTalkResult = await controller.UpdateTechTalk(talkDto.Id, updateTalkRequest, Ct);
        var updatedTalkDto = ExtractValue(updateTalkResult);
        Assert.Equal(60, updatedTalkDto.DurationMinutes);
        Assert.Contains("Console Sentinels", updatedTalkDto.Topic);

        // 7. Delete Tech Talk
        var deleteTalkResult = await controller.DeleteTechTalk(talkDto.Id, Ct);
        Assert.IsType<NoContentResult>(deleteTalkResult);
    }

    [Fact]
    public async Task MonthlyFeedbackController_Update_And_Delete_Lifecycle()
    {
        using var db = CreateInMemoryDb();
        var controller = new MonthlyFeedbackController(db);

        var member = new TeamMember { Id = Guid.NewGuid(), Name = "Kiran Dev", IsActive = true };
        db.TeamMembers.Add(member);
        await db.SaveChangesAsync();

        // 1. Submit
        var submitRequest = new SubmitMonthlyFeedbackRequest(
            TeamMemberId: member.Id,
            MonthYear: "2026-09",
            ScrumMasterFeedback: "High story point velocity",
            CdlFeedback: "Architectural alignment",
            ClientFeedback: "Clear demo delivery",
            SelfReflection: "Met all personal sprint goals",
            SmRating: 4,
            HappinessIndex: 4,
            ActionItems: "Pair program on microservices",
            NextMonthGoals: "Deliver distributed tenant switcher"
        );
        var submitResult = await controller.Submit(submitRequest, Ct);
        var feedbackDto = ExtractValue(submitResult);
        Assert.Equal(4, feedbackDto.SmRating);

        // 2. Update
        var updateRequest = new SubmitMonthlyFeedbackRequest(
            TeamMemberId: member.Id,
            MonthYear: "2026-09",
            ScrumMasterFeedback: "High story point velocity - Promoted to Squad Lead",
            CdlFeedback: "Architectural alignment",
            ClientFeedback: "Clear demo delivery",
            SelfReflection: "Met all personal sprint goals",
            SmRating: 5,
            HappinessIndex: 5,
            ActionItems: "Pair program on microservices",
            NextMonthGoals: "Deliver distributed tenant switcher"
        );
        var updateResult = await controller.Update(feedbackDto.Id, updateRequest, Ct);
        var updatedDto = ExtractValue(updateResult);
        Assert.Equal(5, updatedDto.SmRating);
        Assert.Equal(5, updatedDto.HappinessIndex);
        Assert.Contains("Promoted to Squad Lead", updatedDto.ScrumMasterFeedback);

        // 3. Delete
        var deleteResult = await controller.Delete(feedbackDto.Id, Ct);
        Assert.IsType<NoContentResult>(deleteResult);

        var afterDelete = await controller.GetAll(null, Ct);
        var afterDeleteOk = Assert.IsType<OkObjectResult>(afterDelete.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<MonthlyFeedbackDto>>(afterDeleteOk.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task SprintsController_Create_Update_And_Delete_Succeed()
    {
        using var db = CreateInMemoryDb();
        var controller = new SprintsController(CreateMediatorForSprints(db));

        // 1. Validation: EndDate < StartDate should return BadRequest
        var invalidRequest = new CreateSprintRequest(
            Name: "Invalid Sprint",
            Goal: "Fail validation",
            StartDate: DateTime.UtcNow,
            EndDate: DateTime.UtcNow.AddDays(-5),
            IsActive: false,
            CommittedStoryPoints: 20,
            ConfidenceScore: 8,
            ConfidenceNotes: "None",
            DailyWorkingHours: 7.5
        );
        var badResult = await controller.Create(invalidRequest, Ct);
        Assert.IsType<BadRequestObjectResult>(badResult.Result);

        // 2. Valid Create
        var validRequest = new CreateSprintRequest(
            Name: "Sprint 42",
            Goal: "Deploy multi-squad resilience pipeline",
            StartDate: DateTime.UtcNow,
            EndDate: DateTime.UtcNow.AddDays(14),
            IsActive: true,
            CommittedStoryPoints: 45,
            ConfidenceScore: 9,
            ConfidenceNotes: "Team at full capacity",
            DailyWorkingHours: 8.0
        );
        var createResult = await controller.Create(validRequest, Ct);
        var sprintDto = ExtractValue(createResult);
        Assert.Equal("Sprint 42", sprintDto.Name);
        Assert.True(sprintDto.IsActive);

        // 3. Update Sprint
        var updateRequest = new UpdateSprintRequest(
            Name: "Sprint 42 - Extended",
            Goal: "Deploy multi-squad resilience pipeline and automated E2E gate",
            StartDate: DateTime.UtcNow,
            EndDate: DateTime.UtcNow.AddDays(16),
            CommittedStoryPoints: 50,
            DeliveredStoryPoints: 48,
            ConfidenceScore: 10,
            ConfidenceNotes: "All risk mitigations completed",
            DailyWorkingHours: 8.0
        );
        var updateResult = await controller.Update(sprintDto.Id, updateRequest, Ct);
        var updatedDto = ExtractValue(updateResult);
        Assert.Equal("Sprint 42 - Extended", updatedDto.Name);
        Assert.Equal(50, updatedDto.CommittedStoryPoints);

        // 4. Delete Sprint
        var deleteResult = await controller.Delete(sprintDto.Id, Ct);
        Assert.IsType<NoContentResult>(deleteResult);
    }

    [Fact]
    public async Task TeamMembersController_Create_Update_And_Delete_WorkCorrectly()
    {
        using var db = CreateInMemoryDb();
        var controller = new TeamMembersController(db);

        // 1. Create Team Member
        var createRequest = new CreateTeamMemberRequest(
            Name: "Siddharth Rao",
            Email: "siddharth.rao@scrumpulse.com",
            Role: RoleType.Developer,
            Location: "Bangalore Offshore",
            TimeZone: "Asia/Kolkata",
            Avatar: "SR",
            ActiveWipLimit: 3,
            TeamId: null
        );
        var createResult = await controller.Create(createRequest, Ct);
        var memberDto = ExtractValue(createResult);
        Assert.Equal("Siddharth Rao", memberDto.Name);
        Assert.Equal(RoleType.Developer, memberDto.Role);

        // 2. Update Member
        var updateRequest = new UpdateTeamMemberRequest(
            Name: "Siddharth Rao (Lead)",
            Email: "siddharth.rao@scrumpulse.com",
            Role: RoleType.ScrumMaster,
            Location: "Bangalore Offshore",
            TimeZone: "Asia/Kolkata",
            Avatar: "SR",
            ActiveWipLimit: 4,
            TeamId: null
        );
        var updateResult = await controller.Update(memberDto.Id, updateRequest, Ct);
        var updatedDto = ExtractValue(updateResult);
        Assert.Equal("Siddharth Rao (Lead)", updatedDto.Name);
        Assert.Equal(RoleType.ScrumMaster, updatedDto.Role);

        // 3. Delete Member
        var deleteResult = await controller.Delete(memberDto.Id, Ct);
        Assert.IsType<NoContentResult>(deleteResult);
    }
}
