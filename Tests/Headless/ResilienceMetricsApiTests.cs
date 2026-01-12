#nullable enable
using System.Net;
using System.Net.Http;
using System.Text.Json;
using RahBuilder.Settings;
using RahBuilder.Tests.Helpers;
using Xunit;

namespace RahBuilder.Tests.Headless;

public sealed class ResilienceMetricsApiTests
{
    [Fact]
    public async void MetricsResponse_ContainsMetadata()
    {
        var config = new AppConfig { General = { ProviderApiPort = TestPortProvider.GetAvailablePort(), EnableProviderApi = true } };
        using var server = new TestServerFactory(config);
        using var client = new HttpClient();

        var response = await client.GetAsync($"http://localhost:{config.General.ProviderApiPort}/metrics/resilience");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("metadata", out var metadata));
        Assert.True(metadata.TryGetProperty("requestId", out _));
        Assert.True(doc.RootElement.TryGetProperty("data", out _));
    }
}
