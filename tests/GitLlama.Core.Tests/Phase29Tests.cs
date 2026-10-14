using GitLlama.Git;
using LibGit2Sharp;
using Xunit;

namespace GitLlama.Core.Tests;

public sealed class Phase29Tests
{
    [Fact]
    public void ServiceIsAvailable()
    {
        Assert.NotNull(new GitMergeService());
    }
}
