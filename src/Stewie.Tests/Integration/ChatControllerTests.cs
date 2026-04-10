/// <summary>
/// Integration tests for the Chat API (GET/POST /api/projects/{id}/chat).
/// REF: JOB-013 T-134
/// </summary>
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Xunit;

namespace Stewie.Tests.Integration;

/// <summary>
/// Validates chat controller behavior: persistence, pagination, validation, and auth.
/// </summary>
public class ChatControllerTests : IClassFixture<StewieWebApplicationFactory>, IDisposable
{
    private readonly StewieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ChatControllerTests(StewieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.GetAuthToken());
    }

    /// <summary>Seeds a project and returns its ID.</summary>
    private async Task<Guid> SeedProjectAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var projectRepo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = $"Chat Test {Guid.NewGuid().ToString()[..8]}",
            RepoUrl = $"https://github.com/test/{Guid.NewGuid()}",
            CreatedAt = DateTime.UtcNow
        };

        uow.BeginTransaction();
        await projectRepo.SaveAsync(project);

        // Seed an active Architect session so chat messages are accepted (JOB-020)
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IAgentSessionRepository>();
        var architectSession = new AgentSession
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            AgentRole = "architect",
            RuntimeName = "stub",
            ContainerId = "test-fake-container",
            Status = Stewie.Domain.Enums.AgentSessionStatus.Active,
            StartedAt = DateTime.UtcNow
        };
        await sessionRepo.SaveAsync(architectSession);

        await uow.CommitAsync();
        return project.Id;
    }

    // -------------------------------------------------------------------
    // POST → 201, then GET returns the message
    // -------------------------------------------------------------------

    /// <summary>
    /// Sending a message returns 201 and the message appears in GET history.
    /// </summary>
    [Fact]
    public async Task SendMessage_Returns201_PersistsMessage()
    {
        var projectId = await SeedProjectAsync();

        var postResponse = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/chat",
            new { content = "Hello architect!" });

        if (!postResponse.IsSuccessStatusCode) throw new Exception(await postResponse.Content.ReadAsStringAsync());

        var postBody = JsonSerializer.Deserialize<JsonElement>(
            await postResponse.Content.ReadAsStringAsync());
        Assert.Equal("Human", postBody.GetProperty("senderRole").GetString());
        Assert.Equal("Hello architect!", postBody.GetProperty("content").GetString());
        Assert.True(postBody.TryGetProperty("id", out _));

        // Verify GET returns the message
        var getResponse = await _client.GetAsync($"/api/projects/{projectId}/chat");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getBody = JsonSerializer.Deserialize<JsonElement>(
            await getResponse.Content.ReadAsStringAsync());
        var messages = getBody.GetProperty("messages");
        Assert.True(messages.GetArrayLength() >= 1);

        var total = getBody.GetProperty("total").GetInt32();
        Assert.True(total >= 1);
    }

    // -------------------------------------------------------------------
    // GET empty project → empty list
    // -------------------------------------------------------------------

    /// <summary>
    /// GET messages for a project with no messages returns empty array and total=0.
    /// </summary>
    [Fact]
    public async Task GetMessages_EmptyProject_ReturnsEmptyList()
    {
        var projectId = await SeedProjectAsync();

        var response = await _client.GetAsync($"/api/projects/{projectId}/chat");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonSerializer.Deserialize<JsonElement>(
            await response.Content.ReadAsStringAsync());
        Assert.Equal(0, body.GetProperty("messages").GetArrayLength());
        Assert.Equal(0, body.GetProperty("total").GetInt32());
    }

    // -------------------------------------------------------------------
    // Pagination
    // -------------------------------------------------------------------

    /// <summary>
    /// Sending 5 messages then querying with limit=2, offset=2 returns the correct slice.
    /// </summary>
    [Fact]
    public async Task GetMessages_Pagination_RespectsLimitOffset()
    {
        var projectId = await SeedProjectAsync();

        // Send 5 messages
        for (int i = 1; i <= 5; i++)
        {
            await _client.PostAsJsonAsync(
                $"/api/projects/{projectId}/chat",
                new { content = $"Message {i}" });
        }

        // Query with limit=2, offset=2 → should get messages 3 and 4
        var response = await _client.GetAsync(
            $"/api/projects/{projectId}/chat?limit=2&offset=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonSerializer.Deserialize<JsonElement>(
            await response.Content.ReadAsStringAsync());
        var messages = body.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal(5, body.GetProperty("total").GetInt32());
    }

    // -------------------------------------------------------------------
    // Invalid project → 404
    // -------------------------------------------------------------------

    /// <summary>
    /// POST to a nonexistent project returns 404.
    /// </summary>
    [Fact]
    public async Task SendMessage_InvalidProject_Returns404()
    {
        var fakeProjectId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{fakeProjectId}/chat",
            new { content = "Should not persist" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------
    // Empty content → 400
    // -------------------------------------------------------------------

    /// <summary>
    /// POST with empty content returns 400.
    /// </summary>
    [Fact]
    public async Task SendMessage_EmptyContent_Returns400()
    {
        var projectId = await SeedProjectAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/chat",
            new { content = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------
    // Unauthenticated → 401
    // -------------------------------------------------------------------

    /// <summary>
    /// GET without JWT returns 401.
    /// </summary>
    [Fact]
    public async Task GetMessages_Unauthenticated_Returns401()
    {
        var unauthenticatedClient = _factory.CreateClient();
        // No authorization header

        var response = await unauthenticatedClient.GetAsync(
            $"/api/projects/{Guid.NewGuid()}/chat");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        unauthenticatedClient.Dispose();
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
