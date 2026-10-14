using GitLlama.Desktop.ViewModels;
using Xunit;

namespace GitLlama.Desktop.Tests;

public sealed class AvatarTests
{
    [Theory]
    [InlineData("12345+octocat@users.noreply.github.com", "https://avatars.githubusercontent.com/u/12345?s=64")]
    [InlineData("octocat@users.noreply.github.com", "https://github.com/octocat.png?size=64")]
    [InlineData("", null)]
    [InlineData("not-an-email", null)]
    public void AuthorAvatarUrlUsesGitHubAccountsWhenKnown(string email, string? expected) =>
        Assert.Equal(expected, AvatarCache.AuthorAvatarUrl(email));

    [Fact]
    public void AuthorAvatarUrlFallsBackToNormalisedGravatarHash()
    {
        var url = AvatarCache.AuthorAvatarUrl("  Someone@Example.COM ");
        Assert.Equal(AvatarCache.AuthorAvatarUrl("someone@example.com"), url);
        Assert.StartsWith("https://www.gravatar.com/avatar/", url);
        Assert.EndsWith("?s=64&d=404", url);
    }

    [Theory]
    [InlineData("https://github.com/FailMelon/GitLlama.git", "https://github.com/FailMelon.png?size=64")]
    [InlineData("git@github.com:FailMelon/GitLlama.git", "https://github.com/FailMelon.png?size=64")]
    [InlineData("git@gitlab.com:group/project.git", "https://gitlab.com/favicon.ico")]
    [InlineData("https://dev.azure.com/org/project/_git/repo", "https://dev.azure.com/favicon.ico")]
    [InlineData("git@ssh.dev.azure.com:v3/org/project/repo", "https://dev.azure.com/favicon.ico")]
    [InlineData("C:/repos/local", null)]
    [InlineData("../sibling", null)]
    [InlineData("http://localhost:3000/repo.git", null)]
    public void RemoteAvatarUrlPrefersOwnerPicturesThenHostIcons(string remote, string? expected) =>
        Assert.Equal(expected, AvatarCache.RemoteAvatarUrl(remote));

    [Theory]
    [InlineData("""{"author":{"login":"FailMelon","avatar_url":"https://avatars.githubusercontent.com/u/3711395?v=4"}}""", "https://avatars.githubusercontent.com/u/3711395?v=4&s=64")]
    [InlineData("""{"author":null}""", null)]
    [InlineData("""{"author":{"avatar_url":"https://evil.example/u/1"}}""", null)]
    public void GitHubCommitAuthorPictureComesOnlyFromGitHub(string json, string? expected)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(expected, AvatarCache.GitHubAvatarFromCommit(document.RootElement));
    }

    [Fact]
    public void InitialsAreAvailableWithoutNetwork()
    {
        var avatar = AvatarCache.ForAuthor("Ada Lovelace", "ada@example.com");
        Assert.Equal("AL", avatar.Initials);
        Assert.False(avatar.HasImage);
    }
}
