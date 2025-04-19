using Azure;
using Azure.Data.Tables;

namespace PoDebateRap.Shared.Models;

/// <summary>
/// Represents a Rapper entity stored in Azure Table Storage.
/// Implements ITableEntity for compatibility with Azure Tables.
/// </summary>
public class Rapper : ITableEntity
{
    /// <summary>
    /// Gets or sets the name of the rapper. Used as the primary identifier within the partition.
    /// This will also serve as the RowKey.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of debates won by the rapper.
    /// </summary>
    public int Wins { get; set; }

    /// <summary>
    /// Gets or sets the number of debates lost by the rapper.
    /// </summary>
    public int Losses { get; set; }

    /// <summary>
    /// Gets or sets the total number of debates the rapper has participated in.
    /// </summary>
    public int TotalDebates { get; set; }

    // --- ITableEntity Implementation ---

    /// <summary>
    /// Gets or sets the Partition Key for the entity in Azure Table Storage.
    /// All rappers will share the same partition key "Rapper".
    /// </summary>
    public string PartitionKey { get; set; } = "Rapper";

    /// <summary>
    /// Gets or sets the Row Key for the entity in Azure Table Storage.
    /// Using the Rapper's Name as the unique identifier within the partition.
    /// Note: RowKey cannot contain certain characters like '/', '\', '#', '?'. Ensure Name is sanitized if needed.
    /// </summary>
    public string RowKey { get; set; } = string.Empty; // Will be set to Name

    /// <summary>
    /// Gets or sets the timestamp for the entity, managed by Azure Table Storage.
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the ETag for optimistic concurrency control, managed by Azure Table Storage.
    /// </summary>
    public ETag ETag { get; set; }

    /// <summary>
    /// Parameterless constructor required for ITableEntity deserialization.
    /// </summary>
    public Rapper() { }

    /// <summary>
    /// Convenience constructor to initialize a Rapper entity.
    /// </summary>
    /// <param name="name">The name of the rapper.</param>
    public Rapper(string name)
    {
        // Basic validation/sanitization might be needed for RowKey compatibility
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Rapper name cannot be empty.", nameof(name));

        // TODO: Add more robust sanitization for RowKey if names might contain invalid characters.
        Name = name;
        RowKey = name; // Use Name as RowKey
        PartitionKey = "Rapper"; // Set default partition key
        Wins = 0;
        Losses = 0;
        TotalDebates = 0;
    }
}
