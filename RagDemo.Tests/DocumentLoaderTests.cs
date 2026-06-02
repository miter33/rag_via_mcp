using FluentAssertions;
using RagDemo.Core;

namespace RagDemo.Tests;

public class DocumentLoaderTests
{
    private static DocumentLoader Loader() => new DocumentLoader();

    [Fact]
    public async Task ChunkTextAsync_TextShorterThanChunkSize_ReturnsSingleChunk()
    {
        var text = new string('a', 100);

        var chunks = await Loader().ChunkTextAsync(text, "test.txt").ToListAsync();

        chunks.Should().HaveCount(1);
        chunks[0].Text.Should().Be(text);
        chunks[0].SourceFile.Should().Be("test.txt");
        chunks[0].ChunkIndex.Should().Be(0);
    }

    [Fact]
    public async Task ChunkTextAsync_TextExactlyChunkSize_ReturnsSingleChunk()
    {
        var text = new string('b', 500);

        var chunks = await Loader().ChunkTextAsync(text, "exact.txt").ToListAsync();

        chunks.Should().HaveCount(1);
        chunks[0].Text.Length.Should().Be(500);
    }

    [Fact]
    public async Task ChunkTextAsync_LongText_ProducesOverlappingChunks()
    {
        // With chunkSize=500, overlap=50, step=450:
        // chunk 0: [0..500), chunk 1: [450..950), chunk 2: [900..1000)
        var text = new string('c', 1000);

        var chunks = await Loader().ChunkTextAsync(text, "long.md").ToListAsync();

        chunks.Should().HaveCountGreaterThan(1);
        // The last 50 chars of chunk 0 should equal the first 50 chars of chunk 1
        chunks[0].Text[^50..].Should().Be(chunks[1].Text[..50]);
    }

    [Fact]
    public async Task ChunkTextAsync_LongText_AssignsSequentialIndexesFromZero()
    {
        var text = new string('d', 2000);

        var chunks = await Loader().ChunkTextAsync(text, "doc.pdf").ToListAsync();

        chunks.Select(c => c.ChunkIndex)
              .Should().BeEquivalentTo(Enumerable.Range(0, chunks.Count),
                  o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task ChunkTextAsync_EmptyText_ReturnsNoChunks()
    {
        var chunks = await Loader().ChunkTextAsync("", "empty.txt").ToListAsync();

        chunks.Should().BeEmpty();
    }
}
