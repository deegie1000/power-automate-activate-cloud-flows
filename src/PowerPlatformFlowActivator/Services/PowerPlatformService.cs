using System.Diagnostics;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using PowerPlatformFlowActivator.Models;
using Spectre.Console;

namespace PowerPlatformFlowActivator.Services;

/// <summary>
/// Service for interacting with Power Platform Dataverse to manage cloud flows.
/// Handles querying solutions, retrieving flows, and activating/deactivating workflows.
/// </summary>
public class PowerPlatformService : IDisposable
{
    private readonly ServiceClient _serviceClient;
    private readonly string _environmentUrl;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the PowerPlatformService using interactive authentication.
    /// </summary>
    /// <param name="environmentUrl">The Power Platform environment URL.</param>
    public PowerPlatformService(string environmentUrl)
    {
        _environmentUrl = environmentUrl.TrimEnd('/');

        // Detect cloud type based on URL and build appropriate connection string
        var cloudType = DetectCloudType(_environmentUrl);

        AnsiConsole.MarkupLine($"[dim]Detected cloud type: {cloudType}[/]");

        var connectionString = BuildConnectionString(_environmentUrl, cloudType);

        try
        {
            _serviceClient = new ServiceClient(connectionString);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create ServiceClient: {ex.Message}", ex);
        }

        if (!_serviceClient.IsReady)
        {
            var errorDetails = _serviceClient.LastError ?? "No error details available";
            throw new InvalidOperationException(
                $"ServiceClient not ready. Error: {errorDetails}");
        }
    }

    /// <summary>
    /// Detects the cloud type (Commercial, GCC, GCC High, DOD) based on the environment URL.
    /// </summary>
    private static string DetectCloudType(string url)
    {
        var lowerUrl = url.ToLowerInvariant();

        // GCC (Government Community Cloud) - uses crm9.dynamics.com
        if (lowerUrl.Contains(".crm9.dynamics.com"))
            return "GCC";

        // GCC High - uses crm.dynamics.us
        if (lowerUrl.Contains(".crm.dynamics.us"))
            return "GCCHigh";

        // DOD - uses crm.microsoftdynamics.us
        if (lowerUrl.Contains(".crm.microsoftdynamics.us"))
            return "DOD";

        // Commercial cloud (default)
        return "Commercial";
    }

    /// <summary>
    /// Builds the connection string with appropriate settings for the cloud type.
    /// </summary>
    private static string BuildConnectionString(string url, string cloudType)
    {
        // Simplified connection string - let ServiceClient auto-detect cloud settings from URL
        // For GCC (crm9), the SDK should automatically detect the correct endpoints
        var connectionString = $"AuthType=OAuth;" +
                              $"Url={url};" +
                              $"AppId=51f81489-12ee-4a9e-aaae-a2591f45987d;" +
                              $"RedirectUri=app://58145B91-0C36-4500-8554-080854F2AC97;" +
                              $"LoginPrompt=Auto;" +
                              $"RequireNewInstance=True";

        // For GCC High and DOD, we need to explicitly set the authority
        if (cloudType == "GCCHigh" || cloudType == "DOD")
        {
            connectionString += ";Authority=https://login.microsoftonline.us";
        }

        AnsiConsole.MarkupLine($"[dim]Connection URL: {url}[/]");

        return connectionString;
    }

    /// <summary>
    /// Gets the authenticated user's name if available.
    /// </summary>
    public string? AuthenticatedUser => _serviceClient.OAuthUserId;

    /// <summary>
    /// Retrieves the solution ID for a given solution unique name.
    /// </summary>
    /// <param name="solutionUniqueName">The unique name of the solution.</param>
    /// <returns>The solution ID if found, null otherwise.</returns>
    public async Task<Guid?> GetSolutionIdAsync(string solutionUniqueName)
    {
        var query = new QueryExpression("solution")
        {
            ColumnSet = new ColumnSet("solutionid", "friendlyname", "uniquename"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("uniquename", ConditionOperator.Equal, solutionUniqueName)
                }
            }
        };

