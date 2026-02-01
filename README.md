# Power Platform Flow Activator

A command-line tool to activate draft cloud flows in Power Platform solutions. This application simplifies the process of activating multiple cloud flows that are in draft status after solution deployment.

## What It Does

The Power Platform Flow Activator:

1. **Authenticates** to your Power Platform environment using interactive browser-based authentication
2. **Queries** a specified solution for all cloud flows that are in draft status
3. **Displays** the draft flows in a numbered list with details
4. **Allows selection** of which flows to activate (individual, ranges, or all)
5. **Activates** the selected flows and shows real-time progress
6. **Reports** success and failure with detailed error messages
7. **Exports** results to an Excel workbook (optional)

## Prerequisites

- **Operating System**: Windows (required for the save dialog functionality)
- **.NET 8.0 SDK** or later ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- **Power Platform Environment** access with appropriate permissions
- **Azure AD / Microsoft Entra ID** account with access to the target environment

### Required Permissions

Your account must have the following permissions in the Power Platform environment:

- **Read** access to solutions and workflows
- **Write** access to workflows (to change state from Draft to Activated)
- Typically, the **System Administrator** or **System Customizer** security role is sufficient

## Installation

### Option 1: Build from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/power-automate-activate-cloud-flows.git
   cd power-automate-activate-cloud-flows
   ```

2. Build the application:
   ```bash
   dotnet build src/PowerPlatformFlowActivator/PowerPlatformFlowActivator.csproj -c Release
   ```

3. The executable will be located at:
   ```
   src/PowerPlatformFlowActivator/bin/Release/net8.0-windows/PowerPlatformFlowActivator.exe
   ```

### Option 2: Publish as Self-Contained Executable

For a standalone executable that doesn't require .NET to be installed:

```bash
dotnet publish src/PowerPlatformFlowActivator/PowerPlatformFlowActivator.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

This creates a single executable file in the `./publish` directory.

## Usage

### Command-Line Syntax

```bash
PowerPlatformFlowActivator --environment <url> --solution <name>
```

Or using short options:

```bash
PowerPlatformFlowActivator -e <url> -s <name>
```

### Parameters

| Parameter | Short | Required | Description |
|-----------|-------|----------|-------------|
| `--environment` | `-e` | Yes | The Power Platform environment URL (e.g., `https://yourorg.crm.dynamics.com`) |
| `--solution` | `-s` | Yes | The unique name of the solution containing the cloud flows |

### Examples

**Activate flows in a production environment:**
```bash
PowerPlatformFlowActivator -e https://contoso.crm.dynamics.com -s ContosoSolution
```

**Activate flows in a development environment:**
```bash
PowerPlatformFlowActivator --environment https://contoso-dev.crm.dynamics.com --solution MyDevSolution
```

### Interactive Workflow

1. **Authentication**: A browser window opens for you to sign in with your Microsoft account
2. **Flow Discovery**: The application queries the solution and displays all draft flows
3. **Selection**: Enter the numbers of flows to activate:
   - Individual numbers: `1, 3, 5`
   - Ranges: `1-5`
   - All flows: `all`
   - Cancel: `0` or `none`
4. **Confirmation**: Confirm your selection before activation
5. **Activation**: Watch real-time progress as flows are activated
6. **Results**: View the summary of successful and failed activations
7. **Export**: Optionally save detailed results to an Excel file

### Example Output

