#nullable enable
using RahBuilder.Common.Text;
using Xunit;

namespace RahBuilder.Tests.Ui;

public sealed class NoLeakSnapshotTests
{
    [Fact]
    public void ForbiddenTokensAreStripped()
    {
        var input = "WAIT_USER tools invalid";
        var output = UserSafeText.Sanitize(input);
        Assert.DoesNotContain("WAIT_USER", output);
        Assert.DoesNotContain("tools invalid", output, System.StringComparison.OrdinalIgnoreCase);
    }
}
