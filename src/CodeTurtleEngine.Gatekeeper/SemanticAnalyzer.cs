using CodeTurtleEngine.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeTurtleEngine.Gatekeeper;

public sealed class SemanticAnalyzer
{
    private readonly Compilation _compilation;

    public SemanticAnalyzer(Compilation compilation) => _compilation = compilation;

    public IReadOnlyList<MethodFacts> AnalyzeTree(SyntaxTree tree)
    {
        var model = _compilation.GetSemanticModel(tree);
        var walker = new TurtleSyntaxWalker();
        walker.Visit(tree.GetRoot());

        var facts = new List<MethodFacts>();
        foreach (var member in walker.Members)
        {
            var dependencies = ConstructorDependencies(member.Node, model);
            var allocations = DetectBoxing(member.Node, model).Concat(DetectLinqClosures(member.Node, model)).ToList();
            var asyncHealth = DetectAsyncHealth(member.Node, model);
            var resolved = ResolvedSymbols(member.Node, model);
            var risks = RiskDetector.DetectRisks(member.Node, model, _compilation);
            facts.Add(new MethodFacts(member.Name, allocations, asyncHealth, dependencies, resolved, member.StartLine, member.EndLine, risks));
        }
        return facts;
    }

    private static IReadOnlyList<string> ConstructorDependencies(SyntaxNode member, SemanticModel model)
    {
        var type = model.GetDeclaredSymbol(member)?.ContainingType;
        return type is null
            ? Array.Empty<string>()
            : type.Constructors.SelectMany(c => c.Parameters).Select(p => p.Type.Name).Distinct().ToList();
    }

    private string? DetectAsyncHealth(SyntaxNode member, SemanticModel model)
    {
        if (member is MethodDeclarationSyntax m && !m.Modifiers.Any(SyntaxKind.AsyncKeyword)) return null;
        foreach (var awaitExpr in member.DescendantNodes().OfType<AwaitExpressionSyntax>())
        {
            if (awaitExpr.Expression is InvocationExpressionSyntax inv
                && inv.Expression is MemberAccessExpressionSyntax ma
                && ma.Name.Identifier.Text == "ConfigureAwait")
                continue;

            var owner = model.GetSymbolInfo(awaitExpr.Expression).Symbol?.ContainingType?.Name ?? "task";
            var line = awaitExpr.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            return $"Missing ConfigureAwait(false) on {owner} invocation (Line {line})";
        }
        return null;
    }

    private IEnumerable<string> DetectBoxing(SyntaxNode member, SemanticModel model)
    {
        var objectType = _compilation.GetSpecialType(SpecialType.System_Object);
        var seen = new HashSet<int>();
        foreach (var expr in member.DescendantNodes().OfType<ExpressionSyntax>())
        {
            var info = model.GetTypeInfo(expr);
            if (info.Type is { IsValueType: true }
                && (SymbolEqualityComparer.Default.Equals(info.ConvertedType, objectType)
                    || info.ConvertedType?.TypeKind == TypeKind.Interface))
            {
                var line = expr.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                if (seen.Add(line)) yield return $"Implicit Boxing (Line {line})";
            }
        }
    }

    private static IEnumerable<string> DetectLinqClosures(SyntaxNode member, SemanticModel model)
    {
        foreach (var inv in member.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(inv).Symbol is not IMethodSymbol sym) continue;
            if (sym.ContainingNamespace?.ToDisplayString() != "System.Linq") continue;

            var lambda = inv.ArgumentList.Arguments.Select(a => a.Expression)
                .OfType<LambdaExpressionSyntax>().FirstOrDefault();
            if (lambda is null || !CapturesLocal(lambda, model)) continue;

            var line = inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            yield return $"LINQ Closure (Line {line})";
        }
    }

    private static bool CapturesLocal(LambdaExpressionSyntax lambda, SemanticModel model) =>
        lambda.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Any(id => model.GetSymbolInfo(id).Symbol is ILocalSymbol or IParameterSymbol or IFieldSymbol);

    private static IReadOnlyList<string> ResolvedSymbols(SyntaxNode member, SemanticModel model)
    {
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        var declaredSym = model.GetDeclaredSymbol(member);
        var type = declaredSym?.ContainingType;
        if (type is not null)
        {
            symbols.Add(type.ToDisplayString());
            if (declaredSym is not null)
                symbols.Add($"{type.ToDisplayString()}.{declaredSym.Name}");

            foreach (var ctor in type.Constructors)
                foreach (var p in ctor.Parameters)
                    if (p.Type is not null) symbols.Add(p.Type.ToDisplayString());

            foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
                if (field.Type is not null) symbols.Add(field.Type.ToDisplayString());
        }

        foreach (var inv in member.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(inv).Symbol is IMethodSymbol m)
            {
                if (m.ContainingType is not null)
                {
                    symbols.Add(m.ContainingType.ToDisplayString());
                    symbols.Add($"{m.ContainingType.ToDisplayString()}.{m.Name}");
                }
                foreach (var ta in m.TypeArguments)
                    symbols.Add(ta.ToDisplayString());
            }
        }

        foreach (var ma in member.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            var sym = model.GetSymbolInfo(ma).Symbol;
            if (sym?.ContainingType is not null)
            {
                symbols.Add(sym.ContainingType.ToDisplayString());
                symbols.Add($"{sym.ContainingType.ToDisplayString()}.{sym.Name}");
            }
        }

        return symbols.Where(s => !string.IsNullOrWhiteSpace(s))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
    }
}
