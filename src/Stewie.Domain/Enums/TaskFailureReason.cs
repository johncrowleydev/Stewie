/// <summary>
/// Classifies the reason a worker task failed.
/// Used to determine retry eligibility and provide structured failure reporting.
/// REF: GOV-004 (error taxonomy), SPR-005 T-052
/// </summary>
namespace Stewie.Domain.Enums;

/// <summary>
/// Categorizes task failures into transient (retryable) and permanent (non-retryable) reasons.
/// </summary>
public enum TaskFailureReason
{
    /// <summary>Worker process exited with non-zero code (not timeout). Permanent — do not retry.</summary>
    WorkerCrash,

    /// <summary>Container exceeded the configured timeout (exit code 124). Transient — retry once.</summary>
    Timeout,

    /// <summary>Docker daemon error (image not found, socket error). Transient — retry once.</summary>
    ContainerError,

    /// <summary>Container exited 0 but no result.json was produced. Permanent — do not retry.</summary>
    ResultMissing,

    /// <summary>result.json exists but failed deserialization. Permanent — do not retry.</summary>
    ResultInvalid,

    /// <summary>result.json deserialized successfully but status != "success". Permanent — do not retry.</summary>
    WorkerReportedFailure
}
