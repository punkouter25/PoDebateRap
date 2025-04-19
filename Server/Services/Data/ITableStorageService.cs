using Azure.Data.Tables;
using PoDebateRap.Shared.Models;

namespace PoDebateRap.Server.Services.Data;

/// <summary>
/// Defines the contract for interacting with Azure Table Storage.
/// Provides methods for accessing table clients and performing CRUD operations.
/// </summary>
public interface ITableStorageService
{
    /// <summary>
    /// Gets the TableClient for the specified table name.
    /// Creates the table if it doesn't exist.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <returns>A Task representing the asynchronous operation, returning a TableClient.</returns>
    Task<TableClient> GetTableClientAsync(string tableName);

    /// <summary>
    /// Retrieves all entities of a specific type from a table.
    /// </summary>
    /// <typeparam name="T">The type of the entity, must implement ITableEntity.</typeparam>
    /// <param name="tableName">The name of the table.</param>
    /// <returns>A Task representing the asynchronous operation, returning a list of entities.</returns>
    Task<List<T>> GetAllEntitiesAsync<T>(string tableName) where T : class, ITableEntity, new();

    /// <summary>
    /// Retrieves a specific entity by partition key and row key.
    /// </summary>
    /// <typeparam name="T">The type of the entity, must implement ITableEntity.</typeparam>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="partitionKey">The partition key of the entity.</param>
    /// <param name="rowKey">The row key of the entity.</param>
    /// <returns>A Task representing the asynchronous operation, returning the entity or null if not found.</returns>
    Task<T?> GetEntityAsync<T>(string tableName, string partitionKey, string rowKey) where T : class, ITableEntity;

    /// <summary>
    /// Adds or updates an entity in the specified table.
    /// </summary>
    /// <typeparam name="T">The type of the entity, must implement ITableEntity.</typeparam>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="entity">The entity to add or update.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task UpsertEntityAsync<T>(string tableName, T entity) where T : ITableEntity;

    /// <summary>
    /// Deletes an entity from the specified table.
    /// </summary>
    /// <param name="tableName">The name of the table.</param>
    /// <param name="partitionKey">The partition key of the entity.</param>
    /// <param name="rowKey">The row key of the entity.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task DeleteEntityAsync(string tableName, string partitionKey, string rowKey);
}
