namespace FxLink.Abstractions;

internal interface IMessageKeys
{
    void AddMessageKey(Type messageType, Type messageKey);
    Type[] GetKeysByMessageType(Type messageType);
    IReadOnlyDictionary<Type, Type[]> GetMessageKeys();
}