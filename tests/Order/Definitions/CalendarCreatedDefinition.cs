using FxLink.Abstractions;
using FxLink.Registries;
using Order.Dtos.MessageDefinitions;

namespace Order.Definitions;

public sealed class CalendarCreatedDefinition : MessageDefinition<ICalendarCreated>
{
    public override void Configure(IMessageConfigurator<ICalendarCreated> options)
    {
        options.Name("test.calendar.exchange.name");
    }
}