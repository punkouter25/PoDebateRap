using PoDebateRap.Shared.Models;

namespace PoDebateRap.Server.Services.Speech;

/// <summary>
/// Defines the contract for interacting with the Azure Text-to-Speech service.
/// Handles converting debate text into audio data.
/// </summary>
public interface ITextToSpeechService
{
    /// <summary>
    /// Synthesizes speech from the given text using a voice appropriate for the specified rapper.
    /// </summary>
    /// <param name="text">The text content to synthesize.</param>
    /// <param name="rapper">The rapper whose voice characteristics should be approximated.</param>
    /// <returns>
    /// A Task representing the asynchronous operation, returning a byte array containing the audio data (e.g., WAV or MP3).
    /// Returns null if synthesis fails or text is empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if text or rapper is null.</exception>
    /// <exception cref="Exception">Thrown if the Azure Speech API call fails.</exception>
    Task<byte[]?> SynthesizeSpeechAsync(string text, Rapper rapper);

    /// <summary>
    /// Gets a list of available voice names (optional, for configuration or testing).
    /// </summary>
    /// <returns>A Task representing the asynchronous operation, returning a list of voice names.</returns>
    Task<List<string>> GetAvailableVoicesAsync();
}
