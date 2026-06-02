using RagDemo.Core.Models;

namespace RagDemo.Core;

/// <summary>
/// Converts text into a dense float vector (embedding).
/// Implemented by CohereEmbeddingService. Having an interface keeps the
/// service swappable and makes it mockable in unit tests.
/// </summary>
public interface IEmbeddingService
{
    /// <param name="text">The text to embed.</param>
    /// <param name="type">
    /// Document for indexing (better compression), Query for search (asymmetric retrieval).
    /// Cohere's asymmetric embedding improves precision for Q&amp;A tasks.
    /// </param>
    Task<float[]> GetEmbeddingAsync(string text, EmbeddingType type);
}
