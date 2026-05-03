using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace ThemeManagement.Infrastructure;

/// <summary>
/// Development-only authentication handler that auto-authenticates every request
/// without requiring Easy Auth / Entra ID. Enabled via DevAuth:Enabled = true
/// in appsettings.Development.json. Must never be registered in production.
/// </summary>
public class DevBypassAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public DevBypassAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder urlEncoder,
        IConfiguration configuration,
        IWebHostEnvironment environment)
        : base(options, logger, urlEncoder)
    {
        _configuration = configuration;
        _environment = environment;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_environment.IsDevelopment())
            throw new InvalidOperationException(
                "DevBypassAuthHandler must not be used outside the Development environment.");

        var userName = _configuration["DevAuth:UserName"] ?? "開発者";
        var roles = _configuration.GetSection("DevAuth:Roles").Get<string[]>() ?? ["Admin"];

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, userName),
            new Claim(ClaimTypes.Email, "dev@localhost"),
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
