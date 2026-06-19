## Code Conventions

- Minimal API / top-level program style — no controllers, no heavy DI abstractions
- Interfaces (`IEmbeddingService`, `IRerankingService`, `IContextualEnricher`) are in `RagDemo.Core` for testability
- `HttpClient` instances should be injected or reused — never instantiate per-call
- Secrets come exclusively from environment variables — never hardcode API keys
- New tests go in `RagDemo.Tests/` using xUnit
