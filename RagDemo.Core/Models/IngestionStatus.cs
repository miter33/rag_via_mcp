using System.Text.Json.Serialization;

namespace RagDemo.Core.Models;

public record SourceStatus
{
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    [JsonPropertyName("chunk_count")]
    public required int ChunkCount { get; init; }
}

public record IngestionStatus
{
    [JsonPropertyName("is_collection_ready")]
    public required bool IsCollectionReady { get; init; }

    [JsonPropertyName("total_chunks")]
    public required ulong TotalChunks { get; init; }

    [JsonPropertyName("indexed_sources")]
    public required IReadOnlyList<SourceStatus> IndexedSources { get; init; }
}
