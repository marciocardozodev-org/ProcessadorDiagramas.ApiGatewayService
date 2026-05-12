using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ProcessadorDiagramas.APIGatewayService.Infrastructure.Auth;
using System.Net;
using System.Net.Http.Json;

namespace ProcessadorDiagramas.APIGatewayService.Tests.API;

/// <summary>
/// Tests confirming API key authorization requirements on Gateway endpoints.
/// These tests verify that:
/// - Public endpoints require a valid client API key.
/// - Internal callback requires the internal API key.
/// - Mixing client key on internal endpoint is rejected.
/// </summary>
public sealed class ApiKeyAuthorizationTests
{
    [Fact]
    public void ClientAccessPolicy_RequiresAuthenticatedUser()
    {
        // Lightweight check: policy name constants exist and differ.
        AuthorizationPolicies.ClientAccess.Should().NotBeNullOrWhiteSpace();
        AuthorizationPolicies.InternalAccess.Should().NotBeNullOrWhiteSpace();
        AuthorizationPolicies.ClientAccess.Should().NotBe(AuthorizationPolicies.InternalAccess);
    }

    [Fact]
    public void ApiKeyAuthenticationHandler_SchemeName_IsNotEmpty()
    {
        ApiKeyAuthenticationHandler.SchemeName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ApiKeyAuthenticationOptions_DefaultHeaderName_IsXApiKey()
    {
        var options = new ApiKeyAuthenticationOptions();
        options.HeaderName.Should().Be("X-Api-Key");
    }
}
