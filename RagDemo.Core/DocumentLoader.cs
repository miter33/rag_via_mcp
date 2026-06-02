using RagDemo.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace RagDemo.Core;

/// <summary>
/// Loads supported files (.txt, .md, .pdf) and splits their text into overlapping chunks.
///
/// RAG concept: LLMs and embedding models have token limits. Chunking breaks large
/// documents into pieces that fit within those limits. Overlap ensures sentences
/// that straddle a chunk boundary appear complete in at least one chunk, which
/// prevents information from being silently lost during retrieval.
/// </summary>
public sealed class DocumentLoader
{
    private const int ChunkSize = 500;
    private const int Overlap   = 50;

    /// <summary>Recursively scans <paramref name="docsPath"/> and yields chunks for all supported files.</summary>
    public async IAsyncEnumerable<DocumentChunk> LoadFromFolderAsync(string docsPath)
    {
        var files = Directory.EnumerateFiles(docsPath, "*.*", SearchOption.AllDirectories)
            .Where(IsSupported);

        foreach (var file in files)
            await foreach (var chunk in LoadFileAsync(file))
                yield return chunk;
    }

    /// <summary>Loads and chunks a single file. Used by the Ingest app per-file for progress tracking.</summary>
    public async IAsyncEnumerable<DocumentChunk> LoadFileAsync(string filePath)
    {
        var text = await ExtractTextAsync(filePath);
        await foreach (var chunk in ChunkTextAsync(text, filePath))
            yield return chunk;
    }

    /// <summary>
    /// Splits arbitrary text into overlapping chunks. Public so unit tests can exercise
    /// the chunking logic directly without needing real files on disk.
    /// </summary>
    public async IAsyncEnumerable<DocumentChunk> ChunkTextAsync(string text, string sourceFile)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        // step = ChunkSize - Overlap ensures each successive chunk starts Overlap chars
        // before where the previous one ended, creating the desired overlap region.
        int step       = ChunkSize - Overlap;
        int chunkIndex = 0;

        for (int i = 0; i < text.Length; i += step)
        {
            int length = Math.Min(ChunkSize, text.Length - i);
            yield return new DocumentChunk
            {
                Text       = text.Substring(i, length),
                SourceFile = sourceFile,
                ChunkIndex = chunkIndex++
            };

            if (i + length >= text.Length) yield break;
        }

        await Task.CompletedTask; // satisfies the async iterator requirement
    }

    private static bool IsSupported(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".txt" or ".md" or ".pdf";
    }

    private static async Task<string> ExtractTextAsync(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() == ".pdf"
            ? ExtractPdfText(filePath)
            : await File.ReadAllTextAsync(filePath);

    /// <summary>
    /// Extracts raw text from a PDF using PdfPig (pure .NET, no native deps).
    /// Pages are concatenated in order; layout fidelity varies by PDF structure.
    /// </summary>
    private static string ExtractPdfText(string filePath)
    {
        using var doc = PdfDocument.Open(filePath);
        var sb = new System.Text.StringBuilder();
        foreach (Page page in doc.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }
}
