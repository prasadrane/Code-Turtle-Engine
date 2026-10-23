using System.Text.Json;
using CodeTurtleEngine.CitationDemo;

var json = JsonSerializer.Serialize(DemoScenario.Run(), DemoScenario.JsonOptions) + "\n";
if (args.Length == 2 && args[0] == "--out")
{
    var path = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, json);
    Console.WriteLine($"Wrote authored-fixture evidence to {path}");
}
else if (args.Length == 0) Console.Write(json);
else { Console.Error.WriteLine("Usage: citation-demo [--out <path>]"); return 1; }
return 0;
