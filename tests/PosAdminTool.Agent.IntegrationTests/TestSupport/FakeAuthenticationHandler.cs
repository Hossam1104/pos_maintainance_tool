using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PosAdminTool.Agent.IntegrationTests.TestSupport;

public sealed class FakeAuthenticationOptions : AuthenticationSchemeOptions;

/// <summary>
/// Stands in for the real Negotiate handler in tests, which needs a live Windows SSPI handshake
/// that an in-memory TestServer cannot perform. A request with no
/// <see cref="PrincipalNameHeader"/> is treated as fully unauthenticated (mirrors how Negotiate
/// behaves when no credentials are presented), so the "unauthenticated request rejected" case needs
/// no special setup at all.
/// </summary>
public sealed class FakeAuthenticationHandler(
    IOptionsMonitor<FakeAuthenticationOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<FakeAuthenticationOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "TestFake";
    public const string PrincipalNameHeader = "X-Test-Principal-Name";
    public const string IsAdministratorHeader = "X-Test-Is-Administrator";
    public const string AdministratorClaimType = "test-is-administrator";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(PrincipalNameHeader, out var principalName) || string.IsNullOrWhiteSpace(principalName))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var isAdministrator = Request.Headers.TryGetValue(IsAdministratorHeader, out var isAdminValue)
            && string.Equals(isAdminValue, "true", StringComparison.OrdinalIgnoreCase);

        var claims = new List<Claim> { new(ClaimTypes.Name, principalName.ToString()) };
        if (isAdministrator)
        {
            claims.Add(new Claim(AdministratorClaimType, "true"));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
