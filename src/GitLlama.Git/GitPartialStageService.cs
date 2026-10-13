using System.Text;
using System.Text.RegularExpressions;
using LibGit2Sharp;

namespace GitLlama.Git;

public sealed record GitPatchLine(int Index, string Text, string Kind, string Hunk, int? OldNumber = null, int? NewNumber = null)
{
    public bool IsChange => Kind is "Added" or "Deleted";
    public string Color => Kind == "Added" ? "#4EC9B0" : Kind == "Deleted" ? "#F48771" : Kind == "Hunk" ? "#80B8E0" : "#A6A6A6";
    public override string ToString() => Text;
}

/// <summary>Partial staging writes only the index; selection is tied to an exact patch snapshot.</summary>
public sealed partial class GitPartialStageService
{
    [GeneratedRegex(@"^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@")]
    private static partial Regex HunkHeader();

    public static IReadOnlyList<GitPatchLine> Lines(string patch)
    {
        var hunk = ""; var oldLine = 0; var newLine = 0;
        return patch.Split('\n').Select((text, index) =>
        {
            if (text.StartsWith("@@ ", StringComparison.Ordinal)) { hunk = text; var match = HunkHeader().Match(text); if (match.Success) { oldLine = int.Parse(match.Groups[1].Value); newLine = int.Parse(match.Groups[2].Value); } }
            var kind = text.StartsWith("@@ ", StringComparison.Ordinal) ? "Hunk" :
                text.StartsWith("+++", StringComparison.Ordinal) || text.StartsWith("---", StringComparison.Ordinal) || hunk.Length == 0 ? "Metadata" :
                text.StartsWith('+') ? "Added" : text.StartsWith('-') ? "Deleted" : "Context";
            int? before = kind is "Deleted" or "Context" ? oldLine++ : null;
            int? after = kind is "Added" or "Context" ? newLine++ : null;
            if (text.StartsWith("\\ No newline", StringComparison.Ordinal)) { before = after = null; oldLine--; newLine--; }
            return new GitPatchLine(index, text, kind, hunk, before, after);
        }).Where(line => line.Text.Length > 0).ToArray();
    }

    public async Task ApplyAsync(string repositoryPath, string relativePath, string expectedPatch, IReadOnlyCollection<int> selectedLines,
        bool unstage = false, CancellationToken ct = default)
    {
        var current = await new GitDiffService().GetFileDiffAsync(repositoryPath, relativePath, ct);
        var patch = unstage ? current.StagedPatch : current.UnstagedPatch;
        if (!string.Equals(patch, expectedPatch, StringComparison.Ordinal))
            throw new InvalidOperationException("The file changed since this diff was loaded. Refresh and review it again.");
        await Task.Run(() => Apply(repositoryPath, relativePath, patch, selectedLines, unstage, ct), ct);
    }

