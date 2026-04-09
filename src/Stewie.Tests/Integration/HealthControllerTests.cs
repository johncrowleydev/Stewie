/// <summary>
/// Integration tests for the Health endpoint.
/// Verifies response schema matches CON-002 §4.4, §5.4.
///
/// REF: CON-002 §4.4, §5.4
/// REF: GOV-002 (testing protocol)
/// </summary>
using System.Net;
using System.Text.Json;
using Xunit;

namespace Stewie.Tests.Integration;

/// <summary>
/// Verifies the /health endpoint returns correct status, version, and timestamp.
/// </summary>
public class HealthControllerTests : IClassFixture<StewieWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;

    public HealthControllerTests(StewieWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>GET /health returns 200 with status, version, and timestamp.</summary>
    [Fact]
    public async Task GetHealth_Returns200WithExpectedSchema()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<JsonElement>(body);

        // CON-002 §5.4: must have status, version, timestamp
        Assert.Equal("healthy", doc.GetProperty("status").GetString());
        Assert.True(doc.TryGetProperty("version", out var version));
        Assert.False(string.IsNullOrEmpty(version.GetString()));
        Assert.True(doc.TryGetProperty("timestamp", out _));
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
