using PoDebateRap.Shared.Models;

namespace PoDebateRap.Server.Services.Orchestration;

/// <summary>
/// Defines the contract for orchestrating a rap debate.
/// Manages the flow, state, AI interaction, and speech synthesis for a debate session.
/// </summary>
public interface IDebateOrchestrator
{
    /// <summary>
    /// Gets the current state of the debate.
    /// </summary>
    DebateState CurrentState { get; }

    /// <summary>
    /// Event triggered when the debate state changes (e.g., new turn text, audio ready, debate ended).
    /// Components can subscribe to this to update the UI.
    /// </summary>
    event Func<DebateState, Task>? OnStateChangeAsync;

    /// <summary>
    /// Starts a new debate session.
    /// </summary>
    /// <param name="rapper1">The first rapper (argues Topic 1 > Topic 2).</param>
    /// <param name="rapper2">The second rapper (argues Topic 2 > Topic 1).</param>
    /// <param name="topic1">The first selected topic.</param>
    /// <param name="topic2">The second selected topic.</param>
    /// <returns>A Task representing the asynchronous operation of starting the debate flow.</returns>
    Task StartNewDebateAsync(Rapper rapper1, Rapper rapper2, Topic topic1, Topic topic2);

    /// <summary>
    /// Resets the orchestrator to an initial state, clearing any ongoing debate.
    /// </summary>
    void ResetDebate();

    /// <summary>
    /// Signals to the orchestrator that the audio playback for the current turn has completed.
    /// This allows the orchestrator to proceed with the next turn.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task SignalAudioPlaybackCompleteAsync();

    // Potentially add methods for Pause/Resume if needed later
    // Task PauseDebateAsync();
    // Task ResumeDebateAsync();
}

/// <summary>
/// Represents the current state of the ongoing debate.
/// Passed via the OnStateChangeAsync event.
/// </summary>
public record DebateState
{
    public Rapper? Rapper1 { get; init; } // Argues Topic1 is better
    public Rapper? Rapper2 { get; init; } // Argues Topic2 is better
    public Topic? Topic1 { get; init; }   // First selected topic
    public Topic? Topic2 { get; init; }   // Second selected topic
    public bool IsDebateInProgress { get; init; } = false;
    public bool IsDebateFinished { get; init; } = false;
    public bool IsGeneratingTurn { get; init; } = false; // Indicates AI/TTS processing
    public int CurrentTurnNumber { get; init; } = 0; // Overall turn (1-3)
    public int TotalTurns { get; init; } = 3; // Reduced total turns
    public bool IsRapper1Turn { get; init; } = true;
    public string CurrentTurnText { get; init; } = string.Empty;
    public byte[]? CurrentTurnAudio { get; init; } // Audio data for the current turn
    public List<string> DebateHistory { get; init; } = new(); // History of text turns
    public string? ErrorMessage { get; init; } // To communicate errors to the UI
}
