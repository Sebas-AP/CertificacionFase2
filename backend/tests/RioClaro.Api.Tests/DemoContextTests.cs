using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RioClaro.Application;
using RioClaro.Domain;
using RioClaro.Infrastructure.Persistence;
using Xunit;

namespace RioClaro.Api.Tests;

public sealed class DemoContextTests
{
    [Fact]
    public async Task ContextEndpointIsAvailableWithoutBearerToken()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/context");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        Assert.Equal("Demo", root.GetProperty("mode").GetString());
        Assert.Equal(DemoIdentity.OrganizationId,
            root.GetProperty("organization").GetProperty("id").GetGuid());
        Assert.Equal(DemoIdentity.OrganizationName,
            root.GetProperty("organization").GetProperty("name").GetString());
        Assert.Equal(DemoIdentity.UserId,
            root.GetProperty("user").GetProperty("id").GetGuid());
        Assert.Equal(DemoIdentity.UserDisplayName,
            root.GetProperty("user").GetProperty("displayName").GetString());
        Assert.Equal(DemoIdentity.Role,
            root.GetProperty("user").GetProperty("role").GetString());
    }

    [Fact]
    public async Task NoAuthenticationOrOrganizationSelectionRoutesAreExposed()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();

        foreach (var path in new[]
                 {
                     "/api/login",
                     "/api/register",
                     "/api/invitations",
                     "/api/organizations"
                 })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task EveryMappedEndpointIsAccessibleWithoutOrWithInvalidToken()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();

        foreach (var path in new[] { "/health", "/api/context" })
        {
            using var unauthenticatedResponse = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, unauthenticatedResponse.StatusCode);

            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
            using var invalidTokenResponse = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, invalidTokenResponse.StatusCode);
        }
    }

    [Fact]
    public async Task StartupMigratesAndSeedsDemoOrganizationAndUser()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();
        await client.GetAsync("/health");

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();

        var organization = await dbContext.Organizations
            .SingleAsync(item => item.Id == DemoIdentity.OrganizationId);
        var user = await dbContext.Users
            .SingleAsync(item => item.Id == DemoIdentity.UserId);

        Assert.Equal(DemoIdentity.OrganizationName, organization.Name);
        Assert.Equal(DemoIdentity.UserDisplayName, user.DisplayName);
        Assert.Equal(DemoIdentity.Role, user.Role);
        Assert.Equal(DemoIdentity.OrganizationId, user.OrganizationId);
    }

    [Fact]
    public async Task RunningSeederRepeatedlyDoesNotCreateDuplicates()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();
        await client.GetAsync("/health");

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RioClaroDbContext>();

        await DemoDataSeeder.SeedAsync(dbContext);
        await DemoDataSeeder.SeedAsync(dbContext);

        Assert.Equal(1, await dbContext.Organizations
            .CountAsync(item => item.Id == DemoIdentity.OrganizationId));
        Assert.Equal(1, await dbContext.Users
            .CountAsync(item => item.Id == DemoIdentity.UserId));
    }

    [Fact]
    public async Task CurrentUserContextIsRegisteredAsSingletonWithStableDemoValues()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();

        var first = factory.Services.GetRequiredService<ICurrentUserContext>();
        var second = factory.Services.GetRequiredService<ICurrentUserContext>();

        Assert.Same(first, second);
        Assert.Equal(DemoIdentity.OrganizationId, first.OrganizationId);
        Assert.Equal(DemoIdentity.UserId, first.UserId);
        Assert.Equal(DemoIdentity.OrganizationName, first.OrganizationName);
        Assert.Equal(DemoIdentity.UserDisplayName, first.UserDisplayName);
        Assert.Equal(DemoIdentity.Role, first.Role);
        Assert.True(first.IsDemoMode);
    }

    [Fact]
    public async Task CorsAllowsOnlyConfiguredFrontendOrigin()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();

        using var allowedRequest = new HttpRequestMessage(HttpMethod.Options, "/api/context");
        allowedRequest.Headers.Add("Origin", "http://localhost:3000");
        allowedRequest.Headers.Add("Access-Control-Request-Method", "GET");
        using var allowedResponse = await client.SendAsync(allowedRequest);

        Assert.Equal("http://localhost:3000",
            Assert.Single(allowedResponse.Headers.GetValues("Access-Control-Allow-Origin")));

        using var deniedRequest = new HttpRequestMessage(HttpMethod.Options, "/api/context");
        deniedRequest.Headers.Add("Origin", "https://untrusted.example");
        deniedRequest.Headers.Add("Access-Control-Request-Method", "GET");
        using var deniedResponse = await client.SendAsync(deniedRequest);

        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task ProductionCorsAllowsOnlyConfiguredOrigin()
    {
        const string frontendOrigin = "http://localhost:3000";
        await using var factory = new RioClaroApiFactory("Production");
        using var client = factory.CreateClient();

        using var allowedRequest = new HttpRequestMessage(HttpMethod.Options, "/api/context");
        allowedRequest.Headers.Add("Origin", frontendOrigin);
        allowedRequest.Headers.Add("Access-Control-Request-Method", "GET");
        using var allowedResponse = await client.SendAsync(allowedRequest);

        Assert.Equal(frontendOrigin,
            Assert.Single(allowedResponse.Headers.GetValues("Access-Control-Allow-Origin")));

        using var deniedRequest = new HttpRequestMessage(HttpMethod.Options, "/api/context");
        deniedRequest.Headers.Add("Origin", "https://untrusted.example");
        deniedRequest.Headers.Add("Access-Control-Request-Method", "GET");
        using var deniedResponse = await client.SendAsync(deniedRequest);

        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task SwaggerDoesNotDeclareBearerAuthentication()
    {
        await using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.False(root.TryGetProperty("security", out _));
        Assert.False(root.GetProperty("components")
            .TryGetProperty("securitySchemes", out _));

        foreach (var path in root.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                Assert.False(operation.Value.TryGetProperty("security", out _),
                    $"OpenAPI operation {operation.Name} {path.Name} must not require security.");
            }
        }

        foreach (var forbiddenPath in new[]
                 {
                     "/api/login",
                     "/api/register",
                     "/api/invitations",
                     "/api/organizations"
                 })
        {
            Assert.False(root.GetProperty("paths").TryGetProperty(forbiddenPath, out _));
        }
    }

    [Fact]
    public void DemoIdsAreFixedGuidStringConstants()
    {
        Assert.Equal("11111111-1111-1111-1111-111111111111", DemoIdentity.OrganizationIdValue);
        Assert.Equal("22222222-2222-2222-2222-222222222222", DemoIdentity.UserIdValue);
        Assert.Equal(Guid.Parse(DemoIdentity.OrganizationIdValue), DemoIdentity.OrganizationId);
        Assert.Equal(Guid.Parse(DemoIdentity.UserIdValue), DemoIdentity.UserId);
    }
}
