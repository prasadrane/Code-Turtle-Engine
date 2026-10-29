using CodeTurtleEngine.Core;
using Microsoft.CodeAnalysis;

namespace CodeTurtleEngine.Gatekeeper;

public sealed record ApiBreakingChange(
    string DiagnosticId,
    string SymbolFqn,
    string Description,
    Severity Severity = Severity.Error);

public static class PublicApiDiffer
{
    public static IReadOnlyList<ApiBreakingChange> Diff(Compilation baseCompilation, Compilation headCompilation)
    {
        var changes = new List<ApiBreakingChange>();
        var baseTypes = GetExportedTypes(baseCompilation.Assembly.GlobalNamespace, baseCompilation.Assembly);
        var headTypes = GetExportedTypes(headCompilation.Assembly.GlobalNamespace, headCompilation.Assembly);

        foreach (var (fqn, baseType) in baseTypes)
        {
            if (!headTypes.TryGetValue(fqn, out var headType))
            {
                changes.Add(new ApiBreakingChange("CP0001", fqn, $"Type '{fqn}' was removed or made non-public."));
                continue;
            }

            // Check members
            var baseMembers = GetExportedMembers(baseType);
            var headMembers = GetExportedMembers(headType);

            foreach (var (memFqn, baseMember) in baseMembers)
            {
                if (baseMember is IMethodSymbol baseMethod)
                {
                    var matchingMethod = headMembers.Values
                        .OfType<IMethodSymbol>()
                        .FirstOrDefault(m => m.Name == baseMethod.Name && ParametersMatch(baseMethod, m));

                    if (matchingMethod is null)
                    {
                        changes.Add(new ApiBreakingChange("CP0002", memFqn, $"Member '{memFqn}' was removed or made non-public."));
                    }
                    else if (baseMethod.ReturnType.ToDisplayString() != matchingMethod.ReturnType.ToDisplayString())
                    {
                        changes.Add(new ApiBreakingChange("CP0003", memFqn,
                            $"Member '{memFqn}' return type changed from '{baseMethod.ReturnType.ToDisplayString()}' to '{matchingMethod.ReturnType.ToDisplayString()}'."));
                    }
                }
                else if (!headMembers.ContainsKey(memFqn))
                {
                    changes.Add(new ApiBreakingChange("CP0002", memFqn, $"Member '{memFqn}' was removed or made non-public."));
                }
            }
        }

        return changes;
    }

    private static bool ParametersMatch(IMethodSymbol a, IMethodSymbol b)
    {
        if (a.Parameters.Length != b.Parameters.Length) return false;
        for (int i = 0; i < a.Parameters.Length; i++)
        {
            if (a.Parameters[i].Type.ToDisplayString() != b.Parameters[i].Type.ToDisplayString())
                return false;
        }
        return true;
    }

    private static Dictionary<string, INamedTypeSymbol> GetExportedTypes(INamespaceSymbol ns, IAssemblySymbol targetAssembly)
    {
        var dict = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        CollectTypes(ns, targetAssembly, dict);
        return dict;
    }

    private static void CollectTypes(INamespaceOrTypeSymbol container, IAssemblySymbol targetAssembly, Dictionary<string, INamedTypeSymbol> dict)
    {
        foreach (var member in container.GetMembers())
        {
            if (member is INamespaceSymbol subNs)
            {
                CollectTypes(subNs, targetAssembly, dict);
            }
            else if (member is INamedTypeSymbol type)
            {
                if (SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, targetAssembly) && IsExported(type.DeclaredAccessibility))
                {
                    dict[type.ToDisplayString()] = type;
                    CollectTypes(type, targetAssembly, dict);
                }
            }
        }
    }

    private static Dictionary<string, ISymbol> GetExportedMembers(INamedTypeSymbol type)
    {
        var dict = new Dictionary<string, ISymbol>(StringComparer.Ordinal);
        foreach (var member in type.GetMembers())
        {
            if (member is IMethodSymbol method && (method.MethodKind == MethodKind.Constructor || method.MethodKind == MethodKind.StaticConstructor))
                continue;
            if (IsExported(member.DeclaredAccessibility))
            {
                var fqn = $"{type.ToDisplayString()}.{member.Name}";
                dict[fqn] = member;
            }
        }
        return dict;
    }

    private static bool IsExported(Accessibility acc) =>
        acc is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;
}
