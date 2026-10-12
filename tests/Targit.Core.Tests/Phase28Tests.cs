using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class Phase28Tests
{
    [Fact]
    public void ServiceIsAvailable()
    {
        Assert.NotNull(new GitRevertService());
    }
}
