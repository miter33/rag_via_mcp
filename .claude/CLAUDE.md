# CLAUDE.md

This file provides guidance to Claude when working with the `miter33/rag_via_mcp` codebase.

## Project Overview

A **Retrieval-Augmented Generation (RAG)** system built with C# / .NET 9, exposed as an **MCP (Model Context Protocol) server**. It ingests documents, stores embeddings in Qdrant, and answers questions using retrieved context via a Groq LLM.

## Solution Structure

| Project | Purpose |
|---|---|
| `RagDemo.Core` | Core RAG logic — embedding, retrieval, reranking, ingestion pipeline |
| `RagDemo.Ingest` | CLI tool to ingest documents into Qdrant |
| `RagDemo.Chat` | Interactive CLI chat interface |
| `RagDemo.McpServer` | MCP server exposing RAG as tools for AI assistants |
| `RagDemo.Tests` | xUnit test project |

## Key Technologies

- **Embeddings**: Cohere (`embed-english-v3.0`, 1024-dim vectors)
- **Vector store**: Qdrant (collection: `rag-demo`, cosine distance)
- **LLM**: Groq via Microsoft Semantic Kernel (`OpenAIChatCompletion` pointing at Groq endpoint)
- **Reranking**: Cohere reranking API (optional, improves retrieval quality)
- **Contextual RAG**: Optional LLM-based chunk enrichment before embedding (`--contextual` flag)
- **MCP transport**: stdio (`WithStdioServerTransport`)

## Commands

```bash
# Start Qdrant (required before running anything)
docker compose up -d

# Ingest documents from docs/ folder
dotnet run --project RagDemo.Ingest

# Ingest with contextual RAG enrichment
dotnet run --project RagDemo.Ingest -- --contextual

# Start MCP server
dotnet run --project RagDemo.McpServer

# Interactive chat
dotnet run --project RagDemo.Chat

# Run tests
dotnet test
```

## Environment Variables

Copy `.env.example` to `.env` and fill in:

```
GROQ_API_KEY=...
COHERE_API_KEY=...
```

The `.env` file is loaded automatically via `dotenv.net` — it searches the current directory and up to 5 parent directories.

## Architecture Notes

- **Ingest path**: `DocumentChunk` → Cohere embed → Qdrant upsert
- **Query path**: question → Cohere embed → Qdrant search → (optional rerank) → context → Groq LLM → answer
- `RagEngine` in `RagDemo.Core/RagEngine.cs` orchestrates the full pipeline
- Qdrant runs on `localhost:6334` (gRPC) and `localhost:6333` (HTTP)
- Each Qdrant point stores both the dense vector and a payload (text + source metadata) for citation reconstruction

## Code Conventions

- Minimal API / top-level program style — no controllers, no heavy DI abstractions
- Interfaces (`IEmbeddingService`, `IRerankingService`, `IContextualEnricher`) are in `RagDemo.Core` for testability
- `HttpClient` instances should be injected or reused — never instantiate per-call
- Secrets come exclusively from environment variables — never hardcode API keys
- New tests go in `RagDemo.Tests/` using xUnit

## CI/CD (GitHub Actions)

Three automated workflows in `.github/workflows/`:

| Workflow | Trigger | What it does |
|---|---|---|
| `claude.yml` | `@claude` mention in PR/issue | General-purpose Claude assistant |
| `claude-code-review.yml` | PR opened/updated | Posts inline review comments |
| `claude-tests.yml` | PR opened/updated (non-bot only) | Generates xUnit tests for new/changed code |

All workflows use `claude-haiku-4-5-20251001` and `CLAUDE_CODE_OAUTH_TOKEN` secret.

## CI/CD Context

When running in GitHub Actions:
- Do not modify configuration files unless explicitly asked
- Always create a new branch for changes, never commit directly to main
- Format PR comments using GitHub-flavoured Markdown
- Include file paths as clickable links in review comments