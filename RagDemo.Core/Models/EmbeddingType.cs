namespace RagDemo.Core.Models;

/// <summary>
/// Cohere requires different input_type values depending on whether text
/// is being indexed or searched. This enum makes the distinction explicit
/// so the compiler enforces correct usage at call sites.
/// </summary>
public enum EmbeddingType
{
    /// <summary>Use when embedding a document chunk for storage. Maps to "search_document".</summary>
    Document,
    /// <summary>Use when embedding a user's question. Maps to "search_query".</summary>
    Query
}
