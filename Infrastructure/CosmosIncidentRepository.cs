using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using IncidentCopilot.Models;
using System.Net;

namespace IncidentCopilot.Infrastructure;

public class CosmosIncidentRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosIncidentRepository> _logger;

    public CosmosIncidentRepository(
        CosmosClient client,
        IOptions<CosmosDbSettings> settings,
        ILogger<CosmosIncidentRepository> logger)
    {
        var s = settings.Value;
        _container = client.GetContainer(s.DatabaseName, s.IncidentsContainer);
        _logger = logger;
    }

    public async Task<Incident> CreateAsync(Incident incident)
    {
        var response = await _container.CreateItemAsync(
            incident,
            new PartitionKey(incident.Id)
        );

        _logger.LogInformation(
            "Created incident {IncidentId}: {Title}, cost: {RU} RUs",
            incident.Id, incident.Title, response.RequestCharge
        );

        return response.Resource;
    }

    public async Task<Incident?> GetByIdAsync(string id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Incident>(
                id,
                new PartitionKey(id)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Incident> UpdateAsync(Incident incident)
    {
        var response = await _container.UpsertItemAsync(
            incident,
            new PartitionKey(incident.Id)
        );
        return response.Resource;
    }

    public async Task<List<Incident>> GetAllAsync()
    {
        var query = new QueryDefinition("SELECT * FROM c ORDER BY c.startTime DESC");
        var results = new List<Incident>();

        using var iterator = _container.GetItemQueryIterator<Incident>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }
}