using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class Phase28Tests
{
    [Fact]
    public void ServiceIsAvailable()
    {
        Assert.NotNull(new GitRevertService());
    }
}
