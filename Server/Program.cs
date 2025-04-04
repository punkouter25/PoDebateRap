using PoDebateRap.Server.Components;
using PoDebateRap.Server.Services.Data;
using PoDebateRap.Server.Services.AI;
using PoDebateRap.Server.Services.Speech;
using PoDebateRap.Server.Services.Orchestration;
using PoDebateRap.Server.Services.News; // Add namespace for News services
using PoDebateRap.Server.Services.Diagnostics; // Add namespace for Diagnostics services
using Azure.Identity; // Add namespace for Managed Identity
var builder = WebApplication.CreateBuilder(args);

// --- Add Key Vault Configuration ---
// Get Key Vault URI from configuration (set as App Setting or environment variable)
var keyVaultUri = builder.Configuration["KeyVaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    try
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultUri),
            new DefaultAzureCredential()); // Uses managed identity when deployed to Azure
        Console.WriteLine($"Successfully added Key Vault configuration source: {keyVaultUri}"); // Log success
    }
    catch (Exception ex)
    {
        // Log error but continue - app might still work with other config sources
        Console.WriteLine($"Error adding Key Vault configuration source: {ex.Message}");
    }
}
else
{
    Console.WriteLine("KeyVaultUri not found in configuration. Skipping Key Vault setup."); // Log info
}
// --- End Key Vault Configuration ---

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = true); // Enable detailed errors


// Register Data Services (Scoped is suitable for repositories/services interacting with data per request)
builder.Services.AddScoped<ITableStorageService, TableStorageService>();
builder.Services.AddScoped<IRapperRepository, RapperRepository>();
builder.Services.AddScoped<ITopicRepository, TopicRepository>();

// Register AI Service (Scoped or Singleton depending on client cost/thread-safety)
builder.Services.AddScoped<IAzureOpenAIService, AzureOpenAIService>();

// Register TextToSpeech Service (Scoped is likely fine)
builder.Services.AddScoped<ITextToSpeechService, TextToSpeechService>();

// Register Debate Orchestrator (Scoped to manage state per user connection/session)
builder.Services.AddScoped<IDebateOrchestrator, DebateOrchestrator>();

// Register News Service (Scoped is suitable) and configure HttpClient
builder.Services.AddHttpClient<INewsService, NewsService>(); // Registers both service and typed HttpClient

// Register Diagnostics Service (Scoped is appropriate)
builder.Services.AddScoped<IDiagnosticsService, DiagnosticsService>();
// Ensure HttpClientFactory is available (often implicitly registered, but explicit doesn't hurt)
builder.Services.AddHttpClient();

// Add Application Insights for telemetry and logging
// It reads the connection string from configuration (appsettings.json or environment variables/App Service settings)
builder.Services.AddApplicationInsightsTelemetry();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles(); // Ensure static files middleware is added
app.UseAntiforgery();

app.MapStaticAssets(); // Required for .NET 9 static assets
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// --- Data Seeding ---
// Seed initial data after the app is built but before it runs.
// This ensures services are available.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var rapperRepository = services.GetRequiredService<IRapperRepository>();
        var topicRepository = services.GetRequiredService<ITopicRepository>();

        // Check for Azure Storage connection string (using Key Vault name format) before attempting to seed
        var configuration = services.GetRequiredService<IConfiguration>();
        // Use the Key Vault secret name format (dashes instead of colons)
        if (!string.IsNullOrEmpty(configuration["Azure-StorageConnectionString"]))
        {
             // Run seeding asynchronously but wait for completion here
             // Using .GetAwaiter().GetResult() is okay in Program.cs initialization context
             rapperRepository.SeedInitialRappersAsync().GetAwaiter().GetResult();
             topicRepository.SeedInitialTopicsAsync().GetAwaiter().GetResult();
             app.Logger.LogInformation("Initial data seeding completed (if necessary).");
        }
        else
        {
            app.Logger.LogWarning("Azure Storage connection string not found. Skipping initial data seeding.");
            // Consider adding a more visible warning or configuration step for the user
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during data seeding.");
        // Depending on the severity, you might want to stop the application
        // throw;
    }
}
// --- End Data Seeding ---


app.Run();
