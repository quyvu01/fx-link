using FxLink.Abstractions;
using FxLink.Contexts;

namespace Order.Dtos.MessageDefinitions;

public sealed class CalendarConsumers(ILogger<CalendarConsumers> logger) : IConsumer<ICalendarCreated>
{
    public Task ConsumeAsync(IConsumerContext<ICalendarCreated> context, CancellationToken token = default)
    {
        logger.LogInformation("[ICalendarCreated] message: {@Message}", context.Message);
        return Task.CompletedTask;
    }
}