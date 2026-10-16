using CodeTurtleEngine.Gatekeeper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Gatekeeper.Tests;

public class RiskDetectorTests
{
    private static IReadOnlyList<CodeTurtleEngine.Core.MethodFacts> Analyze(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "Test.cs");
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();
        var comp = CSharpCompilation.Create("TestAsm", new[] { tree }, refs);
        return new SemanticAnalyzer(comp).AnalyzeTree(tree);
    }

    [Fact]
    public void Detects_Unawaited_Task_And_SyncOverAsync_And_SwallowedException()
    {
        const string code = """
        using System;
        using System.Threading.Tasks;
        namespace Demo;
        public class Svc
        {
            public async Task<int> WorkAsync()
            {
                try
                {
                    Task.Delay(10); // line 10: unawaited
                    var x = GetValAsync().Result; // line 11: sync-over-async
                    return x;
                }
                catch (Exception)
                {
                    return 0;
                }
            }
            private Task<int> GetValAsync() => Task.FromResult(42);
        }
        """;
        var m = Analyze(code).First(f => f.Method == "WorkAsync");
        Assert.NotNull(m.Risks);
        Assert.Contains(m.Risks!, r => r.Contains("Unawaited Task") && r.Contains("Line 10"));
        Assert.Contains(m.Risks!, r => r.Contains("Sync-over-async") && r.Contains("Line 11"));
        Assert.Contains(m.Risks!, r => r.Contains("Swallowed exception") && r.Contains("Line 14"));
    }

    [Fact]
    public void Detects_HardcodedSecret_SqlInjection_PathTraversal_ServiceLocator_CaptiveDI_And_StaticDict()
    {
        const string code = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        namespace Demo;
        public class Repo { }
        public class ReportSvc { public ReportSvc(Repo r) { } }
        public class Registry
        {
            public void AddScoped<T1, T2>() { }
            public void AddSingleton<T1, T2>() { }
        }
        public class Hazard
        {
            private const string GatewayApiKey = "SECRET-123";
            private static readonly Dictionary<int, decimal> Cache = new();
            private readonly IServiceProvider _sp;
            public Hazard(IServiceProvider sp) => _sp = sp;

            public void Run(string customerId, string fileName, Registry reg)
            {
                var sql = "SELECT * FROM T WHERE Id = '" + customerId + "'";
                File.WriteAllText(Path.Combine("/tmp", fileName), sql);
                var r = _sp.GetService(typeof(Repo));
                Cache[1] = 2m;
                reg.AddScoped<Repo, Repo>();
                reg.AddSingleton<ReportSvc, ReportSvc>();
            }
        }
        """;
        var m = Analyze(code).First(f => f.Method == "Run");
        Assert.NotNull(m.Risks);
        Assert.Contains(m.Risks!, r => r.Contains("Hardcoded secret") && r.Contains("Line 14"));
        Assert.Contains(m.Risks!, r => r.Contains("Dynamic SQL") && r.Contains("Line 21"));
        Assert.Contains(m.Risks!, r => r.Contains("Path.Combine") && r.Contains("Line 22"));
        Assert.Contains(m.Risks!, r => r.Contains("Service locator") && r.Contains("Line 23"));
        Assert.Contains(m.Risks!, r => r.Contains("static non-thread-safe collection") && r.Contains("Line 24"));
        Assert.Contains(m.Risks!, r => r.Contains("Captive dependency") && r.Contains("Line 26"));
        Assert.Contains("Demo.Hazard.Run", m.ResolvedSymbols);
        Assert.Contains("Demo.ReportSvc", m.ResolvedSymbols);
    }

    [Fact]
    public void Detects_PathTraversal_When_Parameter_Is_Transformed_Or_Interpolated()
    {
        const string code = """
        using System.IO;
        namespace Demo;
        public class PathHazard
        {
            private readonly string _root;
            public PathHazard(string root) => _root = root;
            public void Export(string fileName)
            {
                var full = Path.Combine(_root, fileName.Trim());
                File.WriteAllText(full, "data");
            }
        }
        """;
        var m = Analyze(code).First(f => f.Method == "Export");
        Assert.NotNull(m.Risks);
        Assert.Contains(m.Risks!, r => r.Contains("Path.Combine") && r.Contains("fileName"));
    }

    [Fact]
    public void Detects_Unawaited_Task_When_Discarded_With_Underscore()
    {
        const string code = """
        using System.Threading.Tasks;
        namespace Demo;
        public class FireDiscard
        {
            public void Send()
            {
                _ = DoWorkAsync();
            }
            private Task DoWorkAsync() => Task.CompletedTask;
        }
        """;
        var m = Analyze(code).First(f => f.Method == "Send");
        Assert.NotNull(m.Risks);
        Assert.Contains(m.Risks!, r => r.Contains("Unawaited Task"));
    }

    [Fact]
    public void Detects_SqlInjection_When_Assigned_To_CommandText()
    {
        const string code = """
        namespace Demo;
        public class DbHazard
        {
            public void Exec(string input)
            {
                var cmd = new FakeCmd();
                cmd.CommandText = "SELECT * FROM Users WHERE Name = '" + input + "'";
            }
        }
        public class FakeCmd { public string CommandText { get; set; } = ""; }
        """;
        var m = Analyze(code).First(f => f.Method == "Exec");
        Assert.NotNull(m.Risks);
        Assert.Contains(m.Risks!, r => r.Contains("Dynamic SQL"));
    }
}