    private static void Apply(string path, string relativePath, string patch, IReadOnlyCollection<int> selection, bool reverse, CancellationToken ct)
    {
        if (!Lines(patch).Any(line => line.IsChange && selection.Contains(line.Index))) throw new InvalidOperationException("Select at least one added or deleted line.");
        using var repo = new Repository(path);
        if (repo.Info.IsBare || repo.Index.Conflicts.Any()) throw new InvalidOperationException("Resolve conflicts before staging selected lines.");
        var normalized = relativePath.Replace('\\', '/');
        if (Path.IsPathRooted(normalized) || normalized.Split('/').Any(part => part is "." or "..")) throw new ArgumentException("Choose a repository-relative file.");
        var index = repo.Index[normalized];
        if (index is not null && index.Mode is not (Mode.NonExecutableFile or Mode.ExecutableFile))
            throw new InvalidOperationException("Partial staging supports regular text files.");
        var absolute = GitPathGuard.RegularWorkingFile(repo, normalized);
        using var currentPatch = reverse ? repo.Diff.Compare<Patch>(repo.Head.Tip?.Tree, DiffTargets.Index, [normalized]) : repo.Diff.Compare<Patch>([normalized], includeUntracked: true);
        if (currentPatch.Content != patch) throw new InvalidOperationException("The diff changed before staging. Refresh and review it again.");
        var baseBytes = index is null ? [] : ReadBlob(repo.Lookup<Blob>(index.Id)!);
        byte[] sourceBytes;
        bool sourceExists;
        if (reverse)
        {
            var entry = repo.Head.Tip?.Tree[normalized];
            if (entry is not null && entry.Target is not Blob) throw new InvalidOperationException("Partial unstaging supports regular files.");
            sourceExists = entry is not null;
            sourceBytes = entry?.Target is Blob blob ? ReadBlob(blob) : [];
        }
        else
        {
            sourceExists = File.Exists(absolute);
            if (sourceExists && new FileInfo(absolute).Length > 4 * 1024 * 1024) throw new InvalidOperationException("Use whole-file staging for files larger than 4 MB.");
            sourceBytes = sourceExists ? File.ReadAllBytes(absolute) : [];
        }
        var encoding = new UTF8Encoding(false, true);
        if (baseBytes.Contains((byte)0) || sourceBytes.Contains((byte)0)) throw new InvalidOperationException("Use whole-file staging for binary files.");
        var baseline = SplitLines(encoding.GetString(baseBytes));
        var source = SplitLines(encoding.GetString(sourceBytes));
        var output = new List<string>();
        var oldPosition = 0;
        var newPosition = 0;
        var inHunk = false;
        var lines = patch.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var line = lines[i];
            var header = HunkHeader().Match(line);
            if (header.Success)
            {
                var oldStart = int.Parse(header.Groups[reverse ? 2 : 1].Value);
                var newStart = int.Parse(header.Groups[reverse ? 1 : 2].Value);
                var start = Math.Max(0, oldStart - 1);
                while (oldPosition < start) output.Add(baseline[oldPosition++]);
                newPosition = Math.Max(0, newStart - 1);
                inHunk = true;
                continue;
            }
            if (!inHunk || line.Length == 0 || line[0] == '\\') continue;
            var kind = line[0];
            if (reverse) kind = kind == '+' ? '-' : kind == '-' ? '+' : kind;
            if (kind == ' ')
            {
                if (oldPosition >= baseline.Count) throw new InvalidOperationException("Diff context no longer matches the index.");
                output.Add(baseline[oldPosition++]); newPosition++;
            }
            else if (kind == '-')
            {
                if (oldPosition >= baseline.Count) throw new InvalidOperationException("Diff context no longer matches the index.");
                if (!selection.Contains(i)) output.Add(baseline[oldPosition]);
                oldPosition++;
            }
            else if (kind == '+')
            {
                if (selection.Contains(i))
                {
                    if (newPosition >= source.Count) throw new InvalidOperationException("Diff context no longer matches the file.");
                    output.Add(source[newPosition]);
                }
                newPosition++;
            }
        }
        while (oldPosition < baseline.Count) output.Add(baseline[oldPosition++]);
        ct.ThrowIfCancellationRequested();
        if (!reverse && sourceExists && !File.ReadAllBytes(absolute).SequenceEqual(sourceBytes))
            throw new InvalidOperationException("The working file changed during staging. Refresh and review it again.");
        if (output.Count == 0 && !sourceExists) repo.Index.Remove(normalized);
        else
        {
            using var stream = new MemoryStream(encoding.GetBytes(string.Concat(output)));
            var blob = repo.ObjectDatabase.CreateBlob(stream, normalized);
            repo.Index.Add(blob, normalized, index?.Mode ?? Mode.NonExecutableFile);
        }
        repo.Index.Write();
    }
    private static byte[] ReadBlob(Blob blob)
    {
        if (blob.Size > 4 * 1024 * 1024) throw new InvalidOperationException("Use whole-file staging for files larger than 4 MB.");
        using var stream = blob.GetContentStream();
        using var buffer = new MemoryStream(); stream.CopyTo(buffer); return buffer.ToArray();
    }
    private static List<string> SplitLines(string text)
    {
        var result = new List<string>(); var start = 0;
        for (var i = 0; i < text.Length; i++) if (text[i] == '\n') { result.Add(text[start..(i + 1)]); start = i + 1; }
        if (start < text.Length) result.Add(text[start..]);
        return result;
    }
}
