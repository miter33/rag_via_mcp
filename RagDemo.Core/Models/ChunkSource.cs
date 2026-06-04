namespace RagDemo.Core.Models;

/// <summary>A chunk retrieved from Qdrant, with its cosine-similarity score. Used to generate citations.</summary>
public record ChunkSource
{
    public required string Text { get; init; }
    public required string SourceFile { get; init; }
    public required int ChunkIndex { get; init; }
    /// <summary>Cosine similarity score from Qdrant (0–1, higher = more relevant).</summary>
    public required float Score { get; init; }
    /// <summary>LLM-generated context prepended at ingest time (Contextual RAG). Empty for naive-ingested chunks.</summary>
    public string Context { get; init; } = string.Empty;
}
