using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeTurtleEngine.Gatekeeper;

public sealed record WalkedMember(string Name, SyntaxNode Node, int StartLine, int EndLine);

public sealed class TurtleSyntaxWalker : CSharpSyntaxWalker
{
    public List<MethodDeclarationSyntax> Methods { get; } = new();
    public List<WalkedMember> Members { get; } = new();

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        Methods.Add(node);
        AddMember(node.Identifier.Text, node);
        base.VisitMethodDeclaration(node);
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        AddMember(node.Identifier.Text, node);
        base.VisitConstructorDeclaration(node);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        var hasLogic = node.ExpressionBody is not null
            || (node.AccessorList?.Accessors.Any(a => a.Body is not null || a.ExpressionBody is not null) ?? false);
        if (hasLogic)
            AddMember(node.Identifier.Text, node);
        base.VisitPropertyDeclaration(node);
    }

    private void AddMember(string name, SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();
        var start = span.StartLinePosition.Line + 1;
        var end = span.EndLinePosition.Line + 1;
        Members.Add(new WalkedMember(name, node, start, end));
    }
}
