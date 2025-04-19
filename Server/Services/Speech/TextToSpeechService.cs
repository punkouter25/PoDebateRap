using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using PoDebateRap.Shared.Models;

namespace PoDebateRap.Server.Services.Speech;

/// <summary>
/// Implements the ITextToSpeechService interface using Azure Cognitive Services Speech SDK.
/// Handles text-to-speech synthesis.
/// </summary>
public class TextToSpeechService : ITextToSpeechService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TextToSpeechService> _logger;
    private readonly SpeechConfig _speechConfig;

    // Simple mapping of rapper names to potential voice characteristics (can be expanded)
    // Using standard neural voices for now. Custom Neural Voice would be ideal for true persona matching.
    // Voices list: https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=tts
    private static readonly Dictionary<string, string> RapperVoiceMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Male Voices (Examples - adjust based on desired persona fit)
        { "Jay-Z", "en-US-DavisNeural" },
        { "Eminem", "en-US-JasonNeural" }, // Maybe slightly higher pitch?
        { "Nas", "en-US-TonyNeural" }, // Smooth, articulate
        { "Kendrick Lamar", "en-US-GuyNeural" }, // Distinctive, maybe slightly higher pitch
        { "Tupac Shakur", "en-US-DavisNeural" }, // Passionate, strong
        { "The Notorious B.I.G.", "en-US-TonyNeural" }, // Deep, smooth
        { "Rakim", "en-US-TonyNeural" }, // Smooth, authoritative
        { "Andre 3000", "en-US-JasonNeural" }, // Unique, versatile
        { "Snoop Dogg", "en-US-DavisNeural" }, // Laid back
        { "Ice Cube", "en-US-TonyNeural" }, // Strong, direct
        { "Kanye West", "en-US-GuyNeural" }, // Energetic
        { "Lil Wayne", "en-US-JasonNeural" }, // Unique vocal tone (hard to match std voices)
        { "Drake", "en-US-DavisNeural" }, // Smooth, melodic
        { "J. Cole", "en-US-TonyNeural" }, // Conversational
        { "MF DOOM", "en-US-TonyNeural" }, // Deep, masked (metaphorically)
        { "Method Man", "en-US-DavisNeural" }, // Raspy, energetic

        // Female Voices (Examples)
        { "Lauryn Hill", "en-US-JaneNeural" }, // Soulful, strong
        { "Missy Elliott", "en-US-NancyNeural" }, // Energetic, unique
        { "Nicki Minaj", "en-US-JennyNeural" }, // Versatile, animated
        { "Queen Latifah", "en-US-AriaNeural" }, // Strong, clear

        // Default voice if no specific match
        { "DefaultMale", "en-US-DavisNeural" },
        { "DefaultFemale", "en-US-AriaNeural" }
    };

    // Simple gender check based on name (highly unreliable, better to store gender with Rapper model if needed)
    private static readonly List<string> FemaleRapperNames = new() { "Lauryn Hill", "Missy Elliott", "Nicki Minaj", "Queen Latifah" };


    /// <summary>
    /// Initializes a new instance of the <see cref="TextToSpeechService"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="InvalidOperationException">Thrown if Azure Speech configuration is missing.</exception>
    public TextToSpeechService(IConfiguration configuration, ILogger<TextToSpeechService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var subscriptionKey = _configuration["Azure:Speech:SubscriptionKey"];
        var region = _configuration["Azure:Speech:Region"];

        if (string.IsNullOrWhiteSpace(subscriptionKey) || string.IsNullOrWhiteSpace(region))
        {
            _logger.LogError("Azure Speech SubscriptionKey or Region is not configured. Please check Azure:Speech settings.");
            throw new InvalidOperationException("Azure Speech SubscriptionKey and Region must be configured.");
        }

        try
        {
            _speechConfig = SpeechConfig.FromSubscription(subscriptionKey, region);
            // Set the output format (MP3 is generally smaller than WAV)
            _speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio16Khz32KBitRateMonoMp3);
            _logger.LogInformation("TextToSpeechService initialized for region {Region}.", region);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Failed to initialize SpeechConfig.");
             throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> SynthesizeSpeechAsync(string text, Rapper rapper)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("SynthesizeSpeechAsync called with empty text.");
            return null;
        }
        if (rapper == null) throw new ArgumentNullException(nameof(rapper));

        // Select voice based on rapper name
        string voiceName = SelectVoiceForRapper(rapper.Name);
        _speechConfig.SpeechSynthesisVoiceName = voiceName;

        _logger.LogInformation("Synthesizing speech for {RapperName} using voice {VoiceName}. Text length: {Length}",
            rapper.Name, voiceName, text.Length);
        _logger.LogTrace("Synthesizing text: {Text}", text); // Log full text only at Trace level

        try
        {
            // Synthesize to memory stream
            // Using statement ensures disposal of the synthesizer
            using var synthesizer = new SpeechSynthesizer(_speechConfig, null); // null for audio config means in-memory
            using var result = await synthesizer.SpeakTextAsync(text);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                _logger.LogInformation("Speech synthesis completed successfully for {RapperName}.", rapper.Name);
                return result.AudioData;
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                _logger.LogError("Speech synthesis canceled for {RapperName}. Reason: {Reason}. ErrorDetails: {ErrorDetails}",
                    rapper.Name, cancellation.Reason, cancellation.ErrorDetails);

                // Optionally check for specific error codes, e.g., authentication failure
                if (cancellation.Reason == CancellationReason.Error)
                {
                   _logger.LogError($"ErrorCode={cancellation.ErrorCode}");
                   _logger.LogError($"ErrorDetails={cancellation.ErrorDetails}");
                }
                return null;
            }
            else
            {
                 _logger.LogWarning("Speech synthesis for {RapperName} resulted in unexpected status: {Reason}", rapper.Name, result.Reason);
                 return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred during speech synthesis for {RapperName}.", rapper.Name);
            throw; // Re-throw unexpected errors
        }
    }

     /// <summary>
    /// Selects a voice name based on the rapper's name.
    /// Provides a basic mapping; could be enhanced significantly.
    /// </summary>
    /// <param name="rapperName">The name of the rapper.</param>
    /// <returns>The selected voice name.</returns>
    private string SelectVoiceForRapper(string rapperName)
    {
        if (RapperVoiceMap.TryGetValue(rapperName, out var specificVoice))
        {
            return specificVoice;
        }

        // Basic fallback based on assumed gender (highly unreliable)
        bool isFemale = FemaleRapperNames.Contains(rapperName, StringComparer.OrdinalIgnoreCase);
        return isFemale ? RapperVoiceMap["DefaultFemale"] : RapperVoiceMap["DefaultMale"];
    }


    /// <inheritdoc />
    public async Task<List<string>> GetAvailableVoicesAsync()
    {
        _logger.LogInformation("Retrieving available synthesis voices.");
        try
        {
            // Note: SpeechSynthesizer needs to be created to get voices.
            // We don't need audio output here, so AudioConfig can be null.
            using var synthesizer = new SpeechSynthesizer(_speechConfig, null);
            using var result = await synthesizer.GetVoicesAsync(); // Gets voices for the service region

            if (result.Reason == ResultReason.VoicesListRetrieved)
            {
                _logger.LogInformation("Successfully retrieved {Count} voices.", result.Voices.Count);
                return result.Voices.Select(v => v.Name).ToList();
            }
            else if (result.Reason == ResultReason.Canceled)
            {
                 _logger.LogError("Failed to retrieve voices list. Reason: Canceled. ErrorDetails: {ErrorDetails}", result.ErrorDetails);
                 return new List<string>(); // Return empty list on failure
            }
            else
            {
                 _logger.LogWarning("Voice list retrieval returned unexpected status: {Reason}", result.Reason);
                 return new List<string>();
            }
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "An unexpected error occurred while retrieving available voices.");
             throw;
        }
    }
}
