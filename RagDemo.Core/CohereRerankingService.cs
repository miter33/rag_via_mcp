using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using RagDemo.Core.Models;

namespace RagDemo.Core;

/// <summary>
/// Re-ranks candidates using the Cohere Rerank API (rerank-english-v3.0).
///
/// Re-ranking RAG concept: a bi-encoder (Cohere embed) retrieves top-N candidates fast but
/// independently — it never sees the query and document together. A cross-encoder (Cohere Rerank)
/// reads query + document as a pair, which is slower but far more precise. Running it only on
/// the small candidate set (not the full corpus) keeps latency acceptable.
/// </summary>
public sealed class CohereRerankingService : IRerankingService
{
    private const string RerankUrl  = "https://api.cohere.com/v2/rerank";
    private const string ModelName  = "rerank-english-v3.0";

    private readonly HttpClient _http;
    private readonly string     _apiKey;

    public CohereRerankingService(HttpClient http, string apiKey)
    {
        _http   = http;
        _apiKey = apiKey;
    }

    public async Task<IReadOnlyList<RerankResult>> RerankAsync(
        string query, IReadOnlyList<string> documents, int topN)
    {
        // Exponential back-off on HTTP 429 (rate limit): wait 2s, then 4s, then give up.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));

            using var request = new HttpRequestMessage(HttpMethod.Post, RerankUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = JsonContent.Create(new CohereRerankRequest(
                Model:     ModelName,
                Query:     query,
                Documents: documents.ToArray(),
                TopN:      topN));

            using var response = await _http.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 2)
                continue;

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<CohereRerankResponse>();
            return body!.Results
                .Select(r => new RerankResult(r.Index, r.RelevanceScore))
                .ToList();
        }

        throw new InvalidOperationException("Unreachable: loop exhausted without returning or throwing");
    }

    // ── Private DTOs ──────────────────────────────────────────────────────────

    private record CohereRerankRequest(
        [property: JsonPropertyName("model")]     string   Model,
        [property: JsonPropertyName("query")]     string   Query,
        [property: JsonPropertyName("documents")] string[] Documents,
        [property: JsonPropertyName("top_n")]     int      TopN);

    private record CohereRerankResponse(
        [property: JsonPropertyName("results")] CohereRerankItem[] Results);

    private record CohereRerankItem(
        [property: JsonPropertyName("index")]           int   Index,
        [property: JsonPropertyName("relevance_score")] float RelevanceScore);
}
