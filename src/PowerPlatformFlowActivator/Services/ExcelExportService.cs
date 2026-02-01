using System.Windows.Forms;
using ClosedXML.Excel;
using PowerPlatformFlowActivator.Models;
using Spectre.Console;

namespace PowerPlatformFlowActivator.Services;

/// <summary>
/// Service for exporting activation results to Excel workbooks.
/// Provides functionality to create detailed reports and save them using a Windows save dialog.
/// </summary>
public class ExcelExportService
{
    /// <summary>
    /// Prompts the user with a save dialog and exports the activation session to an Excel file.
    /// </summary>
    /// <param name="session">The activation session containing all results.</param>
    /// <returns>True if the file was saved successfully, false if cancelled or failed.</returns>
    public bool ExportWithSaveDialog(ActivationSession session)
    {
        // Generate a default filename with timestamp
        var defaultFileName = $"FlowActivationResults_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        // Show save file dialog
        // Note: This requires the application to run on Windows with WinForms support
        string? filePath = ShowSaveFileDialog(defaultFileName);

        if (string.IsNullOrEmpty(filePath))
        {
            AnsiConsole.MarkupLine("[yellow]Export cancelled by user.[/]");
            return false;
        }

        try
        {
            ExportToExcel(session, filePath);
            AnsiConsole.MarkupLine($"[green]Results exported successfully to:[/] {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to export results: {ex.Message}[/]");
            return false;
        }
    }

