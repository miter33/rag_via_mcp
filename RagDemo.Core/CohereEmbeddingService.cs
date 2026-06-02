using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using RagDemo.Core.Models;

namespace RagDemo.Core;

/// <summary>
/// Embeds text using the Cohere v2 API (embed-english-v3.0, 1024 dimensions).
///
/// RAG concept: embedding models map text into a high-dimensional space where
/// semantically similar texts land near each other. We use Cohere's "asymmetric"
/// mode: documents and queries have different input_type values, which tells the
/// model to optimise for search recall rather than just semantic similarity.
/// </summary>
public sealed class CohereEmbeddingService : IEmbeddingService
{
    private const string EmbedUrl  = "https://api.cohere.com/v2/embed";
    private const string ModelName = "embed-english-v3.0";

    private readonly HttpClient _http;
    private readonly string     _apiKey;

    public CohereEmbeddingService(HttpClient http, string apiKey)
    {
        _http   = http;
        _apiKey = apiKey;
    }

    /// <inheritdoc />
    public async Task<float[]> GetEmbeddingAsync(string text, EmbeddingType type)
    {
        var inputType = type == EmbeddingType.Document ? "search_document" : "search_query";

        // Exponential back-off on HTTP 429 (rate limit): wait 2s, then 4s, then give up.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));

            using var request = new HttpRequestMessage(HttpMethod.Post, EmbedUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = JsonContent.Create(new
            {
                texts            = new[] { text },
                model            = ModelName,
                input_type       = inputType,
                embedding_types  = new[] { "float" }
            });

            var response = await _http.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 2)
                continue;

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<CohereEmbedResponse>();
            return body!.Embeddings.Float[0];
        }

        throw new InvalidOperationException("Unreachable: loop exhausted without returning or throwing");
    }

    // ── Private DTOs for JSON deserialization ────────────────────────────────

    private record CohereEmbedResponse(
        [property: JsonPropertyName("embeddings")] CohereEmbeddings Embeddings);

    private record CohereEmbeddings(
        [property: JsonPropertyName("float")] float[][] Float);
}
