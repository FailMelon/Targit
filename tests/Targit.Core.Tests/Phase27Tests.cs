using LibGit2Sharp;
using Targit.Git;
using Xunit;

namespace Targit.Core.Tests;

public sealed class Phase27Tests
{
    [Fact]
    public void ServiceIsAvailable()
    {
        Assert.NotNull(new GitCherryPickService());
    }
}
