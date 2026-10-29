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

            var baseMembers = GetExportedMembers(baseType);
            var headMembers = GetExportedMembers(headType);

            foreach (var (memKey, (memFqn, baseMember)) in baseMembers)
            {
                if (!headMembers.TryGetValue(memKey, out var headEntry))
                {
                    changes.Add(new ApiBreakingChange("CP0002", memFqn, $"Member '{memFqn}' was removed or made non-public."));
                    continue;
                }

                var headMember = headEntry.Symbol;
                if (baseMember is IMethodSymbol baseMethod && headMember is IMethodSymbol headMethod)
                {
                    if (baseMethod.MethodKind != MethodKind.Constructor &&
                        baseMethod.ReturnType.ToDisplayString() != headMethod.ReturnType.ToDisplayString())
                    {
                        changes.Add(new ApiBreakingChange("CP0003", memFqn,
                            $"Member '{memFqn}' return type changed from '{baseMethod.ReturnType.ToDisplayString()}' to '{headMethod.ReturnType.ToDisplayString()}'."));
                    }
                }
            }
        }

        return changes;
    }

    private static string GetMemberKey(ISymbol member)
    {
        if (member is IMethodSymbol method)
        {
            var paramTypes = string.Join(", ", method.Parameters.Select(FormatParameter));
            var typeArgs = method.TypeParameters.Length > 0
                ? $"<{string.Join(", ", method.TypeParameters.Select(p => p.Name))}>"
                : "";
            var name = method.MethodKind == MethodKind.Constructor ? "#ctor" : method.Name;
            return $"{name}{typeArgs}({paramTypes})";
        }
        if (member is IPropertySymbol prop && prop.Parameters.Length > 0)
        {
            var paramTypes = string.Join(", ", prop.Parameters.Select(FormatParameter));
            return $"{prop.Name}[{paramTypes}]";
        }
        return member.Name;
    }

    private static string FormatParameter(IParameterSymbol p)
    {
        var prefix = p.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            RefKind.RefReadOnlyParameter => "ref readonly ",
            _ => ""
        };
        return $"{prefix}{p.Type.ToDisplayString()}";
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

    private static Dictionary<string, (string Fqn, ISymbol Symbol)> GetExportedMembers(INamedTypeSymbol type)
    {
        var dict = new Dictionary<string, (string Fqn, ISymbol Symbol)>(StringComparer.Ordinal);
        foreach (var member in type.GetMembers())
        {
            if (member is IMethodSymbol method && method.MethodKind == MethodKind.StaticConstructor)
                continue;
            if (IsExported(member.DeclaredAccessibility))
            {
                var key = GetMemberKey(member);
                var fqn = $"{type.ToDisplayString()}.{member.Name}";
                dict[key] = (fqn, member);
            }
        }
        return dict;
    }

    private static bool IsExported(Accessibility acc) =>
        acc is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;
}
