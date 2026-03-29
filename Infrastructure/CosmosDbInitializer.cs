using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using IncidentCopilot.Models;
using System.Collections.ObjectModel;

namespace IncidentCopilot.Infrastructure;

// This class creates the database and containers on startup.
// Think of it like running database migrations in Django or Alembic.
public class CosmosDbInitializer
{
    private readonly CosmosClient _client;
    private readonly CosmosDbSettings _settings;
    private readonly ILogger<CosmosDbInitializer> _logger;

    public CosmosDbInitializer(
        CosmosClient client,
        IOptions<CosmosDbSettings> settings,
        ILogger<CosmosDbInitializer> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Cosmos DB database: {Database}", _settings.DatabaseName);

        // Create the database if it does not exist
        var databaseResponse = await _client.CreateDatabaseIfNotExistsAsync(
            _settings.DatabaseName
        );

        var database = databaseResponse.Database;
        _logger.LogInformation("Database ready: {Database}", _settings.DatabaseName);

        // Create the log-chunks container WITH vector indexing policy
        await CreateLogChunksContainerAsync(database);

        // Create the other three containers (no vector indexing needed)
        await CreateSimpleContainerAsync(database, _settings.ServiceGraphContainer, "/id");
        await CreateSimpleContainerAsync(database, _settings.ConversationsContainer, "/sessionId");
        await CreateSimpleContainerAsync(database, _settings.IncidentsContainer, "/id");

        _logger.LogInformation("All containers initialized successfully");
    }

    private async Task CreateLogChunksContainerAsync(Database database)
    {
        // This container needs a special indexing policy for vector search.
        // Vector search lets us find log chunks that are semantically similar
        // to a user's question, even if the exact words are different.
        //
        // DiskANN is the algorithm Cosmos DB uses for fast approximate
        // nearest-neighbor search on vectors. It is very efficient for
        // high-dimensional vectors (like our 1536-dimension embeddings).

        var containerProperties = new ContainerProperties
        {
            Id = _settings.LogChunksContainer,
            PartitionKeyPath = "/serviceName",
            // Standard indexing for non-vector fields
            IndexingPolicy = new IndexingPolicy
            {
                // Automatically index all fields
                Automatic = true,
                IndexingMode = IndexingMode.Consistent,
            },
            // Vector embedding policy: tells Cosmos DB about our embedding field
            VectorEmbeddingPolicy = new VectorEmbeddingPolicy(
                new Collection<Embedding>
                {
                    new Embedding
                    {
                        Path = "/embedding",
                        DataType = VectorDataType.Float32,
                        DistanceFunction = DistanceFunction.Cosine,
                        Dimensions = _settings.EmbeddingDimensions
                    }
                }
            )
        };

        // Add the vector index to the indexing policy
        // DiskANN is the recommended index type for production workloads
        containerProperties.IndexingPolicy.VectorIndexes.Add(
            new VectorIndexPath
            {
                Path = "/embedding",
                Type = VectorIndexType.DiskANN
            }
        );

        await database.CreateContainerIfNotExistsAsync(
            containerProperties,
            throughput: 1000
        );
        _logger.LogInformation("Container ready with vector index: {Container}", _settings.LogChunksContainer);
    }

    private async Task CreateSimpleContainerAsync(Database database, string containerName, string partitionKeyPath)
    {
        await database.CreateContainerIfNotExistsAsync(
            new ContainerProperties
            {
                Id = containerName,
                PartitionKeyPath = partitionKeyPath
            }
        );
        _logger.LogInformation("Container ready: {Container}", containerName);
    }
}