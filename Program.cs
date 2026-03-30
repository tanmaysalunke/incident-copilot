using Microsoft.Azure.Cosmos;
using IncidentCopilot.Models;
using IncidentCopilot.Infrastructure;
using IncidentCopilot.Services;
using Serilog;

// Configure Serilog with correlation ID support
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext() // This enables correlation IDs in log output
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting Incident Copilot API");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // Bind configuration sections
    builder.Services.Configure<CosmosDbSettings>(
        builder.Configuration.GetSection("CosmosDb"));
    builder.Services.Configure<AzureOpenAISettings>(
        builder.Configuration.GetSection("AzureOpenAI"));

    // Register Cosmos DB client and repositories
    var cosmosDbSettings = builder.Configuration.GetSection("CosmosDb").Get<CosmosDbSettings>();

    if (cosmosDbSettings != null && !string.IsNullOrEmpty(cosmosDbSettings.Endpoint)
        && !string.IsNullOrEmpty(cosmosDbSettings.Key))
    {
        var cosmosClient = new CosmosClient(
            cosmosDbSettings.Endpoint,
            cosmosDbSettings.Key,
            new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
                ConnectionMode = ConnectionMode.Direct,
                ApplicationRegion = Regions.WestUS2
            }
        );

        builder.Services.AddSingleton(cosmosClient);
        builder.Services.AddSingleton<CosmosDbInitializer>();
        builder.Services.AddSingleton<CosmosLogRepository>();
        builder.Services.AddSingleton<CosmosServiceGraphRepository>();
        builder.Services.AddSingleton<CosmosIncidentRepository>();
        builder.Services.AddSingleton<CosmosConversationRepository>();

        // Register pipeline services
        builder.Services.AddSingleton<LogNormalizer>();
        builder.Services.AddSingleton<TemporalChunker>();
        builder.Services.AddSingleton<EmbeddingService>();
        builder.Services.AddSingleton<IngestionService>();
        builder.Services.AddSingleton<RetrievalService>();
        builder.Services.AddSingleton<IncidentInvestigationPlugin>();
        builder.Services.AddSingleton<InvestigationService>();
    }
    else
    {
        Log.Warning("Cosmos DB settings not configured. Database features will not be available.");
    }

    builder.Services.AddControllers();

    var app = builder.Build();

    // Middleware pipeline (order matters!)
    app.UseMiddleware<CorrelationIdMiddleware>();  // First: assign correlation ID
    app.UseMiddleware<ExceptionMiddleware>();       // Second: catch any exceptions

    // Initialize database on startup
    if (app.Services.GetService<CosmosDbInitializer>() != null)
    {
        var initializer = app.Services.GetRequiredService<CosmosDbInitializer>();
        await initializer.InitializeAsync();
    }

    app.MapControllers();

    Log.Information("Incident Copilot API is running");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}