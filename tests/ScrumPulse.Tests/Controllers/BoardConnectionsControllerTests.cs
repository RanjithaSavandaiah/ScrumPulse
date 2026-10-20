namespace ScrumPulse.Tests.Controllers;

using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ScrumPulse.Api.Controllers;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;
using ScrumPulse.Domain.Enums;
using Xunit;

/// <summary>
/// Covers the endpoints that configure a squad's board link and run imports against it.
/// </summary>
/// <remarks>
/// <para>
/// The organisation URL tests are the reason this suite exists. That value becomes the host of
/// a request the server makes while holding a credential, so without an allow-list a Scrum
/// Master could point the connection at an internal address and turn the sync into a
/// server-side request forgery primitive - reaching a cloud metadata endpoint or an internal
/// service the browser could never touch. The HTTPS requirement is separate and equally
/// load-bearing: it is what keeps the client's token off the wire in clear text.
/// </para>
/// <para>
/// The rest pin the shapes the browser depends on: 204 for an absent connection rather than a
/// null body, 400 for an upload that is not a CSV, and 409 rather than 500 when there is
/// simply no board to sync, which is a state the UI can act on rather than a crash.
/// </para>
/// </remarks>
public class BoardConnectionsControllerTests
{
    private static BoardConnectionsController CreateController(FakeBoardConnectionService service) =>
        new(service, NullLogger<BoardConnectionsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static BoardConnectionDto Connection() =>
        new(
            Guid.NewGuid(),
            WorkItemOrigin.AzureDevOps,
            "https://dev.azure.com/contoso",
            "Delivery",
            TeamOrAreaPath: null,
            HasStoredCredential: true,
            BoardAssigneeFilter.RosterMembersOnly,
            ContentSensitivityPolicy.MetricsOnly,
            IsEnabled: true,
            LastSyncedAtUtc: null,
            LastSyncStatus: null,
            LastSyncItemCount: 0,
            new Dictionary<string, WorkItemStatus>());

    private static SaveBoardConnectionRequest SaveRequest(string organizationUrl) =>
        new(organizationUrl, "Delivery", TeamOrAreaPath: null, PersonalAccessToken: "pat-0123456789");

    private static IFormFile UploadedFile(string fileName, string content)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }

    [Fact]
    public async Task GetConnection_ReturnsNoContent_WhenTheSquadHasNotConnectedABoard()
    {
        // Not 404: the endpoint exists and the answer is "none yet", which is what lets the UI
        // render the setup form instead of an error.
        var controller = CreateController(new FakeBoardConnectionService());

        var result = await controller.GetConnection();

        Assert.IsType<NoContentResult>(result.Result);
    }

    [Fact]
    public async Task GetConnection_ReturnsTheConnection_WhenOneIsConfigured()
    {
        var controller = CreateController(new FakeBoardConnectionService { Connection = Connection() });

        var result = await controller.GetConnection();

        var returned = Assert.IsType<BoardConnectionDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("Delivery", returned.ProjectName);
    }

