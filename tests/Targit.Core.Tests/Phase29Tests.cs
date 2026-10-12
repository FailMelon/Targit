using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class Phase29Tests
{
    [Fact]
    public void ServiceIsAvailable()
    {
        Assert.NotNull(new GitMergeService());
    }
}
