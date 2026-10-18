using System.Formats.Tar;

namespace CodeTurtleEngine.Worker;

internal static class SourceArchive
{
    public static void Extract(string archive, string destination)
    {
        Directory.CreateDirectory(destination);
        using var input = File.OpenRead(archive);
        using var reader = new TarReader(input);
        long bytes = 0;
        int files = 0;
        while (reader.GetNextEntry() is { } entry)
        {
            if (++files > 10000 || (bytes += entry.Length) > 64 * 1024 * 1024)
                throw new InvalidDataException("Archive limits exceeded.");
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.Directory))
                throw new InvalidDataException("Links and special archive entries are forbidden.");
            var relative = entry.Name.Replace('\\', '/');
            if (Path.IsPathRooted(relative) || relative.Split('/').Any(p => p == "..") || relative.Contains(':'))
                throw new InvalidDataException("Unsafe archive entry.");
            var path = Path.GetFullPath(Path.Combine(destination, relative));
            if (!path.StartsWith(destination + "/", StringComparison.Ordinal) && path != destination)
                throw new InvalidDataException("Archive escape.");
            if (entry.EntryType == TarEntryType.Directory) Directory.CreateDirectory(path);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var output = new FileStream(path, FileMode.CreateNew);
                entry.DataStream?.CopyTo(output);
            }
        }
    }
}
