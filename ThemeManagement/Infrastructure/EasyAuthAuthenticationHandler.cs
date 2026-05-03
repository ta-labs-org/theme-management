using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThemeManagement.Infrastructure;

/// <summary>
/// Azure App Service Easy Auth authentication handler.
/// Reads the X-MS-CLIENT-PRINCIPAL header injected by Easy Auth and constructs the ClaimsPrincipal.
/// Entra ID App Roles assigned in Azure are automatically included as role claims.
/// </summary>
public class EasyAuthAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public EasyAuthAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder urlEncoder)
        : base(options, logger, urlEncoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var principalHeader = Request.Headers["X-MS-CLIENT-PRINCIPAL"].FirstOrDefault();
        if (string.IsNullOrEmpty(principalHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(principalHeader));
            var easyAuthPrincipal = JsonSerializer.Deserialize<EasyAuthPrincipal>(json);

            if (easyAuthPrincipal?.Claims == null)
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = easyAuthPrincipal.Claims
                .Select(c => new Claim(c.Type, c.Value))
                .ToList();

            var identity = new ClaimsIdentity(
                claims,
                easyAuthPrincipal.AuthTyp ?? Scheme.Name,
                easyAuthPrincipal.NameTyp ?? ClaimsIdentity.DefaultNameClaimType,
                easyAuthPrincipal.RoleTyp ?? ClaimsIdentity.DefaultRoleClaimType
            );

            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse Easy Auth principal header.");
            return Task.FromResult(AuthenticateResult.NoResult());
        }
    }

    /// <summary>
    /// On Azure App Service, Easy Auth intercepts unauthenticated requests before they reach the app,
    /// so a challenge here should never occur in production. In local development (without Easy Auth),
    /// this no-op challenge allows the Blazor app to render so that AuthorizeRouteView can redirect
    /// to /.auth/login/aad itself.
    /// </summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        => Task.CompletedTask;
}

internal sealed class EasyAuthPrincipal
{
    [JsonPropertyName("auth_typ")]
    public string? AuthTyp { get; set; }

    [JsonPropertyName("name_typ")]
    public string? NameTyp { get; set; }

    [JsonPropertyName("role_typ")]
    public string? RoleTyp { get; set; }

    [JsonPropertyName("claims")]
    public List<EasyAuthClaim>? Claims { get; set; }
}

internal sealed class EasyAuthClaim
{
    [JsonPropertyName("typ")]
    public string Type { get; set; } = "";

    [JsonPropertyName("val")]
    public string Value { get; set; } = "";
}
