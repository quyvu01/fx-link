using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using FxLink.Exceptions;

namespace FxLink.Serialization;

/// <summary>
/// Hydrates an instance of <typeparamref name="TMessage"/> — whether an interface message
/// contract (backed by <see cref="MessageContractTypeFactory"/>) or a plain concrete class with a
/// public parameterless constructor. Two input shapes are supported:
/// <list type="bullet">
/// <item>an arbitrary object (an anonymous type, a DTO, another message, etc.) — same-named
/// readable properties are copied;</item>
/// <item>an <see cref="IDictionary{TKey,TValue}"/> of <see cref="string"/> to <see cref="object"/>
/// — treated as a loosely-typed property bag, matching each entry's key against a property name.</item>
/// </list>
/// Beyond an exact type match, a value is also copied when:
/// <list type="bullet">
/// <item>the target and value both implement <see cref="IConvertible"/> (numeric widening/
/// narrowing, string-to-number, etc.) — converted via <see cref="Convert.ChangeType(object,Type)"/>.
/// Types that don't implement <see cref="IConvertible"/> (<see cref="Guid"/>, <see cref="Uri"/>,
/// <see cref="TimeSpan"/>, ...) are out of scope for now;</item>
/// <item>the target is itself a nested message contract (interface, or class with a public
/// parameterless constructor) — hydrated recursively the same way as the top-level value;</item>
/// <item>the target is a <see cref="Dictionary{TKey,TValue}"/>/<see cref="IDictionary{TKey,TValue}"/>/
/// <see cref="IReadOnlyDictionary{TKey,TValue}"/> and the value is any <see cref="IDictionary"/> —
/// entries are copied key-by-key, converting values (including nested contracts) the same way;</item>
/// <item>the target is an array/<see cref="IEnumerable{T}"/>/<see cref="IReadOnlyList{T}"/>/
/// <see cref="IReadOnlyCollection{T}"/>/<see cref="ICollection{T}"/>/<see cref="IList{T}"/>/
/// <see cref="List{T}"/> and the value is any <see cref="IEnumerable"/> (other than
/// <see cref="string"/>) — each element is copied directly if assignable, or recursively converted
/// the same way as a nested value.</item>
/// </list>
/// Anything else (mismatched value types, unsupported shapes, dictionary keys that don't match the
/// target key type) is silently skipped, not an error; this is a static reflection utility with no
/// logger/DI access, used from both PublisherImpl and the JSON converter factory. Not guarded
/// against cyclic object graphs — a self-referencing source object would recurse until the stack
/// overflows, same as MassTransit's own initializer chain.
/// </summary>
internal static class MessageContractActivator
{
    private static readonly ConcurrentDictionary<(Type MessageType, Type ValuesType), PropertyCopyPlan> Plans = new();

    private static readonly ConcurrentDictionary<Type, (Func<object> Factory, Dictionary<string, PropertyInfo> TargetProperties)>
        PropertyBagPlans = new();

