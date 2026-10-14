using GitLlama.Desktop.ViewModels;
using Xunit;

namespace GitLlama.Desktop.Tests;

public sealed class StashTreeTests
{
    [Theory]
    [InlineData("On main: half-done refactor", "main", "half-done refactor")]
    [InlineData("WIP on feature/login: 1a2b3c4 Add login form", "feature/login", "WIP: Add login form")]
    [InlineData("On main: ", "main", "(no message)")]
    [InlineData("custom message", "Other", "custom message")]
    public void DescribeStashGroupsByBranch(string message, string branch, string description) =>
        Assert.Equal((branch, description), MainWindowViewModel.DescribeStash(message));
}
