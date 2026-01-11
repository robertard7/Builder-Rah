#nullable enable
using System.Net;
using System.Net.Http;
using RahBuilder.Settings;
using RahBuilder.Tests.Helpers;
using Xunit;

namespace RahBuilder.Tests.Headless;

public sealed class ErrorResponseTests
{
    [Fact]
    public async void NotFound_Returns404()
    {
        var config = new AppConfig { General = { ProviderApiPort = TestPortProvider.GetAvailablePort(), EnableProviderApi = true } };
        using var server = new TestServerFactory(config);
        using var client = new HttpClient();

        var response = await client.GetAsync($"http://localhost:{config.General.ProviderApiPort}/sessions/missing");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
