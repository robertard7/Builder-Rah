#nullable enable
using RahBuilder.Workflow;
using Xunit;

namespace RahBuilder.Tests.Ui;

public sealed class ChatSanitizationTests
{
    [Fact]
    public void Sanitizer_RemovesForbiddenTokens()
    {
        var input = "WAIT_USER tooling warnings should not leak";
        var output = OutputSanitizer.Sanitize(input);

        Assert.DoesNotContain("WAIT_USER", output);
        Assert.DoesNotContain("tooling warnings", output, System.StringComparison.OrdinalIgnoreCase);
    }
}
