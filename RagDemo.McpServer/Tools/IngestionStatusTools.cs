using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Qdrant.Client;
using RagDemo.Core.Models;

namespace RagDemo.McpServer.Tools;

[McpServerToolType]
public static class IngestionStatusTools
{
    private const string CollectionName = "rag-demo";

    [McpServerTool(Name = "get_ingestion_status")]
    [Description("Returns the current state of the document index: whether the Qdrant collection is ready, the total number of indexed chunks, and a per-source file breakdown of chunk counts.")]
    public static async Task<string> GetIngestionStatusAsync(QdrantClient qdrant)
    {
        bool exists = await qdrant.CollectionExistsAsync(CollectionName);
        if (!exists)
        {
            var empty = new IngestionStatus
            {
                IsCollectionReady = false,
                TotalChunks       = 0,
                IndexedSources    = Array.Empty<SourceStatus>()
            };
            return JsonSerializer.Serialize(empty, new JsonSerializerOptions { WriteIndented = true });
        }

        var info = await qdrant.GetCollectionInfoAsync(CollectionName);
        ulong totalChunks = info.PointsCount;

        var sourceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        Qdrant.Client.Grpc.PointId? offset = null;

        do
        {
            var (points, nextOffset) = await qdrant.ScrollAsync(
                CollectionName,
                limit:       250,
                offset:      offset,
                withPayload: true);

            foreach (var pt in points)
            {
                if (pt.Payload.TryGetValue("source", out var sv))
                    sourceCounts[sv.StringValue] = sourceCounts.GetValueOrDefault(sv.StringValue) + 1;
            }

            offset = nextOffset;
        }
        while (offset is not null);

        var status = new IngestionStatus
        {
            IsCollectionReady = true,
            TotalChunks       = totalChunks,
            IndexedSources    = sourceCounts
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => new SourceStatus { Source = kv.Key, ChunkCount = kv.Value })
                .ToList()
        };

        return JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
    }
}
