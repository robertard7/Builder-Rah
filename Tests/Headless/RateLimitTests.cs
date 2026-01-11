#nullable enable
using System.Net;
using System.Net.Http;
using RahBuilder.Settings;
using RahBuilder.Tests.Helpers;
using Xunit;

namespace RahBuilder.Tests.Headless;

public sealed class RateLimitTests
{
    [Fact]
    public async void RateLimit_NotConfigured_AllowsRequests()
    {
        var config = new AppConfig { General = { ProviderApiPort = TestPortProvider.GetAvailablePort(), EnableProviderApi = true } };
        using var server = new TestServerFactory(config);
        using var client = new HttpClient();

        var response = await client.GetAsync($"http://localhost:{config.General.ProviderApiPort}/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
