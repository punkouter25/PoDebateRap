using Azure;
using Azure.Data.Tables;
using PoDebateRap.Shared.Models;
using System.Collections.Concurrent;

namespace PoDebateRap.Server.Services.Data;

/// <summary>
/// Implements the ITableStorageService interface to interact with Azure Table Storage.
/// Handles table creation, entity CRUD operations, and connection management.
/// </summary>
public class TableStorageService : ITableStorageService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TableStorageService> _logger;
    private TableServiceClient? _tableServiceClient;
    private readonly ConcurrentDictionary<string, TableClient> _tableClients = new();
    private readonly string _connectionString;
    private readonly SemaphoreSlim _clientInitializationLock = new(1, 1); // Lock for initializing TableServiceClient

    /// <summary>
    /// Initializes a new instance of the <see cref="TableStorageService"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="InvalidOperationException">Thrown if the Azure Storage connection string is not configured.</exception>
    public TableStorageService(IConfiguration configuration, ILogger<TableStorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        // Retrieve connection string - Use colon format. Key Vault provider should map the secret name.
        // Store it even if null/empty; validation happens on connection attempt.
        _connectionString = _configuration["Azure:StorageConnectionString"] ?? string.Empty;
        if (string.IsNullOrEmpty(_connectionString))
        {
            _logger.LogWarning("Azure Storage Connection String ('Azure:StorageConnectionString' from Key Vault/config) is not configured. Connection attempts will fail.");
        }
        _logger.LogInformation("TableStorageService initialized (Connection string presence checked later).");
    }

    /// <summary>
    /// Initializes the TableServiceClient asynchronously if it hasn't been already.
    /// Uses a semaphore to ensure thread safety during initialization.
    /// </summary>
    private async Task InitializeTableServiceClientAsync()
    {
        if (_tableServiceClient == null)
        {
            await _clientInitializationLock.WaitAsync();
            try
            {
                // Double-check locking pattern
                if (_tableServiceClient == null)
                {
                    // Check connection string validity *before* attempting to create client
                    if (string.IsNullOrEmpty(_connectionString))
                    {
                        _logger.LogError("Cannot initialize TableServiceClient: Connection string is missing or empty.");
                        throw new InvalidOperationException("Azure Storage Connection String is not configured.");
                    }
                    _logger.LogInformation("Initializing TableServiceClient...");
                    
                    // Parse the connection string manually to handle potential issues with the account key
                    if (_connectionString.Contains("AccountName=") && _connectionString.Contains("AccountKey="))
                    {
                        try
                        {
                            // Extract account name and key from connection string
                            var accountName = ExtractValueFromConnectionString(_connectionString, "AccountName");
                            var accountKey = ExtractValueFromConnectionString(_connectionString, "AccountKey");
                            
                            if (!string.IsNullOrEmpty(accountName) && !string.IsNullOrEmpty(accountKey))
                            {
                                // Create credentials and client manually
                                var credential = new Azure.Data.Tables.TableSharedKeyCredential(accountName, accountKey);
                                var serviceUri = new Uri($"https://{accountName}.table.core.windows.net/");
                                _tableServiceClient = new TableServiceClient(serviceUri, credential);
                                _logger.LogInformation("TableServiceClient initialized successfully with manual credentials.");
                            }
                            else
                            {
                                throw new InvalidOperationException("Could not extract account name or key from connection string.");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to initialize TableServiceClient with manual credentials.");
                            // Fall back to using the connection string directly
                            _tableServiceClient = new TableServiceClient(_connectionString);
                        }
                    }
                    else
                    {
                        // Use the connection string directly if it doesn't contain AccountName and AccountKey
                        _tableServiceClient = new TableServiceClient(_connectionString);
                        _logger.LogInformation("TableServiceClient initialized successfully with connection string.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize TableServiceClient.");
                throw; // Re-throw exception after logging
            }
            finally
            {
                _clientInitializationLock.Release();
            }
        }
    }

    /// <inheritdoc />
    public async Task<TableClient> GetTableClientAsync(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name cannot be empty.", nameof(tableName));
        }

        if (_tableClients.TryGetValue(tableName, out var client))
        {
            return client;
        }

        await InitializeTableServiceClientAsync(); // Ensure service client is ready

        try
        {
            _logger.LogInformation("Getting or creating TableClient for table: {TableName}", tableName);
            var tableClient = _tableServiceClient!.GetTableClient(tableName);
            await tableClient.CreateIfNotExistsAsync();
            _tableClients.TryAdd(tableName, tableClient); // Add to cache
            _logger.LogInformation("TableClient for {TableName} obtained successfully.", tableName);
            return tableClient;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to get or create TableClient for table {TableName}. Status: {Status}, ErrorCode: {ErrorCode}",
                tableName, ex.Status, ex.ErrorCode);
            throw; // Re-throw to indicate failure
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while getting TableClient for table {TableName}.", tableName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<T>> GetAllEntitiesAsync<T>(string tableName) where T : class, ITableEntity, new()
    {
        var tableClient = await GetTableClientAsync(tableName);
        var entities = new List<T>();
        try
        {
            _logger.LogDebug("Querying all entities of type {EntityType} from table {TableName}", typeof(T).Name, tableName);
            await foreach (var entity in tableClient.QueryAsync<T>())
            {
                entities.Add(entity);
            }
            _logger.LogDebug("Successfully retrieved {Count} entities of type {EntityType} from table {TableName}", entities.Count, typeof(T).Name, tableName);
            return entities;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to retrieve entities from table {TableName}. Status: {Status}, ErrorCode: {ErrorCode}",
                tableName, ex.Status, ex.ErrorCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while retrieving entities from table {TableName}.", tableName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T?> GetEntityAsync<T>(string tableName, string partitionKey, string rowKey) where T : class, ITableEntity
    {
        var tableClient = await GetTableClientAsync(tableName);
        try
        {
            _logger.LogDebug("Attempting to retrieve entity with PartitionKey={PartitionKey}, RowKey={RowKey} from table {TableName}", partitionKey, rowKey, tableName);
            var response = await tableClient.GetEntityAsync<T>(partitionKey, rowKey);
            _logger.LogDebug("Successfully retrieved entity with PartitionKey={PartitionKey}, RowKey={RowKey} from table {TableName}", partitionKey, rowKey, tableName);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Entity not found in table {TableName} with PartitionKey={PartitionKey}, RowKey={RowKey}", tableName, partitionKey, rowKey);
            return null; // Entity not found is not necessarily an error in this context
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to retrieve entity from table {TableName} with PartitionKey={PartitionKey}, RowKey={RowKey}. Status: {Status}, ErrorCode: {ErrorCode}",
                tableName, partitionKey, rowKey, ex.Status, ex.ErrorCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while retrieving entity from table {TableName} with PartitionKey={PartitionKey}, RowKey={RowKey}.", tableName, partitionKey, rowKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpsertEntityAsync<T>(string tableName, T entity) where T : ITableEntity
    {
        var tableClient = await GetTableClientAsync(tableName);
        try
        {
            _logger.LogDebug("Upserting entity with PartitionKey={PartitionKey}, RowKey={RowKey} into table {TableName}", entity.PartitionKey, entity.RowKey, tableName);
            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace); // Use Replace mode for simplicity, Merge is also an option
            _logger.LogDebug("Successfully upserted entity with PartitionKey={PartitionKey}, RowKey={RowKey} into table {TableName}", entity.PartitionKey, entity.RowKey, tableName);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to upsert entity into table {TableName}. Status: {Status}, ErrorCode: {ErrorCode}",
                tableName, ex.Status, ex.ErrorCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while upserting entity into table {TableName}.", tableName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteEntityAsync(string tableName, string partitionKey, string rowKey)
    {
        var tableClient = await GetTableClientAsync(tableName);
        try
        {
            _logger.LogDebug("Deleting entity with PartitionKey={PartitionKey}, RowKey={RowKey} from table {TableName}", partitionKey, rowKey, tableName);
            await tableClient.DeleteEntityAsync(partitionKey, rowKey);
            _logger.LogDebug("Successfully deleted entity with PartitionKey={PartitionKey}, RowKey={RowKey} from table {TableName}", partitionKey, rowKey, tableName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Attempted to delete non-existent entity from table {TableName} with PartitionKey={PartitionKey}, RowKey={RowKey}", tableName, partitionKey, rowKey);
            // Deleting a non-existent entity might not be an error depending on context, log as warning.
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to delete entity from table {TableName} with PartitionKey={PartitionKey}, RowKey={RowKey}. Status: {Status}, ErrorCode: {ErrorCode}",
                tableName, partitionKey, rowKey, ex.Status, ex.ErrorCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while deleting entity from table {TableName} with PartitionKey={PartitionKey}, RowKey={RowKey}.", tableName, partitionKey, rowKey);
            throw;
        }
    }

    /// <summary>
    /// Extracts a value from a connection string by key.
    /// </summary>
    /// <param name="connectionString">The connection string to parse.</param>
    /// <param name="key">The key to extract the value for.</param>
    /// <returns>The extracted value, or null if not found.</returns>
    private string? ExtractValueFromConnectionString(string connectionString, string key)
    {
        // Format: Key1=Value1;Key2=Value2;...
        var keyWithEquals = key + "=";
        var parts = connectionString.Split(';');
        
        foreach (var part in parts)
        {
            if (part.Trim().StartsWith(keyWithEquals, StringComparison.OrdinalIgnoreCase))
            {
                return part.Substring(keyWithEquals.Length).Trim();
            }
        }
        
        return null;
    }
}
