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
    /// Starts a new debate session on a single topic.
    /// </summary>
    /// <param name="rapper1">The first rapper (argues FOR the topic).</param>
    /// <param name="rapper2">The second rapper (argues AGAINST the topic).</param>
    /// <param name="topic">The topic of the debate.</param>
    /// <returns>A Task representing the asynchronous operation of starting the debate flow.</returns>
    Task StartNewDebateAsync(Rapper rapper1, Rapper rapper2, Topic topic);

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
    /// <summary> Rapper arguing FOR the topic. </summary>
    public Rapper? Rapper1 { get; init; }
    /// <summary> Rapper arguing AGAINST the topic. </summary>
    public Rapper? Rapper2 { get; init; }
    /// <summary> The single topic of the debate. </summary>
    public Topic? Topic { get; init; }
    public bool IsDebateInProgress { get; init; } = false;
    public bool IsDebateFinished { get; init; } = false;
    public bool IsGeneratingTurn { get; init; } = false; // Indicates AI/TTS processing
    public int CurrentTurnNumber { get; init; } = 0; // Overall turn number (1-6)
    public int TotalTurns { get; init; } = 6; // 3 rounds * 2 rappers = 6 turns
    public bool IsRapper1Turn { get; init; } = true;
    public string CurrentTurnText { get; init; } = string.Empty;
    public byte[]? CurrentTurnAudio { get; init; } // Audio data for the current turn
    public List<string> DebateHistory { get; init; } = new(); // History of text turns
    public string? ErrorMessage { get; init; } // To communicate errors to the UI
    public string? WinnerName { get; init; } // Name of the AI-declared winner
    public string? JudgeReasoning { get; init; } // AI Judge's reasoning text
    public DebateStats? Stats { get; init; } // Numerical stats from AI Judge
}
