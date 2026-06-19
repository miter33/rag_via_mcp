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
