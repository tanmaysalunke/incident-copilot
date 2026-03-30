using Microsoft.Azure.Cosmos;
using IncidentCopilot.Models;
using IncidentCopilot.Infrastructure;
using Serilog;
using IncidentCopilot.Services;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting Incident Copilot API");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // Bind configuration sections to settings classes
    builder.Services.Configure<CosmosDbSettings>(
        builder.Configuration.GetSection("CosmosDb"));
    builder.Services.Configure<AzureOpenAISettings>(
        builder.Configuration.GetSection("AzureOpenAI"));

    // Register the Cosmos DB client as a singleton (one instance shared across the app).
    // In Python, this is like creating a single database connection pool at startup.
    var cosmosDbSettings = builder.Configuration.GetSection("CosmosDb").Get<CosmosDbSettings>();

    if (cosmosDbSettings != null && !string.IsNullOrEmpty(cosmosDbSettings.Endpoint))
    {
        var cosmosClient = new CosmosClient(
            cosmosDbSettings.Endpoint,
            cosmosDbSettings.Key,
            new CosmosClientOptions
            {
                // Serialize enum values as strings (not numbers) in JSON
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
                // Use Direct mode for best performance
                ConnectionMode = ConnectionMode.Direct,
                // Set the application region to match your Cosmos DB location
                ApplicationRegion = Regions.WestUS2
            }
        );

        builder.Services.AddSingleton(cosmosClient);
        builder.Services.AddSingleton<CosmosDbInitializer>();
        builder.Services.AddSingleton<CosmosLogRepository>();
        builder.Services.AddSingleton<CosmosServiceGraphRepository>();
        builder.Services.AddSingleton<CosmosIncidentRepository>();
        builder.Services.AddSingleton<CosmosConversationRepository>();
        builder.Services.AddSingleton<LogNormalizer>();
        builder.Services.AddSingleton<TemporalChunker>();
        builder.Services.AddSingleton<EmbeddingService>();
        builder.Services.AddSingleton<IngestionService>();
    }
    else
    {
        Log.Warning("Cosmos DB settings not configured. Database features will not be available.");
    }

    builder.Services.AddControllers();

    var app = builder.Build();

    // Initialize the database and containers on startup
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