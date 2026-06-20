using System.Text.Json;
using FluentAssertions;
using RagDemo.Core.Models;

namespace RagDemo.Tests;

public class IngestionStatusTests
{
    [Fact]
    public void IngestionStatus_WhenCollectionNotReady_SerializesCorrectly()
    {
        var status = new IngestionStatus
        {
            IsCollectionReady = false,
            TotalChunks       = 0,
            IndexedSources    = Array.Empty<SourceStatus>()
        };

        var json = JsonSerializer.Serialize(status);

        json.Should().Contain("\"is_collection_ready\":false");
        json.Should().Contain("\"total_chunks\":0");
        json.Should().Contain("\"indexed_sources\":[]");
    }

    [Fact]
    public void IngestionStatus_WithSources_SerializesSnakeCaseKeys()
    {
        var status = new IngestionStatus
        {
            IsCollectionReady = true,
            TotalChunks       = 5,
            IndexedSources    =
            [
                new SourceStatus { Source = "docs/a.txt", ChunkCount = 3 },
                new SourceStatus { Source = "docs/b.md",  ChunkCount = 2 },
            ]
        };

        var json = JsonSerializer.Serialize(status);

        json.Should().Contain("\"is_collection_ready\":true");
        json.Should().Contain("\"total_chunks\":5");
        json.Should().Contain("\"indexed_sources\":");
        json.Should().Contain("\"source\":\"docs/a.txt\"");
        json.Should().Contain("\"chunk_count\":3");
        json.Should().Contain("\"source\":\"docs/b.md\"");
        json.Should().Contain("\"chunk_count\":2");
    }

    [Fact]
    public void IngestionStatus_RoundTrips_ThroughJsonDeserialization()
    {
        var original = new IngestionStatus
        {
            IsCollectionReady = true,
            TotalChunks       = 7,
            IndexedSources    = [new SourceStatus { Source = "file.txt", ChunkCount = 7 }]
        };

        var json      = JsonSerializer.Serialize(original);
        var roundTrip = JsonSerializer.Deserialize<IngestionStatus>(json);

        roundTrip.Should().NotBeNull();
        roundTrip!.IsCollectionReady.Should().BeTrue();
        roundTrip.TotalChunks.Should().Be(7UL);
        roundTrip.IndexedSources.Should().HaveCount(1);
        roundTrip.IndexedSources[0].Source.Should().Be("file.txt");
        roundTrip.IndexedSources[0].ChunkCount.Should().Be(7);
    }

    [Fact]
    public void SourceStatus_SerializesCorrectFields()
    {
        var src = new SourceStatus { Source = "report.pdf", ChunkCount = 42 };

        var json = JsonSerializer.Serialize(src);

        json.Should().Contain("\"source\":\"report.pdf\"");
        json.Should().Contain("\"chunk_count\":42");
        json.Should().NotContain("\"Source\"");
        json.Should().NotContain("\"ChunkCount\"");
    }
}
