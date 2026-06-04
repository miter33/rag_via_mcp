using System.Text.Json;
using dotenv.net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using RagDemo.Core;
using RagDemo.Core.Models;
using Spectre.Console;

// Load .env by searching current dir and up to 5 parent directories.
DotEnv.Load(options: new DotEnvOptions(probeForEnv: true, probeLevelsToSearch: 5));

var groqKey   = Environment.GetEnvironmentVariable("GROQ_API_KEY")
                ?? Die("GROQ_API_KEY not found — add it to your .env file");
var cohereKey = Environment.GetEnvironmentVariable("COHERE_API_KEY")
                ?? Die("COHERE_API_KEY not found — add it to your .env file");

// Optional --docs <path> argument; default: search up for a docs/ folder
var docsPath = args.SkipWhile(a => a != "--docs").Skip(1).FirstOrDefault()
               ?? FindDocsFolder(Directory.GetCurrentDirectory());

// Optional --contextual flag: enables Contextual RAG (LLM enriches each chunk before embedding)
var useContextual = args.Contains("--contextual");

if (!Directory.Exists(docsPath))
{
    AnsiConsole.MarkupLine($"[red]Docs folder not found:[/] {docsPath}");
    AnsiConsole.MarkupLine("Create a [yellow]docs/[/] folder and drop .txt/.md/.pdf files in it.");
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

services.AddSingleton<RagEngine>(sp => new RagEngine(
    sp.GetRequiredService<IEmbeddingService>(),
    sp.GetRequiredService<QdrantClient>(),
    sp.GetRequiredService<Kernel>(),
    useContextual ? new GroqContextualEnricher(sp.GetRequiredService<Kernel>()) : null));
services.AddSingleton<DocumentLoader>();
var sp = services.BuildServiceProvider();

var engine = sp.GetRequiredService<RagEngine>();
var loader = sp.GetRequiredService<DocumentLoader>();

// ── Qdrant connectivity check ─────────────────────────────────────────────────
try { await engine.EnsureCollectionAsync(); }
catch
{
    AnsiConsole.MarkupLine("[red bold]Cannot connect to Qdrant on localhost:6334[/]");
    AnsiConsole.MarkupLine("Fix: run [yellow]docker compose up -d[/] from the project root, then retry.");
    return 1;
}

// ── Idempotency manifest ──────────────────────────────────────────────────────
// Tracks filename → last-modified UTC so we skip files that haven't changed.
const string ManifestPath = "ingested.json";
var manifest = LoadManifest(ManifestPath);

var allFiles = GetSupportedFiles(docsPath);
var pending  = allFiles.Where(f => IsModified(f, manifest)).ToList();

var strategyLabel = useContextual ? "[cyan]Contextual RAG[/] (LLM enriches each chunk)" : "[grey]Naive RAG[/]";
AnsiConsole.MarkupLine($"Strategy: {strategyLabel}");
AnsiConsole.MarkupLine(
    $"Found [bold]{allFiles.Count}[/] file(s) in docs/, [yellow]{pending.Count}[/] need ingestion.");

if (pending.Count == 0)
{
    AnsiConsole.MarkupLine("[green]All files are up to date — nothing to ingest.[/]");
    return 0;
}

int filesIngested = 0, totalChunks = 0;

await AnsiConsole.Progress()
    .AutoClear(false)
    .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new SpinnerColumn())
    .StartAsync(async ctx =>
    {
        var progressTask = ctx.AddTask("[green]Ingesting files[/]", maxValue: pending.Count);

        foreach (var file in pending)
        {
            progressTask.Description = $"[blue]{Path.GetFileName(file)}[/]";

            // Collect chunks first so we can count them
            var chunks = new List<DocumentChunk>();
            await foreach (var c in loader.LoadFileAsync(file))
                chunks.Add(c);

            await engine.IngestAsync(AsAsyncEnumerable(chunks));

            manifest[file] = File.GetLastWriteTimeUtc(file).ToString("o");
            SaveManifest(ManifestPath, manifest);

            totalChunks += chunks.Count;
            filesIngested++;
            progressTask.Increment(1);
        }
    });

AnsiConsole.MarkupLine(
    $"\n[green bold]Done![/] {filesIngested} file(s) ingested, {totalChunks} chunk(s) stored in Qdrant.");
return 0;

// ── Helpers ───────────────────────────────────────────────────────────────────

static string Die(string message)
{
    AnsiConsole.MarkupLine($"[red bold]Error:[/] {message}");
    Environment.Exit(1);
    return null!; // unreachable; return satisfies the ?? operator's type requirement
}

static List<string> GetSupportedFiles(string path) =>
    Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
        .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".txt" or ".md" or ".pdf")
        .ToList();

static bool IsModified(string file, Dictionary<string, string> manifest) =>
    !manifest.TryGetValue(file, out var recorded) ||
    recorded != File.GetLastWriteTimeUtc(file).ToString("o");

static Dictionary<string, string> LoadManifest(string path) =>
    File.Exists(path)
        ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new()
        : new();

static void SaveManifest(string path, Dictionary<string, string> m) =>
    File.WriteAllText(path, JsonSerializer.Serialize(m, new JsonSerializerOptions { WriteIndented = true }));

static string FindDocsFolder(string start)
{
    var dir = start;
    for (int i = 0; i < 5; i++)
    {
        var candidate = Path.Combine(dir, "docs", "custom");
        if (Directory.Exists(candidate)) return candidate;
        dir = Path.GetDirectoryName(dir) ?? dir;
    }
    return Path.Combine(start, "docs", "custom");
}

// Converts List<T> to IAsyncEnumerable<T> without requiring extra packages.
static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(IEnumerable<T> source)
{
    foreach (var item in source) yield return item;
    await Task.CompletedTask;
}
