using dotenv.net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qdrant.Client;
using RagDemo.Core;
using RagDemo.McpServer.Tools;

DotEnv.Load(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 5));

var cohereKey = Environment.GetEnvironmentVariable("COHERE_API_KEY")
    ?? throw new InvalidOperationException("COHERE_API_KEY not set");

var builder = Host.CreateApplicationBuilder(args);

// Register services the tool needs
builder.Services.AddSingleton<IEmbeddingService>(
    _ => new CohereEmbeddingService(new HttpClient(), cohereKey));
builder.Services.AddSingleton<IRerankingService>(
    _ => new CohereRerankingService(new HttpClient(), cohereKey));
builder.Services.AddSingleton(_ => new QdrantClient("localhost", 6334));

// Register MCP server with stdio transport
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(typeof(DocumentSearchTools).Assembly);

await builder.Build().RunAsync();
