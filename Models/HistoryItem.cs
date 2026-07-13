using System.Text.Json.Serialization;

namespace AmberolWpf.Models
{
    public class HistoryItem
    {
        [JsonPropertyName("track_id")]
        public int TrackId { get; set; }

        [JsonPropertyName("played_at")]
        public long PlayedAt { get; set; }

        [JsonPropertyName("position")]
        public double Position { get; set; }
    }
}
