using PoDebateRap.Server.Components;
using MudBlazor; // Add MudBlazor namespace
using MudBlazor.Services;
using PoDebateRap.Server.Services.Data;
using PoDebateRap.Server.Services.AI;
using PoDebateRap.Server.Services.Speech;
using PoDebateRap.Server.Services.Orchestration;
using PoDebateRap.Server.Services.News; // Add namespace for News services

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = true); // Enable detailed errors

// Define the custom theme
var rapTheme = new MudTheme()
{
    PaletteDark = new PaletteDark()
    {
            Primary = "#B8860B", // DarkGoldenrod (from app.css --primary-color)
            Secondary = "#8A8A8A", // Lighter Gray for secondary elements if needed
            Background = "#121212", // Near black (from app.css --background-color)
            Surface = "#1f1f1f", // Slightly lighter for cards/surfaces (from app.css .card)
            DrawerBackground = "#181818", // Darker drawer/sidebar
            AppbarBackground = "#000000", // Black top bar (from app.css .top-row)
            TextPrimary = "#E0E0E0", // Light gray (from app.css --text-color)
            TextSecondary = "#A0A0A0", // Slightly dimmer gray for secondary text
            ActionDefault = "#B0B0B0", // Default icon/action color
            ActionDisabled = "#606060", // Disabled action color
            LinesDefault = "#333333", // Darker border (from app.css --border-color)
            Error = "#8B0000", // DarkRed (from app.css --accent-color)
            // Add other colors as needed (Info, Success, Warning)
            Info = "#1E90FF", // DodgerBlue (from original app.css --link-color)
            Success = "#26b050", // Green (from app.css .valid)
            Warning = "#FFA500", // Orange
        },
        // Typography customization: Modify the default typography object
        // (MudThemeProvider applies defaults, we override specific properties here)
        // Note: We don't need to instantiate Typography() here, MudTheme does it.
        // We will modify the properties directly if needed, but often relying on CSS is simpler.
        // For now, let's remove the explicit Typography block and rely on CSS + MudBlazor defaults.
        // If specific MudBlazor component text needs overriding beyond CSS, we can revisit this.

        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "2px" // Slightly sharper edges
        }
    }; // <-- Add missing semicolon here to terminate the variable declaration

// Configure MudBlazor services and pass the theme
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    // Pass the defined theme object here if needed for specific service configs,
    // but the theme itself is applied globally by MudThemeProvider reading it.
    // The MudThemeProvider component will automatically use the registered MudTheme.
});


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

        // Check for Azure Storage connection string before attempting to seed
        var configuration = services.GetRequiredService<IConfiguration>();
        if (!string.IsNullOrEmpty(configuration["Azure:StorageConnectionString"]))
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
