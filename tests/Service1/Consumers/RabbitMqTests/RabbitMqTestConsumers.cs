// using FxLink.Abstractions;
// using FxLink.Abstractions.Contexts;
// using Service1.RabbitMqTests;
//
// namespace Service1.Consumers.RabbitMqTests;
//
// public class RabbitMqTestConsumers(ILogger<RabbitMqTestConsumers> logger) :
//     IConsumer<RabbitMqTestPublisher>,
//     IConsumer<RabbitMqTestRequest>
// {
//     public Task ConsumeAsync(IConsumerContext<RabbitMqTestPublisher> context, CancellationToken token = default)
//     {
//         logger.LogInformation("Logged message publisher: {@Message}", context.Message);
//         return Task.CompletedTask;
//     }
//
//     public async Task ConsumeAsync(IConsumerContext<RabbitMqTestRequest> context, CancellationToken token = default)
//     {
//         logger.LogInformation("Logged message requester: {@Message}", context.Message);
//         await context.ResponseAsync(new RabbitMqTestResponse { TestData = context.Message.TestData }, token);
//     }
// }