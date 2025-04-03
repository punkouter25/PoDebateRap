namespace PoDebateRap.Shared.Models
{
    /// <summary>
    /// Holds numerical statistics for a completed debate, as determined by the AI judge.
    /// Scores are typically on a scale of 1-5.
    /// </summary>
    public class DebateStats
    {
        // Rapper 1 (Pro) Scores
        public int Rapper1LogicScore { get; set; }
        public int Rapper1SentimentScore { get; set; } // Renamed from Aggression
        public int Rapper1AdherenceScore { get; set; } // Renamed from Persona
        public int Rapper1RebuttalScore { get; set; }

        // Rapper 2 (Con) Scores
        public int Rapper2LogicScore { get; set; }
        public int Rapper2SentimentScore { get; set; } // Renamed from Aggression
        public int Rapper2AdherenceScore { get; set; } // Renamed from Persona
        public int Rapper2RebuttalScore { get; set; }

        // Calculated Totals
        public int Rapper1TotalScore { get; set; }
        public int Rapper2TotalScore { get; set; }

        /// <summary>
        /// Creates an empty DebateStats object, typically used when stats generation fails.
        /// </summary>
        public static DebateStats Empty => new DebateStats();
    }
}
