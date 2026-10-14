using GitLlama.Desktop.ViewModels;
using Xunit;

namespace GitLlama.Desktop.Tests;

public sealed class InspectorTests
{
    [Theory]
    [InlineData("Fix typo", "")]
    [InlineData("Fix typo\n", "")]
    [InlineData("Add avatars\n\nShow author pictures.\n\nCo-Authored-By: Someone", "Show author pictures.\n\nCo-Authored-By: Someone")]
    [InlineData("Add avatars\r\n\r\nShow author pictures.\r\n", "Show author pictures.")]
    [InlineData("Subject that wraps\nonto a second line\n\nBody", "Body")]
    public void CommitDescriptionExcludesTheSubject(string message, string expected) =>
        Assert.Equal(expected, MainWindowViewModel.CommitDescription(message));
}
