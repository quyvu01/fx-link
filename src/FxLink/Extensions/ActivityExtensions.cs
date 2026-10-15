using FxLink.Abstractions;

namespace FxLink.Extensions;

internal static class ActivityExtensions
{
    extension(IActivity activity)
    {
        public void SetName(string name) => activity.Name = name;
    }
}
