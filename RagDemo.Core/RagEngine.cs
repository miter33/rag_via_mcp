using System.Security.Cryptography;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RagDemo.Core.Models;

namespace RagDemo.Core;

/// <summary>
/// Orchestrates the complete RAG pipeline.
///
/// Ingest path:  DocumentChunk → Cohere embed → Qdrant upsert
/// Query path:   question → Cohere embed → Qdrant search → context → Groq LLM → answer
///
/// The vector store (Qdrant) is the bridge between ingestion and retrieval.
/// Each stored point contains both the dense vector (for similarity search)
/// and a payload (text + source metadata) so we can reconstruct citations
/// without re-reading the original files.
/// </summary>
public sealed class RagEngine
{
    private const string CollectionName = "rag-demo";
    private const ulong  VectorSize     = 1024;

    private readonly IEmbeddingService   _embeddings;
    private readonly QdrantClient        _qdrant;
    private readonly Kernel              _kernel;
    private readonly IContextualEnricher? _enricher;

    public RagEngine(IEmbeddingService embeddings, QdrantClient qdrant, Kernel kernel,
        IContextualEnricher? enricher = null)
    {
        _embeddings = embeddings;
        _qdrant     = qdrant;
        _kernel     = kernel;
        _enricher   = enricher;
    }

    /// <summary>Creates the Qdrant collection if it doesn't exist. Safe to call on every startup.</summary>
    public async Task EnsureCollectionAsync()
    {
        if (!await _qdrant.CollectionExistsAsync(CollectionName))
        {
            await _qdrant.CreateCollectionAsync(CollectionName,
                new VectorParams { Size = VectorSize, Distance = Distance.Cosine });
        }
    }

    /// <summary>
    /// Embeds each chunk and upserts it into Qdrant.
    /// When an enricher is configured (Contextual RAG), the LLM-generated context is prepended
    /// to the chunk text before embedding so the vector captures richer semantics.
    /// The payload always stores the original chunk text for citation display.
    /// </summary>
    public async Task IngestAsync(IAsyncEnumerable<DocumentChunk> chunks)
    {
        await foreach (var chunk in chunks)
        {
            string context     = string.Empty;
            string textToEmbed = chunk.Text;

            if (_enricher != null && !string.IsNullOrEmpty(chunk.FullDocumentText))
            {
                context     = await _enricher.EnrichAsync(chunk.FullDocumentText, chunk.Text);
                textToEmbed = string.IsNullOrEmpty(context)
                    ? chunk.Text
                    : context + "\n\n" + chunk.Text;
            }

            float[] embedding = await _embeddings.GetEmbeddingAsync(textToEmbed, EmbeddingType.Document);

            // Deterministic ID: same source + same chunk index = same Guid, enabling true upsert semantics
            var idBytes = MD5.HashData(
                Encoding.UTF8.GetBytes($"{chunk.SourceFile}:{chunk.ChunkIndex}"));
            var deterministicId = new Guid(idBytes);

            var point = new PointStruct
            {
                Id      = deterministicId,
                Vectors = (Vectors)embedding
            };
            point.Payload["text"]        = new Qdrant.Client.Grpc.Value { StringValue  = chunk.Text };
            point.Payload["source"]      = new Qdrant.Client.Grpc.Value { StringValue  = chunk.SourceFile };
            point.Payload["chunk_index"] = new Qdrant.Client.Grpc.Value { IntegerValue = chunk.ChunkIndex };
            point.Payload["context"]     = new Qdrant.Client.Grpc.Value { StringValue  = context };

            await _qdrant.UpsertAsync(CollectionName, new[] { point });
        }
    }

    /// <summary>
    /// Full RAG query:
    /// 1. Embed the question (search_query mode)
    /// 2. Retrieve top-N nearest chunks from Qdrant
    /// 3. Build context from those chunks
    /// 4. Ask Groq to answer using only that context
    /// </summary>
    public async Task<QueryResult> QueryAsync(string question, int topN = 3)
    {
        float[] queryVector = await _embeddings.GetEmbeddingAsync(question, EmbeddingType.Query);

        var hits = await _qdrant.SearchAsync(
            CollectionName,
            new ReadOnlyMemory<float>(queryVector),
            limit: (ulong)topN,
            payloadSelector: true);

        var sources = hits.Select(h => new ChunkSource
        {
            Text       = h.Payload["text"].StringValue,
            SourceFile = h.Payload["source"].StringValue,
            ChunkIndex = (int)h.Payload["chunk_index"].IntegerValue,
            Score      = h.Score,
            Context    = h.Payload.TryGetValue("context", out var cv) ? cv.StringValue : string.Empty
        }).ToList();

        var context = string.Join("\n\n---\n\n",
            sources.Select((s, i) =>
            {
                var body = string.IsNullOrEmpty(s.Context) ? s.Text : $"{s.Context}\n\n{s.Text}";
                return $"[{i + 1}] (from {Path.GetFileName(s.SourceFile)}, chunk {s.ChunkIndex}):\n{body}";
            }));

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(
            "You are a concise assistant. Answer the user's question using ONLY the context " +
            "provided below. Cite each context block you use with its number, e.g. [1]. " +
            "If the answer is not in the context, say exactly: " +
            "\"I don't know based on the provided documents.\"");
        history.AddUserMessage($"Context:\n{context}\n\nQuestion: {question}");

        var response = await chat.GetChatMessageContentAsync(history);

        return new QueryResult
        {
            Answer  = response.Content ?? "No answer generated.",
            Sources = sources
        };
    }

    /// <summary>Returns total points stored — shown on Chat startup.</summary>
    public async Task<ulong> GetChunkCountAsync()
    {
        var info = await _qdrant.GetCollectionInfoAsync(CollectionName);
        return info.PointsCount;
    }
}
