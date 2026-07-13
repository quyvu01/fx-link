namespace FxLink.Extensions;

/// <summary>
/// Provides extension methods for Type reflection and introspection.
/// </summary>
/// <remarks>
/// These utilities support the FxMap framework's reflection-based property discovery
/// and type analysis capabilities.
/// </remarks>
public static class TypeExtensions
{
    extension(Type type)
    {
        public bool IsClosedConcreteType() =>
            type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false };

        public Type GetGenericBaseType(Type genericTypeDefinition)
        {
            var current = type;
            while (current != null && current != typeof(object))
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == genericTypeDefinition)
                    return current;
                current = current.BaseType;
            }

            return null;
        }
    }
}