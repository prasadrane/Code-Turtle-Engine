namespace Shop.Services;

public sealed class FileExportService
{
    private readonly string _root;

    public FileExportService(string root) => _root = root;

    public async Task ExportAsync(string fileName, string content)
    {
        var safeName = Path.GetFileName(fileName);
        await File.WriteAllTextAsync(Path.Combine(_root, safeName), content).ConfigureAwait(false);
    }
}
