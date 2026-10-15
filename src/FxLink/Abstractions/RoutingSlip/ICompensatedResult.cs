namespace FxLink.Abstractions;

public interface ICompensatedResult
{
    bool IsCompensated { get; }
    Exception Exception { get; }
}
