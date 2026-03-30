using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.Extensions.Options;
using IncidentCopilot.Models;
using IncidentCopilot.Infrastructure;

namespace IncidentCopilot.Services;

/// <summary>
/// Orchestrates AI-powered incident investigation using Semantic Kernel.
///
/// How it works:
/// 1. User sends a natural language question
/// 2. The question + conversation history is sent to gpt-4o
/// 3. gpt-4o decides which plugin functions to call (search logs, get dependencies, etc.)
/// 4. The function results are fed back to gpt-4o
/// 5. gpt-4o synthesizes a grounded answer with citations
/// 6. The conversation is saved to Cosmos DB for follow-up questions
///
/// In Python terms, this is like a LangChain agent with tools and memory.
/// </summary>
public class InvestigationService
{
    private readonly Kernel _kernel;
    private readonly CosmosConversationRepository _conversationRepo;
    private readonly ILogger<InvestigationService> _logger;

    private const string SystemPrompt = @"You are an expert Site Reliability Engineer (SRE) investigating production incidents. You have access to log data from multiple services and can search through them to find root causes.

When answering questions:
1. Use the available tools to search for relevant log data before answering.
2. Always cite specific log chunks by their ID when making claims (e.g., [chunk-id]).
3. Explain the chain of events chronologically when describing cascading failures.
4. If you find evidence of a root cause, explain the full dependency chain.
5. If you cannot find enough evidence, say so honestly and suggest what additional data might help.
6. Be concise but thorough. Engineers need actionable answers during incidents.

When investigating cascading failures:
- Start from the earliest error and trace forward through the dependency chain.
- Use the service dependency graph to understand which services affect which.
- Look for patterns like connection timeouts, circuit breaker activations, and health check failures.
- Pay attention to timing: the root cause usually appears minutes before downstream failures.";

    public InvestigationService(
        IOptions<AzureOpenAISettings> settings,
        IncidentInvestigationPlugin investigationPlugin,
        CosmosConversationRepository conversationRepo,
        ILogger<InvestigationService> logger)
    {
        _conversationRepo = conversationRepo;
        _logger = logger;

        var s = settings.Value;

        // Build the Semantic Kernel with Azure OpenAI and the investigation plugin
        var builder = Kernel.CreateBuilder();

        builder.AddAzureOpenAIChatCompletion(
            deploymentName: s.ChatDeployment,
            endpoint: s.Endpoint,
            apiKey: s.ApiKey
        );

        builder.Plugins.AddFromObject(investigationPlugin, "IncidentInvestigation");

        _kernel = builder.Build();

        _logger.LogInformation("Investigation service initialized with deployment: {Deployment}", s.ChatDeployment);
    }

    /// <summary>
    /// Investigate a question about an incident.
    /// Supports follow-up questions through conversation memory.
    /// </summary>
    public async Task<InvestigationResponse> InvestigateAsync(string question, string? sessionId = null)
    {
        _logger.LogInformation("Investigation query: {Question}, session: {Session}", question, sessionId);

        // Load or create conversation session
        ConversationSession session;
        ChatHistory chatHistory;

        if (!string.IsNullOrEmpty(sessionId))
        {
            session = await _conversationRepo.GetBySessionIdAsync(sessionId)
                      ?? CreateNewSession();
            chatHistory = RebuildChatHistory(session);
        }
        else
        {
            session = CreateNewSession();
            chatHistory = new ChatHistory(SystemPrompt);
        }

        // Add the user's question
        chatHistory.AddUserMessage(question);
        session.Messages.Add(new ChatMessage
        {
            Role = "user",
            Content = question,
            Timestamp = DateTime.UtcNow
        });

        // Get the AI response with function calling enabled
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        var executionSettings = new AzureOpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        _logger.LogInformation("Sending to AI with function calling enabled...");

        var response = await chatService.GetChatMessageContentAsync(
            chatHistory,
            executionSettings,
            _kernel
        );

        var answer = response.Content ?? "I was unable to generate a response.";

        _logger.LogInformation("AI response received: {Length} characters", answer.Length);

        // Save the assistant's response to conversation history
        session.Messages.Add(new ChatMessage
        {
            Role = "assistant",
            Content = answer,
            Timestamp = DateTime.UtcNow
        });

        // Save the conversation to Cosmos DB
        await _conversationRepo.UpdateAsync(session);

        // Extract cited chunk IDs from the response
        var citations = ExtractCitations(answer);

        return new InvestigationResponse
        {
            Answer = answer,
            SessionId = session.SessionId,
            CitedLogChunkIds = citations
        };
    }

    private ConversationSession CreateNewSession()
    {
        return new ConversationSession
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = Guid.NewGuid().ToString(),
            Messages = new List<ChatMessage>(),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Rebuild a ChatHistory from a saved conversation session.
    /// This is how follow-up questions work: the entire conversation
    /// history is sent to the LLM so it has context.
    /// </summary>
    private ChatHistory RebuildChatHistory(ConversationSession session)
    {
        var history = new ChatHistory(SystemPrompt);

        foreach (var msg in session.Messages)
        {
            if (msg.Role == "user")
                history.AddUserMessage(msg.Content);
            else if (msg.Role == "assistant")
                history.AddAssistantMessage(msg.Content);
        }

        return history;
    }

    /// <summary>
    /// Extract log chunk IDs from the AI's response.
    /// The LLM is instructed to cite chunks like [chunk-id-here].
    /// </summary>
    private List<string> ExtractCitations(string answer)
    {
        var citations = new List<string>();
        var startIndex = 0;

        while (true)
        {
            var openBracket = answer.IndexOf('[', startIndex);
            if (openBracket == -1) break;

            var closeBracket = answer.IndexOf(']', openBracket);
            if (closeBracket == -1) break;

            var content = answer.Substring(openBracket + 1, closeBracket - openBracket - 1);

            // Check if it looks like a chunk ID (contains hyphens, typical of GUIDs)
            if (content.Contains('-') && content.Length > 8)
            {
                citations.Add(content);
            }

            startIndex = closeBracket + 1;
        }

        return citations.Distinct().ToList();
    }
}

/// <summary>
/// Response from the investigation endpoint.
/// </summary>
public class InvestigationResponse
{
    public string Answer { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public List<string> CitedLogChunkIds { get; set; } = new();
}