using FxLink.Abstractions.Contexts;

namespace FxLink.Extensions;

public static class HeaderExtensions
{
    public static IHeaders With(this IHeaders headers, string key, object value)
    {
        headers.Set(key, value);
        return headers;
    }
}
