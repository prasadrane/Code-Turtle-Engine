using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeTurtleEngine.Gatekeeper;

public sealed class TurtleSyntaxWalker : CSharpSyntaxWalker
{
    public List<MethodDeclarationSyntax> Methods { get; } = new();

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        Methods.Add(node);
        base.VisitMethodDeclaration(node);
    }
}
