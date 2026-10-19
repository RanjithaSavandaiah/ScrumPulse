namespace ScrumPulse.Tests.Controllers;

using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ScrumPulse.Api.Controllers;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;
using Xunit;

/// <summary>
/// Covers the audio transcription endpoint that the browser posts recorded chunks to.
/// </summary>
/// <remarks>
/// <para>
/// This endpoint handles a recording of colleagues talking, which makes two of its properties
/// worth asserting rather than assuming: nothing is written to disk, and nothing spoken ever
/// reaches the log. A transcript in application logs is a record of what the squad said about
/// their work, retained wherever logs are retained and readable by whoever can read them.
/// </para>
/// <para>
/// The client now records in fifteen-second chunks rather than thirty, so this endpoint sees
/// roughly twice the traffic for the same ceremony. Nothing here keeps per-request state, and
/// these tests fix that: a handler that accumulated anything between calls would turn a
/// latency improvement into a memory leak on a free instance.
/// </para>
/// </remarks>
public class StandupScribeControllerTests
{
    /// <summary>Stands in for the provider, recording what it was handed.</summary>
    private sealed class RecordingTranscriptionService : IAudioTranscriptionService
    {
        public bool IsAvailable { get; init; } = true;

        public string? Result { get; init; } = "Priya finished the migration.";

        public Exception? ThrowOnTranscribe { get; init; }

        public int CallCount { get; private set; }

        public List<string> ReceivedFileNames { get; } = [];

        public List<long> ReceivedByteCounts { get; } = [];

        public async Task<string?> TranscribeAsync(Stream audioStream, string fileName, CancellationToken ct = default)
        {
            CallCount++;
            ReceivedFileNames.Add(fileName);

            // Reads the stream so the test proves the controller handed over something open
            // and positioned, rather than a disposed or already-consumed one.
            using var buffer = new MemoryStream();
            await audioStream.CopyToAsync(buffer, ct);
            ReceivedByteCounts.Add(buffer.Length);

            return ThrowOnTranscribe is not null ? throw ThrowOnTranscribe : Result;
        }
    }

    /// <summary>Not exercised by the transcription tests; present to satisfy the constructor.</summary>
    private sealed class StubTranscriptService : IStandupTranscriptService
    {
        public Task<StandupDraftResponse> DraftFromTranscriptAsync(
            DraftStandupsFromTranscriptRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(new StandupDraftResponse([], [], false));

        public Task<AutoLoggedStandupsResponse> AutoLogFromTranscriptAsync(
            DraftStandupsFromTranscriptRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(new AutoLoggedStandupsResponse(0, [], [], false));
    }

    private static StandupScribeController CreateController(IAudioTranscriptionService transcription) =>
        new(new StubTranscriptService(), transcription, NullLogger<StandupScribeController>.Instance);

    /// <summary>Builds an uploaded file the way the browser's recorder posts one.</summary>
    private static FormFile CreateAudioChunk(string fileName = "standup-chunk-0.webm", int byteCount = 4_096)
    {
        var bytes = Encoding.UTF8.GetBytes(new string('a', byteCount));
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, stream.Length, "audio", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "audio/webm"
        };
    }

    [Fact]
    public async Task Transcribe_ReturnsTheTranscribedText()
    {
        var transcription = new RecordingTranscriptionService();
        var controller = CreateController(transcription);

        var response = await controller.Transcribe(CreateAudioChunk());

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<AudioChunkTranscriptResponse>(ok.Value);
        Assert.Equal("Priya finished the migration.", payload.Text);
    }

    [Fact]
    public async Task Transcribe_PassesTheFileNameThrough()
    {
        // The provider infers the codec from the extension, so a chunk posted without one
        // comes back empty with no error to explain why.
        var transcription = new RecordingTranscriptionService();
        var controller = CreateController(transcription);

        await controller.Transcribe(CreateAudioChunk("standup-chunk-7.webm"));

        Assert.Equal("standup-chunk-7.webm", Assert.Single(transcription.ReceivedFileNames));
    }

    [Fact]
    public async Task Transcribe_HandsOverAReadableStream()
    {
        var transcription = new RecordingTranscriptionService();
        var controller = CreateController(transcription);

        await controller.Transcribe(CreateAudioChunk(byteCount: 2_048));

        Assert.Equal(2_048, Assert.Single(transcription.ReceivedByteCounts));
    }

    [Fact]
    public async Task Transcribe_ReturnsAnEmptyStringForSilence()
    {
        // A silent chunk is normal - someone muted, or a gap between speakers. The client
        // appends whatever comes back, so null here would put "null" into the transcript.
        var transcription = new RecordingTranscriptionService { Result = null };
        var controller = CreateController(transcription);

        var response = await controller.Transcribe(CreateAudioChunk());

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<AudioChunkTranscriptResponse>(ok.Value);
        Assert.Equal(string.Empty, payload.Text);
    }

