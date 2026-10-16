using System.Text;
using Microsoft.CodeAnalysis;

namespace CodeTurtleEngine.Gatekeeper;

/// <summary>
/// Benchmark oracle: does a cited fully-qualified name refer to a type or member that exists
/// in the compilation (source or references)? Broader than the guard allow-list, which holds
/// only the types a changed method touches. Accepts common display variants: global:: prefix,
/// parameter lists, generic arguments, C# keyword aliases, nullable and array suffixes.
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

    private readonly Compilation _compilation;

    public SymbolOracle(Compilation compilation) => _compilation = compilation;

    public bool Exists(string fqn)
    {
        var name = Normalize(fqn);
        if (name.Length == 0) return false;
        if (ResolveType(name) is not null) return true;

        if (name.EndsWith("..ctor", StringComparison.Ordinal))
        {
            var t = ResolveType(name[..^"..ctor".Length]);
            if (t is not null && t.Constructors.Length > 0) return true;
        }
        else if (name.EndsWith(".ctor", StringComparison.Ordinal))
        {
            var t = ResolveType(name[..^".ctor".Length]);
            if (t is not null && t.Constructors.Length > 0) return true;
        }

        var dot = LastTopLevelDot(name);
        if (dot <= 0) return false;
        var type = ResolveType(name[..dot]);
        if (type is null) return false;

        var member = name[(dot + 1)..];
        var angle = member.IndexOf('<');
        if (angle >= 0) member = member[..angle];
        return HasMember(type, member);
    }

    private static bool HasMember(INamedTypeSymbol type, string member)
    {
        var simple = type.Name;
        if (string.Equals(member, simple, StringComparison.Ordinal) && type.Constructors.Length > 0) return true;
        for (INamedTypeSymbol? t = type; t is not null; t = t.BaseType)
            if (t.GetMembers(member).Length > 0) return true;
        foreach (var i in type.AllInterfaces)
            if (i.GetMembers(member).Length > 0) return true;
        return false;
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
        var all = _compilation.GetTypesByMetadataName(metadataName);
        return all.Length > 0 ? all[0] : null;
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
