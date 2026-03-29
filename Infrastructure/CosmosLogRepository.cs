using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using IncidentCopilot.Models;
using System.Net;

namespace IncidentCopilot.Infrastructure;

public class CosmosLogRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosLogRepository> _logger;

    public CosmosLogRepository(
        CosmosClient client,
        IOptions<CosmosDbSettings> settings,
        ILogger<CosmosLogRepository> logger)
    {
        var s = settings.Value;
        _container = client.GetContainer(s.DatabaseName, s.LogChunksContainer);
        _logger = logger;
    }

    // Create a new log chunk
    public async Task<LogChunk> CreateAsync(LogChunk chunk)
    {
        var response = await _container.CreateItemAsync(
            chunk,
            new PartitionKey(chunk.ServiceName)
        );

        _logger.LogInformation(
            "Created log chunk {ChunkId} for service {Service}, cost: {RU} RUs",
            chunk.Id, chunk.ServiceName, response.RequestCharge
        );

        return response.Resource;
    }

    // Get a log chunk by ID and service name
    public async Task<LogChunk?> GetByIdAsync(string id, string serviceName)
    {
        try
        {
            var response = await _container.ReadItemAsync<LogChunk>(
                id,
                new PartitionKey(serviceName)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // Get all log chunks for a specific service
    public async Task<List<LogChunk>> GetByServiceAsync(string serviceName)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.serviceName = @service"
        ).WithParameter("@service", serviceName);

        var results = new List<LogChunk>();
        using var iterator = _container.GetItemQueryIterator<LogChunk>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(serviceName)
            }
        );

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    // Get log chunks within a time window for a specific service
    public async Task<List<LogChunk>> GetByTimeRangeAsync(
        string serviceName, DateTime start, DateTime end)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.serviceName = @service " +
            "AND c.timeStart >= @start AND c.timeEnd <= @end " +
            "ORDER BY c.timeStart ASC"
        )
        .WithParameter("@service", serviceName)
        .WithParameter("@start", start)
        .WithParameter("@end", end);

        var results = new List<LogChunk>();
        using var iterator = _container.GetItemQueryIterator<LogChunk>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(serviceName)
            }
        );

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    // Vector search: find log chunks semantically similar to a query embedding
    public async Task<List<LogChunk>> VectorSearchAsync(
        float[] queryEmbedding, string? serviceName = null, int topK = 5)
    {
        // Cosmos DB vector search uses the VectorDistance function in SQL
        // It compares the query embedding against stored embeddings
        // and returns the closest matches ranked by similarity

        string sql;
        QueryDefinition query;

        if (serviceName != null)
        {
            // Search within a specific service
            sql = "SELECT TOP @topK c.id, c.serviceName, c.timeStart, c.timeEnd, " +
                  "c.severity, c.rawEntries, c.chunkText, " +
                  "VectorDistance(c.embedding, @embedding) AS similarityScore " +
                  "FROM c WHERE c.serviceName = @service " +
                  "ORDER BY VectorDistance(c.embedding, @embedding)";

            query = new QueryDefinition(sql)
                .WithParameter("@topK", topK)
                .WithParameter("@embedding", queryEmbedding)
                .WithParameter("@service", serviceName);
        }
        else
        {
            // Search across all services
            sql = "SELECT TOP @topK c.id, c.serviceName, c.timeStart, c.timeEnd, " +
                  "c.severity, c.rawEntries, c.chunkText, " +
                  "VectorDistance(c.embedding, @embedding) AS similarityScore " +
                  "FROM c " +
                  "ORDER BY VectorDistance(c.embedding, @embedding)";

            query = new QueryDefinition(sql)
                .WithParameter("@topK", topK)
                .WithParameter("@embedding", queryEmbedding);
        }

        var results = new List<LogChunk>();
        using var iterator = _container.GetItemQueryIterator<LogChunk>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    // Delete a log chunk
    public async Task DeleteAsync(string id, string serviceName)
    {
        await _container.DeleteItemAsync<LogChunk>(
            id,
            new PartitionKey(serviceName)
        );

        _logger.LogInformation("Deleted log chunk {ChunkId}", id);
    }

    // Bulk insert log chunks (used during ingestion)
    public async Task<int> BulkCreateAsync(List<LogChunk> chunks)
    {
        var tasks = chunks.Select(chunk =>
            _container.CreateItemAsync(chunk, new PartitionKey(chunk.ServiceName))
        );

        var results = await Task.WhenAll(tasks);
        var totalRUs = results.Sum(r => r.RequestCharge);

        _logger.LogInformation(
            "Bulk created {Count} log chunks, total cost: {RU} RUs",
            chunks.Count, totalRUs
        );

        return chunks.Count;
    }
}