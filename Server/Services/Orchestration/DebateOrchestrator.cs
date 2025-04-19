using PoDebateRap.Shared.Models;
using PoDebateRap.Server.Services.AI;
using PoDebateRap.Server.Services.Speech;
using System.Threading; // Required for CancellationToken
using System.Threading.Tasks; // Required for TaskCompletionSource

namespace PoDebateRap.Server.Services.Orchestration;

/// <summary>
/// Implements the IDebateOrchestrator interface to manage the debate flow.
/// Coordinates AI text generation and speech synthesis.
/// </summary>
public class DebateOrchestrator : IDebateOrchestrator, IDisposable
{
    private readonly IAzureOpenAIService _openAIService;
    private readonly ITextToSpeechService _speechService;
    private readonly ILogger<DebateOrchestrator> _logger;

    private DebateState _currentState = new(); // Initialize with default state
    private CancellationTokenSource? _debateCts; // To cancel ongoing debate flow
    private TaskCompletionSource? _audioPlaybackTcs; // Used to signal audio playback completion

    public DebateState CurrentState => _currentState;
    public event Func<DebateState, Task>? OnStateChangeAsync;

    /// <summary>
    /// Initializes a new instance of the <see cref="DebateOrchestrator"/> class.
    /// </summary>
    public DebateOrchestrator(
        IAzureOpenAIService openAIService,
        ITextToSpeechService speechService,
        ILogger<DebateOrchestrator> logger)
    {
        _openAIService = openAIService;
        _speechService = speechService;
        _logger = logger;
        _logger.LogInformation("DebateOrchestrator initialized.");
    }

