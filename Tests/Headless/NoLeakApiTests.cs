#nullable enable
using System.Net.Http;
using RahBuilder.Settings;
using RahBuilder.Tests.Helpers;
using Xunit;

namespace RahBuilder.Tests.Headless;

public sealed class NoLeakApiTests
{
    [Fact]
    public async void ResponsesAreSanitized()
    {
        var config = new AppConfig { General = { ProviderApiPort = TestPortProvider.GetAvailablePort(), EnableProviderApi = true } };
        using var server = new TestServerFactory(config);
        using var client = new HttpClient();

        var response = await client.GetAsync($"http://localhost:{config.General.ProviderApiPort}/healthz");
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("WAIT_USER", body);
    }
}
