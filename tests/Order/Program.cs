using Contracts.Messages;
using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.RabbitMq.Extensions;
using FxLink.RoutingSlip.Extensions;
using Microsoft.OpenApi.Models;
using Order.Dtos.Batches;
using Order.Dtos.MessageDefinitions;
using Order.Dtos.Orders;
using Order.TestRoutingSlip.Activities;
using Order.TestRoutingSlip.Contracts;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FxLink Order Sample",
        Version = "v1",
        Description = "Order service: plain pub/sub, raw request/reply, and cross-service messaging " +
                      "with the Payment service. The state machine demo lives in the StateMachine service."
    });
});
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog((services, lc) => lc
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddFxLink(opts =>
{
    opts.AddConsumersFromAssemblies(typeof(Program).Assembly);

    opts.AddConsumerDefinitionsFromAssemblies(typeof(Program).Assembly);

    opts.AddMessageDefinitionsFromAssemblies(typeof(Program).Assembly);

    opts.AddRabbitMq(config =>
    {
        config.Host("localhost", "fxlink");
        config.PrefetchCount(1);
        config.ConcurrentMessageLimit(1);
    });

    opts.AddRoutingSlip(cfg => cfg
        .AddActivity<ReserveInventoryActivity>()
        .AddActivity<DynamicActivity>(new Uri("queue:reverse-inventory-args"))
        .AddActivity<AddOrderActivity>()
        .AddActivity<ChargeOrderPaymentActivity>()
        .AddActivity<ConfirmOrderActivity>()
        .AddActivity<NotifyCustomerActivity>()
    );
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// ----- Plain pub/sub (no state machine) -----

app.MapPost("/orders/place", async (IPublisher publisher, ILogger<Program> logger) =>
    {
        var orderId = Guid.NewGuid();
        await publisher.PublishAsync(new OrderPlaced { OrderId = orderId, OrderTime = DateTime.UtcNow });
        logger.LogInformation("[API/Publisher] Order placed: {@OrderId}", orderId);
        return "Order placed";
    })
    .WithTags("Plain pub/sub")
    .WithSummary("Publish OrderPlaced (no state machine, IPublisher -> IConsumer)")
    .WithOpenApi();

// app.MapPost("/orders/created", async (IPublisher publisher, ILogger<Program> logger) =>
//     {
//         await publisher.PublishAsync<IOrderCreated>(new { OrderId = "1123", Price = 123 });
//         logger.LogInformation("[API/Publisher] Order order created");
//         return "Order created";
//     })
//     .WithOpenApi();

app.MapPost("/orders/get-test", async (IRequester<IOrderCreated> requester) =>
    {
        var result = await requester
            .RequestAsync<IOrderResponse>(new { OrderId = "1123", Price = 123 });
        return result;
    })
    .WithOpenApi();

app.MapGet("/orders/result", async (IRequester<OrderResult> requester, Guid orderId, CancellationToken token) =>
    {
        var result = await requester
            .RequestAsync<OrderResultResponse>(new OrderResult { OrderId = orderId }, token);
        return result;
    })
    .WithTags("Plain pub/sub")
    .WithSummary("Raw IRequester<T> request/reply (no state machine)")
    .WithOpenApi();

// ----- Cross-service: Order -> Payment -----

app.MapPost("/orders/{orderId:guid}/charge", async (IRequester<ChargePayment> requester, Guid orderId,
        decimal amount, CancellationToken token) =>
    {
        var result = await requester.RequestAsync<PaymentResult>(
            new ChargePayment { OrderId = orderId, Amount = amount }, token);
        return result;
    })
    .WithTags("Payment")
    .WithSummary("Cross-service request/reply (Order -> Payment). amount <= 0 exercises the " +
                 "Payment-side retry -> dead-letter path and this call times out.")
    .WithOpenApi();

app.MapPost("/orders/{orderId:guid}/refund", async (IPublisher publisher, Guid orderId, decimal amount) =>
    {
        await publisher.PublishAsync(new PaymentRefundRequested { OrderId = orderId, Amount = amount });
        return "Refund requested";
    })
    .WithTags("Payment")
    .WithSummary("Cross-service plain pub/sub (Order -> Payment), fire and forget")
    .WithOpenApi();

app.MapPost("/test-routing-slip", async (IPublisher publisher, string name = "test-order",
        bool isFaultSimulation = false) =>
    {
        await publisher.PublishAsync<IActiveRoutingSlip>(new { Name = name, IsFaultSimulation = isFaultSimulation });
        return $"Routing slip started for '{name}' (isFaultSimulation={isFaultSimulation})";
    })
    .WithTags("RoutingSlip")
    .WithSummary("5-step order saga: ReserveInventory -> AddOrder -> ChargeOrderPayment -> ConfirmOrder -> " +
                 "NotifyCustomer. isFaultSimulation=true faults at ConfirmOrder, triggering compensate back " +
                 "through ChargeOrderPayment -> AddOrder -> ReserveInventory (NotifyCustomer never runs).")
    .WithOpenApi();

app.MapPost("/calendar/created", async (IPublisher publisher, Guid id, string name) =>
    {
        await publisher.PublishAsync<ICalendarCreated>(new { Id = id, Name = name });
        return "Calendar created";
    })
    .WithTags("Calendar")
    .WithOpenApi();

app.MapPost("/batch/test", async (IPublisher publisher) =>
    {
        var random = new Random();
        var next = random.Next(3);
        await publisher.PublishAsync<IInventoryCreated>(new { Name = $"SomeName: {next}", RandomNumber = next }, c =>
        {
            c.Headers.Set("token", $"Current tick: {DateTime.UtcNow.Ticks}");
        });
        return $"IInventoryCreated with random: {next}";
    })
    .WithTags("Batch consumer")
    .WithOpenApi();

app.Run();