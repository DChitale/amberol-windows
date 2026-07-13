using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AmberolWpf.Models
{
    public class Playlist
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public long UpdatedAt { get; set; }

        [JsonPropertyName("track_ids")]
        public List<int> TrackIds { get; set; } = new List<int>();

        [JsonIgnore]
        public bool CanDelete => Id != 9999 && Id != 2;
    }
}
