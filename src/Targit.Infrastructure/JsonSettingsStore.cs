using System.Text.Json;
using Targit.Core;

namespace Targit.Infrastructure;

public sealed record TargitSettings(List<RepositoryInfo> Repositories, string? ActiveRepository, List<string>? Tabs = null, string ActiveTab = "History", string LeftPanel = "Repositories", string RightPanel = "Inspector", string LayoutName = "Standard");

public sealed class JsonSettingsStore
{
    private readonly string _file;

    public JsonSettingsStore(string? root = null)
    {
        var directory = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Targit");
        _file = Path.Combine(directory, "settings.json");
    }

    public async Task<TargitSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_file)) return new TargitSettings([], null);
        try
        {
            await using var stream = File.OpenRead(_file);
            var settings = await JsonSerializer.DeserializeAsync<TargitSettings>(stream, cancellationToken: ct);
            if (settings?.Repositories is null)
                throw new JsonException("Settings must contain a repository list.");
            return settings;
        }
        catch (JsonException error)
        {
            FileLog.Error(error, "Invalid settings file");
            // Preserve the invalid file for troubleshooting rather than overwriting it.
            var backup = _file + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            try { File.Move(_file, backup); }
            catch (IOException moveError) { FileLog.Error(moveError, "Back up invalid settings"); }
            catch (UnauthorizedAccessException moveError) { FileLog.Error(moveError, "Back up invalid settings"); }
            return new TargitSettings([], null);
        }
    }

    public async Task SaveAsync(TargitSettings settings, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        var temporary = _file + ".tmp";
        await using (var stream = File.Create(temporary))
            await JsonSerializer.SerializeAsync(stream, settings, cancellationToken: ct);
        File.Move(temporary, _file, true);
    }
}
