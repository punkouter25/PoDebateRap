using System.Net.Http;
using System.Net.Http.Json; // Required for ReadFromJsonAsync
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PoDebateRap.Shared.Models;

namespace PoDebateRap.Server.Services.News
{
    /// <summary>
    /// Service implementation for fetching news headlines from NewsAPI.org.
    /// </summary>
    public class NewsService : INewsService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NewsService> _logger;
        private readonly string? _apiKey;
        private const string NewsApiBaseUrl = "https://newsapi.org/v2/";

        /// <summary>
        /// Initializes a new instance of the <see cref="NewsService"/> class.
        /// </summary>
        /// <param name="httpClient">The HttpClient instance for making requests.</param>
        /// <param name="configuration">The application configuration to access settings like API keys.</param>
        /// <param name="logger">The logger instance for logging information and errors.</param>
        public NewsService(HttpClient httpClient, IConfiguration configuration, ILogger<NewsService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Retrieve API key from configuration
            _apiKey = _configuration["NewsApi:ApiKey"]; // Using a specific section "NewsApi"

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogError("NewsAPI API Key is not configured in appsettings.json under 'NewsApi:ApiKey'.");
                // Throwing here might stop the app startup, consider logging and returning empty lists instead.
                // For now, we'll let it proceed but log the error. The GetTopHeadlinesAsync will handle the missing key.
            }

            // Configure HttpClient base address and default headers if needed,
            // but for this service, the full URL is constructed in the method.
            // Setting User-Agent is good practice for external APIs.
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PoDebateRap/1.0");
        }

        /// <inheritdoc />
        public async Task<List<NewsHeadline>> GetTopHeadlinesAsync(int count = 10, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogError("Cannot fetch headlines: NewsAPI API Key is missing or invalid.");
                throw new InvalidOperationException("NewsAPI API Key is not configured.");
            }

            // Construct the request URL for top headlines in the US
            // Reference: https://newsapi.org/docs/endpoints/top-headlines
            var requestUrl = $"{NewsApiBaseUrl}top-headlines?country=us&pageSize={count}&apiKey={_apiKey}";
            _logger.LogInformation("Fetching top {Count} headlines from NewsAPI.", count);

            try
            {
                // Using explicit JsonDeserializeOptions for case-insensitivity
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // Make the GET request and deserialize the response
                var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
                response.EnsureSuccessStatusCode(); // Throws HttpRequestException for non-2xx status codes

                var newsApiResponse = await response.Content.ReadFromJsonAsync<NewsApiResponse>(options, cancellationToken);

                if (newsApiResponse?.Articles == null || newsApiResponse.Status?.ToLowerInvariant() != "ok")
                {
                    _logger.LogWarning("NewsAPI request succeeded but returned status '{Status}' or no articles.", newsApiResponse?.Status ?? "unknown");
                    return new List<NewsHeadline>(); // Return empty list if status is not 'ok' or articles are null
                }

                // Map the API response articles to our NewsHeadline DTO
                var headlines = newsApiResponse.Articles
                    .Where(article => !string.IsNullOrWhiteSpace(article.Title)) // Ensure articles have titles
                    .Select(article => new NewsHeadline
                    {
                        Title = article.Title,
                        Description = article.Description,
                        Url = article.Url,
                        SourceName = article.Source?.Name // Map source name
                    })
                    .ToList();

                _logger.LogInformation("Successfully fetched {HeadlineCount} headlines from NewsAPI.", headlines.Count);
                return headlines;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "HTTP error occurred while fetching headlines from NewsAPI. Status Code: {StatusCode}", httpEx.StatusCode);
                // Consider more specific error handling based on status code if needed
                throw; // Re-throw the exception to be handled by the caller
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Error deserializing NewsAPI response.");
                throw; // Re-throw the exception
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while fetching headlines.");
                throw; // Re-throw any other unexpected exceptions
            }
        }
    }
}
