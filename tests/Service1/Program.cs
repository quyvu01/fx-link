using FxLink.Abstractions;
using FxLink.Contexts;
using FxLink.Extensions;
using FxLink.StateMachine.Extensions;
using Serilog;
using Service1.Dtos;
using Service1.StateMachines;
using Service1.StateMachines.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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
    opts.UseInMemory();
    opts.AddStateMachines(c =>
    {
        c.Of<OrderStateMachine>(cfg =>
        {
            cfg.InMemoryRepository();
        });
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/placeOrder", async (IPublisher publisher, ILogger<Program> logger) =>
    {
        var orderId = Guid.NewGuid();
        await publisher.PublishAsync(new OrderPlaced { OrderId = orderId, OrderTime = DateTime.UtcNow },
            new PublisherContext(Guid.NewGuid(), []) { Delay = TimeSpan.FromSeconds(3) });
        logger.LogInformation("Order placed: {@OrderId}", orderId);
        return "Order placed";
    })
    .WithOpenApi();

app.MapPost("/orderCreated", async (IPublisher publisher, int randomNumber) =>
    {
        var newOrderId = Guid.NewGuid();
        await publisher.PublishAsync(new OrderCreated
            { OrderId = newOrderId, RandomNumber = randomNumber, OrderName = "Some order name" });
        return newOrderId;
    })
    .WithOpenApi();

app.MapPost("/reactiveOrder", async (IPublisher publisher, Guid orderId) =>
    {
        await publisher.PublishAsync(new OrderReactivated { OrderId = orderId });
        return "Order reactivated";
    })
    .WithOpenApi();


app.MapGet("/getOrder", async (IRequest<OrderResult> request, CancellationToken token) =>
    {
        var id = Guid.Parse("c5143803-5477-47b4-8d4f-236cb4b09af9");
        var result = await request.RequestAsync<OrderResultResponse>(new OrderResult { OrderId = id }, token);
        return result;
    })
    .WithOpenApi();

app.MapGet("/getOrderStats", async (IRequest<GetOrderStats> request, Guid orderId, CancellationToken token) =>
    {
        var result = await request.RequestAsync<OrderStatsResponse>(new GetOrderStats { OrderId = orderId }, token);
        return result;
    })
    .WithOpenApi();

app.Run();