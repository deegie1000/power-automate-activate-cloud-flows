namespace PowerPlatformFlowActivator.Models;

/// <summary>
/// Represents a complete activation session with all results and metadata.
/// </summary>
public class ActivationSession
{
    /// <summary>
    /// The Power Platform environment URL used for this session.
    /// </summary>
    public required string EnvironmentUrl { get; set; }

    /// <summary>
    /// The solution name that was queried.
    /// </summary>
    public required string SolutionName { get; set; }

    /// <summary>
    /// The timestamp when the session started.
    /// </summary>
    public DateTime SessionStarted { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The timestamp when the session ended.
    /// </summary>
    public DateTime? SessionEnded { get; set; }

    /// <summary>
    /// The username of the authenticated user.
    /// </summary>
    public string? AuthenticatedUser { get; set; }

    /// <summary>
    /// The total number of draft flows found in the solution.
    /// </summary>
    public int TotalDraftFlowsFound { get; set; }

    /// <summary>
    /// The number of flows selected for activation.
    /// </summary>
    public int FlowsSelectedForActivation { get; set; }

    /// <summary>
    /// The list of activation results.
    /// </summary>
    public List<ActivationResult> Results { get; set; } = new();

    /// <summary>
    /// Gets the count of successfully activated flows.
    /// </summary>
    public int SuccessCount => Results.Count(r => r.Success);

    /// <summary>
    /// Gets the count of failed activation attempts.
    /// </summary>
    public int FailureCount => Results.Count(r => !r.Success);

    /// <summary>
    /// Gets the total duration of the session.
    /// </summary>
    public TimeSpan TotalDuration => (SessionEnded ?? DateTime.UtcNow) - SessionStarted;

    /// <summary>
    /// Gets a summary of the session results.
    /// </summary>
    public string Summary => $"Activated {SuccessCount}/{Results.Count} flows. " +
                             $"{FailureCount} failed.";
}
