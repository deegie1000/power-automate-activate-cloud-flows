using Microsoft.Identity.Client;
using Spectre.Console;

namespace PowerPlatformFlowActivator.Services;

/// <summary>
/// Handles interactive authentication to Power Platform using Microsoft Entra ID (Azure AD).
/// Uses the well-known Power Platform client ID for public client authentication.
/// </summary>
public class AuthenticationService
{
    // Well-known client ID for Power Platform / Power Apps
    // This is a first-party Microsoft application ID that has permissions to access Dataverse
    private const string PowerPlatformClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";

    // Authority for Microsoft Entra ID (multi-tenant)
    private const string Authority = "https://login.microsoftonline.com/organizations";

    private readonly string _environmentUrl;
    private readonly IPublicClientApplication _publicClientApp;

    /// <summary>
    /// Initializes a new instance of the AuthenticationService.
    /// </summary>
    /// <param name="environmentUrl">The Power Platform environment URL (e.g., https://org.crm.dynamics.com)</param>
    public AuthenticationService(string environmentUrl)
    {
        _environmentUrl = environmentUrl.TrimEnd('/');

        // Build the public client application for interactive authentication
        _publicClientApp = PublicClientApplicationBuilder
            .Create(PowerPlatformClientId)
            .WithAuthority(Authority)
            .WithDefaultRedirectUri()
            .Build();
    }

    /// <summary>
    /// Acquires an access token using interactive browser authentication.
    /// The user will be prompted to sign in via their default browser.
    /// </summary>
    /// <returns>The access token for the Power Platform environment.</returns>
    /// <exception cref="AuthenticationException">Thrown when authentication fails.</exception>
    public async Task<string> AcquireTokenInteractiveAsync()
    {
        // Define the scope for Dataverse access
        // Using the environment URL with /.default scope to get all consented permissions
        var scopes = new[] { $"{_environmentUrl}/.default" };

        try
        {
            AnsiConsole.MarkupLine("[yellow]Opening browser for authentication...[/]");
            AnsiConsole.MarkupLine("[dim]Please sign in with your Power Platform account.[/]");

            // First, try to get a token silently (from cache)
            var accounts = await _publicClientApp.GetAccountsAsync();
            AuthenticationResult result;

            try
            {
                // Attempt silent authentication with cached credentials
                result = await _publicClientApp
                    .AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                    .ExecuteAsync();

                AnsiConsole.MarkupLine("[green]Authenticated using cached credentials.[/]");
            }
            catch (MsalUiRequiredException)
            {
                // Silent authentication failed, proceed with interactive
                result = await _publicClientApp
                    .AcquireTokenInteractive(scopes)
                    .WithPrompt(Prompt.SelectAccount)
                    .ExecuteAsync();

                AnsiConsole.MarkupLine("[green]Authentication successful![/]");
            }

            // Display authenticated user information
            if (!string.IsNullOrEmpty(result.Account?.Username))
            {
                AnsiConsole.MarkupLine($"[dim]Signed in as: {result.Account.Username}[/]");
            }

            return result.AccessToken;
        }
        catch (MsalException ex)
        {
            throw new AuthenticationException(
                $"Authentication failed: {ex.Message}. " +
                "Please ensure you have access to the specified Power Platform environment.",
                ex);
        }
        catch (Exception ex)
        {
            throw new AuthenticationException(
                $"An unexpected error occurred during authentication: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Gets the username of the currently authenticated user from the token cache.
    /// </summary>
    /// <returns>The username or null if not authenticated.</returns>
    public async Task<string?> GetAuthenticatedUsernameAsync()
    {
        var accounts = await _publicClientApp.GetAccountsAsync();
        return accounts.FirstOrDefault()?.Username;
    }
}

/// <summary>
/// Exception thrown when authentication fails.
/// </summary>
public class AuthenticationException : Exception
{
    public AuthenticationException(string message) : base(message) { }
    public AuthenticationException(string message, Exception innerException) : base(message, innerException) { }
}
