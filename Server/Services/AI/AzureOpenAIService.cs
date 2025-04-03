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
    // Updated signature to match interface (added currentTurnNumber)
    public async Task<string> GenerateDebateTurnAsync(Rapper activeRapper, Rapper opponentRapper, Topic topic, bool isArguingPro, List<string> debateHistory, int currentTurnNumber, int maxCharacters = 750)
    {
        // Argument validation
        if (activeRapper == null) throw new ArgumentNullException(nameof(activeRapper));
        if (opponentRapper == null) throw new ArgumentNullException(nameof(opponentRapper));
        if (topic == null) throw new ArgumentNullException(nameof(topic));
        if (debateHistory == null) throw new ArgumentNullException(nameof(debateHistory));

        string stance = isArguingPro ? "FOR" : "AGAINST";
        _logger.LogInformation("Generating debate turn for {RapperName} arguing {Stance} '{TopicTitle}' against {OpponentName}",
            activeRapper.Name, stance, topic.Title, opponentRapper.Name);

        try
        {
            // Create the list of messages, starting with the system prompt
            var messages = new List<ChatMessage>
            {
                // Pass turn number to prompt builder
                new SystemChatMessage(BuildSystemPrompt(activeRapper, opponentRapper, topic, isArguingPro, currentTurnNumber, maxCharacters))
            };

            // Add debate history as alternating user/assistant messages
            // The AI (Assistant) is the activeRapper. Opponent turns are User. Active rapper's past turns are Assistant.
            // History alternates: R1(Pro), R2(Con), R1(Pro), R2(Con)...
            // The first turn in history (index 0) is always by Rapper 1 (Pro).
            for (int i = 0; i < debateHistory.Count; i++)
            {
                string turnText = debateHistory[i];
                bool turnWasByRapper1 = (i % 2 == 0); // Turn 0, 2, 4... by Rapper 1 (Pro)

                // Determine if the historical turn was by the *currently* active rapper
                bool turnWasByCurrentActiveRapper = (isArguingPro && turnWasByRapper1) || (!isArguingPro && !turnWasByRapper1);

                if (turnWasByCurrentActiveRapper)
                {
                    messages.Add(new AssistantChatMessage(turnText)); // Past turn by the AI's current persona
                }
                else
                {
                    messages.Add(new UserChatMessage(turnText)); // Past turn by the opponent
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
    /// Constructs the system prompt for the OpenAI chat completion request for a single-topic pro/con debate.
    /// </summary>
    // Updated signature (added currentTurnNumber)
    private string BuildSystemPrompt(Rapper activeRapper, Rapper opponentRapper, Topic topic, bool isArguingPro, int currentTurnNumber, int maxCharacters)
    {
        string stance = isArguingPro ? "FOR" : "AGAINST";
        string opponentStance = isArguingPro ? "AGAINST" : "FOR";

        // Determine round number (1, 2, or 3)
        int roundNumber = ((currentTurnNumber - 1) / 2) + 1;

        string personaInstruction = $"You are embodying the rapper {activeRapper.Name}. Adopt their known style, vernacular, common themes, and philosophical stance. Respond as if you are {activeRapper.Name} in a rap debate battle.";
        string opponentInfo = $"Your opponent is {opponentRapper.Name}.";
        // Describe the single topic and the stances
        string topicInfo = $"The debate topic is: '{topic.Title}'.";
        if (!string.IsNullOrWhiteSpace(topic.Description))
        {
            topicInfo += $" Description: {topic.Description}";
        }
        topicInfo += $" You ({activeRapper.Name}) are arguing {stance} this topic. Your opponent ({opponentRapper.Name}) is arguing {opponentStance} it.";

        // Build rules based on round number
        var rulesBuilder = new StringBuilder();
        rulesBuilder.AppendLine($"You are in a rap debate, currently in Round {roundNumber} (Turn {currentTurnNumber}). Keep your response concise and impactful, strictly under {maxCharacters} characters (approx. 150 words).");
        rulesBuilder.AppendLine($"Focus on arguing {stance} the topic ('{topic.Title}') while maintaining your persona. Address your opponent, {opponentRapper.Name}, directly or indirectly.");
        rulesBuilder.AppendLine($"**Crucially, your response MUST acknowledge or directly counter ONLY THE LAST SENTENCE of the opponent's immediately preceding turn (the last message in the history).**"); // Focus on last sentence

        // Add round-specific tone instructions
        switch (roundNumber)
        {
            case 1: // Turns 1 & 2
                rulesBuilder.AppendLine("Maintain a focused, competitive but respectful tone for this first round.");
                break;
            case 2: // Turns 3 & 4
                rulesBuilder.AppendLine("Escalate the tone. Get more aggressive, personal, and dismissive of your opponent's points.");
                break;
            case 3: // Turns 5 & 6
                rulesBuilder.AppendLine("Go off the rails! Be angry, irrational, and make wild claims. Disregard logic and focus on insults and absurdity while still loosely relating to the topic and your stance. You are allowed to use foul language and profanity in this round."); // Added permission for foul language
                break;
        }
        rulesBuilder.AppendLine($"Do not just state the topic; argue your stance ({stance}) with reasoning (or lack thereof in later rounds) and style.");
        string rules = rulesBuilder.ToString();

        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("SYSTEM INSTRUCTIONS:");
        promptBuilder.AppendLine(personaInstruction);
        promptBuilder.AppendLine(opponentInfo);
        promptBuilder.AppendLine(topicInfo);
        promptBuilder.AppendLine(rules);
        promptBuilder.AppendLine("--- DEBATE HISTORY WILL FOLLOW (Opponent's turns are 'User', your past turns are 'Assistant') ---");
        promptBuilder.AppendLine($"Respond as {activeRapper.Name}, arguing {stance} the topic: '{topic.Title}'.");

        return promptBuilder.ToString();
    }

    /// <inheritdoc />
    // Updated return type to tuple (Reasoning, Stats)
    public async Task<(string? Reasoning, DebateStats? Stats)> JudgeDebateAsync(List<string> debateHistory, Rapper rapper1, Rapper rapper2, Topic topic)
    {
        // Default return values in case of error or parsing failure
        string? reasoning = null;
        DebateStats? stats = null;

        if (debateHistory == null || debateHistory.Count == 0) throw new ArgumentNullException(nameof(debateHistory));
        if (rapper1 == null) throw new ArgumentNullException(nameof(rapper1));
        if (rapper2 == null) throw new ArgumentNullException(nameof(rapper2));
        if (topic == null) throw new ArgumentNullException(nameof(topic));

        _logger.LogInformation("Judging debate between {Rapper1Name} (Pro) and {Rapper2Name} (Con) on topic '{TopicTitle}'",
            rapper1.Name, rapper2.Name, topic.Title);

        try
        {
            // Build the prompt for the judge - requests reasoning and stats ONLY
            string judgePrompt = BuildJudgePrompt(rapper1, rapper2, topic);
            var messages = new List<ChatMessage> { new SystemChatMessage(judgePrompt) };

            // Add the debate history as a single user message for the judge to analyze
            var historyText = new StringBuilder();
            for (int i = 0; i < debateHistory.Count; i++)
            {
                string speaker = (i % 2 == 0) ? rapper1.Name : rapper2.Name; // Rapper1 starts (index 0)
                historyText.AppendLine($"Turn {i + 1} ({speaker}): {debateHistory[i]}");
                historyText.AppendLine(); // Add blank line for readability
            }
            messages.Add(new UserChatMessage(historyText.ToString()));


            var options = new ChatCompletionOptions
            {
                Temperature = 0.3f, // Lower temperature for more deterministic judging
                // MaxTokens = 250 // Increased token limit to allow for reasoning and stats
            };

            _logger.LogDebug("Sending request to Azure OpenAI for judging with stats request.");
            var response = await _chatClient.CompleteChatAsync(messages, options);

            if (response == null || response.Value == null || response.Value.Content == null || response.Value.Content.Count == 0 || string.IsNullOrWhiteSpace(response.Value.Content[0].Text))
            {
                _logger.LogWarning("Azure OpenAI judge response was null or empty.");
                reasoning = "AI Judge did not provide a response.";
            }
            else
            {
                string rawResponse = response.Value.Content[0].Text.Trim();
                _logger.LogDebug("Raw judge response: {RawResponse}", rawResponse);

                // Parse the response based on the expected format (Reasoning and Stats only)
                (reasoning, stats) = ParseJudgeResponse(rawResponse);
            }
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error judging debate using Azure OpenAI.");
             reasoning = "An error occurred during AI judging.";
             stats = null;
        }

        return (reasoning, stats);
    }

     /// <summary>
    /// Constructs the system prompt for the Judge AI persona, requesting winner, reasoning, and stats.
    /// </summary>
    private string BuildJudgePrompt(Rapper rapper1, Rapper rapper2, Topic topic) // Removed history from signature
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("SYSTEM INSTRUCTIONS:");
        promptBuilder.AppendLine("You are an impartial and logical judge for a rap debate battle.");
        promptBuilder.AppendLine($"The debate topic was: '{topic.Title}'.");
        promptBuilder.AppendLine($"{rapper1.Name} argued FOR the topic (Pro).");
        promptBuilder.AppendLine($"{rapper2.Name} argued AGAINST the topic (Con).");
        promptBuilder.AppendLine("The user will provide the full transcript of the debate.");
        promptBuilder.AppendLine("Analyze the entire debate transcript.");
        promptBuilder.AppendLine($"Evaluate which rapper, {rapper1.Name} (Pro) or {rapper2.Name} (Con), presented the more logical, coherent, and persuasive arguments overall, considering their assigned stance.");
        promptBuilder.AppendLine("While the rappers may have become emotional or irrational in later rounds, consider the entire debate.");
        promptBuilder.AppendLine("Provide your response in the following exact format, with each item on a new line:");
        // Removed Winner line
        promptBuilder.AppendLine("Reasoning: [Your 1-2 sentence justification for the overall performance and scores]"); // Modified reasoning prompt
        promptBuilder.AppendLine("Stats:");
        promptBuilder.AppendLine($"Rapper1_Logic: [Score 1-5 for {rapper1.Name}'s logic, focusing on Rounds 1 & 2]");
        promptBuilder.AppendLine($"Rapper2_Logic: [Score 1-5 for {rapper2.Name}'s logic, focusing on Rounds 1 & 2]");
        promptBuilder.AppendLine($"Rapper1_Sentiment: [Score 1-5 for {rapper1.Name}'s overall sentiment (1=Negative, 3=Neutral, 5=Positive)]"); // Changed Stat Name
        promptBuilder.AppendLine($"Rapper2_Sentiment: [Score 1-5 for {rapper2.Name}'s overall sentiment (1=Negative, 3=Neutral, 5=Positive)]"); // Changed Stat Name
        promptBuilder.AppendLine($"Rapper1_Adherence: [Score 1-5 for how well {rapper1.Name} adhered to their persona/style]"); // Changed Stat Name
        promptBuilder.AppendLine($"Rapper2_Adherence: [Score 1-5 for how well {rapper2.Name} adhered to their persona/style]"); // Changed Stat Name
        promptBuilder.AppendLine($"Rapper1_Rebuttal: [Score 1-5 for {rapper1.Name}'s quality of rebutting the opponent's last sentence]");
        promptBuilder.AppendLine($"Rapper2_Rebuttal: [Score 1-5 for {rapper2.Name}'s quality of rebutting the opponent's last sentence]");
        promptBuilder.AppendLine("Ensure scores are integers between 1 and 5.");

        return promptBuilder.ToString();
    }

    /// <summary>
    /// Parses the raw response string from the AI judge to extract reasoning and stats. Calculates total scores.
    /// </summary>
    private (string? Reasoning, DebateStats? Stats) ParseJudgeResponse(string rawResponse) // Removed rapper names
    {
        string? reasoning = null;
        DebateStats stats = new DebateStats(); // Initialize with default scores (0)
        bool statsParsedSuccessfully = false;

        try
        {
            var lines = rawResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Dictionary<string, string> parsedData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                var parts = line.Split(':', 2);
                if (parts.Length == 2)
                {
                    parsedData[parts[0].Trim()] = parts[1].Trim();
                }
            }

            // Extract Reasoning
            if (parsedData.TryGetValue("Reasoning", out var judgeReasoning))
            {
                reasoning = judgeReasoning;
            } else {
                 _logger.LogWarning("Could not parse 'Reasoning:' line from judge response.");
            }

            // Extract Stats (using updated property names and keys)
            stats.Rapper1LogicScore = ParseStatScore(parsedData, "Rapper1_Logic");
            stats.Rapper2LogicScore = ParseStatScore(parsedData, "Rapper2_Logic");
            stats.Rapper1SentimentScore = ParseStatScore(parsedData, "Rapper1_Sentiment"); // Changed Key
            stats.Rapper2SentimentScore = ParseStatScore(parsedData, "Rapper2_Sentiment"); // Changed Key
            stats.Rapper1AdherenceScore = ParseStatScore(parsedData, "Rapper1_Adherence");
            stats.Rapper2AdherenceScore = ParseStatScore(parsedData, "Rapper2_Adherence");
            stats.Rapper1RebuttalScore = ParseStatScore(parsedData, "Rapper1_Rebuttal");
            stats.Rapper2RebuttalScore = ParseStatScore(parsedData, "Rapper2_Rebuttal");

            // Calculate Total Scores
            stats.Rapper1TotalScore = stats.Rapper1LogicScore + stats.Rapper1SentimentScore + stats.Rapper1AdherenceScore + stats.Rapper1RebuttalScore;
            stats.Rapper2TotalScore = stats.Rapper2LogicScore + stats.Rapper2SentimentScore + stats.Rapper2AdherenceScore + stats.Rapper2RebuttalScore;

            statsParsedSuccessfully = true; // Indicate successful parsing and calculation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse judge response: {RawResponse}", rawResponse);
            reasoning = "Could not parse judge's response.";
            stats = null; // Nullify stats on parsing error
        }

        return (reasoning, statsParsedSuccessfully ? stats : null);
    }

    /// <summary>
    /// Helper to safely parse an integer score from the parsed data dictionary.
    /// </summary>
    private int ParseStatScore(Dictionary<string, string> parsedData, string key)
    {
        if (parsedData.TryGetValue(key, out var valueStr) && int.TryParse(valueStr, out var score))
        {
            return Math.Clamp(score, 1, 5); // Clamp score between 1 and 5
        }
        _logger.LogWarning("Could not parse or find score for key: {Key}", key);
        return 0; // Return 0 if parsing fails or key not found
    }
}
