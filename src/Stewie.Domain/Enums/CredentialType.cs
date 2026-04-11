namespace Stewie.Domain.Enums;

/// <summary>
/// Classifies credential types stored in the UserCredential entity.
/// REF: JOB-021 T-183
/// </summary>
public enum CredentialType
{
    /// <summary>GitHub Personal Access Token (existing).</summary>
    GitHubPat = 0,

    /// <summary>Anthropic API key for Claude models.</summary>
    AnthropicApiKey = 1,

    /// <summary>OpenAI API key for GPT models.</summary>
    OpenAiApiKey = 2,

    /// <summary>Google AI API key for Gemini models.</summary>
    GoogleAiApiKey = 3
}
