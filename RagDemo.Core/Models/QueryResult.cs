namespace RagDemo.Core.Models;

/// <summary>The complete output of a RAG query: the LLM's answer plus the chunks used as context.</summary>
public record QueryResult
{
    public required string Answer { get; init; }
    public required IReadOnlyList<ChunkSource> Sources { get; init; }
}
