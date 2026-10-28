using System.Collections.Frozen;
using System.Text.RegularExpressions;
using FxLink.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Implementations;

// Maps the message-type name a transport reads off a delivery (RabbitMQ BasicProperties.Type, the
// SQS/NATS/Service Bus "MessageType" attribute/header) back to a CLR type — but only to one of the
// message types this process actually consumes (IMessageKeys). That name comes from whoever could
// publish to the broker, so it must never reach Type.GetType(): that would let any publisher make
// this process load an arbitrary type/assembly by name, and a per-name cache of the results would
// grow without bound on random names. Unknown names resolve to null (unroutable) instead.
//
// Accepted names per consumed message type:
// - its AssemblyQualifiedName, compared without Version/Culture/PublicKeyToken, so a sender built
//   against a different version of the shared contracts assembly still matches;
// - its custom name from IMessageDefinition (Name(...)), so a producer that doesn't share the .NET
//   type at all (another stack, raw JSON) can address it.
internal sealed class MessageTypeResolver(IMessageKeys messageKeys, IServiceProvider serviceProvider)
{
    private static readonly Regex AssemblyVersionDetails =
        new(@",\s*(?:Version|Culture|PublicKeyToken)=[^,\]]*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Built on first use rather than in the constructor: IMessageKeys is complete only once AddFxLink has
    // finished registering consumers, and this is resolved (lazily, as a singleton) after that.
    private readonly Lazy<FrozenDictionary<string, Type>> _typesByName =
        new(() => Build(messageKeys, serviceProvider));

    public Type Resolve(string messageTypeName) =>
        messageTypeName is { Length: > 0 } &&
        (_typesByName.Value.TryGetValue(messageTypeName, out var type) ||
         _typesByName.Value.TryGetValue(Normalize(messageTypeName), out type))
            ? type
            : null;

    internal static string Normalize(string assemblyQualifiedName) =>
        AssemblyVersionDetails.Replace(assemblyQualifiedName, string.Empty);

    private static FrozenDictionary<string, Type> Build(IMessageKeys messageKeys, IServiceProvider serviceProvider)
    {
        var typesByName = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var messageType in messageKeys.GetMessageKeys().Keys)
        {
            if (messageType.AssemblyQualifiedName is { } assemblyQualifiedName)
                typesByName.TryAdd(Normalize(assemblyQualifiedName), messageType);

            var definition = serviceProvider.GetService(typeof(IMessageDefinition<>).MakeGenericType(messageType));
            if (definition is IMessageDefinition { MessageConfigurator: var configurator } &&
                configurator.GetName() is { Length: > 0 } customName)
                typesByName.TryAdd(customName, messageType);
        }

        return typesByName.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
