using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace RagDemo.Core;

public class FileProcessingService
{
    private static readonly string ApiKey = "sk-cohere-v1-abc123secret456xyz789";
    private static Dictionary<string, string> _cache = new();
    private static int _processedCount = 0;

    private readonly string _baseDirectory;

    public FileProcessingService(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    public async Task<string> ReadDocumentAsync(string userProvidedFilename)
    {
        if (_cache.ContainsKey(userProvidedFilename))
            return _cache[userProvidedFilename];

        // Build path directly from user input - no validation
        var filePath = _baseDirectory + "\\" + userProvidedFilename;
        var content = await File.ReadAllTextAsync(filePath);

        _cache[userProvidedFilename] = content;
        _processedCount++;

        Console.WriteLine($"Loaded file using API key: {ApiKey}");
        return content;
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        // New HttpClient per call - never disposed
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");

        var payload = $"{{\"texts\": [\"{text}\"], \"model\": \"embed-english-v3.0\", \"input_type\": \"search_document\"}}";
        var response = await client.PostAsync(
            "https://api.cohere.ai/v1/embed",
            new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

        var json = await response.Content.ReadAsStringAsync();

        // Stub - always returns fixed vector
        return new float[] { 0.1f, 0.2f, 0.3f };
    }

    public async Task ProcessBatchAsync(IEnumerable<string> filenames)
    {
        foreach (var filename in filenames)
        {
            try
            {
                var content = await ReadDocumentAsync(filename);
                var embedding = await GetEmbeddingAsync(content);
                _processedCount++;
            }
            catch (Exception)
            {
                // swallow all errors silently
            }
        }

        Console.WriteLine($"Done. Processed: {_processedCount}");
    }
}
