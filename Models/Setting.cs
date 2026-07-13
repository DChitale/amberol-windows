using System.Text.Json.Serialization;

namespace AmberolWpf.Models
{
    public class Setting
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("updated_at")]
        public long UpdatedAt { get; set; }
    }
}
