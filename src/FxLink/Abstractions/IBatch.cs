using FxLink.Contexts;

namespace FxLink.Abstractions;

public interface IBatch<out T> : IEnumerable<IConsumeContext<T>> where T : class
{
    IConsumeContext<T> this[int index] { get; }
    int Length { get; }
}