namespace ScrumPulse.Tests.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ScrumPulse.AI.Services;
using ScrumPulse.Api.Controllers;
using ScrumPulse.Application.CQRS;
using ScrumPulse.Application.CQRS.Blockers;
using ScrumPulse.Application.CQRS.WorkItems;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Sagas.WorkItemCompletion;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Repositories;
using ScrumPulse.Infrastructure.Services;
using Xunit;

public class ControllerTests
{
    private (AppDbContext db, IMediator mediator, IIdempotencyStore store, IUnitOfWork uow) CreateTestServices()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ScrumPulse_ControllerTestDb_{Guid.NewGuid()}")
            .Options;

        var db = new AppDbContext(options);
        var eventDispatcher = new DomainEventDispatcher(NullLogger<DomainEventDispatcher>.Instance);
        var uow = new EfUnitOfWork(db, eventDispatcher);
        var store = new MemoryIdempotencyStore();
        var aiService = new MicrosoftAgentService(db);

        var step1 = new ValidateQualityGatesStep(uow);
        var step2 = new TransitionWorkItemStatusStep(uow);
        var step3 = new RecalculateSprintVelocityStep(uow);
        var step4 = new TriggerMicrosoftAgentAiCoachingStep(aiService);
        var saga = new WorkItemCompletionSaga(step1, step2, step3, step4, uow);

        var services = new ServiceCollection();
        services.AddSingleton<IUnitOfWork>(uow);
        services.AddSingleton(saga);
        services.AddSingleton<IQueryHandler<GetWorkItemsQuery, IEnumerable<WorkItemDto>>>(new GetWorkItemsQueryHandler(uow));
        services.AddSingleton<ICommandHandler<CreateWorkItemCommand, WorkItemDto>>(new CreateWorkItemCommandHandler(uow));
        services.AddSingleton<ICommandHandler<AdvanceWorkItemStageCommand, WorkItemDto>>(new AdvanceWorkItemStageCommandHandler(uow, saga));
        services.AddSingleton<IQueryHandler<GetBlockersQuery, IEnumerable<BlockerDto>>>(new GetBlockersQueryHandler(uow));
        services.AddSingleton<ICommandHandler<CreateBlockerCommand, BlockerDto>>(new CreateBlockerCommandHandler(uow));
        services.AddSingleton<ICommandHandler<ResolveBlockerCommand, BlockerDto?>>(new ResolveBlockerCommandHandler(uow));

        var provider = services.BuildServiceProvider();
        var mediator = new AppMediator(provider);

