namespace PowerPlatformFlowActivator.Models;

/// <summary>
/// Represents the result of attempting to activate a cloud flow.
/// </summary>
public class ActivationResult
{
    /// <summary>
    /// The cloud flow that was attempted to be activated.
    /// </summary>
    public required CloudFlow Flow { get; set; }

    /// <summary>
    /// Indicates whether the activation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The error message if the activation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The detailed error information if available.
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// The timestamp when the activation was attempted.
    /// </summary>
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The duration of the activation attempt.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets a summary description of the result.
    /// </summary>
    public string ResultSummary => Success ? "Activated successfully" : $"Failed: {ErrorMessage}";
}
