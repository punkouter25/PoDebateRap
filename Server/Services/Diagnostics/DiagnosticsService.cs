using Azure.Data.Tables;
using PoDebateRap.Server.Services.Data;
using System.Net.NetworkInformation;

namespace PoDebateRap.Server.Services.Diagnostics
{
    /// <summary>
    /// Implements the IDiagnosticsService to perform system health checks.
    /// </summary>
    public class DiagnosticsService : IDiagnosticsService
    {
        private readonly ITableStorageService _tableStorageService;
        private readonly ILogger<DiagnosticsService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        // Inject other dependencies as needed for more checks (e.g., IConfiguration, auth services)

        // Using HttpClientFactory is preferred over injecting HttpClient directly
        public DiagnosticsService(
            ITableStorageService tableStorageService,
            ILogger<DiagnosticsService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _tableStorageService = tableStorageService;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _logger.LogInformation("DiagnosticsService initialized.");
        }

        /// <inheritdoc />
        public async Task<List<DiagnosticResult>> RunChecksAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting diagnostic checks...");
            var results = new List<DiagnosticResult>();

            // Reordered checks: Internet first, then critical data connection
            results.Add(await CheckInternetConnectionAsync(cancellationToken));
            results.Add(await CheckTableStorageConnectionAsync(cancellationToken));
            results.Add(await CheckApiHealthAsync(cancellationToken)); // Placeholder - Less critical
            results.Add(await CheckAuthenticationStatusAsync(cancellationToken)); // Placeholder - Less critical

            // Add more checks here as needed

            _logger.LogInformation("Diagnostic checks completed.");
            // Log detailed results
            foreach (var result in results)
            {
                if (result.Success)
                {
                    _logger.LogInformation("Diagnostic Check '{CheckName}': Success. {Message}", result.CheckName, result.Message);
                }
                else
                {
                    _logger.LogError("Diagnostic Check '{CheckName}': Failed. {Message}", result.CheckName, result.Message);
                }
                // TODO: Add logging to log.txt if required via a dedicated file logger service
            }

            return results;
        }

        /// <summary>
        /// Checks the connection to Azure Table Storage.
        /// </summary>
        private async Task<DiagnosticResult> CheckTableStorageConnectionAsync(CancellationToken cancellationToken)
        {
            var result = new DiagnosticResult { CheckName = "Azure Table Storage Connection" };
            try
            {
                // Attempt to get a client for a known (or dummy) table.
                // This implicitly uses the connection string configured.
                // Using a potentially non-existent table name is okay for a connection check.
                // Removed cancellationToken as the interface method likely doesn't take it.
                await _tableStorageService.GetTableClientAsync("diagnosticschecktable");
                result.Success = true;
                result.Message = "Successfully connected to Azure Table Storage.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Failed to connect: {ex.Message}";
                _logger.LogError(ex, "Azure Table Storage connection check failed.");
            }
            return result;
        }

        /// <summary>
        /// Checks general internet connectivity by pinging a reliable external host.
        /// </summary>
        private async Task<DiagnosticResult> CheckInternetConnectionAsync(CancellationToken cancellationToken)
        {
            var result = new DiagnosticResult { CheckName = "Internet Connectivity" };
            try
            {
                // Using HttpClientFactory to create a client
                var client = _httpClientFactory.CreateClient("DiagnosticsClient");
                // Send a HEAD request as it's lightweight. Use a reliable target.
                // Ensure timeout is reasonable.
                var request = new HttpRequestMessage(HttpMethod.Head, "https://www.google.com"); // Or another reliable endpoint
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5)); // 5 second timeout

                var response = await client.SendAsync(request, cts.Token);

                // Check if the request was successful (doesn't have to be 200 OK for HEAD, just needs to resolve and respond)
                // Note: Some networks might block HEAD requests or specific targets. Consider alternatives if this fails reliably.
                // response.EnsureSuccessStatusCode(); // This might be too strict for just a connectivity check

                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed) // HEAD might return 405
                {
                     result.Success = true;
                     result.Message = "Internet connection appears to be available.";
                }
                else
                {
                    result.Success = false;
                    result.Message = $"Could not reach external test site. Status: {response.StatusCode}";
                }

            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                 // Operation was cancelled by the caller, not a failure
                 result.Success = false;
                 result.Message = "Check cancelled.";
                 _logger.LogWarning("Internet connectivity check cancelled by caller.");
            }
            catch (OperationCanceledException ex) // Catches timeout
            {
                 result.Success = false;
                 result.Message = "Connection attempt timed out.";
                 _logger.LogWarning(ex, "Internet connectivity check timed out.");
            }
            catch (HttpRequestException ex)
            {
                result.Success = false;
                result.Message = $"Network error: {ex.Message}";
                _logger.LogError(ex, "Internet connectivity check failed with network error.");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Unexpected error: {ex.Message}";
                _logger.LogError(ex, "Internet connectivity check failed unexpectedly.");
            }
            return result;
        }


        /// <summary>
        /// Placeholder for checking internal API health.
        /// </summary>
        private Task<DiagnosticResult> CheckApiHealthAsync(CancellationToken cancellationToken)
        {
            // TODO: Implement actual API health check if needed (e.g., call a specific health endpoint)
            var result = new DiagnosticResult
            {
                CheckName = "API Health",
                Success = true, // Assume success for now
                Message = "API health check not implemented (placeholder)."
            };
            return Task.FromResult(result);
        }

        /// <summary>
        /// Placeholder for checking authentication service status.
        /// </summary>
        private Task<DiagnosticResult> CheckAuthenticationStatusAsync(CancellationToken cancellationToken)
        {
            // TODO: Implement check if authentication is added
            var result = new DiagnosticResult
            {
                CheckName = "Authentication Service",
                Success = true, // Assume success as no auth is implemented
                Message = "Authentication not applicable or check not implemented."
            };
            return Task.FromResult(result);
        }
    }
}
