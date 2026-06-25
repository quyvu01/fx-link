namespace FxLink.Abstractions;

public interface IMessageKeys
{
    void AddMessageKey(Type messageType, object messageKey);
    object[] GetMessageKeys(Type messageType);
}