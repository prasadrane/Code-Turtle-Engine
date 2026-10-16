using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeTurtleEngine.Gatekeeper;

public static class RiskDetector
{
    private static readonly string[] SecretKeywords = { "Key", "Secret", "Password", "Token", "Credential" };

    public static IReadOnlyList<string>? DetectRisks(SyntaxNode member, SemanticModel model, Compilation compilation)
    {
        var risks = new List<string>();
        var seenLines = new HashSet<string>(StringComparer.Ordinal);

        void AddRisk(string risk)
        {
            if (seenLines.Add(risk)) risks.Add(risk);
        }

        DetectUnawaitedAndSyncOverAsync(member, model, AddRisk);
        DetectSwallowedExceptions(member, AddRisk);
        DetectHardcodedSecrets(member, AddRisk);
        DetectSqlInjection(member, AddRisk);
        DetectPathTraversal(member, model, AddRisk);
        DetectServiceLocator(member, model, AddRisk);
        DetectCaptiveDependencies(member, model, AddRisk);
        DetectUnsynchronizedStaticCollections(member, model, AddRisk);

        return risks.Count > 0 ? risks : null;
    }

    private static void DetectUnawaitedAndSyncOverAsync(SyntaxNode member, SemanticModel model, Action<string> addRisk)
    {
        foreach (var stmt in member.DescendantNodes().OfType<ExpressionStatementSyntax>())
        {
            if (stmt.Expression is AwaitExpressionSyntax or AssignmentExpressionSyntax) continue;
            var type = model.GetTypeInfo(stmt.Expression).Type;
            if (type is null && stmt.Expression is InvocationExpressionSyntax inv)
                type = (model.GetSymbolInfo(inv).Symbol as IMethodSymbol)?.ReturnType;

            if (IsTaskType(type))
            {
                var symbolName = (stmt.Expression is InvocationExpressionSyntax i && model.GetSymbolInfo(i).Symbol is IMethodSymbol ms)
                    ? (ms.ContainingType is not null ? $"{ms.ContainingType.Name}.{ms.Name}" : ms.Name)
                    : stmt.Expression.ToString();
                var line = stmt.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                addRisk($"Unawaited Task (fire-and-forget) on {symbolName} (Line {line})");
            }
        }

        foreach (var ma in member.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (ma.Name.Identifier.Text == "Result" && IsTaskType(model.GetTypeInfo(ma.Expression).Type))
            {
                var line = ma.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                addRisk($"Sync-over-async blocking call 'Result' (Line {line})");
            }
        }

        foreach (var inv in member.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma && ma.Name.Identifier.Text is "Wait" or "GetResult")
            {
                var line = inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var targetType = model.GetTypeInfo(ma.Expression).Type;
                if (IsTaskType(targetType) || targetType?.Name.Contains("Awaiter") == true)
                    addRisk($"Sync-over-async blocking call '{ma.Name.Identifier.Text}' (Line {line})");
            }
        }
    }

    private static bool IsTaskType(ITypeSymbol? type)
    {
        if (type is null) return false;
        var name = type.Name;
        return name is "Task" or "ValueTask"
            || type.OriginalDefinition.ToDisplayString().StartsWith("System.Threading.Tasks.Task")
            || type.OriginalDefinition.ToDisplayString().StartsWith("System.Threading.Tasks.ValueTask");
    }

    private static void DetectSwallowedExceptions(SyntaxNode member, Action<string> addRisk)
    {
        foreach (var catchClause in member.DescendantNodes().OfType<CatchClauseSyntax>())
        {
            var block = catchClause.Block;
            var hasRethrow = block.DescendantNodes().Any(n => n is ThrowStatementSyntax or ThrowExpressionSyntax);
            var hasLog = block.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Any(inv => inv.Expression.ToString().Contains("Log", StringComparison.OrdinalIgnoreCase)
                         || inv.Expression.ToString().Contains("Write", StringComparison.OrdinalIgnoreCase)
                         || inv.Expression.ToString().Contains("Report", StringComparison.OrdinalIgnoreCase));

            if (!hasRethrow && !hasLog)
            {
                var line = catchClause.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                addRisk($"Swallowed exception in catch block without logging or rethrow (Line {line})");
            }
        }
    }

    private static void DetectHardcodedSecrets(SyntaxNode member, Action<string> addRisk)
    {
        var classDecl = member.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (classDecl is not null)
        {
            foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>())
            {
                foreach (var v in field.Declaration.Variables)
                {
                    if (IsSecretName(v.Identifier.Text) && HasStringLiteral(v.Initializer?.Value))
                    {
                        var line = v.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        addRisk($"Hardcoded secret/credential in '{v.Identifier.Text}' (Line {line})");
                    }
                }
            }
        }

        foreach (var local in member.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
        {
            foreach (var v in local.Declaration.Variables)
            {
                if (IsSecretName(v.Identifier.Text) && HasStringLiteral(v.Initializer?.Value))
                {
                    var line = v.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    addRisk($"Hardcoded secret/credential in '{v.Identifier.Text}' (Line {line})");
                }
            }
        }
    }

    private static bool IsSecretName(string name) =>
        SecretKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static bool HasStringLiteral(ExpressionSyntax? expr) =>
        expr is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression) && !string.IsNullOrEmpty(lit.Token.ValueText);

    private static void DetectSqlInjection(SyntaxNode member, Action<string> addRisk)
    {
        foreach (var binary in member.DescendantNodes().OfType<BinaryExpressionSyntax>())
        {
            if (!binary.IsKind(SyntaxKind.AddExpression)) continue;
            if (IsSqlContext(binary))
            {
                var line = binary.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                addRisk($"Dynamic SQL string concatenation/interpolation (Line {line})");
            }
        }

        foreach (var interp in member.DescendantNodes().OfType<InterpolatedStringExpressionSyntax>())
        {
            if (interp.Contents.OfType<InterpolationSyntax>().Any() && IsSqlContext(interp))
            {
                var line = interp.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                addRisk($"Dynamic SQL string concatenation/interpolation (Line {line})");
            }
        }
    }

    private static bool IsSqlContext(ExpressionSyntax expr)
    {
        var parentVar = expr.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (parentVar is not null && IsSqlName(parentVar.Identifier.Text)) return true;

        var parentAssign = expr.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault();
        if (parentAssign is not null && IsSqlName(parentAssign.Left.ToString())) return true;

        var parentArg = expr.Ancestors().OfType<ArgumentSyntax>().FirstOrDefault();
        if (parentArg?.Parent?.Parent is InvocationExpressionSyntax inv)
        {
            var methodName = inv.Expression.ToString();
            if (methodName.Contains("Query", StringComparison.OrdinalIgnoreCase)
                || methodName.Contains("Execute", StringComparison.OrdinalIgnoreCase)
                || methodName.Contains("FromSql", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsSqlName(string text) =>
        text.Contains("sql", StringComparison.OrdinalIgnoreCase) || text.Contains("query", StringComparison.OrdinalIgnoreCase);

    private static void DetectPathTraversal(SyntaxNode member, SemanticModel model, Action<string> addRisk)
    {
        foreach (var inv in member.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!inv.Expression.ToString().Contains("Path.Combine")) continue;

            foreach (var arg in inv.ArgumentList.Arguments)
            {
                var sym = model.GetSymbolInfo(arg.Expression).Symbol;
                if (sym is IParameterSymbol param)
                {
                    var hasCheck = member.DescendantNodes().OfType<InvocationExpressionSyntax>()
                        .Any(i => i.Expression.ToString().Contains("GetFullPath") || i.Expression.ToString().Contains("StartsWith"));
                    if (!hasCheck)
                    {
                        var line = inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        addRisk($"Unvalidated parameter '{param.Name}' passed to Path.Combine without traversal check (Line {line})");
                    }
                }
            }
        }
    }

    private static void DetectServiceLocator(SyntaxNode member, SemanticModel model, Action<string> addRisk)
    {
        foreach (var inv in member.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma)
            {
                var name = ma.Name.Identifier.Text;
                if (name is "GetService" or "GetRequiredService")
                {
                    var targetType = model.GetTypeInfo(ma.Expression).Type;
                    if (targetType?.Name == "IServiceProvider" || targetType?.AllInterfaces.Any(i => i.Name == "IServiceProvider") == true)
                    {
                        var line = inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        addRisk($"Service locator call '{name}' on IServiceProvider (Line {line})");
                    }
                }
            }
        }
    }

    private static void DetectCaptiveDependencies(SyntaxNode member, SemanticModel model, Action<string> addRisk)
    {
        var scopedTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var inv in member.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = (inv.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.Text;
            if (name is "AddScoped" or "AddTransient" && model.GetSymbolInfo(inv).Symbol is IMethodSymbol ms)
            {
                foreach (var ta in ms.TypeArguments) scopedTypes.Add(ta);
            }
        }

        if (scopedTypes.Count == 0) return;

        foreach (var inv in member.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = (inv.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.Text;
            if (name == "AddSingleton" && model.GetSymbolInfo(inv).Symbol is IMethodSymbol ms)
            {
                var implType = ms.TypeArguments.Length >= 2 ? ms.TypeArguments[1] : ms.TypeArguments.FirstOrDefault();
                if (implType is INamedTypeSymbol nts)
                {
                    foreach (var ctor in nts.Constructors)
                    {
                        foreach (var param in ctor.Parameters)
                        {
                            if (scopedTypes.Any(st => SymbolEqualityComparer.Default.Equals(st, param.Type) || st.Name == param.Type.Name))
                            {
                                var line = inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                                addRisk($"Captive dependency: Singleton '{implType.Name}' depends on Scoped/Transient '{param.Type.Name}' (Line {line})");
                            }
                        }
                    }
                }
            }
        }
    }

    private static void DetectUnsynchronizedStaticCollections(SyntaxNode member, SemanticModel model, Action<string> addRisk)
    {
        foreach (var node in member.DescendantNodes())
        {
            if (node is not (IdentifierNameSyntax or MemberAccessExpressionSyntax)) continue;
            var sym = model.GetSymbolInfo(node).Symbol;
            if (sym is IFieldSymbol field && field.IsStatic)
            {
                var typeName = field.Type.Name;
                if (typeName is "Dictionary" or "List" or "HashSet")
                {
                    var isLocked = node.Ancestors().OfType<LockStatementSyntax>().Any();
                    if (!isLocked)
                    {
                        var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        addRisk($"Unsynchronized access to static non-thread-safe collection '{field.Name}' of type {typeName} (Line {line})");
                    }
                }
            }
        }
    }
}
