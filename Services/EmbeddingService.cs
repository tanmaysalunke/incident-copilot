using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using IncidentCopilot.Models;
using OpenAI.Embeddings;

namespace IncidentCopilot.Services;

/// <summary>
/// Generates vector embeddings from text using Azure OpenAI.
///
/// What are embeddings?
/// An embedding is a list of numbers (a vector) that represents the "meaning"
/// of a piece of text. Similar texts produce similar vectors. For example:
/// - "database connection timeout" and "DB connection pool exhausted"
///   would have very similar vectors (close in vector space)
/// - "database connection timeout" and "user logged in successfully"
///   would have very different vectors (far apart in vector space)
///
/// This lets us do semantic search: instead of matching exact keywords,
/// we find logs that are conceptually similar to the user's question.
/// </summary>
public class EmbeddingService
{
    private readonly EmbeddingClient _client;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        IOptions<AzureOpenAISettings> settings,
        ILogger<EmbeddingService> logger)
    {
        _logger = logger;

        var s = settings.Value;
        var azureClient = new AzureOpenAIClient(
            new Uri(s.Endpoint),
            new ApiKeyCredential(s.ApiKey)
        );

        _client = azureClient.GetEmbeddingClient(s.EmbeddingDeployment);
        _logger.LogInformation("Embedding service initialized with deployment: {Deployment}", s.EmbeddingDeployment);
    }

    /// <summary>
    /// Generate an embedding vector for a single piece of text.
    /// Returns a float array of 1536 dimensions.
    /// </summary>
    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        // Truncate very long texts to avoid token limits
        // text-embedding-3-small has an 8191 token limit
        var truncated = TruncateText(text, 7000);

        var result = await _client.GenerateEmbeddingAsync(truncated);
        var embedding = result.Value.ToFloats().ToArray();

        _logger.LogDebug(
            "Generated embedding: {Dimensions} dimensions for {Length} chars of text",
            embedding.Length, truncated.Length
        );

        return embedding;
    }

    /// <summary>
    /// Generate embeddings for multiple texts in a single batch call.
    /// More efficient than calling one at a time.
    /// </summary>
    public async Task<List<float[]>> GenerateEmbeddingBatchAsync(List<string> texts)
    {
        var truncated = texts.Select(t => TruncateText(t, 7000)).ToList();

        var result = await _client.GenerateEmbeddingsAsync(truncated);

        var embeddings = result.Value
            .OrderBy(e => e.Index)
            .Select(e => e.ToFloats().ToArray())
            .ToList();

        _logger.LogInformation(
            "Generated {Count} embeddings in batch",
            embeddings.Count
        );

        return embeddings;
    }

    /// <summary>
    /// Truncate text to approximately the given character count.
    /// This is a rough approximation since tokens != characters,
    /// but it prevents hitting the API's token limit.
    /// </summary>
    private string TruncateText(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;

        _logger.LogWarning(
            "Truncating text from {Original} to {Max} characters",
            text.Length, maxChars
        );

        return text[..maxChars];
    }
}