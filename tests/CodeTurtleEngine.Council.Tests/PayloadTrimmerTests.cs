using CodeTurtleEngine.Core;
using CodeTurtleEngine.Council;

namespace CodeTurtleEngine.Council.Tests;

public class PayloadTrimmerTests
{
    private static CouncilOptions TinyCaps() => new()
    {
        MaxSymbolsPerMethod = 2,
        MaxAllocationsPerMethod = 1,
        MaxMethodsPerFile = 3,
        MaxFiles = 2,
        MaxFindingsPerPersona = 5
    };

    private static MethodFacts Method(string name, int symbols, int allocations) => new(
        name,
        Enumerable.Range(0, allocations).Select(i => $"Alloc{i}").ToList(),
        "danger",
        new[] { "IService" },
        Enumerable.Range(0, symbols).Select(i => $"{name}.Sym{i}").ToList());

    private static RoslynPayload Sample() => new("Slug", "main", new[]
    {
        new FileFacts("A.cs", Enumerable.Range(0, 5).Select(i => Method($"AM{i}", 5, 4)).ToList()),
        new FileFacts("B.cs", new[] { Method("B0", 5, 4) }),
        new FileFacts("C.cs", new[] { Method("C0", 5, 4) })
    });

    [Fact]
    public void Trims_Files_Methods_Symbols_Allocations_To_Caps()
    {
        var trimmed = PayloadTrimmer.TrimForPrompt(Sample(), TinyCaps());

        Assert.Equal(2, trimmed.Files.Count);
        Assert.Equal(3, trimmed.Files[0].Methods.Count);
        var m = trimmed.Files[0].Methods[0];
        Assert.Equal(2, m.ResolvedSymbols.Count);
        Assert.Single(m.Allocations);
    }

    [Fact]
    public void Preserves_Slug_Baseline_And_Untouched_Facts()
    {
        var trimmed = PayloadTrimmer.TrimForPrompt(Sample(), TinyCaps());

        Assert.Equal("Slug", trimmed.RepoSlug);
        Assert.Equal("main", trimmed.DiffBaseline);
        var m = trimmed.Files[1].Methods[0];
        Assert.Equal("B0", m.Method);
        Assert.Equal("danger", m.AsyncHealth);
        Assert.Equal(new[] { "IService" }, m.Dependencies);
    }

    [Fact]
    public void Does_Not_Mutate_Original()
    {
        var full = Sample();
        _ = PayloadTrimmer.TrimForPrompt(full, TinyCaps());

        Assert.Equal(3, full.Files.Count);
        Assert.Equal(5, full.Files[0].Methods.Count);
        Assert.Equal(5, full.Files[0].Methods[0].ResolvedSymbols.Count);
        Assert.Equal(4, full.Files[0].Methods[0].Allocations.Count);
    }

    [Fact]
    public void Empty_Payload_Trims_To_Empty()
    {
        var empty = new RoslynPayload("S", "b", Array.Empty<FileFacts>());
        var trimmed = PayloadTrimmer.TrimForPrompt(empty, TinyCaps());
        Assert.Empty(trimmed.Files);
    }
}
