namespace FxLink.Contexts;

internal interface IInternalContext
{
    IContext Context { get; }
    void SetContext(IContext context);
}