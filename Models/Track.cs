using System.Text.Json.Serialization;

namespace AmberolWpf.Models
{
    public class Track
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("file_name")]
        public string FileName { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("artist")]
        public string Artist { get; set; }

        [JsonPropertyName("album")]
        public string Album { get; set; }

        [JsonPropertyName("duration")]
        public double Duration { get; set; }

        [JsonPropertyName("cover_art")]
        public string CoverArt { get; set; } // Base64 data URL

        [JsonPropertyName("size_bytes")]
        public long SizeBytes { get; set; }

        [JsonPropertyName("modified_at")]
        public long ModifiedAt { get; set; }

        [JsonPropertyName("added_at")]
        public long AddedAt { get; set; }

        [JsonPropertyName("last_played_at")]
        public long? LastPlayedAt { get; set; }

        [JsonIgnore]
        public string DurationString => $"{(int)(Duration / 60)}:{(int)(Duration % 60):D2}";
    }
}
