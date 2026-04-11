/// <summary>
/// Integration tests for the Credential API (GET/POST/DELETE /api/settings/credentials).
/// REF: JOB-023 T-202
/// </summary>
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Stewie.Application.Interfaces;
using Stewie.Domain.Entities;
using Stewie.Domain.Enums;
using Xunit;

namespace Stewie.Tests.Integration;

/// <summary>
/// Validates credential controller behavior: CRUD, masking, duplicate detection, auth.
/// </summary>
public class CredentialControllerTests : IClassFixture<StewieWebApplicationFactory>, IDisposable
{
    private readonly StewieWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CredentialControllerTests(StewieWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", factory.GetAuthToken());
    }

    // -------------------------------------------------------------------
    // POST → 201, credential stored with masked value
    // -------------------------------------------------------------------

    /// <summary>
    /// Adding a credential returns 201 with masked value including last 4 chars.
    /// </summary>
    [Fact]
    public async Task AddCredential_Returns201_WithMaskedValue()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/settings/credentials",
            new { credentialType = "GoogleAiApiKey", value = "AIzaSyD-test-key-1234aBcD" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = JsonSerializer.Deserialize<JsonElement>(
            await response.Content.ReadAsStringAsync());

        Assert.True(body.TryGetProperty("id", out _));
        Assert.Equal("GoogleAiApiKey", body.GetProperty("credentialType").GetString());

        var maskedValue = body.GetProperty("maskedValue").GetString()!;
        Assert.StartsWith("••••••••", maskedValue);
        Assert.EndsWith("aBcD", maskedValue);
    }

    // -------------------------------------------------------------------
    // GET → returns all credentials for the user (masked)
    // -------------------------------------------------------------------

    /// <summary>
    /// GET returns credentials added by POST, with properly masked values.
    /// </summary>
    [Fact]
    public async Task GetCredentials_ReturnsMaskedValues()
    {
        // Add a credential first
        await _client.PostAsJsonAsync(
            "/api/settings/credentials",
            new { credentialType = "AnthropicApiKey", value = "sk-ant-test-key-5678xYzW" });

        var response = await _client.GetAsync("/api/settings/credentials");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonSerializer.Deserialize<JsonElement>(
            await response.Content.ReadAsStringAsync());

        Assert.True(body.GetArrayLength() >= 1);

        // Find the Anthropic credential
        var found = false;
        foreach (var item in body.EnumerateArray())
        {
            if (item.GetProperty("credentialType").GetString() == "AnthropicApiKey")
            {
                var masked = item.GetProperty("maskedValue").GetString()!;
                Assert.StartsWith("••••••••", masked);
                Assert.EndsWith("xYzW", masked);
                Assert.True(item.TryGetProperty("createdAt", out _));
                found = true;
                break;
            }
        }
        Assert.True(found, "AnthropicApiKey credential not found in GET response.");
    }

    // -------------------------------------------------------------------
    // POST duplicate → 409 Conflict
    // -------------------------------------------------------------------

    /// <summary>
    /// Adding a second credential of the same type returns 409 Conflict.
    /// </summary>
    [Fact]
    public async Task AddCredential_DuplicateType_Returns409()
    {
        // Add first credential
        var first = await _client.PostAsJsonAsync(
            "/api/settings/credentials",
            new { credentialType = "OpenAiApiKey", value = "sk-openai-first-key-1111" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Try to add same type again
        var second = await _client.PostAsJsonAsync(
            "/api/settings/credentials",
            new { credentialType = "OpenAiApiKey", value = "sk-openai-second-key-2222" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // -------------------------------------------------------------------
    // DELETE → 204, credential removed
    // -------------------------------------------------------------------

    /// <summary>
    /// Deleting a credential returns 204 and it no longer appears in GET.
    /// </summary>
    [Fact]
    public async Task DeleteCredential_Returns204_RemovedFromList()
    {
        // Add a credential
        var addResponse = await _client.PostAsJsonAsync(
            "/api/settings/credentials",
            new { credentialType = "GitHubPat", value = "ghp_test1234567890abcdefg" });

        // Get the ID from the response — handle both 201 (new) and 409 (existing)
        Guid credentialId;
        if (addResponse.StatusCode == HttpStatusCode.Conflict)
        {
            // Credential already exists from another test; find it via GET
            var listResponse = await _client.GetAsync("/api/settings/credentials");
            var listBody = JsonSerializer.Deserialize<JsonElement>(
                await listResponse.Content.ReadAsStringAsync());
            credentialId = Guid.Empty;
            foreach (var item in listBody.EnumerateArray())
            {
                if (item.GetProperty("credentialType").GetString() == "GitHubPat")
                {
                    credentialId = Guid.Parse(item.GetProperty("id").GetString()!);
                    break;
                }
            }
            Assert.NotEqual(Guid.Empty, credentialId);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
            var addBody = JsonSerializer.Deserialize<JsonElement>(
                await addResponse.Content.ReadAsStringAsync());
            credentialId = Guid.Parse(addBody.GetProperty("id").GetString()!);
        }

        // Delete it
        var deleteResponse = await _client.DeleteAsync($"/api/settings/credentials/{credentialId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify it's gone from GET
        var getResponse = await _client.GetAsync("/api/settings/credentials");
        var getBody = JsonSerializer.Deserialize<JsonElement>(
            await getResponse.Content.ReadAsStringAsync());

        foreach (var item in getBody.EnumerateArray())
        {
            Assert.NotEqual(credentialId.ToString(), item.GetProperty("id").GetString());
        }
    }

    // -------------------------------------------------------------------
    // DELETE nonexistent → 404
    // -------------------------------------------------------------------

    /// <summary>
    /// Deleting a nonexistent credential returns 404.
    /// </summary>
    [Fact]
    public async Task DeleteCredential_NotFound_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/settings/credentials/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -------------------------------------------------------------------
    // POST invalid type → 400
    // -------------------------------------------------------------------

    /// <summary>
    /// Adding a credential with an invalid type name returns 400.
    /// </summary>
    [Fact]
    public async Task AddCredential_InvalidType_Returns400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/settings/credentials",
            new { credentialType = "InvalidProviderXyz", value = "some-value" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------
    // POST empty value → 400
    // -------------------------------------------------------------------

    /// <summary>
    /// Adding a credential with empty value returns 400.
    /// </summary>
    [Fact]
    public async Task AddCredential_EmptyValue_Returns400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/settings/credentials",
            new { credentialType = "GoogleAiApiKey", value = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -------------------------------------------------------------------
    // Unauthenticated → 401
    // -------------------------------------------------------------------

    /// <summary>
    /// GET without JWT returns 401.
    /// </summary>
    [Fact]
    public async Task GetCredentials_Unauthenticated_Returns401()
    {
        var unauthenticatedClient = _factory.CreateClient();

        var response = await unauthenticatedClient.GetAsync("/api/settings/credentials");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        unauthenticatedClient.Dispose();
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
