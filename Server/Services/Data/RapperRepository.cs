using PoDebateRap.Shared.Models;

namespace PoDebateRap.Server.Services.Data;

/// <summary>
/// Implements the IRapperRepository interface using Azure Table Storage via ITableStorageService.
/// Handles data operations for Rapper entities.
/// </summary>
public class RapperRepository : IRapperRepository
{
    private readonly ITableStorageService _tableStorageService;
    private readonly ILogger<RapperRepository> _logger;
    private const string TableName = "Rappers"; // Define table name constant
    private const string PartitionKeyValue = "Rapper"; // Define partition key constant

    // List of initial rappers for seeding
    private static readonly List<string> InitialRapperNames = new()
    {
        "Jay-Z", "Eminem", "Nas", "Kendrick Lamar", "Tupac Shakur",
        "The Notorious B.I.G.", "Rakim", "Lauryn Hill", "Andre 3000", "Snoop Dogg",
        "Missy Elliott", "Ice Cube", "Kanye West", "Lil Wayne", "Drake",
        "Nicki Minaj", "J. Cole", "MF DOOM", "Queen Latifah", "Method Man"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="RapperRepository"/> class.
    /// </summary>
    /// <param name="tableStorageService">The table storage service dependency.</param>
    /// <param name="logger">The logger instance.</param>
    public RapperRepository(ITableStorageService tableStorageService, ILogger<RapperRepository> logger)
    {
        _tableStorageService = tableStorageService;
        _logger = logger;
        _logger.LogInformation("RapperRepository initialized.");
    }

    /// <inheritdoc />
    public async Task<List<Rapper>> GetAllRappersAsync()
    {
        _logger.LogInformation("Attempting to retrieve all rappers from table {TableName}", TableName);
        try
        {
            var rappers = await _tableStorageService.GetAllEntitiesAsync<Rapper>(TableName);
            _logger.LogInformation("Successfully retrieved {Count} rappers.", rappers.Count);
            return rappers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all rappers from table {TableName}", TableName);
            throw; // Re-throw to allow higher layers to handle
        }
    }

    /// <inheritdoc />
    public async Task<Rapper?> GetRapperByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Attempted to get rapper with null or empty name.");
            return null;
        }
        _logger.LogInformation("Attempting to retrieve rapper by name (RowKey): {RapperName}", name);
        try
        {
            // Assuming Name is used as RowKey and PartitionKey is constant
            var rapper = await _tableStorageService.GetEntityAsync<Rapper>(TableName, PartitionKeyValue, name);
            if (rapper == null)
            {
                _logger.LogWarning("Rapper with name {RapperName} not found.", name);
            }
            else
            {
                _logger.LogInformation("Successfully retrieved rapper: {RapperName}", name);
            }
            return rapper;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rapper by name {RapperName}", name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpsertRapperAsync(Rapper rapper)
    {
        if (rapper == null)
        {
            throw new ArgumentNullException(nameof(rapper));
        }
        // Ensure PartitionKey and RowKey are set correctly
        rapper.PartitionKey = PartitionKeyValue;
        rapper.RowKey = rapper.Name; // Assuming Name is the RowKey

        if (string.IsNullOrWhiteSpace(rapper.RowKey))
        {
             throw new InvalidOperationException("Rapper Name (used as RowKey) cannot be empty.");
        }

        _logger.LogInformation("Attempting to upsert rapper: {RapperName}", rapper.Name);
        try
        {
            await _tableStorageService.UpsertEntityAsync(TableName, rapper);
            _logger.LogInformation("Successfully upserted rapper: {RapperName}", rapper.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting rapper: {RapperName}", rapper.Name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteRapperAsync(string name)
    {
         if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Attempted to delete rapper with null or empty name.");
            return; // Or throw? Decide based on requirements.
        }
        _logger.LogInformation("Attempting to delete rapper: {RapperName}", name);
        try
        {
            await _tableStorageService.DeleteEntityAsync(TableName, PartitionKeyValue, name);
            _logger.LogInformation("Successfully deleted rapper: {RapperName} (if existed)", name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting rapper: {RapperName}", name);
            throw;
        }
    }

     /// <inheritdoc />
    public async Task UpdateWinLossRecordAsync(string winnerName, string loserName)
    {
        _logger.LogInformation("Updating win/loss record. Winner: {WinnerName}, Loser: {LoserName}", winnerName, loserName);
        try
        {
            var winner = await GetRapperByNameAsync(winnerName);
            var loser = await GetRapperByNameAsync(loserName);

            if (winner == null)
            {
                _logger.LogError("Winner rapper not found: {WinnerName}", winnerName);
                // Consider throwing an exception or handling this case based on requirements
                return;
            }
             if (loser == null)
            {
                _logger.LogError("Loser rapper not found: {LoserName}", loserName);
                // Consider throwing an exception or handling this case
                return;
            }

            winner.Wins++;
            winner.TotalDebates++;
            loser.Losses++;
            loser.TotalDebates++;

            // Upsert both entities - consider transaction if atomicity is critical (requires Azure Cosmos DB Table API or custom logic)
            await UpsertRapperAsync(winner);
            await UpsertRapperAsync(loser);

            _logger.LogInformation("Successfully updated win/loss record for Winner: {WinnerName} and Loser: {LoserName}", winnerName, loserName);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error updating win/loss record for Winner: {WinnerName}, Loser: {LoserName}", winnerName, loserName);
             throw;
        }
    }

    /// <inheritdoc />
    public async Task SeedInitialRappersAsync()
    {
        _logger.LogInformation("Checking if initial rapper seeding is required for table {TableName}.", TableName);
        try
        {
            // Check if the table is empty or has few entries before seeding
            var existingRappers = await GetAllRappersAsync();
            if (existingRappers.Count == 0)
            {
                _logger.LogInformation("Seeding initial rappers into table {TableName}.", TableName);
                var rappersToSeed = InitialRapperNames.Select(name => new Rapper(name)).ToList();

                // Consider batch operation for efficiency if TableStorageService supports it
                // For now, upserting individually
                foreach (var rapper in rappersToSeed)
                {
                    await UpsertRapperAsync(rapper);
                }
                _logger.LogInformation("Successfully seeded {Count} initial rappers.", rappersToSeed.Count);
            }
            else
            {
                _logger.LogInformation("Table {TableName} already contains {Count} rappers. Skipping initial seeding.", TableName, existingRappers.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial rapper seeding for table {TableName}.", TableName);
            // Decide if this error should prevent application startup or just be logged.
            // Depending on the error, retrying might be an option.
            throw; // Re-throw for now
        }
    }
}
