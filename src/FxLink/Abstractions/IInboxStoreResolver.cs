namespace FxLink.Abstractions;

public interface IInboxStoreResolver<TMessage> where TMessage : class
{
    IInboxStore GetInboxStore();
}
