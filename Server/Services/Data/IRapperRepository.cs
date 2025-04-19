using PoDebateRap.Shared.Models;

namespace PoDebateRap.Server.Services.Data;

/// <summary>
/// Defines the contract for a repository specifically handling Rapper data.
/// Abstracts the data access logic for Rapper entities.
/// </summary>
public interface IRapperRepository
{
    /// <summary>
    /// Retrieves all Rapper entities.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation, returning a list of all Rappers.</returns>
    Task<List<Rapper>> GetAllRappersAsync();

    /// <summary>
    /// Retrieves a specific Rapper by name (which is the RowKey).
    /// </summary>
    /// <param name="name">The name (RowKey) of the rapper.</param>
    /// <returns>A Task representing the asynchronous operation, returning the Rapper or null if not found.</returns>
    Task<Rapper?> GetRapperByNameAsync(string name);

    /// <summary>
    /// Adds or updates a Rapper entity.
    /// </summary>
    /// <param name="rapper">The Rapper entity to add or update.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task UpsertRapperAsync(Rapper rapper);

    /// <summary>
    /// Deletes a Rapper entity by name (RowKey).
    /// </summary>
    /// <param name="name">The name (RowKey) of the rapper to delete.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task DeleteRapperAsync(string name);

    /// <summary>
    /// Updates the win/loss record for a specific rapper.
    /// This might involve retrieving the entity, updating properties, and upserting.
    /// </summary>
    /// <param name="winnerName">The name of the winning rapper.</param>
    /// <param name="loserName">The name of the losing rapper.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task UpdateWinLossRecordAsync(string winnerName, string loserName);

    /// <summary>
    /// Seeds the initial list of rappers if the table is empty.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task SeedInitialRappersAsync();
}
