using RagDemo.Core.Models;

namespace RagDemo.Core;

/// <summary>
/// Re-ranks a list of candidate documents against a query using a cross-encoder model.
/// Used as the second stage in two-stage retrieval (Re-ranking RAG strategy).
/// </summary>
public interface IRerankingService
{
    /// <param name="query">The user's question.</param>
    /// <param name="documents">Candidate document texts retrieved in stage 1.</param>
    /// <param name="topN">How many top results to return after re-ranking.</param>
    /// <returns>Results sorted by relevance score descending, each carrying the original candidate index.</returns>
    Task<IReadOnlyList<RerankResult>> RerankAsync(string query, IReadOnlyList<string> documents, int topN);
}
