using PoDebateRap.Shared.Models;

namespace PoDebateRap.Server.Services.Data;

/// <summary>
/// Defines the contract for a repository specifically handling Topic data.
/// Abstracts the data access logic for Topic entities.
/// </summary>
public interface ITopicRepository
{
    /// <summary>
    /// Retrieves all Topic entities.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation, returning a list of all Topics.</returns>
    Task<List<Topic>> GetAllTopicsAsync();

    /// <summary>
    /// Retrieves a specific Topic by its RowKey (GUID).
    /// </summary>
    /// <param name="rowKey">The RowKey (GUID) of the topic.</param>
    /// <returns>A Task representing the asynchronous operation, returning the Topic or null if not found.</returns>
    Task<Topic?> GetTopicByRowKeyAsync(string rowKey);

    /// <summary>
    /// Adds or updates a Topic entity.
    /// </summary>
    /// <param name="topic">The Topic entity to add or update.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task UpsertTopicAsync(Topic topic);

    /// <summary>
    /// Deletes a Topic entity by its RowKey (GUID).
    /// </summary>
    /// <param name="rowKey">The RowKey (GUID) of the topic to delete.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task DeleteTopicAsync(string rowKey);

    /// <summary>
    /// Seeds the initial list of topics if the table is empty.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task SeedInitialTopicsAsync();
}
