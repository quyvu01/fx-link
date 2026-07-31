namespace FxLink.Abstractions.Contexts;

internal interface IInternalContext
{
    IContext Context { get; }
    void SetContext(IContext context);
}