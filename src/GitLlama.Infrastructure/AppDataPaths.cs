namespace GitLlama.Infrastructure;

public static class AppDataPaths
{
    public static string SettingsDirectory(string? applicationData = null)
    {
        var root = applicationData ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directory = Path.Combine(root, "GitLlama");
        var legacy = Path.Combine(root, "Targit");
        var marker = Path.Combine(directory, ".legacy-imported");
        if (Directory.Exists(legacy) && !File.Exists(marker))
        {
            Directory.CreateDirectory(directory);
            foreach (var name in new[] { "settings.json", "workspace.json" })
            {
                var source = Path.Combine(legacy, name);
                var destination = Path.Combine(directory, name);
                if (File.Exists(source) && !File.Exists(destination))
                {
                    File.Copy(source, destination, overwrite: false);
                }
            }

            File.WriteAllText(marker, "Imported existing TaRGit preferences; the original files are preserved.");
        }

        return directory;
    }
}
