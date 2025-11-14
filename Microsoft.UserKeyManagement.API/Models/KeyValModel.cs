using System.Text.Json.Serialization;

namespace Microsoft.UserKeyManagement.API.Models
{
    public class KeyValModel
    {
        [JsonPropertyName("userName")]
        public string UserName { get; set; }

        [JsonPropertyName("apiKey")]
        public string APIKey { get; set; }
    }
}