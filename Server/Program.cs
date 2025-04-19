using PoDebateRap.Server.Components;
using PoDebateRap.Server.Services.Data;
using PoDebateRap.Server.Services.AI;
using PoDebateRap.Server.Services.Speech;
using PoDebateRap.Server.Services.Orchestration;
using PoDebateRap.Server.Services.News; // Add namespace for News services
using PoDebateRap.Server.Services.Diagnostics; // Add namespace for Diagnostics services
using Azure.Identity; // Add namespace for Managed Identity
using PoDebateRap.Server.Logging; // Add namespace for File Logger

var builder = WebApplication.CreateBuilder(args);

// --- Configure Logging ---
builder.Logging.ClearProviders(); // Remove default providers like Console
builder.Logging.AddConsole(); // Re-add console logger
builder.Logging.AddDebug(); // Re-add debug logger
// Add File Logger only for Development environment
if (builder.Environment.IsDevelopment())
{
    // Ensure log file path is relative to the content root (project directory)
    var logFilePath = Path.Combine(builder.Environment.ContentRootPath, "..", "log.txt"); // Place log.txt in solution root
    builder.Logging.AddProvider(new FileLoggerProvider(logFilePath));
    Console.WriteLine($"File logging enabled at: {logFilePath}"); // Log info
}
// --- End Logging Configuration ---


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

        // Check for Azure Storage connection string (using colon format) before attempting to seed
        var configuration = services.GetRequiredService<IConfiguration>();
        // Use the colon format. Key Vault provider should map the secret name.
        if (!string.IsNullOrEmpty(configuration["Azure:StorageConnectionString"]))
        {
            try
            {
                app.Logger.LogInformation("Attempting initial data seeding...");
                // Run seeding asynchronously but wait for completion here
                 // Using .GetAwaiter().GetResult() is okay in Program.cs initialization context
                 rapperRepository.SeedInitialRappersAsync().GetAwaiter().GetResult();
                 // topicRepository.SeedInitialTopicsAsync().GetAwaiter().GetResult(); // Removed topic seeding
                 app.Logger.LogInformation("Initial data seeding completed (if necessary).");
            }
            catch (Exception seedEx)
            {
                // Log seeding error but allow app to continue starting
                app.Logger.LogError(seedEx, "Error during initial data seeding (likely due to invalid connection string or table access issue). Seeding skipped.");
            }
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
