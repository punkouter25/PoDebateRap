using Azure;
using Azure.Data.Tables;

namespace PoDebateRap.Shared.Models;

/// <summary>
/// Represents a Debate Topic entity stored in Azure Table Storage.
/// Implements ITableEntity for compatibility with Azure Tables.
/// </summary>
public class Topic : ITableEntity
{
    /// <summary>
    /// Gets or sets the title of the debate topic.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a brief description of the debate topic.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    // --- ITableEntity Implementation ---

    /// <summary>
    /// Gets or sets the Partition Key for the entity in Azure Table Storage.
    /// All topics will share the same partition key "Topic".
    /// </summary>
    public string PartitionKey { get; set; } = "Topic";

    /// <summary>
    /// Gets or sets the Row Key for the entity in Azure Table Storage.
    /// Using a GUID ensures uniqueness within the partition.
    /// </summary>
    public string RowKey { get; set; } = Guid.NewGuid().ToString();

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
    public Topic() { }

    /// <summary>
    /// Convenience constructor to initialize a Topic entity.
    /// </summary>
    /// <param name="title">The title of the topic.</param>
    /// <param name="description">The description of the topic.</param>
    public Topic(string title, string description = "")
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Topic title cannot be empty.", nameof(title));

        Title = title;
        Description = description;
        PartitionKey = "Topic"; // Set default partition key
        RowKey = Guid.NewGuid().ToString(); // Generate unique RowKey
    }
}
