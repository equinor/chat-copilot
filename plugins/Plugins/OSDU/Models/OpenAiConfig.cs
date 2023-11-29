using System.Text.Json.Serialization;

namespace OSDU.Models;

public class OpenAiConfig
{
    [JsonPropertyName("EndPoint")] public string EndPoint { get; set; } = string.Empty;
    [JsonPropertyName("EmbeddingModel")] public string EmbeddingModel { get; set; } = string.Empty;
}