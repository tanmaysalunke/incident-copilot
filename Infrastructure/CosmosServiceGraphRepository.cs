using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using IncidentCopilot.Models;
using System.Net;

namespace IncidentCopilot.Infrastructure;

public class CosmosServiceGraphRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosServiceGraphRepository> _logger;

    public CosmosServiceGraphRepository(
        CosmosClient client,
        IOptions<CosmosDbSettings> settings,
        ILogger<CosmosServiceGraphRepository> logger)
    {
        var s = settings.Value;
        _container = client.GetContainer(s.DatabaseName, s.ServiceGraphContainer);
        _logger = logger;
    }

    // Add or update a service in the graph
    public async Task<ServiceNode> UpsertAsync(ServiceNode node)
    {
        // Upsert = insert if new, update if exists
        // Like Python's dict.update() but for the database
        var response = await _container.UpsertItemAsync(
            node,
            new PartitionKey(node.Id)
        );

        _logger.LogInformation(
            "Upserted service node {ServiceName}, cost: {RU} RUs",
            node.Id, response.RequestCharge
        );

        return response.Resource;
    }

    // Get a service by name
    public async Task<ServiceNode?> GetByIdAsync(string serviceName)
    {
        try
        {
            var response = await _container.ReadItemAsync<ServiceNode>(
                serviceName,
                new PartitionKey(serviceName)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // Get all services in the graph
    public async Task<List<ServiceNode>> GetAllAsync()
    {
        var query = new QueryDefinition("SELECT * FROM c");
        var results = new List<ServiceNode>();

        using var iterator = _container.GetItemQueryIterator<ServiceNode>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    // Get a service and all its related services (upstream + downstream)
    public async Task<List<ServiceNode>> GetRelatedServicesAsync(string serviceName)
    {
        var service = await GetByIdAsync(serviceName);
        if (service == null) return new List<ServiceNode>();

        var relatedNames = service.UpstreamServices
            .Concat(service.DownstreamServices)
            .Distinct()
            .ToList();

        var results = new List<ServiceNode> { service };

        foreach (var name in relatedNames)
        {
            var related = await GetByIdAsync(name);
            if (related != null) results.Add(related);
        }

        return results;
    }

    // Delete a service from the graph
    public async Task DeleteAsync(string serviceName)
    {
        await _container.DeleteItemAsync<ServiceNode>(
            serviceName,
            new PartitionKey(serviceName)
        );

        _logger.LogInformation("Deleted service node {ServiceName}", serviceName);
    }
}