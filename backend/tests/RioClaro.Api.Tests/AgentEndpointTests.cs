using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RioClaro.Application.Agents;
using Xunit;

namespace RioClaro.Api.Tests;

public sealed class AgentEndpointTests
{
    private const string TestAgentToolsKey =
        "test-only-agent-tool-key-32-characters";

    [Fact]
    public async Task ChatReturnsNotConfiguredWhenFoundryIsNotConfigured()
    {
        using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/chat",
            new { message = "Resume las ventas del mes." });
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("chat_not_configured", body.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ChatRejectsEmptyMessages(string message)
    {
        using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/chat", new { message });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChatRejectsConversationIdUntilConversationPersistenceExists()
    {
        using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/chat",
            new { message = "Consulta ficticia", conversationId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChatReturnsFoundryAgentResponseWithoutAzureCredentialsInApi()
    {
        using var factory = new RioClaroApiFactory(configureTestServices: services =>
        {
            services.RemoveAll<IBusinessAgentClient>();
            services.AddSingleton<IBusinessAgentClient>(new TestBusinessAgentClient(
                new AgentChatResponse("Respuesta de prueba", [])));
        });
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/chat",
            new { message = "Consulta ficticia" });
        var result = await response.Content.ReadFromJsonAsync<AgentChatResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Respuesta de prueba", result?.Reply);
        Assert.Empty(result?.Sources ?? []);
    }

    [Fact]
    public async Task AgentToolEndpointsFailClosedWhenApiKeyIsNotConfigured()
    {
        using var factory = new RioClaroApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/agent-tools/v1/integrations/health");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(
            "agent_tools_not_configured",
            body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AgentToolEndpointsRequireBearerKeyAndReturnOnlyConfiguredHealth()
    {
        using var factory = new RioClaroApiFactory(agentToolsApiKey: TestAgentToolsKey);
        using var client = factory.CreateClient();

        using var deniedResponse = await client.GetAsync(
            "/api/agent-tools/v1/integrations/health");
        Assert.Equal(HttpStatusCode.Unauthorized, deniedResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAgentToolsKey);
        using var response = await client.GetAsync("/api/agent-tools/v1/integrations/health");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "NotConfigured",
            body.RootElement.GetProperty("agents").GetProperty("businessAgent")
                .GetProperty("status").GetString());
        Assert.False(body.RootElement.GetProperty("blobStorageConfigured").GetBoolean());
    }

    private sealed class TestBusinessAgentClient(AgentChatResponse response)
        : IBusinessAgentClient
    {
        public bool IsConfigured => true;

        public Task<AgentChatResponse> SendMessageAsync(
            string message,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(response);
    }
}
