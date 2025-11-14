using System.Text.Json.Serialization;

namespace Microsoft.UserKeyManagement.API.Models
{
    public class CacheValModel
    {

        [JsonPropertyName("cacheKey")]
        public string CacheKey { get; set; }
    }
}
