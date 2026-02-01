using System.CommandLine;
using PowerPlatformFlowActivator.Models;
using PowerPlatformFlowActivator.Services;
using Spectre.Console;

namespace PowerPlatformFlowActivator;

/// <summary>
/// Power Platform Flow Activator - A console application for activating cloud flows in Power Platform solutions.
///
/// This application:
/// 1. Authenticates to a Power Platform environment using interactive (browser-based) authentication
/// 2. Queries a specified solution for cloud flows that are in draft status
/// 3. Displays the draft flows and allows the user to select which ones to activate
/// 4. Attempts to activate the selected flows and reports results
/// 5. Optionally exports detailed results to an Excel workbook
/// </summary>
public class Program
{
    /// <summary>
    /// Application entry point. Parses command line arguments and runs the activation workflow.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code (0 for success, non-zero for errors).</returns>
    public static async Task<int> Main(string[] args)
    {
        // Display application banner
        DisplayBanner();

        // Define command line options
        var environmentUrlOption = new Option<string>(
            aliases: new[] { "--environment", "-e" },
            description: "The Power Platform environment URL (e.g., https://org.crm.dynamics.com)")
        {
            IsRequired = true
        };

        var solutionNameOption = new Option<string>(
            aliases: new[] { "--solution", "-s" },
            description: "The unique name of the solution containing the cloud flows")
        {
            IsRequired = true
        };

        // Create root command
        var rootCommand = new RootCommand(
            "Power Platform Flow Activator - Activate draft cloud flows in your Power Platform solutions")
        {
            environmentUrlOption,
            solutionNameOption
        };

        // Set up command handler
        rootCommand.SetHandler(async (environmentUrl, solutionName) =>
        {
            await RunActivationWorkflowAsync(environmentUrl, solutionName);
        }, environmentUrlOption, solutionNameOption);

        // Parse and execute
        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Displays the application banner with title and version.
    /// </summary>
    private static void DisplayBanner()
    {
        AnsiConsole.Write(
            new FigletText("Flow Activator")
                .LeftJustified()
                .Color(Spectre.Console.Color.Blue));

        AnsiConsole.MarkupLine("[dim]Power Platform Cloud Flow Activation Tool v1.0.0[/]");
        AnsiConsole.MarkupLine("[dim]Activate draft cloud flows in your Power Platform solutions[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Runs the main activation workflow.
    /// </summary>
    /// <param name="environmentUrl">The Power Platform environment URL.</param>
    /// <param name="solutionName">The solution unique name.</param>
    private static async Task RunActivationWorkflowAsync(string environmentUrl, string solutionName)
    {
        // Initialize session tracking
        var session = new ActivationSession
        {
            EnvironmentUrl = environmentUrl,
            SolutionName = solutionName
        };

        try
        {
            // Validate environment URL format
            if (!ValidateEnvironmentUrl(environmentUrl))
            {
                return;
            }

            AnsiConsole.MarkupLine($"[blue]Environment:[/] {environmentUrl}");
            AnsiConsole.MarkupLine($"[blue]Solution:[/] {solutionName}");
            AnsiConsole.WriteLine();

            // Step 1: Authenticate
            AnsiConsole.MarkupLine("[bold yellow]Step 1: Authentication[/]");
            var authService = new AuthenticationService(environmentUrl);
            string accessToken;

            try
            {
                accessToken = await authService.AcquireTokenInteractiveAsync();
                session.AuthenticatedUser = await authService.GetAuthenticatedUsernameAsync();
            }
            catch (AuthenticationException ex)
            {
                AnsiConsole.MarkupLine($"[red]Authentication failed: {ex.Message}[/]");
                return;
            }

            AnsiConsole.WriteLine();

            // Step 2: Connect and query flows
            AnsiConsole.MarkupLine("[bold yellow]Step 2: Querying Draft Cloud Flows[/]");

            using var powerPlatformService = new PowerPlatformService(environmentUrl, accessToken);
            List<CloudFlow> draftFlows;

            try
            {
                draftFlows = await powerPlatformService.GetDraftCloudFlowsInSolutionAsync(solutionName);
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
                return;
            }

            session.TotalDraftFlowsFound = draftFlows.Count;

            if (draftFlows.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]No draft cloud flows found in the solution.[/]");
                AnsiConsole.MarkupLine("[dim]All flows may already be activated or the solution contains no cloud flows.[/]");
                return;
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]Found {draftFlows.Count} draft cloud flow(s):[/]");
            AnsiConsole.WriteLine();

            // Step 3: Display flows with numbers
            DisplayFlowsTable(draftFlows);

            // Step 4: Get user selection
            AnsiConsole.WriteLine();
            var selectedFlows = GetUserFlowSelection(draftFlows);

            if (selectedFlows.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No flows selected for activation.[/]");
                return;
            }

            session.FlowsSelectedForActivation = selectedFlows.Count;
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[blue]Selected {selectedFlows.Count} flow(s) for activation.[/]");
            AnsiConsole.WriteLine();

            // Confirm before proceeding
            if (!ConfirmActivation(selectedFlows.Count))
            {
                AnsiConsole.MarkupLine("[yellow]Activation cancelled by user.[/]");
                return;
            }

            // Step 5: Activate selected flows
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold yellow]Step 3: Activating Flows[/]");
            AnsiConsole.WriteLine();

            var results = await powerPlatformService.ActivateFlowsAsync(selectedFlows);
            session.Results = results;
            session.SessionEnded = DateTime.UtcNow;

            // Step 6: Display results summary
            DisplayResultsSummary(session);

            // Step 7: Show detailed errors if any
            if (session.FailureCount > 0)
            {
                DisplayDetailedErrors(results.Where(r => !r.Success));
            }

            // Step 8: Prompt for Excel export
            AnsiConsole.WriteLine();
            if (PromptForExcelExport())
            {
                var exportService = new ExcelExportService();
                exportService.ExportWithSaveDialog(session);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]An unexpected error occurred: {ex.Message}[/]");
            AnsiConsole.MarkupLine($"[dim]{ex.StackTrace}[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to exit...[/]");
        Console.ReadKey(true);
    }

    /// <summary>
    /// Validates the format of the environment URL.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    private static bool ValidateEnvironmentUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            AnsiConsole.MarkupLine("[red]Invalid environment URL format. Please provide a valid URL.[/]");
            AnsiConsole.MarkupLine("[dim]Example: https://yourorg.crm.dynamics.com[/]");
            return false;
        }

        if (uri.Scheme != "https")
        {
            AnsiConsole.MarkupLine("[red]Environment URL must use HTTPS.[/]");
            return false;
        }

        if (!uri.Host.Contains("dynamics.com") && !uri.Host.Contains("crm"))
        {
            AnsiConsole.MarkupLine("[yellow]Warning: URL doesn't appear to be a standard Dynamics 365/Power Platform URL.[/]");
            AnsiConsole.MarkupLine("[dim]Expected format: https://yourorg.crm.dynamics.com or similar[/]");

            if (!AnsiConsole.Confirm("Continue anyway?", false))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Displays the list of draft flows in a formatted table.
    /// </summary>
    /// <param name="flows">The list of flows to display.</param>
    private static void DisplayFlowsTable(List<CloudFlow> flows)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("#").Centered())
            .AddColumn("Flow Name")
            .AddColumn("Status")
            .AddColumn("Modified On")
            .AddColumn("Modified By");

        for (var i = 0; i < flows.Count; i++)
        {
            var flow = flows[i];
            table.AddRow(
                $"[yellow]{i + 1}[/]",
                flow.Name.EscapeMarkup(),
                $"[dim]{flow.StateDescription}[/]",
                flow.ModifiedOn?.ToString("yyyy-MM-dd HH:mm") ?? "N/A",
                flow.ModifiedByName?.EscapeMarkup() ?? "N/A"
            );
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Gets the user's selection of which flows to activate.
    /// Supports multiple formats: individual numbers, ranges, "all", or comma-separated lists.
    /// </summary>
    /// <param name="flows">The list of available flows.</param>
    /// <returns>The list of selected flows.</returns>
    private static List<CloudFlow> GetUserFlowSelection(List<CloudFlow> flows)
    {
        AnsiConsole.MarkupLine("[blue]Enter the numbers of the flows you want to activate:[/]");
        AnsiConsole.MarkupLine("[dim]  - Enter individual numbers separated by commas (e.g., 1, 3, 5)[/]");
        AnsiConsole.MarkupLine("[dim]  - Enter a range using a dash (e.g., 1-5)[/]");
        AnsiConsole.MarkupLine("[dim]  - Enter 'all' to select all flows[/]");
        AnsiConsole.MarkupLine("[dim]  - Enter '0' or 'none' to cancel[/]");
        AnsiConsole.WriteLine();

        var input = AnsiConsole.Ask<string>("[green]Your selection:[/]");

        if (string.IsNullOrWhiteSpace(input))
        {
            return new List<CloudFlow>();
        }

        input = input.Trim().ToLowerInvariant();

        // Handle special inputs
        if (input == "0" || input == "none" || input == "cancel")
        {
            return new List<CloudFlow>();
        }

        if (input == "all")
        {
            return flows.ToList();
        }

        // Parse the selection
        var selectedIndices = new HashSet<int>();

        foreach (var part in input.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();

            // Check for range (e.g., "1-5")
            if (trimmed.Contains('-'))
            {
                var rangeParts = trimmed.Split('-');
                if (rangeParts.Length == 2 &&
                    int.TryParse(rangeParts[0].Trim(), out var start) &&
                    int.TryParse(rangeParts[1].Trim(), out var end))
                {
                    for (var i = start; i <= end; i++)
                    {
                        if (i >= 1 && i <= flows.Count)
                        {
                            selectedIndices.Add(i - 1); // Convert to 0-based index
                        }
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]Invalid range format: {trimmed}[/]");
                }
            }
            // Check for single number
            else if (int.TryParse(trimmed, out var number))
            {
                if (number >= 1 && number <= flows.Count)
                {
                    selectedIndices.Add(number - 1); // Convert to 0-based index
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]Number out of range: {number} (valid: 1-{flows.Count})[/]");
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]Invalid input: {trimmed}[/]");
            }
        }

        return selectedIndices.OrderBy(i => i).Select(i => flows[i]).ToList();
    }

    /// <summary>
    /// Prompts the user to confirm the activation.
    /// </summary>
    /// <param name="count">The number of flows to be activated.</param>
    /// <returns>True if confirmed, false otherwise.</returns>
    private static bool ConfirmActivation(int count)
    {
        return AnsiConsole.Confirm(
            $"[yellow]Are you sure you want to activate {count} flow(s)?[/]",
            true);
    }

    /// <summary>
    /// Displays a summary of the activation results.
    /// </summary>
    /// <param name="session">The activation session with results.</param>
    private static void DisplayResultsSummary(ActivationSession session)
    {
        AnsiConsole.WriteLine();

        var panel = new Spectre.Console.Panel(
            new Markup(
                $"[green]Successfully activated:[/] {session.SuccessCount}\n" +
                $"[red]Failed:[/] {session.FailureCount}\n" +
                $"[blue]Total time:[/] {session.TotalDuration:mm\\:ss}"))
        {
            Header = new PanelHeader("[bold]Activation Results Summary[/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Displays detailed error information for failed activations.
    /// </summary>
    /// <param name="failedResults">The failed activation results.</param>
    private static void DisplayDetailedErrors(IEnumerable<ActivationResult> failedResults)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold red]Detailed Error Information:[/]");
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Flow Name")
            .AddColumn("Error Message");

        foreach (var result in failedResults)
        {
            table.AddRow(
                result.Flow.Name.EscapeMarkup(),
                $"[red]{result.ErrorMessage?.EscapeMarkup() ?? "Unknown error"}[/]"
            );
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Prompts the user to export results to Excel.
    /// </summary>
    /// <returns>True if the user wants to export, false otherwise.</returns>
    private static bool PromptForExcelExport()
    {
        return AnsiConsole.Confirm(
            "[blue]Would you like to download an Excel workbook with detailed results?[/]",
            true);
    }
}
