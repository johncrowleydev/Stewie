using System.Text.Json;
using System.Text.Json.Serialization;

const string inputPath = "/workspace/input/task.json";
const string outputDir = "/workspace/output";
const string resultPath = "/workspace/output/result.json";
const string logPath = "/workspace/output/log.txt";

Console.WriteLine("Stewie Dummy Worker starting...");

// Read task.json
if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"ERROR: task.json not found at {inputPath}");
    Environment.Exit(1);
}

var taskJson = File.ReadAllText(inputPath);
Console.WriteLine($"Read task.json ({taskJson.Length} bytes)");

var task = JsonSerializer.Deserialize<TaskPacket>(taskJson);
if (task is null)
{
    Console.Error.WriteLine("ERROR: Failed to deserialize task.json");
    Environment.Exit(1);
}

Console.WriteLine($"Task ID: {task.TaskId}");
Console.WriteLine($"Run ID: {task.RunId}");
Console.WriteLine($"Role: {task.Role}");
Console.WriteLine($"Objective: {task.Objective}");

// Ensure output directory exists
Directory.CreateDirectory(outputDir);

// Write result.json
var result = new ResultPacket
{
    TaskId = task.TaskId,
    Status = "success",
    Summary = "Dummy worker executed successfully. Runtime contract verified.",
    FilesChanged = [],
    TestsPassed = false,
    Errors = [],
    Notes = "No real implementation work performed. This worker exists to prove the Stewie runtime contract.",
    NextAction = "review"
};

var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(resultPath, resultJson);
Console.WriteLine($"Wrote result.json ({resultJson.Length} bytes)");

// Write log
var logContent = $"""
    Stewie Dummy Worker Log
    =======================
    Timestamp: {DateTime.UtcNow:O}
    Task ID: {task.TaskId}
    Run ID: {task.RunId}
    Role: {task.Role}
    Status: SUCCESS
    
    Worker read task.json and produced result.json.
    Runtime contract verified.
    """;

File.WriteAllText(logPath, logContent);
Console.WriteLine("Wrote log.txt");
Console.WriteLine("Dummy worker completed successfully.");

// --- Local DTOs (isolated from Stewie.Domain) ---

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
