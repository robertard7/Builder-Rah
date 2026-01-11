#nullable enable
using Xunit;

namespace RahBuilder.Tests.E2E;

public sealed class RollbackTests
{
    [Fact]
    public void RollbackPlanDocumented()
    {
        Assert.True(System.IO.File.Exists("docs/rollback.md"));
    }
}
