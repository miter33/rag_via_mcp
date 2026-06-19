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
