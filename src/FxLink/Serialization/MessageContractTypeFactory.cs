using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using FxLink.Exceptions;

namespace FxLink.Serialization;

/// <summary>
/// Generates a concrete, sealed implementation type for a getter-only message contract interface
/// via Reflection.Emit — every property gets a private backing field plus a real get AND set
/// accessor (regardless of whether the interface declares a setter), so the interface can be
/// hydrated by <see cref="MessageContractActivator"/> despite exposing no setter itself.
/// Incompatible with Native AOT/trimming (Reflection.Emit requires a JIT-capable runtime).
/// </summary>
internal static class MessageContractTypeFactory
{
    private static readonly AssemblyBuilder AssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
        new AssemblyName("FxLink.DynamicMessageContracts"), AssemblyBuilderAccess.RunAndCollect);

    private static readonly ModuleBuilder ModuleBuilder =
        AssemblyBuilder.DefineDynamicModule("FxLink.DynamicMessageContracts");

    // Reflection.Emit's ModuleBuilder/TypeBuilder isn't documented thread-safe for concurrent
    // DefineType calls across different keys — a Lazy<> alone only serializes same-key access.
    // A plain object lock is used (not System.Threading.Lock) so this still compiles under net8.0.
    private static readonly object BuildLock = new();

    private static readonly ConcurrentDictionary<Type, Lazy<Type>> ImplementationTypeCache = new();

    public static Type GetImplementationType(Type interfaceType) =>
        ImplementationTypeCache.GetOrAdd(interfaceType,
            static t => new Lazy<Type>(() => BuildImplementationType(t), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    private static Type BuildImplementationType(Type interfaceType)
    {
        if (!interfaceType.IsInterface)
            throw new FxLinkException.MessageContractMustBeInterface(interfaceType);

        var properties = GetAllProperties(interfaceType);

        var nonPropertyMethodExists = interfaceType.GetInterfaces().Prepend(interfaceType)
            .SelectMany(i => i.GetMethods())
            .Any(m => !m.IsSpecialName);
        if (nonPropertyMethodExists)
            throw new FxLinkException.MessageContractMustOnlyDeclareProperties(interfaceType);

        lock (BuildLock)
        {
            var typeBuilder = ModuleBuilder.DefineType(BuildSafeTypeName(interfaceType),
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed,
                typeof(object), [interfaceType]);

            foreach (var property in properties) EmitAutoProperty(typeBuilder, property);

            return typeBuilder.CreateType();
        }
    }

    // Recurses into inherited interfaces so a multi-level contract (IOrderCreated : ICorrelated)
    // gets every property across the whole hierarchy; dedupes by name so diamond-inherited
    // properties don't get a backing field emitted twice.
    private static PropertyInfo[] GetAllProperties(Type interfaceType) =>
    [
        .. interfaceType.GetInterfaces().Prepend(interfaceType)
            .SelectMany(i => i.GetProperties())
            .GroupBy(p => p.Name)
            .Select(g => g.First())
    ];

    private static void EmitAutoProperty(TypeBuilder typeBuilder, PropertyInfo property)
    {
        var propertyType = property.PropertyType;
        var field = typeBuilder.DefineField($"_{property.Name}", propertyType, FieldAttributes.Private);
        var propertyBuilder = typeBuilder.DefineProperty(property.Name, PropertyAttributes.None, propertyType, null);

        // Public + SpecialName + HideBySig + Final + Virtual + VtableLayoutMask(NewSlot) is the
        // exact attribute combination MassTransit's shipped DynamicImplementationBuilder uses —
        // implicit interface implementation (matching get_X/set_X by name+signature) satisfies the
        // interface's getter without an explicit TypeBuilder.DefineMethodOverride call.
        const MethodAttributes accessorAttributes = MethodAttributes.Public | MethodAttributes.SpecialName
            | MethodAttributes.HideBySig | MethodAttributes.Final | MethodAttributes.Virtual
            | MethodAttributes.VtableLayoutMask;

        var getMethod = typeBuilder.DefineMethod($"get_{property.Name}", accessorAttributes, propertyType, Type.EmptyTypes);
        var getIl = getMethod.GetILGenerator();
        getIl.Emit(OpCodes.Ldarg_0);
        getIl.Emit(OpCodes.Ldfld, field);
        getIl.Emit(OpCodes.Ret);

        // Always emit a real setter regardless of whether the interface declares one — this is
        // the crux of the whole feature: MessageContractActivator sets values through this
        // implementation-type property, while callers only ever see the interface's getter.
        var setMethod = typeBuilder.DefineMethod($"set_{property.Name}", accessorAttributes, typeof(void), [propertyType]);
        var setIl = setMethod.GetILGenerator();
        setIl.Emit(OpCodes.Ldarg_0);
        setIl.Emit(OpCodes.Ldarg_1);
        setIl.Emit(OpCodes.Stfld, field);
        setIl.Emit(OpCodes.Ret);

        propertyBuilder.SetGetMethod(getMethod);
        propertyBuilder.SetSetMethod(setMethod);
    }

    private static string BuildSafeTypeName(Type interfaceType) =>
        $"FxLink.DynamicMessageContracts.{interfaceType.FullName ?? interfaceType.Name}Proxy"
            .Replace('+', '_')
            .Replace('`', '_');
}