    public static TMessage CreateFrom<TMessage>(object values) where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(values);
        return (TMessage)CreateFrom(typeof(TMessage), values);
    }

    private static object CreateFrom(Type messageType, object values)
    {
        if (values is IDictionary<string, object> propertyBag)
            return CreateFromPropertyBag(messageType, propertyBag);

        var plan = Plans.GetOrAdd((messageType, values.GetType()),
            static key => BuildPlan(key.MessageType, key.ValuesType));

        var instance = plan.CreateInstance();
        foreach (var copy in plan.PropertyCopies) copy(instance, values);
        return instance;
    }

    private static object CreateFromPropertyBag(Type messageType, IDictionary<string, object> propertyBag)
    {
        var (factory, targetProperties) = PropertyBagPlans.GetOrAdd(messageType, static type =>
        {
            var (implementationType, typeFactory) = type.IsInterface ? BuildInterfaceFactory(type) : BuildConcreteFactory(type);
            var properties = implementationType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            return (typeFactory, properties);
        });

        var instance = factory();
        foreach (var (key, value) in propertyBag)
        {
            if (!targetProperties.TryGetValue(key, out var targetProperty)) continue;
            if (TryConvertValue(targetProperty.PropertyType, value, out var converted))
                targetProperty.SetValue(instance, converted);
        }

        return instance;
    }

    private static PropertyCopyPlan BuildPlan(Type messageType, Type valuesType)
    {
        var (implementationType, factory) = messageType.IsInterface
            ? BuildInterfaceFactory(messageType)
            : BuildConcreteFactory(messageType);

        // For an interface TMessage, PropertyInfo must come from the GENERATED implementation
        // type, not the interface — the interface's own PropertyInfo.GetSetMethod() is always
        // null (no setter declared); only the emitted proxy has a real settable property.
        var targetProperties = implementationType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var copies = new List<Action<object, object>>();
        foreach (var sourceProperty in valuesType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!sourceProperty.CanRead) continue;
            if (!targetProperties.TryGetValue(sourceProperty.Name, out var targetProperty)) continue;

            copies.Add(BuildPropertyCopy(targetProperty, sourceProperty));
        }

        return new PropertyCopyPlan(factory, copies);
    }

    private static Action<object, object> BuildPropertyCopy(PropertyInfo targetProperty, PropertyInfo sourceProperty)
    {
        if (targetProperty.PropertyType == sourceProperty.PropertyType)
            return (instance, source) => targetProperty.SetValue(instance, sourceProperty.GetValue(source));

        // Beyond a static type match, conversion (dictionary/collection/nested contract) depends
        // on what the value actually is at call time, not just the source property's declared
        // type — e.g. a property declared as `object` might hold a Dictionary or anonymous object.
        var targetType = targetProperty.PropertyType;
        return (instance, source) =>
        {
            if (TryConvertValue(targetType, sourceProperty.GetValue(source), out var converted))
                targetProperty.SetValue(instance, converted);
        };
    }

    private static bool TryConvertValue(Type targetType, object value, out object converted)
    {
        if (value is null)
        {
            converted = null;
            return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            converted = value;
            return true;
        }

        if (TryConvertPrimitive(targetType, value, out converted))
            return true;

        if (TryGetDictionaryTypes(targetType, out var keyType, out var valueType) && value is IDictionary sourceDictionary)
        {
            converted = BuildDictionary(keyType, valueType, sourceDictionary);
            return true;
        }

        if (value is not string && value is IEnumerable sourceItems && TryGetCollectionElementType(targetType, out var elementType))
        {
            converted = BuildCollection(targetType, elementType, sourceItems);
            return true;
        }

        if (IsEligibleNestedContract(targetType))
        {
            converted = CreateFrom(targetType, value);
            return true;
        }

        converted = null;
        return false;
    }

    // Numeric/string/bool/char/DateTime widening-narrowing conversion via the BCL's own
    // IConvertible machinery — e.g. an anonymous object's `Price = 123` (int) into a `decimal
    // Price` property, or `Count = "5"` (string) into an `int Count` property. Deliberately not a
    // general-purpose converter: types that don't implement IConvertible (Guid, Uri, TimeSpan,
    // Version, ...) are out of scope for now and fall through unconverted.
    internal static bool TryConvertPrimitive(Type targetType, object value, out object converted)
    {
        var effectiveTargetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(effectiveTargetType))
        {
            try
            {
                converted = Convert.ChangeType(value, effectiveTargetType, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
            {
            }
        }

        converted = null;
        return false;
    }

    private static bool TryGetDictionaryTypes(Type type, out Type keyType, out Type valueType)
    {
        if (type.IsGenericType)
        {
            var genericDefinition = type.GetGenericTypeDefinition();
            if (genericDefinition == typeof(Dictionary<,>) || genericDefinition == typeof(IDictionary<,>)
                || genericDefinition == typeof(IReadOnlyDictionary<,>))
            {
                var arguments = type.GetGenericArguments();
                keyType = arguments[0];
                valueType = arguments[1];
                return true;
            }
        }

        keyType = null;
        valueType = null;
        return false;
    }

    // Always builds a concrete Dictionary<TKey,TValue> — it satisfies IDictionary<,>/
    // IReadOnlyDictionary<,> targets directly. No key-conversion support (unlike MassTransit) —
    // an entry whose key isn't already assignable to the target key type is skipped.
    private static object BuildDictionary(Type keyType, Type valueType, IDictionary sourceDictionary)
    {
        var dictionary = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, valueType))!;

        foreach (DictionaryEntry entry in sourceDictionary)
        {
            if (!keyType.IsInstanceOfType(entry.Key)) continue;
            if (TryConvertValue(valueType, entry.Value, out var value)) dictionary[entry.Key] = value;
        }

        return dictionary;
    }

    // Array, or one of the common read/write collection shapes closing over a single element type
    // — an array of the target element type satisfies all the interface cases directly (arrays
    // implement IEnumerable<T>/IReadOnlyList<T>/IReadOnlyCollection<T>/ICollection<T>/IList<T> by
    // CLR design), and List<T> is built from that array via its IEnumerable<T> constructor.
    private static bool TryGetCollectionElementType(Type collectionType, out Type elementType)
    {
        if (collectionType.IsArray)
        {
            elementType = collectionType.GetElementType()!;
            return true;
        }

        if (collectionType.IsGenericType)
        {
            var genericDefinition = collectionType.GetGenericTypeDefinition();
            if (genericDefinition == typeof(IEnumerable<>) || genericDefinition == typeof(IReadOnlyList<>)
                || genericDefinition == typeof(IReadOnlyCollection<>) || genericDefinition == typeof(ICollection<>)
                || genericDefinition == typeof(IList<>) || genericDefinition == typeof(List<>))
            {
                elementType = collectionType.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = null;
        return false;
    }

    private static object BuildCollection(Type targetCollectionType, Type targetElementType, IEnumerable sourceItems)
    {
        var sourceElements = sourceItems.Cast<object>().ToList();

        var array = Array.CreateInstance(targetElementType, sourceElements.Count);
        for (var i = 0; i < sourceElements.Count; i++)
        {
            if (TryConvertValue(targetElementType, sourceElements[i], out var converted))
                array.SetValue(converted, i);
            // else: no compatible conversion for this element — leave the default slot
        }

        if (targetCollectionType.IsArray) return array;

        return targetCollectionType.IsGenericType && targetCollectionType.GetGenericTypeDefinition() == typeof(List<>)
            ? Activator.CreateInstance(targetCollectionType, [array])!
            : array; // IEnumerable<T>/IReadOnlyList<T>/IReadOnlyCollection<T>/ICollection<T>/IList<T>
    }

    private static bool IsEligibleNestedContract(Type type) =>
        type != typeof(string) && (type.IsInterface || (type.IsClass && type.GetConstructor(Type.EmptyTypes) is not null));

    private static (Type ImplementationType, Func<object> Factory) BuildInterfaceFactory(Type interfaceType)
    {
        var implementationType = MessageContractTypeFactory.GetImplementationType(interfaceType);
        return (implementationType, () => Activator.CreateInstance(implementationType)!);
    }

    private static (Type ImplementationType, Func<object> Factory) BuildConcreteFactory(Type messageType)
    {
        var ctor = messageType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        if (ctor is null)
            throw new FxLinkException.MessageContractRequiresParameterlessConstructor(messageType);

        return (messageType, () => ctor.Invoke(null));
    }

    private sealed record PropertyCopyPlan(Func<object> CreateInstance, List<Action<object, object>> PropertyCopies);
}