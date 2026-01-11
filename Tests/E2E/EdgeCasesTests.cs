#nullable enable
using System.Net;
using System.Net.Http;
using System.Text;
using RahBuilder.Settings;
using RahBuilder.Tests.Helpers;
using Xunit;

namespace RahBuilder.Tests.E2E;

public sealed class EdgeCasesTests
{
    [Fact]
    public async void BadRequest_Returns400()
    {
        var config = new AppConfig { General = { ProviderApiPort = TestPortProvider.GetAvailablePort(), EnableProviderApi = true } };
        using var server = new TestServerFactory(config);
        using var client = new HttpClient();

        var response = await client.PostAsync($"http://localhost:{config.General.ProviderApiPort}/sessions/abc/message", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
