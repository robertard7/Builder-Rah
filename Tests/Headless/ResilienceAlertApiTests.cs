#nullable enable
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using RahBuilder.Settings;
using RahBuilder.Tests.Helpers;
using Xunit;

namespace RahBuilder.Tests.Headless;

public sealed class ResilienceAlertApiTests
{
    [Fact]
    public async void AlertRuleLifecycle_Works()
    {
        var config = new AppConfig { General = { ProviderApiPort = TestPortProvider.GetAvailablePort(), EnableProviderApi = true } };
        using var server = new TestServerFactory(config);
        using var client = new HttpClient();

        var createResponse = await client.PostAsJsonAsync(
            $"http://localhost:{config.General.ProviderApiPort}/alerts",
            new { name = "test", openThreshold = 1, retryThreshold = 0, windowMinutes = 30, severity = "warning" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var createdDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var ruleId = createdDoc.RootElement.GetProperty("data").GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(ruleId));

        using var patchRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"http://localhost:{config.General.ProviderApiPort}/alerts/{ruleId}")
        {
            Content = JsonContent.Create(new { openThreshold = 2, enabled = false })
        };
        var patchResponse = await client.SendAsync(patchRequest);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var listResponse = await client.GetAsync($"http://localhost:{config.General.ProviderApiPort}/alerts?severity=warning");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listDoc = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var rules = listDoc.RootElement.GetProperty("rules");
        Assert.True(rules.GetArrayLength() > 0);
        Assert.True(rules[0].TryGetProperty("recentEvents", out _));

        var deleteResponse = await client.DeleteAsync($"http://localhost:{config.General.ProviderApiPort}/alerts?ruleId={ruleId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
    }
}
