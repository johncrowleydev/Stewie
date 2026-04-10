using Microsoft.Extensions.Logging;

namespace Stewie.Infrastructure.Services;

/// <summary>
/// Local disk implementation of IArtifactWorkspaceStore.
/// Maintains compatibility with MVP local disk execution where containers
/// mount local directories from workspaces/.
/// </summary>
public class LocalDiskArtifactStore : Stewie.Application.Interfaces.IArtifactWorkspaceStore
{
    private readonly string _workspaceRoot;
    private readonly ILogger<LocalDiskArtifactStore> _logger;

    public LocalDiskArtifactStore(string workspaceRoot, ILogger<LocalDiskArtifactStore> logger)
    {
        _workspaceRoot = Path.GetFullPath(workspaceRoot);
        _logger = logger;
    }

    private string GetFilePath(string taskId, string filename)
    {
        var taskDir = Path.Combine(_workspaceRoot, taskId);
        return Path.Combine(taskDir, filename);
    }

    private void EnsureDirectoryExists(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public async Task<string> ReadTextArtifactAsync(string taskId, string filename)
    {
        var path = GetFilePath(taskId, filename);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Artifact not found at {path}");
        }

        var content = await File.ReadAllTextAsync(path);
        _logger.LogInformation("Read text artifact {Filename} for task {TaskId}", filename, taskId);
        return content;
    }

    public async Task WriteTextArtifactAsync(string taskId, string filename, string content)
    {
        var path = GetFilePath(taskId, filename);
        EnsureDirectoryExists(path);

        await File.WriteAllTextAsync(path, content);
        _logger.LogInformation("Wrote text artifact {Filename} for task {TaskId}", filename, taskId);
    }

    public async Task<Stream> ReadBinaryArtifactAsync(string taskId, string filename)
    {
        var path = GetFilePath(taskId, filename);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Artifact not found at {path}");
        }

        _logger.LogInformation("Read binary artifact {Filename} for task {TaskId}", filename, taskId);
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public async Task WriteBinaryArtifactAsync(string taskId, string filename, Stream content)
    {
        var path = GetFilePath(taskId, filename);
        EnsureDirectoryExists(path);

        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream);
        _logger.LogInformation("Wrote binary artifact {Filename} for task {TaskId}", filename, taskId);
    }
}
