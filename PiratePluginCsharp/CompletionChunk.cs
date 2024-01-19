using System.Text.Json.Serialization;

namespace PiratePluginCsharp;

public class CompletionChunk
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; } = "chat.completion.chunk";

    [JsonPropertyName("created")]
    public long Created { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    [JsonPropertyName("choices")]
    public Choice[]? Choices { get; set; }

    public class Choice
    {
        [JsonPropertyName("delta")]
        public Delta? Delta { get; set; }

        [JsonPropertyName("index")]
        public int Index { get; set; } = 0;

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; } = null;
    }

    public class Delta
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
