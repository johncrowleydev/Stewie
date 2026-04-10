namespace Stewie.Application.Interfaces;

/// <summary>
/// Abstract artifact storage. Used to push/pull operational parameters and task results
/// independently of the physical block storage used for repository cloning.
/// </summary>
public interface IArtifactWorkspaceStore
{
    // Text / JSON operations
    
    /// <summary>Reads text artifacts (e.g. task.json, result.json).</summary>
    Task<string> ReadTextArtifactAsync(string taskId, string filename);
    
    /// <summary>Writes text artifacts (e.g. task.json, result.json).</summary>
    Task WriteTextArtifactAsync(string taskId, string filename, string content);

    // Generic Binary operations
    
    /// <summary>Reads binary artifacts (e.g. screenshots).</summary>
    Task<Stream> ReadBinaryArtifactAsync(string taskId, string filename);
    
    /// <summary>Writes binary artifacts (e.g. screenshots).</summary>
    Task WriteBinaryArtifactAsync(string taskId, string filename, Stream content);
}
