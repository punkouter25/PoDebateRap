using PoDebateRap.Shared.Models;

namespace PoDebateRap.Server.Services.AI;

/// <summary>
/// Defines the contract for interacting with the Azure OpenAI service.
/// Handles generating debate turns based on rapper personas and topics.
/// </summary>
public interface IAzureOpenAIService
{
    /// <summary>
    /// Generates the next debate turn for a given rapper based on the topic and previous turns.
    /// </summary>
    /// <param name="activeRapper">The rapper whose turn it is.</param>
    /// <param name="opponentRapper">The opposing rapper.</param>
    /// <param name="topic1">The first selected topic.</param>
    /// <param name="topic2">The second selected topic.</param>
    /// <param name="isRapper1Turn">Boolean indicating if it's currently Rapper 1's turn (arguing Topic 1 > Topic 2).</param>
    /// <param name="debateHistory">A list of previous turns in the debate (alternating between rappers).</param>
    /// <param name="maxCharacters">The maximum character limit for the generated turn.</param>
    /// <returns>A Task representing the asynchronous operation, returning the generated debate text.</returns>
    /// <exception cref="ArgumentNullException">Thrown if required parameters are null.</exception>
    /// <exception cref="Exception">Thrown if the OpenAI API call fails.</exception>
    Task<string> GenerateDebateTurnAsync(Rapper activeRapper, Rapper opponentRapper, Topic topic1, Topic topic2, bool isRapper1Turn, List<string> debateHistory, int maxCharacters = 750);

    // Potential future methods:
    // Task<string> GetRapperStyleSummaryAsync(Rapper rapper); // Could be used for prompt engineering
}
