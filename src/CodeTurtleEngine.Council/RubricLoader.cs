namespace CodeTurtleEngine.Council;

public sealed class RubricLoader
{
    public string Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Rubric not found at '{path}'.", path);
        return File.ReadAllText(path);
    }
}