    /// <summary>
    /// Shows a Windows save file dialog for selecting the export location.
    /// </summary>
    /// <param name="defaultFileName">The default filename to suggest.</param>
    /// <returns>The selected file path, or null if cancelled.</returns>
    private static string? ShowSaveFileDialog(string defaultFileName)
    {
        string? result = null;

        // SaveFileDialog must run on an STA thread
        var thread = new Thread(() =>
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Save Flow Activation Results",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                DefaultExt = "xlsx",
                FileName = defaultFileName,
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                result = dialog.FileName;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return result;
    }

    /// <summary>
    /// Exports the activation session to an Excel workbook.
    /// </summary>
    /// <param name="session">The activation session to export.</param>
    /// <param name="filePath">The file path to save the workbook.</param>
    public void ExportToExcel(ActivationSession session, string filePath)
    {
        using var workbook = new XLWorkbook();

        // Create Summary sheet
        CreateSummarySheet(workbook, session);

        // Create Detailed Results sheet
        CreateDetailedResultsSheet(workbook, session);

        // Create Errors sheet (only if there are failures)
        if (session.FailureCount > 0)
        {
            CreateErrorsSheet(workbook, session);
        }

        // Save the workbook
        workbook.SaveAs(filePath);
    }

    /// <summary>
    /// Creates the summary sheet with session overview information.
    /// </summary>
    private static void CreateSummarySheet(XLWorkbook workbook, ActivationSession session)
    {
        var sheet = workbook.Worksheets.Add("Summary");

        // Title
        sheet.Cell("A1").Value = "Power Platform Flow Activation Report";
        sheet.Cell("A1").Style.Font.Bold = true;
        sheet.Cell("A1").Style.Font.FontSize = 16;
        sheet.Range("A1:D1").Merge();

        // Session Information
        var row = 3;
        sheet.Cell($"A{row}").Value = "Session Information";
        sheet.Cell($"A{row}").Style.Font.Bold = true;
        sheet.Cell($"A{row}").Style.Font.FontSize = 12;
        row++;

        AddSummaryRow(sheet, ref row, "Environment URL:", session.EnvironmentUrl);
        AddSummaryRow(sheet, ref row, "Solution Name:", session.SolutionName);
        AddSummaryRow(sheet, ref row, "Authenticated User:", session.AuthenticatedUser ?? "N/A");
        AddSummaryRow(sheet, ref row, "Session Started:", session.SessionStarted.ToString("yyyy-MM-dd HH:mm:ss UTC"));
        AddSummaryRow(sheet, ref row, "Session Ended:", session.SessionEnded?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "N/A");
        AddSummaryRow(sheet, ref row, "Total Duration:", session.TotalDuration.ToString(@"hh\:mm\:ss"));

        row += 2;

        // Results Summary
        sheet.Cell($"A{row}").Value = "Results Summary";
        sheet.Cell($"A{row}").Style.Font.Bold = true;
        sheet.Cell($"A{row}").Style.Font.FontSize = 12;
        row++;

        AddSummaryRow(sheet, ref row, "Total Draft Flows Found:", session.TotalDraftFlowsFound.ToString());
        AddSummaryRow(sheet, ref row, "Flows Selected for Activation:", session.FlowsSelectedForActivation.ToString());
        AddSummaryRow(sheet, ref row, "Successfully Activated:", session.SuccessCount.ToString());
        AddSummaryRow(sheet, ref row, "Failed:", session.FailureCount.ToString());

        // Calculate success rate
        var successRate = session.Results.Count > 0
            ? (double)session.SuccessCount / session.Results.Count * 100
            : 0;
        AddSummaryRow(sheet, ref row, "Success Rate:", $"{successRate:F1}%");

        // Auto-fit columns
        sheet.Columns().AdjustToContents();
        sheet.Column("B").Width = Math.Max(sheet.Column("B").Width, 50);
    }

    /// <summary>
    /// Adds a summary row with label and value.
    /// </summary>
    private static void AddSummaryRow(IXLWorksheet sheet, ref int row, string label, string value)
    {
        sheet.Cell($"A{row}").Value = label;
        sheet.Cell($"A{row}").Style.Font.Bold = true;
        sheet.Cell($"B{row}").Value = value;
        row++;
    }

    /// <summary>
    /// Creates the detailed results sheet with all activation attempts.
    /// </summary>
    private static void CreateDetailedResultsSheet(XLWorkbook workbook, ActivationSession session)
    {
        var sheet = workbook.Worksheets.Add("Detailed Results");

        // Headers
        var headers = new[]
        {
            "Flow Name",
            "Workflow ID",
            "Status",
            "Attempted At",
            "Duration (ms)",
            "Description",
            "Created On",
            "Modified On",
            "Created By",
            "Modified By",
            "Error Message"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        // Data rows
        var row = 2;
        foreach (var result in session.Results.OrderBy(r => r.Flow.Name))
        {
            sheet.Cell(row, 1).Value = result.Flow.Name;
            sheet.Cell(row, 2).Value = result.Flow.WorkflowId.ToString();
            sheet.Cell(row, 3).Value = result.Success ? "Success" : "Failed";

            // Apply conditional formatting for status
            var statusCell = sheet.Cell(row, 3);
            if (result.Success)
            {
                statusCell.Style.Font.FontColor = XLColor.DarkGreen;
                statusCell.Style.Fill.BackgroundColor = XLColor.LightGreen;
            }
            else
            {
                statusCell.Style.Font.FontColor = XLColor.DarkRed;
                statusCell.Style.Fill.BackgroundColor = XLColor.LightPink;
            }

            sheet.Cell(row, 4).Value = result.AttemptedAt.ToString("yyyy-MM-dd HH:mm:ss");
            sheet.Cell(row, 5).Value = result.Duration.TotalMilliseconds;
            sheet.Cell(row, 6).Value = result.Flow.Description ?? "";
            sheet.Cell(row, 7).Value = result.Flow.CreatedOn?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            sheet.Cell(row, 8).Value = result.Flow.ModifiedOn?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            sheet.Cell(row, 9).Value = result.Flow.CreatedByName ?? "";
            sheet.Cell(row, 10).Value = result.Flow.ModifiedByName ?? "";
            sheet.Cell(row, 11).Value = result.ErrorMessage ?? "";

            row++;
        }

        // Enable filtering
        if (session.Results.Count > 0)
        {
            sheet.RangeUsed()?.SetAutoFilter();
        }

        // Auto-fit columns
        sheet.Columns().AdjustToContents();

        // Set maximum column widths for readability
        foreach (var column in sheet.Columns())
        {
            if (column.Width > 50)
            {
                column.Width = 50;
            }
        }
    }

    /// <summary>
    /// Creates an errors sheet with detailed failure information.
    /// </summary>
    private static void CreateErrorsSheet(XLWorkbook workbook, ActivationSession session)
    {
        var sheet = workbook.Worksheets.Add("Errors");

        // Headers
        var headers = new[] { "Flow Name", "Workflow ID", "Error Message", "Error Details", "Attempted At" };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightCoral;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        // Data rows - only failures
        var row = 2;
        foreach (var result in session.Results.Where(r => !r.Success).OrderBy(r => r.Flow.Name))
        {
            sheet.Cell(row, 1).Value = result.Flow.Name;
            sheet.Cell(row, 2).Value = result.Flow.WorkflowId.ToString();
            sheet.Cell(row, 3).Value = result.ErrorMessage ?? "Unknown error";
            sheet.Cell(row, 4).Value = result.ErrorDetails ?? "";
            sheet.Cell(row, 5).Value = result.AttemptedAt.ToString("yyyy-MM-dd HH:mm:ss");
            row++;
        }

        // Auto-fit columns
        sheet.Columns().AdjustToContents();

        // Set maximum column widths
        foreach (var column in sheet.Columns())
        {
            if (column.Width > 60)
            {
                column.Width = 60;
            }
        }
    }
}