        return (db, mediator, store, uow);
    }

    private T ExtractValue<T>(ActionResult<T> actionResult) where T : class
    {
        if (actionResult.Value != null) return actionResult.Value;
        if (actionResult.Result is OkObjectResult okResult && okResult.Value is T directValue) return directValue;
        if (actionResult.Result is CreatedAtActionResult createdResult && createdResult.Value is T createdValue) return createdValue;
        throw new InvalidOperationException($"Could not extract value of type {typeof(T).Name} from ActionResult");
    }

    [Fact]
    public async Task WorkItemsController_CRUD_AndStageTransitions_Succeed()
    {
        var (db, mediator, store, _) = CreateTestServices();
        var controller = new WorkItemsController(mediator, store, db);

        // 1. Create Work Item
        var createRequest = new CreateWorkItemRequest(
            Title: "Build OAuth Flow",
            Description: "Implement PKCE auth for SPA",
            Type: WorkItemType.UserStory,
            Priority: PriorityLevel.High,
            StoryPoints: 5,
            AssigneeId: null,
            SprintId: null,
            PrNumber: null,
            PrUrl: null,
            PrBranch: null,
            TargetBranch: "main"
        );

        var createResult = await controller.Create(createRequest, "idemp-key-1");
        var createdDto = ExtractValue(createResult);
        Assert.Equal("Build OAuth Flow", createdDto.Title);

        // 2. Advance Stage to InProgress
        var advanceRequest = new AdvanceStageRequest(WorkItemStatus.InProgress);
        var advanceResult = await controller.AdvanceStage(createdDto.Id, advanceRequest);
        var updatedDto = ExtractValue(advanceResult);
        Assert.Equal(WorkItemStatus.InProgress, updatedDto.Status);

        // 3. Update Quality Gates
        var gatesRequest = new UpdateQualityGatesRequest(
            DorAcceptanceCriteria: true,
            DorDependencies: true,
            DorWireframe: true,
            DodUnitTests: true,
            DodPeerReview: true,
            DodMergedToMaster: true,
            DodStagingVerified: true
        );
        var gatesResult = await controller.UpdateQualityGates(createdDto.Id, gatesRequest);
        var gatesDto = ExtractValue(gatesResult);
        Assert.True(gatesDto.DodUnitTestsPassed);

        // 4. GetAll
        var getAllResult = await controller.GetAll(null, null);
        var allOk = Assert.IsType<OkObjectResult>(getAllResult.Result);
        var allItems = Assert.IsAssignableFrom<IEnumerable<WorkItemDto>>(allOk.Value);
        Assert.Single(allItems);

        // 5. Create Work Item with 0 story points and 15 hours estimation
        var zeroPointRequest = new CreateWorkItemRequest(
            Title: "Backend refactor with hours",
            Description: "Team uses hour estimation",
            Type: WorkItemType.TaskPbi,
            Priority: PriorityLevel.Medium,
            StoryPoints: 0,
            AssigneeId: null,
            SprintId: null,
            PrNumber: null,
            PrUrl: null,
            PrBranch: null,
            TargetBranch: "main",
            EstimatedHours: 15.0
        );
        var zeroPointResult = await controller.Create(zeroPointRequest, "idemp-key-zero-pts");
        var zeroPointDto = ExtractValue(zeroPointResult);
        Assert.Equal(0, zeroPointDto.StoryPoints);
        Assert.Equal(15.0, zeroPointDto.EstimatedHours);
    }

    [Fact]
    public async Task BlockersController_CreateAndResolve_WorksAccurately()
    {
        var (db, mediator, store, _) = CreateTestServices();
        var controller = new BlockersController(mediator, store);
        var memberId = Guid.NewGuid();

        var createRequest = new CreateBlockerRequest(
            Title: "Waiting for database credentials",
            Description: "Need staging connection string from DevOps",
            Category: BlockerCategory.EnvironmentAccess,
            SlaHoursLimit: 4,
            WorkItemId: null,
            RaisedById: memberId,
            SprintId: null
        );

        var createResult = await controller.Create(createRequest, "idemp-blocker-1");
        var blockerDto = ExtractValue(createResult);
        Assert.False(blockerDto.IsResolved);

        // Resolve
        var resolveRequest = new ResolveBlockerRequest("Credentials provided via Azure KeyVault");
        var resolveResult = await controller.Resolve(blockerDto.Id, resolveRequest);
        var resolvedDto = ExtractValue(resolveResult);
        Assert.True(resolvedDto.IsResolved);

        var resolvedBlocker = await db.Blockers.FindAsync(blockerDto.Id);
        Assert.NotNull(resolvedBlocker);
        Assert.True(resolvedBlocker.IsResolved);
        Assert.Equal("Credentials provided via Azure KeyVault", resolvedBlocker.ResolutionNotes);
    }

    [Fact]
    public async Task StandupsController_SubmitAndGetAll_Succeeds()
    {
        var (db, _, _, _) = CreateTestServices();
        var controller = new StandupsController(db);
        var member = new TeamMember { Id = Guid.NewGuid(), Name = "Aarav Gupta" };
        db.TeamMembers.Add(member);
        await db.SaveChangesAsync();

        var request = new SubmitStandupRequest(
            TeamMemberId: member.Id,
            YesterdaySummary: "Created Dockerfile",
            TodayPlan: "Setup CI GitHub Actions",
            BlockersText: "None",
            MoodScore: 5,
            SprintId: null
        );

        var submitResult = await controller.Submit(request);
        var standupDto = ExtractValue(submitResult);
        Assert.Equal("Aarav Gupta", standupDto.TeamMemberName);

        var allResult = await controller.GetAll(null, null, null);
        var allOk = Assert.IsType<OkObjectResult>(allResult.Result);
        var standups = Assert.IsAssignableFrom<IEnumerable<DailyStandupDto>>(allOk.Value);
        Assert.Single(standups);
    }

    [Fact]
    public async Task LeavesController_SubmitAndGetCapacity_Succeeds()
    {
        var (db, _, _, _) = CreateTestServices();
        var sprint = new Sprint { Id = Guid.NewGuid(), Name = "Sprint 1", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(10) };
        var member = new TeamMember { Id = Guid.NewGuid(), Name = "Rohan Verma", IsActive = true };
        db.Sprints.Add(sprint);
        db.TeamMembers.Add(member);
        await db.SaveChangesAsync();

        var metricsService = new MetricsCalculatorService(db);
        var controller = new LeavesController(db, metricsService);

        var request = new SubmitLeaveRequest(
            TeamMemberId: member.Id,
            StartDate: DateTime.UtcNow,
            EndDate: DateTime.UtcNow.AddDays(2),
            Reason: "Medical checkup",
            LeaveType: "Sick Leave",
            Location: "Bangalore Offshore"
        );

        var submitResult = await controller.Submit(request);
        var leaveDto = ExtractValue(submitResult);
        Assert.Equal("Rohan Verma", leaveDto.TeamMemberName);

        var capacityResult = await controller.GetCapacity(sprint.Id);
        var capacityDto = ExtractValue(capacityResult);
        Assert.NotNull(capacityDto);
    }

    [Fact]
    public async Task MonthlyFeedbackController_SubmitAndGetAll_Succeeds()
    {
        var (db, _, _, _) = CreateTestServices();
        var controller = new MonthlyFeedbackController(db);
        var member = new TeamMember { Id = Guid.NewGuid(), Name = "Deepa Nair" };
        db.TeamMembers.Add(member);
        await db.SaveChangesAsync();

        var request = new SubmitMonthlyFeedbackRequest(
            TeamMemberId: member.Id,
            MonthYear: "August 2026",
            ScrumMasterFeedback: "Outstanding sprint cadence",
            CdlFeedback: "Ready for Senior Developer promotion track",
            ClientFeedback: "Strong communication during demo",
            SelfReflection: "Met all velocity goals",
            SmRating: 9,
            HappinessIndex: 9,
            ActionItems: "Lead next architectural spike",
            NextMonthGoals: "Deliver microservice refactoring"
        );

        var submitResult = await controller.Submit(request);
        var feedbackDto = ExtractValue(submitResult);
        Assert.Equal(9, feedbackDto.SmRating);

        var allResult = await controller.GetAll(null);
        var allOk = Assert.IsType<OkObjectResult>(allResult.Result);
        var feedbacks = Assert.IsAssignableFrom<IEnumerable<MonthlyFeedbackDto>>(allOk.Value);
        Assert.Single(feedbacks);
    }

    [Fact]
    public async Task RetrospectivesController_CardsAndActions_Succeed()
    {
        var (db, _, _, _) = CreateTestServices();
        var controller = new RetrospectivesController(db);

        // 1. Create Card
        var cardRequest = new CreateRetroCardRequest(
            SprintId: null,
            Category: RetroCategory.Ideas,
            Content: "Use Playwright for end-to-end testing",
            AuthorId: null,
            IsAnonymous: true
        );
        var cardResult = await controller.CreateCard(cardRequest);
        var cardDto = ExtractValue(cardResult);
        Assert.Equal("Anonymous", cardDto.AuthorName);

        // 2. Vote on Card
        var voteResult = await controller.VoteCard(cardDto.Id);
        var voteOk = Assert.IsType<OkObjectResult>(voteResult);
        Assert.NotNull(voteOk.Value);

        // 3. Update Card
        var updateCardRequest = new UpdateRetroCardRequest(
            SprintId: null,
            Category: RetroCategory.WentWell,
            Content: "Updated: Use Playwright and Cypress for test suites",
            AuthorId: null,
            IsAnonymous: true
        );
        var updateCardResult = await controller.UpdateCard(cardDto.Id, updateCardRequest);
        var updatedCardDto = ExtractValue(updateCardResult);
        Assert.Equal("Updated: Use Playwright and Cypress for test suites", updatedCardDto.Content);

        // 4. Create Action Item
        var actionRequest = new CreateRetroActionItemRequest(
            SprintId: null,
            Title: "Setup Playwright scaffold",
            AssigneeId: null,
            DueDate: DateTime.UtcNow.AddDays(7)
        );
        var actionResult = await controller.CreateActionItem(actionRequest);
        var actionDto = ExtractValue(actionResult);
        Assert.False(actionDto.IsCompleted);

        // 5. Update Action Item
        var updateActionRequest = new UpdateRetroActionItemRequest(
            SprintId: null,
            Title: "Setup Playwright scaffold and CI steps",
            AssigneeId: null,
            DueDate: DateTime.UtcNow.AddDays(10),
            IsCompleted: true
        );
        var updateActionResult = await controller.UpdateActionItem(actionDto.Id, updateActionRequest);
        var updatedActionDto = ExtractValue(updateActionResult);
        Assert.True(updatedActionDto.IsCompleted);
        Assert.Equal("Setup Playwright scaffold and CI steps", updatedActionDto.Title);

        // 6. Toggle Action Item
        var toggleResult = await controller.ToggleActionItem(actionDto.Id);
        var toggleOk = Assert.IsType<OkObjectResult>(toggleResult);
        Assert.NotNull(toggleOk.Value);

        // 7. Delete Card and Action Item
        var delCardResult = await controller.DeleteCard(cardDto.Id);
        Assert.IsType<NoContentResult>(delCardResult);

        var delActionResult = await controller.DeleteActionItem(actionDto.Id);
        Assert.IsType<NoContentResult>(delActionResult);
    }

    [Fact]
    public async Task KudosController_SendAndReaction_Succeeds()
    {
        var (db, _, _, _) = CreateTestServices();
        var controller = new KudosController(db);
        var sender = new TeamMember { Id = Guid.NewGuid(), Name = "Alice Lead" };
        var receiver = new TeamMember { Id = Guid.NewGuid(), Name = "Bob Dev" };
        db.TeamMembers.AddRange(sender, receiver);
        await db.SaveChangesAsync();

        var request = new SendKudosRequest(
            SenderId: sender.Id,
            ReceiverId: receiver.Id,
            Badge: BadgeType.QualityGuardian,
            Message: "Zero escaped defects in sprint!"
        );

        var sendResult = await controller.Send(request);
        var kudosDto = ExtractValue(sendResult);
        Assert.Equal("Alice Lead", kudosDto.SenderName);

        // Reaction
        var reactionResult = await controller.AddReaction(kudosDto.Id, "🚀");
        var reactionOk = Assert.IsType<OkObjectResult>(reactionResult);
        var updatedKudos = Assert.IsType<KudosDto>(reactionOk.Value);
        Assert.Equal(1, updatedKudos.ReactionEmojis["🚀"]);
    }

    [Fact]
    public async Task TechHubController_ReturnsDebtAndTalks()
    {
        var (db, _, _, _) = CreateTestServices();
        var controller = new TechHubController(db);
        var presenter = new TeamMember { Id = Guid.NewGuid(), Name = "Priya" };
        db.TeamMembers.Add(presenter);
        db.TechDebtItems.Add(new TechDebtItem { Title = "Upgrade packages", Severity = "Low" });
        db.TechTalkLogs.Add(new TechTalkLog { Topic = "Angular Signals", PresenterId = presenter.Id });
        await db.SaveChangesAsync();

        var debtResult = await controller.GetTechDebt();
        var debtOk = Assert.IsType<OkObjectResult>(debtResult.Result);
        var debts = Assert.IsAssignableFrom<IEnumerable<object>>(debtOk.Value);
        Assert.Single(debts);

        var talksResult = await controller.GetTechTalks();
        var talksOk = Assert.IsType<OkObjectResult>(talksResult.Result);
        var talks = Assert.IsAssignableFrom<IEnumerable<object>>(talksOk.Value);
        Assert.Single(talks);
    }

    [Fact]
    public async Task ExecutiveReportsController_ReturnsReportForSprint()
    {
        var (db, _, _, _) = CreateTestServices();
        var sprint = new Sprint { Id = Guid.NewGuid(), Name = "Sprint 10" };
        db.Sprints.Add(sprint);
        await db.SaveChangesAsync();

        var metricsService = new MetricsCalculatorService(db);
        var controller = new ExecutiveReportsController(metricsService, db);

        var reportResult = await controller.GetSprintReport(sprint.Id);
        var reportOk = Assert.IsType<OkObjectResult>(reportResult.Result);
        var reportDto = Assert.IsType<ExecutiveReportDto>(reportOk.Value);
        Assert.Equal("Sprint 10", reportDto.SprintName);

        var exportResult = await controller.ExportJson();
        var fileResult = Assert.IsType<FileContentResult>(exportResult);
        Assert.Equal("application/json", fileResult.ContentType);
    }

    [Fact]
    public async Task AiCoachController_ReturnsAiSuggestions()
    {
        var (db, _, _, _) = CreateTestServices();
        var aiService = new MicrosoftAgentService(db);
        var controller = new AiCoachController(aiService);

        var indResult = await controller.GetIndividual(Guid.NewGuid());
        var indOk = Assert.IsType<OkObjectResult>(indResult.Result);
        var indDto = Assert.IsType<AiSuggestionResponse>(indOk.Value);
        Assert.NotNull(indDto);

        var projResult = await controller.GetProject(Guid.NewGuid());
        var projOk = Assert.IsType<OkObjectResult>(projResult.Result);
        var projDto = Assert.IsType<AiSuggestionResponse>(projOk.Value);
        Assert.NotNull(projDto);

        var compResult = await controller.GetCompany();
        var compOk = Assert.IsType<OkObjectResult>(compResult.Result);
        var compDto = Assert.IsType<AiSuggestionResponse>(compOk.Value);
        Assert.NotNull(compDto);

        var chatResult = await controller.Chat(new CopilotChatRequest("Tell me about velocity", "ScrumMaster"));
        var chatOk = Assert.IsType<OkObjectResult>(chatResult.Result);
        var chatDto = Assert.IsType<CopilotChatResponse>(chatOk.Value);
        Assert.NotEmpty(chatDto.Answer);
    }

    [Fact]
    public async Task SprintsController_And_TeamMembersController_ReturnData()
    {
        var (db, _, _, _) = CreateTestServices();
        var sprint = new Sprint { Id = Guid.NewGuid(), Name = "Active Sprint", IsActive = true };
        var member = new TeamMember { Id = Guid.NewGuid(), Name = "John Member", IsActive = true };
        db.Sprints.Add(sprint);
        db.TeamMembers.Add(member);
        await db.SaveChangesAsync();

        var sprintsController = new SprintsController(db);
        var membersController = new TeamMembersController(db);

        var allSprintsResult = await sprintsController.GetAll();
        var allSprintsOk = Assert.IsType<OkObjectResult>(allSprintsResult.Result);
        var allSprintsList = Assert.IsAssignableFrom<IEnumerable<Sprint>>(allSprintsOk.Value);
        Assert.Single(allSprintsList);

        var membersResult = await membersController.GetAll();
        var membersOk = Assert.IsType<OkObjectResult>(membersResult.Result);
        var membersList = Assert.IsAssignableFrom<IEnumerable<TeamMember>>(membersOk.Value);
        Assert.Single(membersList);
    }

    [Fact]
    public async Task LeavesController_Submit_Update_Delete_AndValidation_Succeed()
    {
        var (db, _, _, _) = CreateTestServices();
        var member = new TeamMember { Id = Guid.NewGuid(), Name = "Dev Tester", IsActive = true };
        db.TeamMembers.Add(member);
        await db.SaveChangesAsync();

        var metricsService = new MetricsCalculatorService(db);
        var controller = new LeavesController(db, metricsService);

        // 1. Submit leave where EndDate is earlier than StartDate (auto-sanitized) and reason is empty
        var submitRequest = new SubmitLeaveRequest(
            TeamMemberId: member.Id,
            StartDate: new DateTime(2026, 8, 28),
            EndDate: new DateTime(2026, 8, 26),
            Reason: null,
            LeaveType: "Privilege Leave",
            Location: "Offshore"
        );

        var submitResult = await controller.Submit(submitRequest);
        var leaveDto = ExtractValue(submitResult);
        Assert.NotNull(leaveDto);
        Assert.Equal(member.Id, leaveDto.TeamMemberId);
        Assert.Equal("Planned Leave", leaveDto.Reason);
        Assert.True(leaveDto.EndDate >= leaveDto.StartDate);

        // 2. Get All Leaves
        var allResult = await controller.GetAll(null, null, null);
        var allOk = Assert.IsType<OkObjectResult>(allResult.Result);
        var allList = Assert.IsAssignableFrom<IEnumerable<TeamLeaveDto>>(allOk.Value);
        Assert.Single(allList);

        // 3. Update Leave
        var updateRequest = new SubmitLeaveRequest(
            TeamMemberId: member.Id,
            StartDate: new DateTime(2026, 8, 28),
            EndDate: new DateTime(2026, 8, 30),
            Reason: "Vacation with family",
            LeaveType: "Privilege Leave",
            Location: "Bangalore Offshore"
        );
        var updateResult = await controller.Update(leaveDto.Id, updateRequest);
        var updatedDto = ExtractValue(updateResult);
        Assert.Equal("Vacation with family", updatedDto.Reason);
        Assert.Equal(new DateTime(2026, 8, 30), updatedDto.EndDate);

        // 4. Delete Leave
        var deleteResult = await controller.Delete(leaveDto.Id);
        Assert.IsType<NoContentResult>(deleteResult);

        var afterDelete = await controller.GetAll(null, null, null);
        var afterDeleteOk = Assert.IsType<OkObjectResult>(afterDelete.Result);
        var afterDeleteList = Assert.IsAssignableFrom<IEnumerable<TeamLeaveDto>>(afterDeleteOk.Value);
        Assert.Empty(afterDeleteList);
    }
}
