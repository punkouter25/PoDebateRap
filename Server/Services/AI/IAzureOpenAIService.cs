using PoDebateRap.Shared.Models;

namespace PoDebateRap.Server.Services.AI;

/// <summary>
/// Defines the contract for interacting with the Azure OpenAI service.
/// Handles generating debate turns based on rapper personas and topics.
/// </summary>
public interface IAzureOpenAIService
{
    /// <summary>
    /// Generates the next debate turn for a given rapper based on a single topic and their stance (pro/con).
    /// </summary>
    /// <param name="activeRapper">The rapper whose turn it is.</param>
    /// <param name="opponentRapper">The opposing rapper.</param>
    /// <param name="topic">The single topic of the debate.</param>
    /// <param name="isArguingPro">Boolean indicating if the active rapper is arguing FOR the topic (true) or AGAINST it (false).</param>
    /// <param name="debateHistory">A list of previous turns in the debate (alternating between rappers).</param>
    /// <param name="currentTurnNumber">The current turn number (1-6) to adjust tone.</param>
    /// <param name="maxCharacters">The maximum character limit for the generated turn.</param>
    /// <returns>A Task representing the asynchronous operation, returning the generated debate text.</returns>
    /// <exception cref="ArgumentNullException">Thrown if required parameters are null.</exception>
    /// <exception cref="Exception">Thrown if the OpenAI API call fails.</exception>
    Task<string> GenerateDebateTurnAsync(Rapper activeRapper, Rapper opponentRapper, Topic topic, bool isArguingPro, List<string> debateHistory, int currentTurnNumber, int maxCharacters = 750);

    /// <summary>
    /// Analyzes the complete debate history and declares a winner based on argumentation.
    /// </summary>
    /// <param name="debateHistory">The full list of turns in the debate.</param>
    /// <param name="rapper1">The rapper who argued FOR the topic.</param>
    /// <param name="rapper2">The rapper who argued AGAINST the topic.</param>
    /// <param name="topic">The topic of the debate.</param>
    /// <returns>A Task representing the asynchronous operation, returning a tuple containing the judge's reasoning and debate statistics (or nulls if errors occurred).</returns>
    /// <exception cref="ArgumentNullException">Thrown if required parameters are null.</exception>
    /// <exception cref="Exception">Thrown if the OpenAI API call fails.</exception>
    Task<(string? Reasoning, DebateStats? Stats)> JudgeDebateAsync(List<string> debateHistory, Rapper rapper1, Rapper rapper2, Topic topic);

    // Potential future methods:
    // Task<string> GetRapperStyleSummaryAsync(Rapper rapper); // Could be used for prompt engineering
}