    [Theory]
    [InlineData("http://dev.azure.com/contoso")]
    [InlineData("https://evil.com/contoso")]
    [InlineData("https://dev.azure.com.evil.com/contoso")]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://localhost:5001/contoso")]
    [InlineData("https://10.0.0.5/contoso")]
    [InlineData("https://notvisualstudio.com/contoso")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not-a-url-at-all")]
    public async Task SaveConnection_RejectsAnythingThatIsNotAnHttpsAzureDevOpsOrganisation(string organizationUrl)
    {
        // Each of these is a distinct way through a weaker check. Plain HTTP puts the token on
        // the wire; a lookalike host passes a naive "contains dev.azure.com"; the link-local
        // address is the cloud metadata endpoint, which is the classic request-forgery target.
        var service = new FakeBoardConnectionService();
        var controller = CreateController(service);

        var result = await controller.SaveConnection(SaveRequest(organizationUrl));

        Assert.IsType<ObjectResult>(result.Result);
        Assert.False(service.SaveWasCalled, "A rejected URL must never reach the service.");
    }

    [Theory]
    [InlineData("https://dev.azure.com/contoso")]
    [InlineData("https://contoso.visualstudio.com")]
    [InlineData("https://DEV.AZURE.COM/contoso")]
    public async Task SaveConnection_AcceptsBothAzureDevOpsOrganisationForms(string organizationUrl)
    {
        // The older {organisation}.visualstudio.com form is still live for plenty of long-lived
        // clients, so rejecting it would make the feature unusable for exactly those engagements.
        var service = new FakeBoardConnectionService { Connection = Connection() };
        var controller = CreateController(service);

        var result = await controller.SaveConnection(SaveRequest(organizationUrl));

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(service.SaveWasCalled);
    }

    [Fact]
    public async Task ImportFile_RejectsAMissingFile()
    {
        var service = new FakeBoardConnectionService();
        var controller = CreateController(service);

        var result = await controller.ImportFile(null!);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(service.ImportedContent);
    }

    [Fact]
    public async Task ImportFile_RejectsAnEmptyFile()
    {
        // A zero-byte upload is almost always a picker that returned nothing. Passing it on
        // would produce a successful import of nothing and no clue why.
        var service = new FakeBoardConnectionService();
        var controller = CreateController(service);

        var result = await controller.ImportFile(UploadedFile("export.csv", string.Empty));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(service.ImportedContent);
    }

    [Theory]
    [InlineData("export.xlsx")]
    [InlineData("export.csv.exe")]
    [InlineData("export")]
    public async Task ImportFile_RejectsAnythingThatIsNotACsv(string fileName)
    {
        var service = new FakeBoardConnectionService();
        var controller = CreateController(service);

        var result = await controller.ImportFile(UploadedFile(fileName, "ID,Title\n4182,Totals\n"));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(service.ImportedContent);
    }

    [Fact]
    public async Task ImportFile_PassesTheDecodedContentThrough()
    {
        var service = new FakeBoardConnectionService();
        var controller = CreateController(service);

        var result = await controller.ImportFile(UploadedFile("export.CSV", "ID,Title,State\n4182,Totals,Active\n"));

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("ID,Title,State\n4182,Totals,Active\n", service.ImportedContent);
    }

    [Fact]
    public async Task Sync_ReturnsConflictRatherThanAServerError_WhenNoBoardIsConnected()
    {
        // The request was well formed; the squad simply has nothing to sync yet. A 500 would
        // send the user to a support channel and would be logged as an application fault.
        var service = new FakeBoardConnectionService
        {
            SyncFailure = new InvalidOperationException("No board is connected for this squad.")
        };

        var controller = CreateController(service);

        var result = await controller.Sync();

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
    }

    [Fact]
    public async Task Sync_ReturnsTheResult_WhenTheBoardAnswers()
    {
        var controller = CreateController(new FakeBoardConnectionService());

        var result = await controller.Sync();

        Assert.IsType<BoardSyncResultDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public void Controller_HasRequireScrumMasterAttribute()
    {
        Assert.True(typeof(BoardConnectionsController).IsDefined(typeof(ScrumPulse.Api.Filters.RequireScrumMasterAttribute), inherit: true));
    }

    [Fact]
    public void GetStateMappingDefaults_ReturnsDefaultStateMappings()
    {
        var controller = CreateController(new FakeBoardConnectionService());

        var result = controller.GetStateMappingDefaults();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var defaults = Assert.IsType<BoardStateMappingDefaultsDto>(ok.Value);
        Assert.NotNull(defaults.AzureDevOpsDefaults);
        Assert.Contains("Active", defaults.AzureDevOpsDefaults.Keys);
        Assert.Contains("Closed", defaults.AzureDevOpsDefaults.Keys);
    }

    [Fact]
    public async Task SaveConnection_ReturnsValidationProblem_WhenModelStateIsInvalid()
    {
        var service = new FakeBoardConnectionService();
        var controller = CreateController(service);
        controller.ModelState.AddModelError("ProjectName", "Project name is required.");

        var result = await controller.SaveConnection(SaveRequest("https://dev.azure.com/contoso"));

        Assert.IsType<ObjectResult>(result.Result);
        Assert.False(service.SaveWasCalled);
    }

    [Fact]
    public async Task DeleteConnection_ReturnsNoContent_WhenDeletedSuccessfully()
    {
        var service = new FakeBoardConnectionService { DeleteReturnsSuccess = true };
        var controller = CreateController(service);

        var result = await controller.DeleteConnection();

        Assert.IsType<NoContentResult>(result);
        Assert.True(service.DeleteWasCalled);
    }

    [Fact]
    public async Task DeleteConnection_ReturnsNotFound_WhenConnectionDoesNotExist()
    {
        var service = new FakeBoardConnectionService { DeleteReturnsSuccess = false };
        var controller = CreateController(service);

        var result = await controller.DeleteConnection();

        Assert.IsType<NotFoundResult>(result);
        Assert.True(service.DeleteWasCalled);
    }

    [Fact]
    public async Task TestConnection_ReturnsResultFromService()
    {
        var service = new FakeBoardConnectionService
        {
            TestResult = new BoardConnectionTestResult(true, "Token valid.")
        };
        var controller = CreateController(service);

        var result = await controller.TestConnection();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var testResult = Assert.IsType<BoardConnectionTestResult>(ok.Value);
        Assert.True(testResult.Succeeded);
        Assert.Equal("Token valid.", testResult.Message);
        Assert.True(service.TestWasCalled);
    }

    [Fact]
    public async Task Sync_PassesFullResyncFlagToService()
    {
        var service = new FakeBoardConnectionService();
        var controller = CreateController(service);

        var result = await controller.Sync(fullResync: true);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(service.LastFullResync);
    }

    [Fact]
    public async Task ImportFile_ReturnsResultFromService_WhenValidCsvFileProvided()
    {
        var expected = new BoardSyncResultDto(5, 3, ["Unmapped"], ["Unmatched"], ["Warning"], DateTime.UtcNow);
        var service = new FakeBoardConnectionService
        {
            CustomSyncResult = expected
        };
        var controller = CreateController(service);

        var result = await controller.ImportFile(UploadedFile("export.csv", "ID,Title\n1,Task\n"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var returned = Assert.IsType<BoardSyncResultDto>(ok.Value);
        Assert.Equal(5, returned.ImportedCount);
        Assert.Equal(3, returned.CreatedCount);
        Assert.Contains("Unmapped", returned.UnmappedStates);
    }

    /// <summary>Records what reached the service, so a test can assert what did not.</summary>
    private sealed class FakeBoardConnectionService : IBoardConnectionService
    {
        public BoardConnectionDto? Connection { get; init; }

        public Exception? SyncFailure { get; init; }

        public BoardSyncResultDto? CustomSyncResult { get; init; }

        public bool SaveWasCalled { get; private set; }

        public string? ImportedContent { get; private set; }

        public bool DeleteWasCalled { get; private set; }

        public bool DeleteReturnsSuccess { get; init; } = true;

        public bool TestWasCalled { get; private set; }

        public bool LastFullResync { get; private set; }

        public BoardConnectionTestResult TestResult { get; init; } = new(true, null);

        public Task<BoardConnectionDto?> GetConnectionAsync(CancellationToken ct = default) =>
            Task.FromResult(Connection);

        public Task<BoardConnectionDto> SaveConnectionAsync(
            SaveBoardConnectionRequest request,
            CancellationToken ct = default)
        {
            SaveWasCalled = true;
            return Task.FromResult(Connection ?? BoardConnectionsControllerTests.Connection());
        }

        public Task<bool> DeleteConnectionAsync(CancellationToken ct = default)
        {
            DeleteWasCalled = true;
            return Task.FromResult(DeleteReturnsSuccess);
        }

        public Task<BoardConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
        {
            TestWasCalled = true;
            return Task.FromResult(TestResult);
        }

        public Task<BoardSyncResultDto> SyncAsync(bool fullResync = false, CancellationToken ct = default)
        {
            LastFullResync = fullResync;
            return SyncFailure is null
                ? Task.FromResult(EmptyResult())
                : Task.FromException<BoardSyncResultDto>(SyncFailure);
        }

        public Task<BoardSyncResultDto> ImportFromFileAsync(string csvContent, CancellationToken ct = default)
        {
            ImportedContent = csvContent;
            return Task.FromResult(CustomSyncResult ?? EmptyResult());
        }

        private static BoardSyncResultDto EmptyResult() => new(0, 0, [], [], [], DateTime.UtcNow);
    }
}
