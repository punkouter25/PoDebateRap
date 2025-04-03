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
        // Retrieve connection string - ensure it's configured in appsettings.json or environment variables
        _connectionString = _configuration["Azure:StorageConnectionString"]
            ?? throw new InvalidOperationException("Azure Storage Connection String ('Azure:StorageConnectionString') is not configured.");
        _logger.LogInformation("TableStorageService initialized.");
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
                    _logger.LogInformation("Initializing TableServiceClient...");
                    _tableServiceClient = new TableServiceClient(_connectionString);
                    _logger.LogInformation("TableServiceClient initialized successfully.");
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
}
