using CodeTurtleEngine.Cli;
using CodeTurtleEngine.Core;
using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Cli.Tests;

public class ExtractionPipelineTests
{
    [Fact]
    public async Task Extracts_Payload_To_Json_Without_Llm_Calls()
    {
        var tempOut = Path.GetTempFileName();
        try
        {
            var tree = CSharpSyntaxTree.ParseText("namespace Demo; public class A { public void M() {} }", path: "/repo/A.cs");
            var compilation = CSharpCompilation.Create("Demo", new[] { tree });
            var loader = new FakeLoader(compilation);
            var diff = new FakeDiff(new[] { "A.cs" });

            var extractor = new ExtractionPipeline(diff, loader);
            var payload = await extractor.ExtractAsync("/repo", "/repo/Demo.csproj", null);
            var json = extractor.SerializePayload(payload);

            Assert.Equal("Demo", payload.RepoSlug);
            Assert.Contains("\"RepoSlug\": \"Demo\"", json);
            Assert.Contains("Demo.A", json);
        }
        finally
        {
            if (File.Exists(tempOut)) File.Delete(tempOut);
        }
    }
}
