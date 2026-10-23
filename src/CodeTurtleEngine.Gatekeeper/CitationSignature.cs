using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeTurtleEngine.Gatekeeper;

/// <summary>Conservative declaration-signature comparison; never an invocation binder.</summary>
internal static class CitationSignature
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["bool"] = "System.Boolean", ["byte"] = "System.Byte", ["sbyte"] = "System.SByte",
        ["char"] = "System.Char", ["decimal"] = "System.Decimal", ["double"] = "System.Double",
        ["float"] = "System.Single", ["int"] = "System.Int32", ["uint"] = "System.UInt32",
        ["long"] = "System.Int64", ["ulong"] = "System.UInt64", ["short"] = "System.Int16",
        ["ushort"] = "System.UInt16", ["object"] = "System.Object", ["string"] = "System.String",
        ["nint"] = "System.IntPtr", ["nuint"] = "System.UIntPtr", ["void"] = "System.Void"
    };

    public static bool Matches(IMethodSymbol method, INamedTypeSymbol ownerType, string owner, string member, string signature)
    {
        if (!signature.EndsWith(')') || signature[1..^1].Contains('(') || signature[1..^1].Contains(')')
            || SyntaxFactory.ParseTokens(signature).Any(t => t.IsKind(SyntaxKind.DelegateKeyword))) return false;
        // Constructed generic owners/methods require substitution/binding beyond this contract.
        // Do not silently erase their arguments and claim an exact signature.
        if (owner.Contains('<') && Canonical(owner) != Canonical(ownerType.ToDisplayString())) return false;
        if (member.Contains('<'))
        {
            var declared = method.Name + "<" + string.Join(",", method.TypeParameters.Select(p => p.Name)) + ">";
            if (Canonical(member) != Canonical(declared)) return false;
        }
        else if (method.Arity != 0) return false;

        var supplied = SplitParameters(signature[1..^1]);
        if (supplied is null || supplied.Count != method.Parameters.Length) return false;
        for (var i = 0; i < supplied.Count; i++)
        {
            var parameter = method.Parameters[i];
            if (parameter.RefKind is not (RefKind.None or RefKind.Ref or RefKind.Out or RefKind.In)) return false;
            var prefix = parameter.RefKind switch
            {
                RefKind.Ref => "ref ", RefKind.Out => "out ", RefKind.In => "in ",
                _ => ""
            };
            if (Canonical(supplied[i]) != Canonical(prefix + parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                return false;
        }
        return true;
    }

    private static List<string>? SplitParameters(string value)
    {
        if (value.Length == 0) return [];
        var result = new List<string>();
        int depth = 0, start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is '<' or '[') depth++;
            if (value[i] is '>' or ']') depth--;
            if (depth < 0) return null;
            if (value[i] == ',' && depth == 0) { result.Add(value[start..i].Trim()); start = i + 1; }
        }
        result.Add(value[start..].Trim());
        return depth != 0 || result.Any(string.IsNullOrEmpty) ? null : result;
    }

    private static string Canonical(string value)
    {
        // Tokenization preserves identifier boundaries: "ref int" cannot equal "refint".
        var tokens = SyntaxFactory.ParseTokens(value).Where(t => !t.IsKind(SyntaxKind.EndOfFileToken))
            .Select(t => Aliases.TryGetValue(t.Text, out var full) ? full : t.Text).ToList();
        return string.Join("|", tokens).Replace("global|::|", "", StringComparison.Ordinal)
            .Replace("System.", "System|.|", StringComparison.Ordinal);
    }
}
