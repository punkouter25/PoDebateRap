using PoDebateRap.Shared.Models;
using System.Linq; // Add System.Linq for .Any()

namespace PoDebateRap.Server.Services.Data;

/// <summary>
/// Implements the ITopicRepository interface using Azure Table Storage via ITableStorageService.
/// Handles data operations for Topic entities.
/// </summary>
public class TopicRepository : ITopicRepository
{
    private readonly ITableStorageService _tableStorageService;
    private readonly ILogger<TopicRepository> _logger;
    private const string TableName = "Topics"; // Define table name constant
    private const string PartitionKeyValue = "Topic"; // Define partition key constant

    // List of initial single-word/concept topics for seeding
    private static readonly List<Topic> InitialTopics = new()
    {
        new("Technology", "The application of scientific knowledge for practical purposes."),
        new("Nature", "The physical world collectively, including plants, animals, the landscape, etc."),
        new("Art", "The expression or application of human creative skill and imagination."),
        new("Commerce", "The activity of buying and selling, especially on a large scale."),
        new("East Coast", "Referring to the style and culture associated with the Eastern US."),
        new("West Coast", "Referring to the style and culture associated with the Western US."),
        new("Social Media", "Websites and applications that enable users to create and share content or to participate in social networking."),
        new("Privacy", "The state of being free from public attention."),
        new("Old School", "Traditional or conventional."),
        new("New School", "Modern or up-to-date."),
        new("Freedom", "The power or right to act, speak, or think as one wants."),
        new("Security", "The state of being free from danger or threat."),
        new("AI", "Artificial intelligence; the theory and development of computer systems able to perform tasks that normally require human intelligence."),
        new("Humanity", "The human race; human beings collectively."),
        new("Education", "The process of receiving or giving systematic instruction."),
        new("Experience", "Practical contact with and observation of facts or events."),
        new("Wealth", "An abundance of valuable possessions or money."),
        new("Health", "The state of being free from illness or injury."),
        new("Power", "The ability or capacity to do something or act in a particular way."),
        new("Love", "An intense feeling of deep affection.")
        // Add more single-word concepts as needed
    };


    /// <summary>
    /// Initializes a new instance of the <see cref="TopicRepository"/> class.
    /// </summary>
    /// <param name="tableStorageService">The table storage service dependency.</param>
    /// <param name="logger">The logger instance.</param>
    public TopicRepository(ITableStorageService tableStorageService, ILogger<TopicRepository> logger)
    {
        _tableStorageService = tableStorageService;
        _logger = logger;
        _logger.LogInformation("TopicRepository initialized.");
    }

    /// <inheritdoc />
    public async Task<List<Topic>> GetAllTopicsAsync()
    {
        _logger.LogInformation("Attempting to retrieve all topics from table {TableName}", TableName);
        try
        {
            var topics = await _tableStorageService.GetAllEntitiesAsync<Topic>(TableName);
            _logger.LogInformation("Successfully retrieved {Count} topics.", topics.Count);
            return topics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all topics from table {TableName}", TableName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Topic?> GetTopicByRowKeyAsync(string rowKey)
    {
        if (string.IsNullOrWhiteSpace(rowKey) || !Guid.TryParse(rowKey, out _))
        {
             _logger.LogWarning("Attempted to get topic with invalid RowKey: {RowKey}", rowKey);
             return null;
        }
        _logger.LogInformation("Attempting to retrieve topic by RowKey: {RowKey}", rowKey);
        try
        {
            var topic = await _tableStorageService.GetEntityAsync<Topic>(TableName, PartitionKeyValue, rowKey);
             if (topic == null)
            {
                _logger.LogWarning("Topic with RowKey {RowKey} not found.", rowKey);
            }
            else
            {
                _logger.LogInformation("Successfully retrieved topic with RowKey: {RowKey}", rowKey);
            }
            return topic;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving topic by RowKey {RowKey}", rowKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpsertTopicAsync(Topic topic)
    {
        if (topic == null)
        {
            throw new ArgumentNullException(nameof(topic));
        }
        // Ensure PartitionKey is set, RowKey should be set by constructor or already exist
        topic.PartitionKey = PartitionKeyValue;

        if (string.IsNullOrWhiteSpace(topic.RowKey) || !Guid.TryParse(topic.RowKey, out _))
        {
             throw new InvalidOperationException("Topic RowKey is invalid or missing.");
        }
         if (string.IsNullOrWhiteSpace(topic.Title))
        {
             throw new InvalidOperationException("Topic Title cannot be empty.");
        }

        _logger.LogInformation("Attempting to upsert topic: {TopicTitle} (RowKey: {RowKey})", topic.Title, topic.RowKey);
        try
        {
            await _tableStorageService.UpsertEntityAsync(TableName, topic);
            _logger.LogInformation("Successfully upserted topic: {TopicTitle} (RowKey: {RowKey})", topic.Title, topic.RowKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting topic: {TopicTitle} (RowKey: {RowKey})", topic.Title, topic.RowKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteTopicAsync(string rowKey)
    {
        if (string.IsNullOrWhiteSpace(rowKey) || !Guid.TryParse(rowKey, out _))
        {
             _logger.LogWarning("Attempted to delete topic with invalid RowKey: {RowKey}", rowKey);
             return; // Or throw?
        }
        _logger.LogInformation("Attempting to delete topic with RowKey: {RowKey}", rowKey);
        try
        {
            await _tableStorageService.DeleteEntityAsync(TableName, PartitionKeyValue, rowKey);
             _logger.LogInformation("Successfully deleted topic with RowKey: {RowKey} (if existed)", rowKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting topic with RowKey: {RowKey}", rowKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SeedInitialTopicsAsync()
    {
        _logger.LogInformation("Attempting to seed/update initial topics into table {TableName}.", TableName);
        try
        {
            // Force seeding attempt every time.
            // NOTE: In a real application, you'd likely want a more robust migration/seeding strategy.
            foreach (var topic in InitialTopics)
            {
                // Use Upsert which handles create or update
                await UpsertTopicAsync(topic);
            }
            _logger.LogInformation("Successfully attempted seeding/updating {Count} initial topics.", InitialTopics.Count);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error during initial topic seeding/updating for table {TableName}.", TableName);
             // Decide if this should prevent startup. For now, re-throw.
             throw;
        }
    }
}
