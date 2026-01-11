#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using RahBuilder.Settings;
using RahBuilder.Tests.Helpers;
using Xunit;

namespace RahBuilder.Tests.E2E;

public sealed class SessionE2ETests
{
    [Fact]
    public async void CreateSendCancelFlow()
    {
        var config = new AppConfig { General = { ProviderApiPort = TestPortProvider.GetAvailablePort(), EnableProviderApi = true } };
        using var server = new TestServerFactory(config);
        using var client = new HttpClient();

        var create = await client.PostAsync($"http://localhost:{config.General.ProviderApiPort}/sessions", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var createdJson = await create.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(createdJson);
        var sessionId = doc.RootElement.GetProperty("sessionId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(sessionId));

        var msgBody = new StringContent("{\"text\":\"hello\"}", Encoding.UTF8, "application/json");
        var send = await client.PostAsync($"http://localhost:{config.General.ProviderApiPort}/sessions/{sessionId}/message", msgBody);
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);

        var cancel = await client.PostAsync($"http://localhost:{config.General.ProviderApiPort}/sessions/{sessionId}/cancel", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
    }
}
