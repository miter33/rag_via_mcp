using System.Net;
using System.Text;
using FluentAssertions;
using RagDemo.Core;
using RagDemo.Core.Models;

namespace RagDemo.Tests;

public class CohereEmbeddingServiceTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Fake handler that always returns the same response.</summary>
    private sealed class ConstantHandler(HttpStatusCode code, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }

    /// <summary>Fake handler that returns responses from a queue, one per call.</summary>
    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode, string)> _queue;
        public int CallCount { get; private set; }

        public SequenceHandler(params (HttpStatusCode, string)[] responses) =>
            _queue = new Queue<(HttpStatusCode, string)>(responses);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var (code, body) = _queue.Dequeue();
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEmbeddingAsync_ValidResponse_ReturnsParsedVector()
    {
        const string json = """
            {
                "embeddings": {
                    "float": [[0.1, 0.2, 0.3]]
                }
            }
            """;
        var client = new HttpClient(new ConstantHandler(HttpStatusCode.OK, json));
        var svc = new CohereEmbeddingService(client, "test-key");

        var result = await svc.GetEmbeddingAsync("hello", EmbeddingType.Document);

        result.Should().BeEquivalentTo(new float[] { 0.1f, 0.2f, 0.3f },
            o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task GetEmbeddingAsync_TwoRateLimitsThenSuccess_RetriesAndReturns()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.TooManyRequests, "{}"),
            (HttpStatusCode.TooManyRequests, "{}"),
            (HttpStatusCode.OK, """{"embeddings":{"float":[[0.5]]}}""")
        );
        var svc = new CohereEmbeddingService(new HttpClient(handler), "test-key");

        var result = await svc.GetEmbeddingAsync("hello", EmbeddingType.Query);

        result.Should().BeEquivalentTo(new float[] { 0.5f });
        handler.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task GetEmbeddingAsync_ThreeConsecutive429s_ThrowsHttpRequestException()
    {
        var handler = new SequenceHandler(
            (HttpStatusCode.TooManyRequests, "{}"),
            (HttpStatusCode.TooManyRequests, "{}"),
            (HttpStatusCode.TooManyRequests, "{}")
        );
        var svc = new CohereEmbeddingService(new HttpClient(handler), "test-key");

        Func<Task> act = () => svc.GetEmbeddingAsync("hello", EmbeddingType.Document);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
