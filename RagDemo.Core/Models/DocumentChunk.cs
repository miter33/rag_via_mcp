namespace RagDemo.Core.Models;

/// <summary>A fixed-size text segment extracted from a source file, ready to be embedded and stored.</summary>
public record DocumentChunk
{
    public required string Text { get; init; }
    public required string SourceFile { get; init; }
    /// <summary>Zero-based position of this chunk within its source file.</summary>
    public required int ChunkIndex { get; init; }
}
