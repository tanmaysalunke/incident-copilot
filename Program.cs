using IncidentCopilot.Models;
using Serilog;

// Configure Serilog for structured JSON logging
// This is like setting up Python's logging module, but with JSON output
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting Incident Copilot API");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog instead of the default logger
    builder.Host.UseSerilog();

    // Register configuration sections so they can be injected into services later
    // This is like loading a config file in Python and making it available everywhere
    builder.Services.Configure<CosmosDbSettings>(
        builder.Configuration.GetSection("CosmosDb"));
    builder.Services.Configure<AzureOpenAISettings>(
        builder.Configuration.GetSection("AzureOpenAI"));

    // Add controllers (tells ASP.NET to look for classes with [ApiController])
    builder.Services.AddControllers();

    var app = builder.Build();

    // Map controller routes (connects URL paths to controller methods)
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