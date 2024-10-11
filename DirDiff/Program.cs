using System.Collections.Immutable;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DirDiff;

[Flags]
public enum State
{
    None = 0,
    Equal = 1,
    HashMismatch = 2,
    LeftMissing = 4,
    RightMissing = 8,
    Missing = LeftMissing | RightMissing,
    Different = HashMismatch | Missing,
    All = Equal | Different
}

public enum Format
{
    Default,
    Text = Default,
    Csv,
    Json,
}
internal class Program
{
    private const string MISSING = "MISSING";

    static async Task Main(string[] args)
    {
        var root = new RootCommand("DirDiff - Finds discrepancies in file trees by comparing SHA256 hashes of file contents");
        var left = new Option<DirectoryInfo>(new[] { "-l", "--left" }, "The left directory to compare") { IsRequired = true };
        var right = new Option<DirectoryInfo>(new[] { "-r", "--right" }, "The right directory to compare") { IsRequired = true };
        var mode = new Option<State>(new[] { "-m", "--mode" }, () => State.Different, "Print only the selected results") { AllowMultipleArgumentsPerToken = true };
        var csv = new Option<Format>(new[] { "-f", "--format" }, () => Format.Default, "Selects the output format");
        root.Add(left);
        root.Add(right);
        root.Add(mode);
        root.Add(csv);
        root.SetHandler(CompareDirs, left, right, mode, csv);
        await root.InvokeAsync(args);
    }

    private static Task CompareDirs(DirectoryInfo left, DirectoryInfo right, State state, Format format)
        => CompareDirs(left, right, state, format, CancellationToken.None);

    private static async Task CompareDirs(DirectoryInfo left, DirectoryInfo right, State state, Format format, CancellationToken cancellationToken)
    {
        var leftTask = left.GetFiles(cancellationToken);
        var rightTask = right.GetFiles(cancellationToken);
        await Task.WhenAll(leftTask, rightTask).ConfigureAwait(false);

        var leftFileSet = await leftTask;
        var rightFileSet = await rightTask;

        var leftFiles = leftFileSet.Files.ToImmutableDictionary(f => f.Path);
        var rightFiles = rightFileSet.Files.ToImmutableDictionary(f => f.Path);

        var allFiles = rightFiles.Keys.Concat(leftFiles.Keys).Distinct();
        var differences = allFiles
            .Select(path =>
            {
                var left = leftFiles.GetValueOrDefault(path)?.Hash ?? MISSING;
                var right = rightFiles.GetValueOrDefault(path)?.Hash ?? MISSING;
                return new FileComparison(path, left, right, ComputeResult(left, right));
            })
            .OrderBy(x => x.Path)
            .ToList();


        await (format switch
        {
            Format.Csv => RenderCsv(state, leftFileSet, rightFileSet, differences),
            Format.Json => RenderJson(state, leftFileSet, rightFileSet, differences),
            _ => RenderText(state, leftFileSet, rightFileSet, differences),

        }).ConfigureAwait(false);
    }

    private static async Task RenderCsv(State state, FileSet leftFileSet, FileSet rightFileSet, List<FileComparison> differences)
    {
        await Console.Out.WriteLineAsync("Path,State,LeftHash,RightHash");
        foreach (var dif in differences)
        {
            if ((dif.State & state) != State.None)
                await Console.Out.WriteLineAsync($"{dif.Path},{dif.State},{dif.LeftHash},{dif.RightHash}").ConfigureAwait(false);
        }
    }

    private static async Task RenderJson(State state, FileSet leftFileSet, FileSet rightFileSet, List<FileComparison> differences)
    {
        var dd = differences.Where(dif => (dif.State & state) != State.None).ToList();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new
        {
            Left = leftFileSet.RootPath,
            Right = rightFileSet.RootPath,
            Files = dd
        }, options)).ConfigureAwait(false);
    }

    private static async Task RenderText(State state, FileSet leftFileSet, FileSet rightFileSet, List<FileComparison> differences)
    {
        await Console.Out.WriteLineAsync($"Left : {leftFileSet.RootPath}");
        await Console.Out.WriteLineAsync($"Right: {rightFileSet.RootPath}");
        await Console.Out.WriteLineAsync("");

        foreach (var dif in differences)
        {
            if ((dif.State & state) != State.None)
                await Console.Out.WriteLineAsync($"{dif.Path} : {dif.State.ToString().CamelCaseToSpaces()}\n\tleft  : {dif.LeftHash,-40}\n\tright : {dif.RightHash,-40}\n").ConfigureAwait(false);
        }

        await Console.Out.WriteLineAsync("");
        await Console.Out.WriteLineAsync($"{differences.Count(d => d.LeftHash != MISSING),5} files in the left directory");
        await Console.Out.WriteLineAsync($"{differences.Count(d => d.RightHash != MISSING),5} files in the right directory");
        await Console.Out.WriteLineAsync($"{differences.Count(d => d.State == State.Equal),5} equal files");
        await Console.Out.WriteLineAsync($"{differences.Count(d => d.State == State.HashMismatch),5} hash mismatches");
        await Console.Out.WriteLineAsync($"{differences.Count(d => d.State == State.RightMissing),5} files at the left with the right counterpart missing");
        await Console.Out.WriteLineAsync($"{differences.Count(d => d.State == State.LeftMissing),5} files at the right with the left counterpart missing");
        await Console.Out.WriteLineAsync($"{differences.Count,5} files total");
    }

    private static State ComputeResult(string left, string right)
    {
        var leftMissing = left == MISSING;
        var rightMissing = right == MISSING;
        var equal = left == right;
        var hashMismatch = !leftMissing && !rightMissing && !equal;

        var state = State.None;
        if (equal && !leftMissing && !rightMissing)
            state |= State.Equal;
        if (leftMissing) state |= State.LeftMissing;
        if (rightMissing) state |= State.RightMissing;
        if (hashMismatch) state |= State.HashMismatch;
        return state;
    }
}

public record FileSet(string RootPath, ImmutableList<FileData> Files);
public record FileData(string Path, string Hash);
public record FileComparison(string Path, string LeftHash, string RightHash, State State);

internal static class FileSetExtensions
{
    public static string CamelCaseToSpaces(this string s)
    {
        var sb = new StringBuilder(s.Length + s.Length / 2);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsUpper(c) && i > 0)
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();

    }
    public static async Task<FileSet> GetFiles(this DirectoryInfo root, CancellationToken cancellationToken)
    {
        var files = ImmutableList<FileData>.Empty;
        var rootPath = root.FullName;
        var rootLength = 0;
        if (rootPath.EndsWith(Path.DirectorySeparatorChar) || rootPath.EndsWith(Path.AltDirectorySeparatorChar))
            rootLength = rootPath.Length - 1;
        else
            rootLength = rootPath.Length;

        foreach (var file in root.EnumerateFiles("*", GetEnumerationOptions()))
        {
            var hash = await ComputeHash(file, cancellationToken).ConfigureAwait(false);
            var relativePath = file.FullName.Substring(rootLength);
            files = files.Add(new FileData(relativePath, hash));
        }
        return new FileSet(rootPath, files);
    }

    private static EnumerationOptions GetEnumerationOptions()
    {
        return new EnumerationOptions
        {
            IgnoreInaccessible = true,
            MatchType = MatchType.Simple,
            RecurseSubdirectories = true,
            ReturnSpecialDirectories = false,
        };
    }

    private static async Task<string> ComputeHash(FileInfo file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenRead();
        using var hash = SHA256.Create();
        var hashBytes = await hash.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return BitConverter.ToString(hashBytes).Replace("-", "");
    }
}