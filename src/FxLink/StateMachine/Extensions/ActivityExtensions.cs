using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Extensions;

internal static class ActivityExtensions
{
    extension(IActivity activity)
    {
        public void SetName(string name) => activity.Name = name;
    }
}