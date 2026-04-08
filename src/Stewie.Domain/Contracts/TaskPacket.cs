using System.Text.Json.Serialization;

namespace Stewie.Domain.Contracts;

public class TaskPacket
{
    [JsonPropertyName("taskId")]
    public Guid TaskId { get; set; }

    [JsonPropertyName("runId")]
    public Guid RunId { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("objective")]
    public string Objective { get; set; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    [JsonPropertyName("allowedPaths")]
    public List<string> AllowedPaths { get; set; } = [];

    [JsonPropertyName("forbiddenPaths")]
    public List<string> ForbiddenPaths { get; set; } = [];

    [JsonPropertyName("acceptanceCriteria")]
    public List<string> AcceptanceCriteria { get; set; } = [];
}
