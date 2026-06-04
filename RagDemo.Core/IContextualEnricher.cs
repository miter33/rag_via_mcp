namespace RagDemo.Core;

/// <summary>
/// Generates a short contextual description for a chunk relative to its source document.
/// Used during ingest to enrich chunk text before embedding (Contextual RAG strategy).
/// </summary>
public interface IContextualEnricher
{
    /// <param name="documentText">Full text of the source document.</param>
    /// <param name="chunkText">The specific chunk being embedded.</param>
    /// <returns>A 1-2 sentence context string to prepend to the chunk before embedding.</returns>
    Task<string> EnrichAsync(string documentText, string chunkText);
}
