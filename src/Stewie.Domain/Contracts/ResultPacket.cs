using System.Text.Json.Serialization;

namespace Stewie.Domain.Contracts;

public class ResultPacket
{
    [JsonPropertyName("taskId")]
    public Guid TaskId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("filesChanged")]
    public List<string> FilesChanged { get; set; } = [];

    [JsonPropertyName("testsPassed")]
    public bool TestsPassed { get; set; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = [];

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonPropertyName("nextAction")]
    public string NextAction { get; set; } = string.Empty;
}
