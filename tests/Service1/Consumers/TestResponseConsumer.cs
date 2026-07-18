using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;

namespace Service1.Consumers;

public sealed class TestResponseConsumer : IConsumer<GetPerson>
{
    public async Task ConsumeAsync(IConsumerContext<GetPerson> context, CancellationToken token = default)
    {
        await context.ResponseAsync(new PersonResponse { PersonId = context.Message.PersonId }, token);
    }
}