## Architecture Notes

- **Ingest path**: `DocumentChunk` → Cohere embed → Qdrant upsert
- **Query path**: question → Cohere embed → Qdrant search → (optional rerank) → context → Groq LLM → answer
- `RagEngine` in `RagDemo.Core/RagEngine.cs` orchestrates the full pipeline
- Qdrant runs on `localhost:6334` (gRPC) and `localhost:6333` (HTTP)
- Each Qdrant point stores both the dense vector and a payload (text + source metadata) for citation reconstruction