```
   _____ _                  _        _   _            _
  |  ___| | _____      __  / \   ___| |_(_)_   ____ _| |_ ___  _ __
  | |_  | |/ _ \ \ /\ / / / _ \ / __| __| \ \ / / _` | __/ _ \| '__|
  |  _| | | (_) \ V  V / / ___ \ (__| |_| |\ V / (_| | || (_) | |
  |_|   |_|\___/ \_/\_/ /_/   \_\___|\__|_| \_/ \__,_|\__\___/|_|

Power Platform Cloud Flow Activation Tool v1.0.0

Environment: https://contoso.crm.dynamics.com
Solution: ContosoSolution

Step 1: Authentication
Opening browser for authentication...
Please sign in with your Power Platform account.
Authentication successful!
Signed in as: user@contoso.com

Step 2: Querying Draft Cloud Flows
Found solution with ID: 12345678-1234-1234-1234-123456789012
Found 5 workflow components in solution.

Found 3 draft cloud flow(s):

╭───┬────────────────────────────┬────────┬──────────────────┬───────────────╮
│ # │ Flow Name                  │ Status │ Modified On      │ Modified By   │
├───┼────────────────────────────┼────────┼──────────────────┼───────────────┤
│ 1 │ Process New Orders         │ Draft  │ 2024-01-15 14:30 │ John Doe      │
│ 2 │ Send Notification Email    │ Draft  │ 2024-01-14 09:15 │ Jane Smith    │
│ 3 │ Update Customer Records    │ Draft  │ 2024-01-13 16:45 │ John Doe      │
╰───┴────────────────────────────┴────────┴──────────────────┴───────────────╯

Your selection: 1, 3

Selected 2 flow(s) for activation.
Are you sure you want to activate 2 flow(s)? [y/n] (y): y

Step 3: Activating Flows
Activating: Process New Orders
  ✓ Process New Orders
Activating: Update Customer Records
  ✓ Update Customer Records

╭─────────────────────────────────╮
│    Activation Results Summary   │
├─────────────────────────────────┤
│ Successfully activated: 2       │
│ Failed: 0                       │
│ Total time: 00:05               │
╰─────────────────────────────────╯

Would you like to download an Excel workbook with detailed results? [y/n] (y):
```

## Excel Report

The Excel export includes three worksheets:

### Summary Sheet
- Environment URL and solution name
- Authenticated user
- Session timestamps and duration
- Total flows found and selected
- Success and failure counts
- Success rate percentage

### Detailed Results Sheet
- Flow name and workflow ID
- Activation status (Success/Failed)
- Timestamp and duration
- Flow description and metadata
- Error messages for failures

### Errors Sheet (if any failures)
- Failed flow names
- Detailed error messages
- Error details for troubleshooting

## Troubleshooting

### Authentication Fails

- Ensure you have access to the Power Platform environment
- Check that your account is not blocked or requires MFA that isn't completing
- Verify the environment URL is correct and accessible

### Solution Not Found

- Verify the solution unique name (not the display name)
- Ensure the solution exists in the target environment
- Check that you have read permissions for solutions

### Flow Activation Fails

Common reasons for activation failures:

1. **Insufficient Permissions**: Your account lacks the required security role
2. **Connection Issues**: Flow connections are not configured or have expired
3. **License Issues**: Required Power Automate license is not available
4. **Flow Errors**: The flow has validation errors that prevent activation

### No Flows Found

- Verify the solution contains cloud flows (Modern Flows, category = 5)
- Check that some flows are in Draft status (StateCode = 0)
- Ensure you have read permissions for workflows

## Technical Details

### Architecture

```
src/PowerPlatformFlowActivator/
├── Program.cs                 # Entry point and main workflow
├── Models/
│   ├── CloudFlow.cs          # Cloud flow entity model
│   ├── ActivationResult.cs   # Individual activation result
│   └── ActivationSession.cs  # Session tracking model
└── Services/
    ├── AuthenticationService.cs   # Interactive authentication
    ├── PowerPlatformService.cs    # Dataverse operations
    └── ExcelExportService.cs      # Excel report generation
```

### Dependencies

| Package | Purpose |
|---------|---------|
| Microsoft.Identity.Client | Azure AD / Entra ID authentication |
| Microsoft.PowerPlatform.Dataverse.Client | Dataverse SDK for Power Platform |
| ClosedXML | Excel file generation |
| System.CommandLine | Command-line argument parsing |
| Spectre.Console | Rich console output and formatting |

### Authentication

The application uses the well-known Power Platform client ID (`51f81489-12ee-4a9e-aaae-a2591f45987d`) for public client authentication. This is a first-party Microsoft application that has delegated permissions to access Dataverse APIs.

## License

This project is provided as-is for internal use. See LICENSE file for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## Support

For issues and feature requests, please open an issue in the GitHub repository.
