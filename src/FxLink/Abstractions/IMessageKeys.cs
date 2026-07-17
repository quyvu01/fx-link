namespace FxLink.Abstractions;

public interface IMessageKeys
{
    void AddMessageKey(Type messageType, Type messageKey);
    Type[] GetKeysByMessageType(Type messageType);
    IReadOnlyDictionary<Type, Type[]> GetMessageKeys();
}