using Contracts.Messages;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Extensions;
using FxLink.RabbitMq.Extensions;
using Microsoft.OpenApi.Models;
using Order.Dtos.Orders;
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

    opts.AddRabbitMq(config => config.Host("localhost", "fxlink"));
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
        logger.LogInformation("Order placed: {@OrderId}", orderId);
        return "Order placed";
    })
    .WithTags("Plain pub/sub")
    .WithSummary("Publish OrderPlaced (no state machine, IPublisher -> IConsumer)")
    .WithOpenApi();

app.MapGet("/orders/result", async (IRequester<OrderResult> requester, Guid orderId, CancellationToken token) =>
    {
        var result = await requester.RequestAsync<OrderResultResponse>(new OrderResult { OrderId = orderId },
            RequestContext.New(timeout: TimeSpan.FromSeconds(5)), token);
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
            new ChargePayment { OrderId = orderId, Amount = amount },
            RequestContext.New(timeout: TimeSpan.FromSeconds(5)), token);
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

app.Run();