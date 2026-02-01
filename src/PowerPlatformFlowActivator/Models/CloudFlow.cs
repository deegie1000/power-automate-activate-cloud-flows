namespace PowerPlatformFlowActivator.Models;

/// <summary>
/// Represents a cloud flow (workflow) from Power Platform Dataverse.
/// </summary>
public class CloudFlow
{
    /// <summary>
    /// The unique identifier of the workflow in Dataverse.
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// The display name of the cloud flow.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The description of the cloud flow, if available.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The current state code of the workflow.
    /// 0 = Draft, 1 = Activated, 2 = Suspended
    /// </summary>
    public int StateCode { get; set; }

    /// <summary>
    /// The current status code of the workflow.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// The date and time when the workflow was created.
    /// </summary>
    public DateTime? CreatedOn { get; set; }

    /// <summary>
    /// The date and time when the workflow was last modified.
    /// </summary>
    public DateTime? ModifiedOn { get; set; }

    /// <summary>
    /// The name of the user who created the workflow.
    /// </summary>
    public string? CreatedByName { get; set; }

    /// <summary>
    /// The name of the user who last modified the workflow.
    /// </summary>
    public string? ModifiedByName { get; set; }

    /// <summary>
    /// The unique name of the workflow (used internally by Power Platform).
    /// </summary>
    public string? UniqueName { get; set; }

    /// <summary>
    /// Gets a human-readable state description.
    /// </summary>
    public string StateDescription => StateCode switch
    {
        0 => "Draft",
        1 => "Activated",
        2 => "Suspended",
        _ => "Unknown"
    };

    /// <summary>
    /// Indicates whether the flow is in draft status.
    /// </summary>
    public bool IsDraft => StateCode == 0;

    /// <summary>
    /// Indicates whether the flow is currently activated.
    /// </summary>
    public bool IsActivated => StateCode == 1;
}
