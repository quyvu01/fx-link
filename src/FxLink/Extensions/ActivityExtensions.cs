using FxLink.Abstractions.StateMachine;

namespace FxLink.Extensions;

internal static class ActivityExtensions
{
    extension(IActivity activity)
    {
        public void SetName(string name) => activity.Name = name;
    }
}
