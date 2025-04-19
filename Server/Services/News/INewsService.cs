using PoDebateRap.Shared.Models;

namespace PoDebateRap.Server.Services.News
{
    /// <summary>
    /// Defines the contract for a service that fetches news headlines.
    /// </summary>
    public interface INewsService
    {
        /// <summary>
        /// Asynchronously retrieves a list of top news headlines.
        /// </summary>
        /// <param name="count">The maximum number of headlines to retrieve.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of <see cref="NewsHeadline"/> objects.</returns>
        /// <exception cref="HttpRequestException">Thrown if the HTTP request to the news API fails.</exception>
        /// <exception cref="System.Text.Json.JsonException">Thrown if the response from the news API cannot be deserialized.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the NewsAPI key is missing or invalid.</exception>
        Task<List<NewsHeadline>> GetTopHeadlinesAsync(int count = 10, CancellationToken cancellationToken = default);
    }
}
