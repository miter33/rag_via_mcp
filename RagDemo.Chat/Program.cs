using dotenv.net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using RagDemo.Core;
using RagDemo.Core.Models;
using Spectre.Console;

DotEnv.Load(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 5));

var groqKey   = Environment.GetEnvironmentVariable("GROQ_API_KEY");
var cohereKey = Environment.GetEnvironmentVariable("COHERE_API_KEY");

if (string.IsNullOrWhiteSpace(groqKey) || string.IsNullOrWhiteSpace(cohereKey))
{
    AnsiConsole.MarkupLine("[red bold]Missing API keys![/]");
    AnsiConsole.MarkupLine("Create a [yellow].env[/] file in the project root with:");
    AnsiConsole.MarkupLine("  [dim]GROQ_API_KEY=your_key[/]");
    AnsiConsole.MarkupLine("  [dim]COHERE_API_KEY=your_key[/]");
    AnsiConsole.MarkupLine("Get free keys at:");
    AnsiConsole.MarkupLine("  https://console.groq.com");
    AnsiConsole.MarkupLine("  https://dashboard.cohere.com");
    return 1;
}

// ── Dependency injection ──────────────────────────────────────────────────────
var services = new ServiceCollection();
services.AddSingleton<IEmbeddingService>(_ => new CohereEmbeddingService(new HttpClient(), cohereKey));
services.AddSingleton(_ => new QdrantClient("localhost", 6334));

#pragma warning disable SKEXP0010
services.AddSingleton(_ =>
    Kernel.CreateBuilder()
        .AddOpenAIChatCompletion(
            modelId:  "llama-3.1-8b-instant",
            apiKey:   groqKey,
            endpoint: new Uri("https://api.groq.com/openai/v1"))
        .Build());
#pragma warning restore SKEXP0010

services.AddSingleton<RagEngine>();
var sp = services.BuildServiceProvider();
var engine = sp.GetRequiredService<RagEngine>();

// ── Startup: verify Qdrant and show chunk count ───────────────────────────────
ulong chunkCount;
try
{
    await engine.EnsureCollectionAsync();
    chunkCount = await engine.GetChunkCountAsync();
}
catch
{
    AnsiConsole.MarkupLine("[red bold]Cannot connect to Qdrant on localhost:6334[/]");
    AnsiConsole.MarkupLine("Fix: run [yellow]docker compose up -d[/] from the project root, then retry.");
    return 1;
}

// ── Welcome banner ────────────────────────────────────────────────────────────
AnsiConsole.Write(new FigletText("RAG Demo").Color(Color.Blue));
AnsiConsole.MarkupLine($"Knowledge base: [green]{chunkCount}[/] chunk(s) indexed.");
AnsiConsole.MarkupLine("[dim]Commands: [yellow]/clear[/] to clear screen, [yellow]/quit[/] to exit.[/]\n");

if (chunkCount == 0)
    AnsiConsole.MarkupLine(
        "[yellow]No documents indexed yet. Run [bold]dotnet run --project RagDemo.Ingest[/] first.[/]\n");

// ── Interactive chat loop ─────────────────────────────────────────────────────
while (true)
{
    var input = AnsiConsole.Ask<string>("[bold blue]>[/]");

    if (string.IsNullOrWhiteSpace(input))
        continue;

    if (input.Equals("/quit", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine("[dim]Goodbye![/]");
        break;
    }

    if (input.Equals("/clear", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.Clear();
        continue;
    }

    // ── Query the RAG pipeline ────────────────────────────────────────────────
    QueryResult result;
    try
    {
        result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("[dim]Retrieving and generating...[/]",
                _ => engine.QueryAsync(input));
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
        continue;
    }

    // ── Display answer in a rounded panel ─────────────────────────────────────
    AnsiConsole.Write(new Panel(Markup.Escape(result.Answer))
    {
        Header = new PanelHeader("[bold green] Answer [/]"),
        Border = BoxBorder.Rounded,
        Padding = new Padding(1, 0)
    });

    // ── Display source citations as a tree ────────────────────────────────────
    if (result.Sources.Count > 0)
    {
        var tree = new Tree("[dim]Sources[/]");
        foreach (var source in result.Sources)
        {
            var name = Path.GetFileName(source.SourceFile);
            tree.AddNode(
                $"[yellow]{Markup.Escape(name)}[/] " +
                $"[dim]chunk #{source.ChunkIndex} · score {source.Score:F3}[/]");
        }
        AnsiConsole.Write(tree);
    }

    AnsiConsole.WriteLine();
}

return 0;