    /// <inheritdoc />
    // Updated signature for single topic
    public async Task StartNewDebateAsync(Rapper rapper1, Rapper rapper2, Topic topic)
    {
        // Argument validation
        if (topic == null) throw new ArgumentNullException(nameof(topic));
        if (rapper1 == null) throw new ArgumentNullException(nameof(rapper1));
        if (rapper2 == null) throw new ArgumentNullException(nameof(rapper2));
        if (rapper1.Name == rapper2.Name)
        {
            _logger.LogError("Attempted to start debate with the same rapper selected twice: {RapperName}", rapper1.Name);
            throw new ArgumentException("Cannot start a debate with the same rapper selected twice.");
        }

        _logger.LogInformation("Starting new debate. Rapper1 (Pro): {R1}, Rapper2 (Con): {R2}. Topic: '{TopicTitle}'",
            rapper1.Name, rapper2.Name, topic.Title);

        // Cancel any previous debate task
        ResetDebate();
        _debateCts = new CancellationTokenSource();
        var cancellationToken = _debateCts.Token;

        // Initialize state
        _currentState = new DebateState
        {
            Rapper1 = rapper1, // Assumed Pro
            Rapper2 = rapper2, // Assumed Con
            Topic = topic,     // Single topic
            IsDebateInProgress = true,
            IsDebateFinished = false,
            CurrentTurnNumber = 0,
            IsRapper1Turn = true, // Rapper 1 (Pro) starts
            // Updated initial text for pro/con
            CurrentTurnText = $"The debate begins! Topic: '{topic.Title}'. {rapper1.Name} (Pro) vs {rapper2.Name} (Con). {rapper1.Name} starts arguing FOR the topic.",
            DebateHistory = new List<string>()
        };
        await NotifyStateChangeAsync(); // Notify UI about the initial state

        // Start the debate loop (run in background, don't await here)
        _ = Task.Run(() => RunDebateLoopAsync(cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Runs the main loop managing the turns of the debate.
    /// </summary>
    private async Task RunDebateLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Debate loop started.");
            // No initial delay needed if we wait for audio completion signal later

            while (_currentState.CurrentTurnNumber < _currentState.TotalTurns && !cancellationToken.IsCancellationRequested)
            {
                 // Wait for the previous turn's audio to finish playing, unless it's the very first turn
                 // or if the previous turn had no audio.
                if (_audioPlaybackTcs != null)
                {
                    _logger.LogDebug("Waiting for audio playback signal for turn {TurnNumber}...", _currentState.CurrentTurnNumber);
                    await _audioPlaybackTcs.Task; // Wait for SignalAudioPlaybackCompleteAsync
                    _logger.LogDebug("Audio playback signal received for turn {TurnNumber}.", _currentState.CurrentTurnNumber);
                    _audioPlaybackTcs = null; // Reset TCS for the next turn
                }

                if (cancellationToken.IsCancellationRequested) break;

                _currentState = _currentState with { CurrentTurnNumber = _currentState.CurrentTurnNumber + 1 };
                _logger.LogInformation("Starting Turn {TurnNumber}. Active Rapper: {ActiveRapperName}",
                    _currentState.CurrentTurnNumber,
                    _currentState.IsRapper1Turn ? _currentState.Rapper1!.Name : _currentState.Rapper2!.Name);

                // Indicate processing
                _currentState = _currentState with { IsGeneratingTurn = true, CurrentTurnAudio = null }; // Clear previous audio
                await NotifyStateChangeAsync();

                var activeRapper = _currentState.IsRapper1Turn ? _currentState.Rapper1! : _currentState.Rapper2!;
                var opponent = _currentState.IsRapper1Turn ? _currentState.Rapper2! : _currentState.Rapper1!;
                var currentTopic = _currentState.Topic!; // Use the single topic
                bool isProArgument = _currentState.IsRapper1Turn; // Rapper1 is Pro

                string generatedText = string.Empty;
                byte[]? audioData = null;
                string? turnError = null;

                try
                {
                    // 1. Generate Text using OpenAI
                    // Call the updated AI service method with single topic and stance
                    generatedText = await _openAIService.GenerateDebateTurnAsync(
                        activeRapper,
                        opponent,
                        currentTopic,   // Pass the single topic
                        isProArgument, // Indicate stance (Pro if Rapper1's turn)
                        _currentState.DebateHistory,
                        _currentState.CurrentTurnNumber, // Pass the current turn number
                        750); // Max tokens

                    if (cancellationToken.IsCancellationRequested) break;

                    // 2. Synthesize Speech using Azure Speech
                    audioData = await _speechService.SynthesizeSpeechAsync(generatedText, activeRapper);

                    if (cancellationToken.IsCancellationRequested) break;

                    if (audioData == null || audioData.Length == 0)
                    {
                        _logger.LogWarning("Speech synthesis returned null or empty audio data for turn {TurnNumber}.", _currentState.CurrentTurnNumber);
                        audioData = null; // Ensure it's null if empty
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing turn {TurnNumber} for {ActiveRapperName}.", _currentState.CurrentTurnNumber, activeRapper.Name);
                    turnError = $"An error occurred generating the response for {activeRapper.Name}.";
                    generatedText = turnError; // Display error in text
                    audioData = null; // No audio on error
                }

                // Create a new TCS *before* updating the state with audio data
                // Only create if audio data exists, otherwise we don't need to wait
                if (audioData != null)
                {
                    _audioPlaybackTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                }
                else
                {
                    _audioPlaybackTcs = null; // Ensure it's null if no audio
                }


                // Update state with results
                var newHistory = new List<string>(_currentState.DebateHistory) { generatedText }; // Add current turn text
                _currentState = _currentState with
                {
                    CurrentTurnText = generatedText,
                    CurrentTurnAudio = audioData,
                    DebateHistory = newHistory,
                    IsGeneratingTurn = false, // Finished processing
                    ErrorMessage = turnError // Set or clear error message
                };
                await NotifyStateChangeAsync(); // Send text and audio (if any) to UI

                _logger.LogDebug("Turn {TurnNumber} processed. Text length: {Length}, Audio size: {Size}",
                    _currentState.CurrentTurnNumber, generatedText.Length, audioData?.Length ?? 0);

                // If there was no audio, we don't need to wait for a signal, so we can proceed immediately
                // after a short simulated reading delay. If there *was* audio, the loop will wait
                // at the top on the _audioPlaybackTcs.
                if (audioData == null)
                {
                    _logger.LogDebug("No audio for turn {TurnNumber}, adding short delay before switching.", _currentState.CurrentTurnNumber);
                    await Task.Delay(1000, cancellationToken); // Short delay if no audio
                }

                // Switch turns (only relevant if loop continues)
                 if (_currentState.CurrentTurnNumber < _currentState.TotalTurns)
                 {
                    _currentState = _currentState with { IsRapper1Turn = !_currentState.IsRapper1Turn };
                 }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Debate loop cancelled.");
             _currentState = _currentState with { IsDebateInProgress = false, ErrorMessage = "Debate cancelled." };
            }
            else // Debate finished normally
            {
                 // Wait for the *final* turn's audio to finish before judging
                 if (_audioPlaybackTcs != null)
                 {
                    _logger.LogDebug("Waiting for final audio playback signal (Turn {TurnNumber})...", _currentState.CurrentTurnNumber);
                    await _audioPlaybackTcs.Task;
                    _logger.LogDebug("Final audio playback signal received.");
                    _audioPlaybackTcs = null;
                 }

                _logger.LogInformation("Debate finished after {TotalTurns} turns on topic '{TopicTitle}'. Determining winner...",
                    _currentState.TotalTurns, _currentState.Topic?.Title);

                // Call the AI Judge to get reasoning and stats
                string? reasoning = null;
                DebateStats? stats = null;
                string winnerName = "Error Judging"; // Default winner status

                try
                {
                    (reasoning, stats) = await _openAIService.JudgeDebateAsync(
                       _currentState.DebateHistory,
                       _currentState.Rapper1!,
                       _currentState.Rapper2!,
                       _currentState.Topic!);

                    // Determine winner based on total scores if stats were parsed
                    if (stats != null)
                    {
                        if (stats.Rapper1TotalScore > stats.Rapper2TotalScore)
                        {
                            winnerName = _currentState.Rapper1!.Name;
                        }
                        else if (stats.Rapper2TotalScore > stats.Rapper1TotalScore)
                        {
                            winnerName = _currentState.Rapper2!.Name;
                        }
                        else
                        {
                            winnerName = "Draw"; // Explicitly handle ties
                        }
                         _logger.LogInformation("Winner determined by stats: {WinnerName} (R1 Total: {R1Score}, R2 Total: {R2Score})",
                            winnerName, stats.Rapper1TotalScore, stats.Rapper2TotalScore);
                    }
                    else
                    {
                         winnerName = "Stats Error"; // Indicate stats parsing failed
                         reasoning ??= "Could not determine winner because stats failed to generate or parse.";
                         _logger.LogWarning("Could not determine winner because stats object was null.");
                    }
                }
                catch(Exception judgeEx)
                {
                    _logger.LogError(judgeEx, "Error occurred during AI judging call.");
                    reasoning ??= "An error occurred while the judge was deliberating."; // Provide error reasoning if not already set
                    winnerName = "Error Judging"; // Ensure winner reflects the error
                }

                _currentState = _currentState with
                {
                    IsDebateInProgress = false,
                    IsDebateFinished = true,
                    CurrentTurnText = "Debate Concluded!",
                    WinnerName = winnerName, // Store the calculated winner (or Draw/Error)
                    JudgeReasoning = reasoning, // Store the reasoning
                    Stats = stats // Store the stats (might be null)
                };
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Debate loop explicitly cancelled via CancellationToken.");
             _currentState = _currentState with { IsDebateInProgress = false, ErrorMessage = "Debate cancelled." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in debate loop.");
            _currentState = _currentState with { IsDebateInProgress = false, IsDebateFinished = true, ErrorMessage = "An unexpected error ended the debate." };
        }
        finally
        {
             _currentState = _currentState with { IsGeneratingTurn = false }; // Ensure spinner stops
             // Ensure TCS is cleared if loop exits unexpectedly
             _audioPlaybackTcs?.TrySetResult();
             _audioPlaybackTcs = null;
             await NotifyStateChangeAsync(); // Notify final state
             _logger.LogInformation("Debate loop ended.");
        }
    }

     /// <inheritdoc />
    public Task SignalAudioPlaybackCompleteAsync()
    {
        _logger.LogDebug("Received signal for audio playback completion.");
        // Set the result on the TCS. If it's null or already completed, this does nothing.
        _audioPlaybackTcs?.TrySetResult();
        return Task.CompletedTask;
    }


    /// <inheritdoc />
    public void ResetDebate()
    {
        _logger.LogInformation("Resetting debate state.");
        _debateCts?.Cancel(); // Cancel any ongoing loop
        _debateCts?.Dispose();
        _debateCts = null;
        _audioPlaybackTcs?.TrySetCanceled(); // Cancel any pending wait
        _audioPlaybackTcs = null;
        _currentState = new DebateState(); // Reset to default
        // No need to notify state change here, let the caller decide if needed
    }

    /// <summary>
    /// Notifies subscribers about the current state change.
    /// </summary>
    private async Task NotifyStateChangeAsync()
    {
        if (OnStateChangeAsync != null)
        {
            try
            {
                // Use GetInvocationList for safety with multiple subscribers
                foreach (Func<DebateState, Task> handler in OnStateChangeAsync.GetInvocationList())
                {
                    await handler(_currentState);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during OnStateChangeAsync notification.");
            }
        }
    }

    /// <summary>
    /// Disposes the cancellation token source if it exists.
    /// </summary>
    public void Dispose()
    {
        _debateCts?.Cancel();
        _debateCts?.Dispose();
        _audioPlaybackTcs?.TrySetCanceled();
        GC.SuppressFinalize(this);
        _logger.LogDebug("DebateOrchestrator disposed.");
    }
}
