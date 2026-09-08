namespace FxLink.Abstractions;

public interface IOutboxStoreResolver<TMessage> where TMessage : class
{
    IOutboxStore GetOutboxStore();
}
