using Microsoft.CodeAnalysis;
namespace CodeTurtleEngine.Gatekeeper;

internal static class SymbolEvidence
{
    public static bool IsResolved(ISymbol symbol) => symbol switch
    {
        IArrayTypeSymbol array => IsResolved(array.ElementType),
        IPointerTypeSymbol pointer => IsResolved(pointer.PointedAtType),
        INamedTypeSymbol type => type.TypeKind != TypeKind.Error && type.TypeArguments.All(IsResolved),
        ITypeSymbol type => type.TypeKind != TypeKind.Error,
        IMethodSymbol method => IsResolved(method.ReturnType) && method.Parameters.All(p => IsResolved(p.Type)),
        IPropertySymbol property => IsResolved(property.Type) && property.Parameters.All(p => IsResolved(p.Type)),
        IFieldSymbol field => IsResolved(field.Type),
        IEventSymbol evt => IsResolved(evt.Type),
        _ => true
    };
}
