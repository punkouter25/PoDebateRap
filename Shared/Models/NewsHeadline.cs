namespace PoDebateRap.Shared.Models
{
    /// <summary>
    /// Represents a news headline fetched from an external API.
    /// </summary>
    public class NewsHeadline
    {
        /// <summary>
        /// Gets or sets the title of the news article.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets a short description or snippet of the news article.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the source URL of the news article.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// Gets or sets the name of the news source.
        /// </summary>
        public string? SourceName { get; set; } // Added source name
    }

    // Helper classes to deserialize the NewsAPI response structure

    /// <summary>
    /// Represents the overall response from the NewsAPI.
    /// </summary>
    public class NewsApiResponse // Changed internal to public
    {
        public string? Status { get; set; }
        public int TotalResults { get; set; }
        public List<NewsApiArticle>? Articles { get; set; }
    }

    /// <summary>
    /// Represents a single article within the NewsAPI response.
    /// </summary>
    public class NewsApiArticle // Changed internal to public
    {
        public NewsApiSource? Source { get; set; }
        public string? Author { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Url { get; set; }
        public string? UrlToImage { get; set; }
        public DateTime PublishedAt { get; set; }
        public string? Content { get; set; }
    }

    /// <summary>
    /// Represents the source of an article within the NewsAPI response.
    /// </summary>
    public class NewsApiSource // Changed internal to public
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }
}
