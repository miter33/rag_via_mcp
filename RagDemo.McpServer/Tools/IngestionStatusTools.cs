using System.ComponentModel;
using ModelContextProtocol.Server;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace RagDemo.McpServer.Tools;

[McpServerToolType]
public static class IngestionStatusTools
{
    private const string CollectionName = "rag-demo";
    private const uint ScrollPageSize = 250;

    [McpServerTool(Name = "get_ingestion_status")]
    [Description(
        "Returns how many documents and chunks are currently indexed in the vector store. " +
        "Optionally scope the result to a single source file by providing its name.")]
    public static async Task<string> GetIngestionStatusAsync(
        QdrantClient qdrant,
        [Description("Optional source filename (e.g. \"report.pdf\") to scope to a single file.")]
        string? source = null)
    {
        if (!await qdrant.CollectionExistsAsync(CollectionName))
            return "The vector store collection does not exist yet. Run the Ingest CLI to index documents.";

        bool isScoped = !string.IsNullOrWhiteSpace(source);

        ulong totalChunks = 0;
        if (!isScoped)
        {
            var info = await qdrant.GetCollectionInfoAsync(CollectionName);
            totalChunks = info.PointsCount;
        }

        Filter? filter = isScoped
            ? new Filter { Must = { Conditions.MatchKeyword("source", source!) } }
            : null;

        var sourceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        PointId? nextOffset = null;
        bool firstPage = true;

        while (firstPage || nextOffset is not null)
        {
            firstPage = false;
            var response = await qdrant.ScrollAsync(
                collectionName: CollectionName,
                filter: filter,
                limit: ScrollPageSize,
                offset: nextOffset,
                payloadSelector: true,
                vectorsSelector: false);

            foreach (var point in response.Result)
                if (point.Payload.TryGetValue("source", out var sv))
                {
                    var name = Path.GetFileName(sv.StringValue);
                    sourceCounts[name] = sourceCounts.GetValueOrDefault(name) + 1;
                }

            nextOffset = response.NextPageOffset;
        }

        return BuildReport(sourceCounts, totalChunks, isScoped, source);
    }

    private static string BuildReport(
        Dictionary<string, int> sourceCounts,
        ulong totalChunks,
        bool isScoped,
        string? requestedSource)
    {
        var sb = new System.Text.StringBuilder();

        if (isScoped)
        {
            int count = sourceCounts.Values.Sum();
            if (count == 0)
                return $"No chunks found for source \"{requestedSource}\". Check that the file was ingested and the name matches exactly.";
            sb.AppendLine($"Source: \"{requestedSource}\"");
            sb.AppendLine($"Indexed chunks: {count}");
            return sb.ToString().TrimEnd();
        }

        if (sourceCounts.Count == 0 && totalChunks == 0)
            return "Collection exists but is empty. Run the Ingest CLI to index documents.";

        sb.AppendLine("=== RAG Vector Store Status ===");
        sb.AppendLine($"Total chunks : {totalChunks}");
        sb.AppendLine($"Source files : {sourceCounts.Count}");
        sb.AppendLine();
        sb.AppendLine("Chunks per document:");
        foreach (var (name, count) in sourceCounts.OrderByDescending(x => x.Value))
            sb.AppendLine($"  {name}: {count}");

        return sb.ToString().TrimEnd();
    }
}
