using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace RagDemo.Core;

/// <summary>
/// Calls the Groq LLM to generate a situating context for each chunk before embedding.
///
/// Contextual RAG concept: raw chunks often lack the context needed for precise retrieval.
/// "Results showed a 12% improvement" is ambiguous without knowing it is from a CV describing
/// a performance optimization. Prepending a short LLM-generated context before embedding
/// gives the vector a richer semantic footprint and improves cosine similarity matching.
/// </summary>
public sealed class GroqContextualEnricher : IContextualEnricher
{
    private readonly Kernel _kernel;

    public GroqContextualEnricher(Kernel kernel) => _kernel = kernel;

    public async Task<string> EnrichAsync(string documentText, string chunkText)
    {
        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddUserMessage(
            $"<document>\n{documentText}\n</document>\n\n" +
            $"<chunk>\n{chunkText}\n</chunk>\n\n" +
            "Give a short succinct context (1-2 sentences) to situate this chunk within the overall " +
            "document for the purpose of improving search retrieval. " +
            "Reply with only the context and nothing else.");

        // Retry on HTTP 429: Groq's message always contains "Please try again in X.XXs",
        // so we parse that instead of guessing a fixed delay.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var response = await chat.GetChatMessageContentAsync(history);
                return response.Content ?? string.Empty;
            }
            catch (HttpOperationException ex)
                when (ex.StatusCode == HttpStatusCode.TooManyRequests && attempt < 2)
            {
                var waitSeconds = ParseRetryAfterSeconds(ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
            }
        }

        throw new InvalidOperationException("Unreachable: loop exhausted without returning or throwing");
    }

    // Extracts "17.21" from "Please try again in 17.21s." and adds a 1s buffer.
    // Falls back to 20s if the pattern isn't found.
    private static double ParseRetryAfterSeconds(string message)
    {
        var match = Regex.Match(message, @"try again in (\d+\.?\d*)s");
        return match.Success && double.TryParse(
                match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var seconds)
            ? seconds + 1
            : 20;
    }
}
