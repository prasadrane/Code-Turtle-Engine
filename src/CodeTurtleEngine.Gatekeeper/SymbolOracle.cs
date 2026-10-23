using System.Text;
using CodeTurtleEngine.Core;
using Microsoft.CodeAnalysis;

namespace CodeTurtleEngine.Gatekeeper;

/// <summary>
/// Benchmark oracle: does a cited fully-qualified name refer to a type or member that exists
/// in the compilation (source or references)? Broader than the guard allow-list, which holds
/// only the types a changed method touches. Accepts common display variants: global:: prefix,
/// generic arguments, C# keyword aliases, nullable and array suffixes.
/// Explicit parameter lists are matched conservatively against declaration signatures.
/// Members are resolved on the type, its base-type chain and all implemented interfaces; a member
/// named like its type resolves to a constructor. Namespaces are not symbols: only types and
/// members count as existing.
/// </summary>
public sealed class SymbolOracle
{
    private static readonly Dictionary<string, string> Keywords = new(StringComparer.Ordinal)
    {
        ["bool"] = "System.Boolean", ["byte"] = "System.Byte", ["sbyte"] = "System.SByte",
        ["char"] = "System.Char", ["decimal"] = "System.Decimal", ["double"] = "System.Double",
        ["float"] = "System.Single", ["int"] = "System.Int32", ["uint"] = "System.UInt32",
        ["long"] = "System.Int64", ["ulong"] = "System.UInt64", ["short"] = "System.Int16",
        ["ushort"] = "System.UInt16", ["object"] = "System.Object", ["string"] = "System.String",
        ["nint"] = "System.IntPtr", ["nuint"] = "System.UIntPtr"
    };

    private readonly IReadOnlyList<Compilation> _compilations;

    public SymbolOracle(Compilation compilation) : this(new[] { compilation }) { }

    public SymbolOracle(IReadOnlyList<Compilation> compilations) => _compilations = compilations;

    public bool Exists(string fqn) => Resolve(fqn).Exists;

    public CitationResolution Resolve(string fqn)
    {
        var raw = fqn.Trim();
        var paren = raw.IndexOf('(');
        var signature = paren >= 0;
        // Suffix normalization is only valid for name-only type displays. Applying
        // it to an explicit member head would accept malformed Run?(int)/Run[](int).
        var name = signature ? raw[..paren].Trim() : Normalize(raw);
        if (signature && name.StartsWith("global::", StringComparison.Ordinal)) name = name[8..];
        var rejected = new CitationResolution(false, "Rejected", []);
        if (name.Length == 0) return rejected;
        if (!signature && ResolveType(name) is { } namedType)
            return Evidence([namedType], "NameOnly");

        INamedTypeSymbol? type;
        string member;
        if (name.EndsWith("..ctor", StringComparison.Ordinal))
        {
            type = ResolveType(name[..^"..ctor".Length]);
            member = ".ctor";
        }
        else
        {
            var dot = LastTopLevelDot(name);
            if (dot <= 0) return rejected;
            type = ResolveType(name[..dot]);
            member = name[(dot + 1)..];
        }
        if (type is null) return rejected;
        var members = Members(type, member).Where(SymbolEvidence.IsResolved).ToList();
        if (signature)
        {
            // Declaration matching only: no invocation binding or generic argument inference.
            var owner = name.EndsWith("..ctor", StringComparison.Ordinal)
                ? name[..^"..ctor".Length] : name[..LastTopLevelDot(name)];
            members = members.OfType<IMethodSymbol>()
                .Where(m => CitationSignature.Matches(m, type, owner, member, raw[paren..])).Cast<ISymbol>().ToList();
        }
        return members.Count == 0 ? rejected : Evidence(members, signature ? "Signature" : "NameOnly");
    }

    private static CitationResolution Evidence(IEnumerable<ISymbol> symbols, string match)
    {
        var identities = symbols.Select(s => new CitationSymbol(
            s.ContainingAssembly?.Identity.ToString() ?? "", s.GetDocumentationCommentId() ?? "",
            s.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)))
            .Distinct().OrderBy(s => s.Assembly, StringComparer.Ordinal)
            .ThenBy(s => s.DocumentationId, StringComparer.Ordinal).ToList();
        return new(true, match, identities);
    }

    private static IEnumerable<ISymbol> Members(INamedTypeSymbol type, string member)
    {
        var angle = member.IndexOf('<');
        var simpleMember = angle < 0 ? member : member[..angle];
        if (simpleMember == type.Name || simpleMember is ".ctor" or "ctor")
            return type.Constructors;
        var members = new List<ISymbol>();
        for (INamedTypeSymbol? t = type; t is not null; t = t.BaseType)
            members.AddRange(t.GetMembers(simpleMember));
        members.AddRange(type.AllInterfaces.SelectMany(i => i.GetMembers(simpleMember)));
        return members;
    }

    private static string Normalize(string fqn)
    {
        var s = fqn.Trim();
        if (s.StartsWith("global::", StringComparison.Ordinal)) s = s["global::".Length..];
        var paren = s.IndexOf('(');
        if (paren >= 0) s = s[..paren];
        while (s.EndsWith("[]", StringComparison.Ordinal)) s = s[..^2];
        s = s.TrimEnd('?');
        return Keywords.TryGetValue(s, out var full) ? full : s;
    }

    private INamedTypeSymbol? ResolveType(string name)
    {
        var metadata = ToMetadataName(Keywords.TryGetValue(name, out var full) ? full : name);
        var found = First(metadata);
        if (found is not null) return found;

        // Nested types: Demo.Outer.Inner -> Demo.Outer+Inner (try each split point).
        var parts = metadata.Split('.');
        for (var i = parts.Length - 1; i > 0; i--)
        {
            found = First(string.Join('.', parts[..i]) + "+" + string.Join('+', parts[i..]));
            if (found is not null) return found;
        }
        return null;
    }

    private INamedTypeSymbol? First(string metadataName)
    {
        var all = _compilations.SelectMany(c => c.GetTypesByMetadataName(metadataName))
            .Where(t => SymbolEvidence.IsResolved(t)).ToList();
        var source = all.Where(t => t.Locations.Any(l => l.IsInSource))
            .Distinct(SymbolEqualityComparer.Default).ToList();
        if (source.Count > 1) return null;
        // Same referenced declaration seen in several projects is one identity. Distinct
        // assemblies with the same name are ambiguous without an assembly-qualified citation.
        var identities = all.GroupBy(t => (t.ContainingAssembly.Identity.ToString(), t.GetDocumentationCommentId())).ToList();
        return identities.Count == 1 ? identities[0].First() : null;
    }

    // "System.Collections.Generic.Dictionary<string, int>" -> "System.Collections.Generic.Dictionary`2"
    private static string ToMetadataName(string name)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (name[i] != '<') { sb.Append(name[i]); continue; }
            int depth = 1, args = 1;
            for (i++; i < name.Length && depth > 0; i++)
            {
                if (name[i] == '<') depth++;
                else if (name[i] == '>') depth--;
                else if (name[i] == ',' && depth == 1) args++;
            }
            i--; // the outer loop's i++ lands just past the closing '>'
            sb.Append('`').Append(args);
        }
        return sb.ToString();
    }

    private static int LastTopLevelDot(string name)
    {
        var depth = 0;
        for (var i = name.Length - 1; i >= 0; i--)
        {
            if (name[i] == '>') depth++;
            else if (name[i] == '<') depth--;
            else if (name[i] == '.' && depth == 0) return i;
        }
        return -1;
    }
}
