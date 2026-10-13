using FxLink.Abstractions;
using FxLink.Contexts;

namespace FxLink.IntegrationTests.Fixtures;

public sealed class EchoConsumer : IConsumer<EchoRequest>
{
    public async Task ConsumeAsync(IConsumeContext<EchoRequest> context, CancellationToken token = default) =>
        await context.ResponseAsync(new EchoResponse { Value = context.Message.Value }, token);
}
