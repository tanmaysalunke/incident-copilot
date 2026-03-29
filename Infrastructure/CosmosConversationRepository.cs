using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using IncidentCopilot.Models;
using System.Net;

namespace IncidentCopilot.Infrastructure;

public class CosmosConversationRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosConversationRepository> _logger;

    public CosmosConversationRepository(
        CosmosClient client,
        IOptions<CosmosDbSettings> settings,
        ILogger<CosmosConversationRepository> logger)
    {
        var s = settings.Value;
        _container = client.GetContainer(s.DatabaseName, s.ConversationsContainer);
        _logger = logger;
    }

    public async Task<ConversationSession> CreateAsync(ConversationSession session)
    {
        var response = await _container.CreateItemAsync(
            session,
            new PartitionKey(session.SessionId)
        );
        return response.Resource;
    }

    public async Task<ConversationSession?> GetBySessionIdAsync(string sessionId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.sessionId = @sessionId"
        ).WithParameter("@sessionId", sessionId);

        using var iterator = _container.GetItemQueryIterator<ConversationSession>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(sessionId)
            }
        );

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }

        return null;
    }

    public async Task<ConversationSession> UpdateAsync(ConversationSession session)
    {
        var response = await _container.UpsertItemAsync(
            session,
            new PartitionKey(session.SessionId)
        );
        return response.Resource;
    }
}