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
    public async Task StartNewDebateAsync(Rapper rapper1, Rapper rapper2, Topic topic1, Topic topic2)
    {
        if (topic1 == null || topic2 == null)
        {
            throw new ArgumentNullException(topic1 == null ? nameof(topic1) : nameof(topic2));
        }
        if (topic1.RowKey == topic2.RowKey) // Ensure different topics were selected
        {
             _logger.LogError("Attempted to start debate with the same topic selected twice: {TopicTitle}", topic1.Title);
             throw new ArgumentException("Cannot start a debate with the same topic selected twice.");
        }

        _logger.LogInformation("Starting new debate. Rapper1: {R1}, Rapper2: {R2}. Debate: '{Topic1Title}' vs '{Topic2Title}'",
            rapper1.Name, rapper2.Name, topic1.Title, topic2.Title);

        // Cancel any previous debate task
        ResetDebate();
        _debateCts = new CancellationTokenSource();
        var cancellationToken = _debateCts.Token;

        // Initialize state
        _currentState = new DebateState
        {
            Rapper1 = rapper1, // Argues Topic1 > Topic2
            Rapper2 = rapper2, // Argues Topic2 > Topic1
            Topic1 = topic1,
            Topic2 = topic2,
            IsDebateInProgress = true,
            IsDebateFinished = false,
            CurrentTurnNumber = 0,
            IsRapper1Turn = true, // Rapper 1 starts
            CurrentTurnText = $"The debate begins! {rapper1.Name} ('{topic1.Title}') vs {rapper2.Name} ('{topic2.Title}'). {rapper1.Name} argues why '{topic1.Title}' is better.",
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
                // No single "active topic" - the context is the comparison between Topic1 and Topic2

                string generatedText = string.Empty;
                byte[]? audioData = null;
                string? turnError = null;

                try
                {
                    // 1. Generate Text using OpenAI
                    generatedText = await _openAIService.GenerateDebateTurnAsync(
                        activeRapper,
                        opponent,
                        _currentState.Topic1!, // Pass Topic 1
                        _currentState.Topic2!, // Pass Topic 2
                        _currentState.IsRapper1Turn, // Indicate whose turn it is
                        _currentState.DebateHistory,
                        750);

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
            else
            {
                _logger.LogInformation("Debate finished after {TotalTurns} turns on '{Topic1Title}' vs '{Topic2Title}'.",
                    _currentState.TotalTurns, _currentState.Topic1?.Title, _currentState.Topic2?.Title);
                _currentState = _currentState with { IsDebateInProgress = false, IsDebateFinished = true, CurrentTurnText = "Debate Concluded!" };
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
