using Azure.AI.OpenAI;
using PoDebateRap.Shared.Models;
using System.Text;
using Azure.Core; // Needed for AzureKeyCredential
using Azure; // Needed for Response
using OpenAI.Chat; // Added for ChatClient and related types

namespace PoDebateRap.Server.Services.AI;

/// <summary>
/// Implements the IAzureOpenAIService interface to interact with Azure OpenAI.
/// Handles prompt engineering and generation of debate content using the newer AzureOpenAIClient pattern.
/// </summary>
public class AzureOpenAIService : IAzureOpenAIService
{
    // Use AzureOpenAIClient instead of OpenAIClient
    private readonly AzureOpenAIClient _azureClient;
    private readonly ChatClient _chatClient; // Specific client for chat operations
    private readonly ILogger<AzureOpenAIService> _logger;
    private readonly string _deploymentName;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureOpenAIService"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="InvalidOperationException">Thrown if required Azure OpenAI configuration is missing.</exception>
    public AzureOpenAIService(IConfiguration configuration, ILogger<AzureOpenAIService> logger)
    {
        _logger = logger;

        var endpoint = configuration["Azure:OpenAI:Endpoint"];
        var apiKey = configuration["Azure:OpenAI:ApiKey"];
        // Deployment name is now used when getting the ChatClient
        _deploymentName = configuration["Azure:OpenAI:DeploymentName"] ?? "gpt-35-turbo";

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("Azure OpenAI Endpoint or ApiKey is not configured. Please check Azure:OpenAI settings.");
            throw new InvalidOperationException("Azure OpenAI Endpoint and ApiKey must be configured.");
        }

