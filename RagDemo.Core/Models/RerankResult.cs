namespace RagDemo.Core.Models;

/// <summary>A single result from the re-ranking step: original candidate index and its cross-encoder score.</summary>
public record RerankResult(int Index, float RelevanceScore);
