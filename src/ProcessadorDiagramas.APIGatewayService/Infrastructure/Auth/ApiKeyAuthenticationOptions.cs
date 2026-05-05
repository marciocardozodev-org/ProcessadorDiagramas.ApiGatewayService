using Microsoft.AspNetCore.Authentication;

namespace ProcessadorDiagramas.APIGatewayService.Infrastructure.Auth;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public string HeaderName { get; set; } = "X-Api-Key";
    public string ClientApiKey { get; set; } = string.Empty;
    public string InternalApiKey { get; set; } = string.Empty;
}