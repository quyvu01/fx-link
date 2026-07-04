using FxLink.StateMachine.Abstractions;

namespace FxLink.StateMachine.Extensions;

internal static class ActivityExtensions
{
    public static void SetName(this IActivity activity, string name) => activity.Name = name;
}