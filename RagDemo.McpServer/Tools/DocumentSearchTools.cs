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
        IRerankingService reranker,
        QdrantClient qdrant,
        [Description("The search query to find relevant document chunks for.")] string query,
        [Description("Number of top results to return (default: 5).")] int topN = 5)
    {
        topN = Math.Max(1, topN);

        float[] vector = await embeddings.GetEmbeddingAsync(query, EmbeddingType.Query);

        // Stage 1: fetch more candidates than needed so the re-ranker has something to work with
        int candidateCount = Math.Max(topN * 4, 20);
        var hits = await qdrant.SearchAsync(
            "rag-demo",
            new ReadOnlyMemory<float>(vector),
            limit: (ulong)candidateCount,
            payloadSelector: true);

        if (hits.Count == 0)
            return "No relevant documents found for this query.";

        // Stage 2: re-rank with cross-encoder and keep topN
        var texts    = hits.Select(h => {
            var text = h.Payload["text"].StringValue;
            var ctx  = h.Payload.TryGetValue("context", out var cv) ? cv.StringValue : string.Empty;
            return string.IsNullOrEmpty(ctx) ? text : $"{ctx}\n\n{text}";
        }).ToList();
        var reranked = await reranker.RerankAsync(query, texts, topN);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Found {reranked.Count} relevant chunk(s) (re-ranked from {hits.Count} candidates):");
        sb.AppendLine();

        for (int i = 0; i < reranked.Count; i++)
        {
            var h          = hits[reranked[i].Index];
            var source     = Path.GetFileName(h.Payload["source"].StringValue);
            var chunkIndex = h.Payload["chunk_index"].IntegerValue;
            var text       = h.Payload["text"].StringValue;
            var context    = h.Payload.TryGetValue("context", out var cv) ? cv.StringValue : string.Empty;

            sb.AppendLine($"[{i + 1}] score: {reranked[i].RelevanceScore:F3} | source: {source} | chunk #{chunkIndex}");
            if (!string.IsNullOrEmpty(context))
                sb.AppendLine($"context: {context}");
            sb.AppendLine(text);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
