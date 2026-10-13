namespace GitLlama.Core;

public sealed record AzureDevOpsRemote(string Organization, string Project, string Repository)
{
    public static AzureDevOpsRemote? Parse(string url)
    {
        if (url.StartsWith("git@ssh.dev.azure.com:v3/", StringComparison.OrdinalIgnoreCase)) return Parts(url[25..].Split('/').Select(Uri.UnescapeDataString).ToArray());
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "ssh") || !uri.IsDefaultPort) return null;
        var parts = uri.AbsolutePath.Trim('/').Split('/').Select(Uri.UnescapeDataString).ToArray();
        if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase) && parts.Length == 4 && parts[2] == "_git") return Parts([parts[0], parts[1], parts[3]]);
        if (uri.Scheme == "ssh" && uri.Host.Equals("ssh.dev.azure.com", StringComparison.OrdinalIgnoreCase) && parts.Length == 4 && parts[0] == "v3") return Parts(parts[1..]);
        if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase) && parts.Length >= 3 && parts[^2] == "_git") return Parts([uri.Host[..^17], parts[^3], parts[^1]]);
        return null;
    }
    private static AzureDevOpsRemote? Parts(string[] parts) => parts.Length == 3 && parts.All(p => !string.IsNullOrWhiteSpace(p) && p is not "." and not ".." && p.IndexOfAny(['/', '\\', '\r', '\n']) < 0) && ValidOrganization(parts[0]) ? new(parts[0], parts[1], parts[2]) : null;
    public static bool ValidOrganization(string value) => value.Length is > 0 and <= 64 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public static string NormalizeOrganization(string input)
    {
        var organization = input.Trim();
        if (Uri.TryCreate(organization, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment))
        {
            if (uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase)) organization = uri.AbsolutePath.Trim('/');
            else if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase)) organization = uri.Host[..^17];
        }
        if (!ValidOrganization(organization)) throw new ArgumentException("Enter your Azure DevOps organization name or https://dev.azure.com/organization URL.");
        return organization;
    }
    public static string? BearerScope(string remoteUrl, string organization)
    {
        var target = Parse(remoteUrl);
        if (target is null || !target.Organization.Equals(organization, StringComparison.OrdinalIgnoreCase) || !Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Contains(':')) return null;
        return uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase) ? "https://dev.azure.com/" + Uri.EscapeDataString(target.Organization) + "/" : "https://" + uri.Host + "/";
    }
}
