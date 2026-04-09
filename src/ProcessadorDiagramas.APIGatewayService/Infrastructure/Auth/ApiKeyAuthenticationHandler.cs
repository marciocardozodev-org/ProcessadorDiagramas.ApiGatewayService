using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Auth;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string SchemeName = "ApiKey";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var headerValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var providedApiKey = headerValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey))
            return Task.FromResult(AuthenticateResult.Fail("API key is missing."));

        string? role = providedApiKey switch
        {
            var key when !string.IsNullOrWhiteSpace(Options.ClientApiKey) && key == Options.ClientApiKey => "client",
            var key when !string.IsNullOrWhiteSpace(Options.InternalApiKey) && key == Options.InternalApiKey => "internal",
            _ => null
        };

        if (role is null)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, role),
            new(ClaimTypes.Name, $"{role}-caller"),
            new(ClaimTypes.Role, role)
        };

        if (role == "internal")
            claims.Add(new Claim(ClaimTypes.Role, "client"));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}