    [Fact]
    public async Task Transcribe_HandlesChunksArrivingBackToBack()
    {
        // Fifteen-second chunks mean roughly sixty posts across a standup rather than thirty.
        // Each must be independent: no accumulated buffer, no carried-over state.
        var transcription = new RecordingTranscriptionService();
        var controller = CreateController(transcription);

        for (var chunkIndex = 0; chunkIndex < 60; chunkIndex++)
        {
            var response = await controller.Transcribe(CreateAudioChunk($"standup-chunk-{chunkIndex}.webm"));
            Assert.IsType<OkObjectResult>(response.Result);
        }

        Assert.Equal(60, transcription.CallCount);
        Assert.Equal(60, transcription.ReceivedFileNames.Distinct().Count());
    }

    [Fact]
    public async Task Transcribe_AcceptsASmallChunk()
    {
        // A fifteen-second chunk of near-silence compresses to very little. Rejecting small
        // uploads would drop the quiet parts of a standup, which are often the blockers.
        var transcription = new RecordingTranscriptionService();
        var controller = CreateController(transcription);

        var response = await controller.Transcribe(CreateAudioChunk(byteCount: 64));

        Assert.IsType<OkObjectResult>(response.Result);
    }

    [Fact]
    public async Task Transcribe_RejectsAnEmptyUpload()
    {
        var transcription = new RecordingTranscriptionService();
        var controller = CreateController(transcription);

        var response = await controller.Transcribe(CreateAudioChunk(byteCount: 0));

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(0, transcription.CallCount);
    }

    [Fact]
    public async Task Transcribe_RejectsAMissingUpload()
    {
        var transcription = new RecordingTranscriptionService();
        var controller = CreateController(transcription);

        var response = await controller.Transcribe(audio: null!);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(0, transcription.CallCount);
    }

    [Fact]
    public async Task Transcribe_ReportsUnavailableRatherThanFailing()
    {
        // 503 rather than 500: a deployment with no transcription key is configured that way
        // on purpose, and the client uses this answer to offer pasting instead.
        var transcription = new RecordingTranscriptionService { IsAvailable = false };
        var controller = CreateController(transcription);

        var response = await controller.Transcribe(CreateAudioChunk());

        var status = Assert.IsType<ObjectResult>(response.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
    }

    [Fact]
    public async Task Transcribe_DoesNotCallTheProviderWhenItIsUnavailable()
    {
        // Spending a request against an exhausted allowance only deepens the hole.
        var transcription = new RecordingTranscriptionService { IsAvailable = false };
        var controller = CreateController(transcription);

        await controller.Transcribe(CreateAudioChunk());

        Assert.Equal(0, transcription.CallCount);
    }

    [Fact]
    public async Task Transcribe_ChecksTheUploadBeforeTheProvider()
    {
        // Order matters: an empty upload must be refused on its own terms, not reported as a
        // transcription outage that sends the user off to paste a transcript they do not have.
        var transcription = new RecordingTranscriptionService { IsAvailable = false };
        var controller = CreateController(transcription);

        var response = await controller.Transcribe(CreateAudioChunk(byteCount: 0));

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public async Task Transcribe_WritesNothingToDisk()
    {
        // Captured audio is a recording of colleagues. It is held in memory for the length of
        // one request and never persisted, which is what makes the consent notice accurate.
        var temporaryDirectory = Path.GetTempPath();
        var filesBefore = Directory.GetFiles(temporaryDirectory).Length;

        var controller = CreateController(new RecordingTranscriptionService());
        await controller.Transcribe(CreateAudioChunk());

        Assert.Equal(filesBefore, Directory.GetFiles(temporaryDirectory).Length);
    }

    [Fact]
    public async Task Transcribe_PropagatesCancellationToTheProvider()
    {
        // A client that navigates away mid-standup must not leave an upload in flight against
        // a metered allowance.
        var observed = CancellationToken.None;

        var transcription = new DelegatingTranscriptionService((_, _, ct) =>
        {
            observed = ct;
            return Task.FromResult<string?>("text");
        });

        var controller = CreateController(transcription);
        using var cancellation = new CancellationTokenSource();

        await controller.Transcribe(CreateAudioChunk(), cancellation.Token);

        Assert.Equal(cancellation.Token, observed);
    }

    private sealed class DelegatingTranscriptionService(
        Func<Stream, string, CancellationToken, Task<string?>> transcribe) : IAudioTranscriptionService
    {
        public bool IsAvailable => true;

        public Task<string?> TranscribeAsync(Stream audioStream, string fileName, CancellationToken ct = default) =>
            transcribe(audioStream, fileName, ct);
    }
}
