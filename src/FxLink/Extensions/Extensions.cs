namespace FxLink.Extensions;

internal static class Extensions
{
    internal static void ForEach<T>(this IEnumerable<T> src, Action<T> action)
    {
        foreach (var item in src ?? []) action?.Invoke(item);
    }
}