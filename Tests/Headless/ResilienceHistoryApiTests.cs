#nullable enable
using System.Net;
using System.Net.Http;
using System.Text.Json;
using RahBuilder.Settings;
using RahBuilder.Tests.Helpers;
using Xunit;

namespace RahBuilder.Tests.Headless;

public sealed class ResilienceHistoryApiTests
{
    [Fact]
    public async void HistoryRejectsInvalidDateRange()
    {
        var config = new AppConfig { General = { ProviderApiPort = TestPortProvider.GetAvailablePort(), EnableProviderApi = true } };
        using var server = new TestServerFactory(config);
        using var client = new HttpClient();

        var response = await client.GetAsync($"http://localhost:{config.General.ProviderApiPort}/metrics/resilience/history?start=2026-01-11T02:00:00Z&end=2026-01-11T01:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("metadata", out _));
        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.True(error.TryGetProperty("code", out _));
    }
}
