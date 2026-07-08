namespace FxLink.Abstractions;

public interface IMessageKeys
{
    void AddMessageKey(Type messageType, object messageKey);
    object[] GetKeysByMessageType(Type messageType);
    IReadOnlyDictionary<Type, object[]> GetMessageKeys();
}