namespace FxLink.Contexts;

public interface IContextPayload
{
    T GetPayload<T>();
    void SetPayload<T>(T payload);
}