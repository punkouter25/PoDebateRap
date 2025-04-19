using PoDebateRap.Server.Services.Diagnostics; // Add this using directive

namespace PoDebateRap.Server.Services.Diagnostics
{
    /// <summary>
    /// Defines the contract for a service that performs system diagnostics checks.
    /// </summary>
    public interface IDiagnosticsService
    {
        /// <summary>
        /// Runs all diagnostic checks asynchronously.
        /// </summary>
        /// <returns>A list of diagnostic results.</returns>
        Task<List<DiagnosticResult>> RunChecksAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents the result of a single diagnostic check.
    /// </summary>
    public class DiagnosticResult
    {
        /// <summary>
        /// Gets or sets the name of the check performed.
        /// </summary>
        public required string CheckName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the check was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets an optional message providing more details about the check result.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
