namespace FxLink.Core.Extensions;

/// <summary>
/// Provides extension methods for Type reflection and introspection.
/// </summary>
/// <remarks>
/// These utilities support the FxMap framework's reflection-based property discovery
/// and type analysis capabilities.
/// </remarks>
public static class TypeExtensions
{
    internal static bool IsClosedConcreteType(this Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false };
}