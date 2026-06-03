using System.ComponentModel;
using ModelContextProtocol.Server;
using Qdrant.Client;
using RagDemo.Core;
using RagDemo.Core.Models;

namespace RagDemo.McpServer.Tools;

[McpServerToolType]
public static class DocumentSearchTools
{
    [McpServerTool(Name = "search_documents")]
    [Description("Search the indexed document knowledge base using semantic similarity. Returns the most relevant text chunks along with their source file and relevance score.")]
    public static async Task<string> SearchDocumentsAsync(
        IEmbeddingService embeddings,
        QdrantClient qdrant,
        [Description("The search query to find relevant document chunks for.")] string query,
        [Description("Number of top results to return (default: 5).")] int topN = 5)
    {
        topN = Math.Max(1, topN);

        float[] vector = await embeddings.GetEmbeddingAsync(query, EmbeddingType.Query);

        var hits = await qdrant.SearchAsync(
            "rag-demo",
            new ReadOnlyMemory<float>(vector),
            limit: (ulong)topN,
            payloadSelector: true);

        if (hits.Count == 0)
            return "No relevant documents found for this query.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Found {hits.Count} relevant chunk(s):");
        sb.AppendLine();

        for (int i = 0; i < hits.Count; i++)
        {
            var h = hits[i];
            var source = Path.GetFileName(h.Payload["source"].StringValue);
            var chunkIndex = h.Payload["chunk_index"].IntegerValue;
            var text = h.Payload["text"].StringValue;

            sb.AppendLine($"[{i + 1}] score: {h.Score:F3} | source: {source} | chunk #{chunkIndex}");
            sb.AppendLine(text);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