        var results = await Task.Run(() => _serviceClient.RetrieveMultiple(query));

        if (results.Entities.Count == 0)
        {
            return null;
        }

        return results.Entities[0].Id;
    }

    /// <summary>
    /// Retrieves all cloud flows in a solution that are in draft status.
    /// </summary>
    /// <param name="solutionUniqueName">The unique name of the solution.</param>
    /// <returns>A list of cloud flows in draft status.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the solution is not found.</exception>
    public async Task<List<CloudFlow>> GetDraftCloudFlowsInSolutionAsync(string solutionUniqueName)
    {
        // First, verify the solution exists
        var solutionId = await GetSolutionIdAsync(solutionUniqueName);
        if (!solutionId.HasValue)
        {
            throw new InvalidOperationException(
                $"Solution '{solutionUniqueName}' was not found in the environment. " +
                "Please verify the solution name is correct.");
        }

        AnsiConsole.MarkupLine($"[dim]Found solution with ID: {solutionId.Value}[/]");

        // Query solution components to find workflows in the solution
        // Component type 29 = Workflow (Cloud Flow)
        var solutionComponentQuery = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("objectid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId.Value),
                    new ConditionExpression("componenttype", ConditionOperator.Equal, 29) // Workflow
                }
            }
        };

        var components = await Task.Run(() => _serviceClient.RetrieveMultiple(solutionComponentQuery));

        if (components.Entities.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No workflows found in the solution.[/]");
            return new List<CloudFlow>();
        }

        AnsiConsole.MarkupLine($"[dim]Found {components.Entities.Count} workflow components in solution.[/]");

        // Get the workflow IDs from the solution components
        var workflowIds = components.Entities
            .Select(e => e.GetAttributeValue<Guid>("objectid"))
            .Where(id => id != Guid.Empty)
            .ToList();

        // Query workflows that are cloud flows (category = 5 for Modern Flow) and in draft state
        var workflowQuery = new QueryExpression("workflow")
        {
            ColumnSet = new ColumnSet(
                "workflowid",
                "name",
                "description",
                "statecode",
                "statuscode",
                "createdon",
                "modifiedon",
                "uniquename",
                "category"
            ),
            Criteria = new FilterExpression
            {
                FilterOperator = LogicalOperator.And,
                Conditions =
                {
                    // Category 5 = Modern Flow (Cloud Flow)
                    new ConditionExpression("category", ConditionOperator.Equal, 5),
                    // StateCode 0 = Draft
                    new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                }
            }
        };

        // Add condition to filter by workflow IDs in the solution
        if (workflowIds.Count > 0)
        {
            workflowQuery.Criteria.Conditions.Add(
                new ConditionExpression("workflowid", ConditionOperator.In, workflowIds.ToArray()));
        }

        // Add link to get created by and modified by names
        var createdByLink = workflowQuery.AddLink("systemuser", "createdby", "systemuserid", JoinOperator.LeftOuter);
        createdByLink.Columns = new ColumnSet("fullname");
        createdByLink.EntityAlias = "createdbyuser";

        var modifiedByLink = workflowQuery.AddLink("systemuser", "modifiedby", "systemuserid", JoinOperator.LeftOuter);
        modifiedByLink.Columns = new ColumnSet("fullname");
        modifiedByLink.EntityAlias = "modifiedbyuser";

        var workflows = await Task.Run(() => _serviceClient.RetrieveMultiple(workflowQuery));

        var cloudFlows = new List<CloudFlow>();

        foreach (var entity in workflows.Entities)
        {
            var flow = new CloudFlow
            {
                WorkflowId = entity.Id,
                Name = entity.GetAttributeValue<string>("name") ?? "Unnamed Flow",
                Description = entity.GetAttributeValue<string>("description"),
                StateCode = entity.GetAttributeValue<OptionSetValue>("statecode")?.Value ?? 0,
                StatusCode = entity.GetAttributeValue<OptionSetValue>("statuscode")?.Value ?? 0,
                CreatedOn = entity.GetAttributeValue<DateTime?>("createdon"),
                ModifiedOn = entity.GetAttributeValue<DateTime?>("modifiedon"),
                UniqueName = entity.GetAttributeValue<string>("uniquename")
            };

            // Get aliased values for created by and modified by
            if (entity.Contains("createdbyuser.fullname"))
            {
                flow.CreatedByName = ((AliasedValue)entity["createdbyuser.fullname"]).Value?.ToString();
            }

            if (entity.Contains("modifiedbyuser.fullname"))
            {
                flow.ModifiedByName = ((AliasedValue)entity["modifiedbyuser.fullname"]).Value?.ToString();
            }

            cloudFlows.Add(flow);
        }

        return cloudFlows.OrderBy(f => f.Name).ToList();
    }

    /// <summary>
    /// Activates a cloud flow by changing its state to Activated.
    /// </summary>
    /// <param name="flow">The cloud flow to activate.</param>
    /// <returns>The result of the activation attempt.</returns>
    public async Task<ActivationResult> ActivateFlowAsync(CloudFlow flow)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ActivationResult
        {
            Flow = flow,
            AttemptedAt = DateTime.UtcNow
        };

        try
        {
            // To activate a workflow, we need to use SetStateRequest
            // StateCode = 1 (Activated), StatusCode = 2 (Activated)
            var setStateRequest = new Microsoft.Crm.Sdk.Messages.SetStateRequest
            {
                EntityMoniker = new EntityReference("workflow", flow.WorkflowId),
                State = new OptionSetValue(1),  // Activated
                Status = new OptionSetValue(2)  // Activated
            };

            await Task.Run(() => _serviceClient.Execute(setStateRequest));

            result.Success = true;
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Success = false;
            result.ErrorMessage = ex.Message;

            // Extract more detailed error information if available
            if (ex.InnerException != null)
            {
                result.ErrorDetails = ex.InnerException.Message;
            }

            // Check for common error scenarios and provide helpful messages
            if (ex.Message.Contains("privilege"))
            {
                result.ErrorMessage = "Insufficient permissions to activate this flow. " +
                                     "Ensure you have the required security roles.";
            }
            else if (ex.Message.Contains("connection") || ex.Message.Contains("connector"))
            {
                result.ErrorMessage = "Flow activation failed due to connection issues. " +
                                     "Please verify all connections in the flow are configured correctly.";
            }
            else if (ex.Message.Contains("license"))
            {
                result.ErrorMessage = "Flow activation failed due to licensing. " +
                                     "Please verify you have the required Power Automate license.";
            }

            return result;
        }
    }

    /// <summary>
    /// Activates multiple flows and reports progress.
    /// </summary>
    /// <param name="flows">The flows to activate.</param>
    /// <returns>A list of activation results.</returns>
    public async Task<List<ActivationResult>> ActivateFlowsAsync(IEnumerable<CloudFlow> flows)
    {
        var results = new List<ActivationResult>();
        var flowList = flows.ToList();

        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            })
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Activating flows...[/]", maxValue: flowList.Count);

                foreach (var flow in flowList)
                {
                    task.Description = $"[green]Activating:[/] {flow.Name.EscapeMarkup()}";

                    var result = await ActivateFlowAsync(flow);
                    results.Add(result);

                    if (result.Success)
                    {
                        AnsiConsole.MarkupLine($"  [green]\u2713[/] {flow.Name.EscapeMarkup()}");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"  [red]\u2717[/] {flow.Name.EscapeMarkup()}");
                        AnsiConsole.MarkupLine($"    [red]{result.ErrorMessage?.EscapeMarkup()}[/]");
                    }

                    task.Increment(1);
                }

                task.Description = "[green]Activation complete![/]";
            });

        return results;
    }

    /// <summary>
    /// Disposes the service client connection.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _serviceClient?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