        try
        {
            // Initialize the AzureOpenAIClient using API Key
            _azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            // Get the specific ChatClient for the deployment
            _chatClient = _azureClient.GetChatClient(_deploymentName);

            _logger.LogInformation("AzureOpenAIService initialized with endpoint {Endpoint} and deployment {DeploymentName}", endpoint, _deploymentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize AzureOpenAIClient or ChatClient.");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GenerateDebateTurnAsync(Rapper activeRapper, Rapper opponentRapper, Topic topic1, Topic topic2, bool isRapper1Turn, List<string> debateHistory, int maxCharacters = 750)
    {
        if (activeRapper == null) throw new ArgumentNullException(nameof(activeRapper));
        if (opponentRapper == null) throw new ArgumentNullException(nameof(opponentRapper));
        if (topic1 == null) throw new ArgumentNullException(nameof(topic1));
        if (topic2 == null) throw new ArgumentNullException(nameof(topic2));
        if (debateHistory == null) throw new ArgumentNullException(nameof(debateHistory));

        // Determine which topic the active rapper is arguing FOR
        var topicToChampion = isRapper1Turn ? topic1 : topic2;
        var topicToOppose = isRapper1Turn ? topic2 : topic1;

        _logger.LogInformation("Generating debate turn for {RapperName} arguing '{TopicChampion}' > '{TopicOppose}' against {OpponentName}",
            activeRapper.Name, topicToChampion.Title, topicToOppose.Title, opponentRapper.Name);

        try
        {
             // Create the list of messages, starting with the system prompt
            var messages = new List<ChatMessage>
            {
                // Pass both topics and the turn indicator to the prompt builder
                new SystemChatMessage(BuildSystemPrompt(activeRapper, opponentRapper, topic1, topic2, isRapper1Turn, maxCharacters))
            };

            // Add debate history as alternating user/assistant messages
            // History starts with opponent's first turn (User message if Rapper 1 starts)
            // If Rapper 1 (Side A) starts, the first item in history (if any) is Rapper 2's response (User)
            // If Rapper 2 (Side B) starts (not the current setup), the first item would be Rapper 1's (User)
            // The logic needs to align with who the *current* activeRapper is.
            // If it's Rapper 1's turn now, the last message in history was Rapper 2 (User).
            // If it's Rapper 2's turn now, the last message in history was Rapper 1 (User).
            // The prompt expects the history to end with a User message (the opponent's last turn).

            bool isAssistantMessage = !isRapper1Turn; // If it's Rapper 1's turn, history messages from Rapper 1 are Assistant, Rapper 2 are User.
                                                     // If it's Rapper 2's turn, history messages from Rapper 2 are Assistant, Rapper 1 are User.

            foreach (var turn in debateHistory)
            {
                // This logic seems reversed. Let's rethink.
                // The prompt assumes the AI *is* the active rapper (Assistant).
                // Therefore, turns from the *opponent* should be User messages,
                // and turns from the *active rapper* in the past should be Assistant messages.

                // Let's track based on the *current* turn perspective.
                // If it's Rapper 1's turn now (isRapper1Turn = true), past Rapper 1 turns are Assistant, past Rapper 2 turns are User.
                // If it's Rapper 2's turn now (isRapper1Turn = false), past Rapper 2 turns are Assistant, past Rapper 1 turns are User.

                // We need to know which rapper made the historical turn. The history list doesn't store this.
                // Assuming history alternates correctly starting with Rapper 1's first turn.
                // Turn 1 (R1), Turn 2 (R2), Turn 3 (R1), Turn 4 (R2)...
                // If current turn is R1 (e.g., Turn 3), history is [Turn 1 (R1), Turn 2 (R2)].
                //   - Turn 1 (R1) should be Assistant.
                //   - Turn 2 (R2) should be User.
                // If current turn is R2 (e.g., Turn 4), history is [Turn 1 (R1), Turn 2 (R2), Turn 3 (R1)].
                //   - Turn 1 (R1) should be User.
                //   - Turn 2 (R2) should be Assistant.
                //   - Turn 3 (R1) should be User.

                // Let's determine role based on index and whose turn it is *now*.
                int turnIndex = messages.Count - 1; // Index in the messages list (0 is system prompt)
                bool turnWasByRapper1 = (turnIndex % 2 != 0); // Odd indices (1, 3, 5...) correspond to Rapper 1's historical turns

                bool isTurnByCurrentActiveRapper = (isRapper1Turn && turnWasByRapper1) || (!isRapper1Turn && !turnWasByRapper1);

                if (isTurnByCurrentActiveRapper)
                {
                    messages.Add(new AssistantChatMessage(turn)); // Past turn by the AI's current persona
                }
                else
                {
                    messages.Add(new UserChatMessage(turn)); // Past turn by the opponent
                }
            }


            // The next message is implicitly from the assistant (the AI we want to generate for the activeRapper)
            // The system prompt already tells it who it is (e.g., "Respond as {rapper.Name}.")

            // Create chat completion options
            var options = new ChatCompletionOptions
            {
                Temperature = 0.7f,
                // MaxTokens = 100, // Removed due to build errors - relying on prompt instructions
                // TopP = 0.95f, // Optional parameters from sample
                // FrequencyPenalty = 0,
                // PresencePenalty = 0
            };

            _logger.LogDebug("Sending request to Azure OpenAI via ChatClient. Messages Count: {Count}", messages.Count);

            // Use the ChatClient to get the completion - use var for response type
            // The return type is likely ClientResult<T> which has a Value property
            var response = await _chatClient.CompleteChatAsync(messages, options);

            // Check response structure - ClientResult<T> uses .Value
            if (response == null || response.Value == null || response.Value.Content == null || response.Value.Content.Count == 0)
            {
                 _logger.LogWarning("Azure OpenAI response was null or contained no content.");
                 return "I got nothing to say right now..."; // Fallback response
            }

            // Extract the generated text
            string generatedText = response.Value.Content[0].Text;

            // Optional: Trim response if it exceeds maxCharacters (though prompt requests it)
            // Note: MaxCompletionTokens limits output length, but character count might still exceed maxCharacters slightly.
            if (generatedText.Length > maxCharacters)
            {
                // Find the last space within the limit to avoid cutting words
                int lastSpace = generatedText.LastIndexOf(' ', maxCharacters - 1);
                if (lastSpace > 0)
                {
                    generatedText = generatedText.Substring(0, lastSpace).TrimEnd() + "...";
                }
                else // No space found, hard truncate
                {
                    generatedText = generatedText.Substring(0, maxCharacters).TrimEnd() + "...";
                }
                 _logger.LogWarning("Generated text exceeded max characters ({MaxLength}) and was truncated. Rapper: {RapperName}", maxCharacters, activeRapper.Name);
            }

            _logger.LogInformation("Successfully generated debate turn for {RapperName}. Length: {Length}", activeRapper.Name, generatedText.Length);
            _logger.LogTrace("Generated text for {RapperName}: {Text}", activeRapper.Name, generatedText);

            return generatedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating debate turn for {RapperName} using Azure OpenAI.", activeRapper.Name);
            return $"Yo, my mic just cut out... (Error generating response for {activeRapper.Name})";
        }
    }

    /// <summary>
    /// Constructs the system prompt for the OpenAI chat completion request.
    /// </summary>
    private string BuildSystemPrompt(Rapper activeRapper, Rapper opponentRapper, Topic topic1, Topic topic2, bool isRapper1Turn, int maxCharacters)
    {
        // Determine which topic the active rapper is arguing FOR and which they are arguing AGAINST
        var topicToChampion = isRapper1Turn ? topic1 : topic2;
        var topicToOppose = isRapper1Turn ? topic2 : topic1;

        string personaInstruction = $"You are embodying the rapper {activeRapper.Name}. Adopt their known style, vernacular, common themes, and philosophical stance. Respond as if you are {activeRapper.Name} in a rap debate battle.";
        string opponentInfo = $"Your opponent is {opponentRapper.Name}.";
        // Describe the debate: Rapper 1 argues Topic 1 > Topic 2, Rapper 2 argues Topic 2 > Topic 1
        string topicInfo = $"The debate is about which topic is better: '{topic1.Title}' vs '{topic2.Title}'. You ({activeRapper.Name}) must argue why '{topicToChampion.Title}' is superior to '{topicToOppose.Title}'. Your opponent ({opponentRapper.Name}) is arguing the opposite.";
        if (!string.IsNullOrWhiteSpace(topicToChampion.Description))
        {
            topicInfo += $" Your topic ('{topicToChampion.Title}') description: {topicToChampion.Description}";
        }
         if (!string.IsNullOrWhiteSpace(topicToOppose.Description))
        {
            topicInfo += $" Opponent's topic ('{topicToOppose.Title}') description: {topicToOppose.Description}";
        }

        string rules = $"You are in a rap debate. Keep your response concise and impactful, strictly under {maxCharacters} characters (approx. 150 words). Focus on arguing FOR your assigned stance (why '{topicToChampion.Title}' is better than '{topicToOppose.Title}') while maintaining your persona. Address your opponent, {opponentRapper.Name}, directly or indirectly. **Crucially, your response MUST acknowledge or directly counter the points made in the opponent's immediately preceding turn (the last message in the history).** Do not just state your topic; argue its merits in comparison to the opponent's topic.";

        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("SYSTEM INSTRUCTIONS:");
        promptBuilder.AppendLine(personaInstruction);
        promptBuilder.AppendLine(opponentInfo);
        promptBuilder.AppendLine(topicInfo);
        promptBuilder.AppendLine(rules);
        promptBuilder.AppendLine("--- DEBATE HISTORY WILL FOLLOW (Opponent's turns are 'User', your past turns are 'Assistant') ---");
        promptBuilder.AppendLine($"Respond as {activeRapper.Name}, arguing why '{topicToChampion.Title}' is better than '{topicToOppose.Title}'.");

        return promptBuilder.ToString();
    }
}
