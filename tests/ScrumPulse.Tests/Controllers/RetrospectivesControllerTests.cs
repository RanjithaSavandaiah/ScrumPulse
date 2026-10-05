namespace ScrumPulse.Tests.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Api.Controllers;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// Targeted tests for the RetrospectivesController, with emphasis on the
/// ToggleActionItem fix (must return a full RetroActionItemDto, not an
/// anonymous object).
/// </summary>
public class RetrospectivesControllerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ScrumPulse_RetroTests_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private T ExtractValue<T>(ActionResult<T> result) where T : class
    {
        if (result.Value != null) return result.Value;
        if (result.Result is OkObjectResult ok && ok.Value is T val) return val;
        throw new InvalidOperationException($"Could not extract {typeof(T).Name}");
    }

    // ── Action Item Create ───────────────────────────────────────────

    [Fact]
    public async Task CreateActionItem_ReturnsDto_WithCorrectFields()
    {
        var db = CreateDb();
        var assignee = new TeamMember { Id = Guid.NewGuid(), Name = "Alice Dev" };
        db.TeamMembers.Add(assignee);
        await db.SaveChangesAsync(Ct);

        var controller = new RetrospectivesController(db);
        var request = new CreateRetroActionItemRequest(
            SprintId: null,
            Title: "Automate regression suite",
            AssigneeId: assignee.Id,
            DueDate: DateTime.UtcNow.AddDays(7)
        );

        var result = await controller.CreateActionItem(request, Ct);
        var dto = ExtractValue(result);

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("Automate regression suite", dto.Title);
        Assert.Equal(assignee.Id, dto.AssigneeId);
        Assert.Equal("Alice Dev", dto.AssigneeName);
        Assert.False(dto.IsCompleted);
    }

    // ── Toggle Action Item (the bug fix) ─────────────────────────────

    [Fact]
    public async Task ToggleActionItem_ReturnsFullDto_NotAnonymousObject()
    {
        // Arrange
        var db = CreateDb();
        var assignee = new TeamMember { Id = Guid.NewGuid(), Name = "Bob QA" };
        db.TeamMembers.Add(assignee);
        var actionItem = new RetroActionItem
        {
            Title = "Setup Playwright scaffold",
            AssigneeId = assignee.Id,
            IsCompleted = false,
            DueDate = DateTime.UtcNow.AddDays(7)
        };
        db.RetroActionItems.Add(actionItem);
        await db.SaveChangesAsync(Ct);

        var controller = new RetrospectivesController(db);

        // Act — toggle from false to true
        var result = await controller.ToggleActionItem(actionItem.Id, Ct);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<RetroActionItemDto>(ok.Value);

        // Assert — full DTO with all fields preserved
        Assert.Equal(actionItem.Id, dto.Id);
        Assert.Equal("Setup Playwright scaffold", dto.Title);
        Assert.Equal(assignee.Id, dto.AssigneeId);
        Assert.Equal("Bob QA", dto.AssigneeName);
        Assert.True(dto.IsCompleted);
        Assert.NotNull(dto.DueDate);
    }

    [Fact]
    public async Task ToggleActionItem_TogglesBackToFalse()
    {
        var db = CreateDb();
        var actionItem = new RetroActionItem
        {
            Title = "Refactor CI config",
            IsCompleted = true
        };
        db.RetroActionItems.Add(actionItem);
        await db.SaveChangesAsync(Ct);

        var controller = new RetrospectivesController(db);

        var result = await controller.ToggleActionItem(actionItem.Id, Ct);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<RetroActionItemDto>(ok.Value);

        Assert.False(dto.IsCompleted);
        Assert.Equal("Refactor CI config", dto.Title);
    }

    [Fact]
    public async Task ToggleActionItem_ReturnsNotFound_ForMissingId()
    {
        var db = CreateDb();
        var controller = new RetrospectivesController(db);

        var result = await controller.ToggleActionItem(Guid.NewGuid(), Ct);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ── Update Action Item ──────────────────────────────────────────

    [Fact]
    public async Task UpdateActionItem_ReturnsUpdatedDto()
    {
        var db = CreateDb();
        var assignee = new TeamMember { Id = Guid.NewGuid(), Name = "Carol" };
        db.TeamMembers.Add(assignee);
        var actionItem = new RetroActionItem
        {
            Title = "Original",
            AssigneeId = assignee.Id,
            IsCompleted = false
        };
        db.RetroActionItems.Add(actionItem);
        await db.SaveChangesAsync(Ct);

        var controller = new RetrospectivesController(db);

        var updateRequest = new UpdateRetroActionItemRequest(
            SprintId: null,
            Title: "Updated action",
            AssigneeId: assignee.Id,
            DueDate: DateTime.UtcNow.AddDays(14),
            IsCompleted: true
        );

        var result = await controller.UpdateActionItem(actionItem.Id, updateRequest, Ct);
        var dto = ExtractValue(result);

        Assert.Equal("Updated action", dto.Title);
        Assert.True(dto.IsCompleted);
        Assert.Equal("Carol", dto.AssigneeName);
    }

    [Fact]
    public async Task UpdateActionItem_ReturnsNotFound_ForMissingId()
    {
        var db = CreateDb();
        var controller = new RetrospectivesController(db);

        var request = new UpdateRetroActionItemRequest(null, "Test", null, null, false);
        var result = await controller.UpdateActionItem(Guid.NewGuid(), request, Ct);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ── Delete Action Item ──────────────────────────────────────────

    [Fact]
    public async Task DeleteActionItem_ReturnsNoContent()
    {
        var db = CreateDb();
        var actionItem = new RetroActionItem { Title = "To delete" };
        db.RetroActionItems.Add(actionItem);
        await db.SaveChangesAsync(Ct);

        var controller = new RetrospectivesController(db);
        var result = await controller.DeleteActionItem(actionItem.Id, Ct);
        Assert.IsType<NoContentResult>(result);

        var remaining = await db.RetroActionItems.CountAsync(Ct);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task DeleteActionItem_ReturnsNotFound_ForMissingId()
    {
        var db = CreateDb();
        var controller = new RetrospectivesController(db);
        var result = await controller.DeleteActionItem(Guid.NewGuid(), Ct);
        Assert.IsType<NotFoundResult>(result);
    }

    // ── Cards: Create, Vote, Update, Delete ─────────────────────────

    [Fact]
    public async Task CreateCard_ReturnsAnonymousAuthorName_WhenIsAnonymous()
    {
        var db = CreateDb();
        var controller = new RetrospectivesController(db);

        var request = new CreateRetroCardRequest(
            SprintId: null,
            Category: RetroCategory.Ideas,
            Content: "Try Playwright for E2E tests",
            AuthorId: null,
            IsAnonymous: true
        );

        var result = await controller.CreateCard(request, Ct);
        var dto = ExtractValue(result);

        Assert.Equal("Anonymous", dto.AuthorName);
        Assert.True(dto.IsAnonymous);
        Assert.Equal(1, dto.UpvotesCount); // default initial vote
    }

    [Fact]
    public async Task VoteCard_IncrementsUpvotesCount()
    {
        var db = CreateDb();
        var controller = new RetrospectivesController(db);

        var createReq = new CreateRetroCardRequest(null, RetroCategory.WentWell, "Great sprint", null, false);
        var created = ExtractValue(await controller.CreateCard(createReq, Ct));
        Assert.Equal(1, created.UpvotesCount);

        var voteResult = await controller.VoteCard(created.Id, Ct);
        var voteOk = Assert.IsType<OkObjectResult>(voteResult);
        var voted = Assert.IsType<RetroCardDto>(voteOk.Value);
        Assert.Equal(2, voted.UpvotesCount);
    }

    [Fact]
    public async Task VoteCard_ReturnsNotFound_ForMissingId()
    {
        var db = CreateDb();
        var controller = new RetrospectivesController(db);
        var result = await controller.VoteCard(Guid.NewGuid(), Ct);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetCards_ReturnsFilteredBySprint()
    {
        var db = CreateDb();
        var sprint1 = Guid.NewGuid();
        var sprint2 = Guid.NewGuid();
        db.RetroCards.AddRange(
            new RetroCard { SprintId = sprint1, Category = RetroCategory.WentWell, Content = "Card 1", UpvotesCount = 1 },
            new RetroCard { SprintId = sprint2, Category = RetroCategory.Ideas, Content = "Card 2", UpvotesCount = 1 }
        );
        await db.SaveChangesAsync(Ct);

        var controller = new RetrospectivesController(db);

        var allResult = await controller.GetCards(null, Ct);
        var allDtos = ExtractValue(allResult);
        Assert.Equal(2, allDtos.Count());

        var filteredResult = await controller.GetCards(sprint1, Ct);
        var filteredDtos = ExtractValue(filteredResult);
        Assert.Single(filteredDtos);
        Assert.Equal("Card 1", filteredDtos.First().Content);
    }

    [Fact]
    public async Task GetActionItems_ReturnsFilteredBySprint()
    {
        var db = CreateDb();
        var sprint1 = Guid.NewGuid();
        var sprint2 = Guid.NewGuid();
        db.RetroActionItems.AddRange(
            new RetroActionItem { SprintId = sprint1, Title = "Action 1" },
            new RetroActionItem { SprintId = sprint2, Title = "Action 2" }
        );
        await db.SaveChangesAsync(Ct);

        var controller = new RetrospectivesController(db);

        var allResult = await controller.GetActionItems(null, Ct);
        var allDtos = ExtractValue(allResult);
        Assert.Equal(2, allDtos.Count());

        var filteredResult = await controller.GetActionItems(sprint1, Ct);
        var filteredDtos = ExtractValue(filteredResult);
        Assert.Single(filteredDtos);
        Assert.Equal("Action 1", filteredDtos.First().Title);
    }

    [Fact]
    public async Task UpdateCard_ReturnsNotFound_ForMissingId()
    {
        var db = CreateDb();
        var controller = new RetrospectivesController(db);
        var request = new UpdateRetroCardRequest(null, RetroCategory.WentWell, "Test", null, false);
        var result = await controller.UpdateCard(Guid.NewGuid(), request, Ct);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task DeleteCard_ReturnsNoContent_AndRemovesFromDb()
    {
        var db = CreateDb();
        var card = new RetroCard { Category = RetroCategory.Ideas, Content = "To delete", UpvotesCount = 1 };
        db.RetroCards.Add(card);
        await db.SaveChangesAsync(Ct);

        var controller = new RetrospectivesController(db);
        var result = await controller.DeleteCard(card.Id, Ct);
        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, await db.RetroCards.CountAsync(Ct));
    }

    [Fact]
    public async Task DeleteCard_ReturnsNotFound_ForMissingId()
    {
        var db = CreateDb();
        var controller = new RetrospectivesController(db);
        var result = await controller.DeleteCard(Guid.NewGuid(), Ct);
        Assert.IsType<NotFoundResult>(result);
    }
}
