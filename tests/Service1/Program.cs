using System.Data;
using System.Reflection;
using FxLink.Abstractions;
using FxLink.Abstractions.Contexts;
using FxLink.Extensions;
using FxLink.RabbitMq.Extensions;
using FxLink.Registries;
using FxLink.StateMachine.EntityFrameworkCore.Extensions;
using FxLink.StateMachine.EntityFrameworkCore.Registries;
using FxLink.StateMachine.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using Service1.Databases;
using Service1.Dtos.Inventory;
using Service1.Dtos.Orders;
using Service1.RabbitMqTests;
using Service1.StateMachines.Inventory;
using Service1.StateMachines.Orders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FxLink Service1 Sample",
        Version = "v1",
        Description = "Sample endpoints exercising FxLink, FxLink.StateMachine and " +
                      "FxLink.StateMachine.EntityFrameworkCore (Orders = Optimistic concurrency, " +
                      "Inventory = Pessimistic concurrency)."
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

builder.Services.AddDbContextPool<AppDbContext>(options =>
{
    options.UseNpgsql("Host=localhost;Username=postgres;Password=Abcd@2021;Database=FxLinkService1", b =>
    {
        b.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name);
        b.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
    });
});

builder.Services.AddFxLink(opts =>
{
    opts.AddConsumersFromAssemblies(typeof(Program).Assembly);

    opts.AddConsumerDefinitionsFromAssemblies(typeof(Program).Assembly);
    
    opts.AddRabbitMq(config => config.Host("localhost", "/"));

    opts.AddStateMachines(c =>
    {
        c.AddActivitiesFromAssemblies(typeof(Program).Assembly);

        c.Of<OrderStateMachine>(cfg =>
        {
            cfg.EntityFrameworkRepository(config =>
            {
                config.UseConcurrencyMode(x => x.Optimistic());
                config.AddDbContext<AppDbContext>();
            });
        });

        c.Of<InventoryReservationStateMachine>(cfg =>
        {
            cfg.EntityFrameworkRepository(config =>
            {
                config.SetIsolationLevel(IsolationLevel.ReadCommitted);
                config.UseConcurrencyMode(x => x.Pessimistic(SqlDialect.PostgreSql));
                config.DbContextFactory(sp => sp.GetRequiredService<AppDbContext>());
            });
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

if (app.Environment.IsDevelopment())
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// ----- Plain pub/sub (no state machine) -----

app.MapPost("/orders/place", async (IPublisher publisher, ILogger<Program> logger) =>
    {
        var orderId = Guid.NewGuid();
        // IPublisherContext.Delay used directly (Schedule(...) is the state-machine sugar for this).
        await publisher.PublishAsync(new OrderPlaced { OrderId = orderId, OrderTime = DateTime.UtcNow },
            new PublisherContext(Guid.NewGuid(), []) { Delay = TimeSpan.FromSeconds(3) });
        logger.LogInformation("Order placed: {@OrderId}", orderId);
        return "Order placed";
    })
    .WithTags("Plain pub/sub")
    .WithSummary("Publish OrderPlaced with a 3s delay (IPublisherContext.Delay, no state machine)")
    .WithOpenApi();

app.MapGet("/orders/result", async (IRequester<OrderResult> requester, Guid orderId, CancellationToken token) =>
    {
        var result = await requester.RequestAsync<OrderResultResponse>(new OrderResult { OrderId = orderId },
            new RequestContext(Guid.NewGuid(), []) { Timeout = TimeSpan.FromSeconds(5) }, token);
        return result;
    })
    .WithTags("Plain pub/sub")
    .WithSummary("Raw IRequester<T> request/reply (no state machine)")
    .WithOpenApi();

// ----- OrderStateMachine -----

app.MapPost("/orders/create", async (IPublisher publisher, Guid? orderId, int randomNumber,
        string orderName = "Some order name") =>
    {
        var newOrderId = orderId ?? Guid.NewGuid();
        await publisher.PublishAsync(new OrderCreated
            { OrderId = newOrderId, RandomNumber = randomNumber, OrderName = orderName });
        return newOrderId;
    })
    .WithTags("Orders")
    .WithSummary("Create an order (randomNumber > 3 succeeds, < 0 forces the history request to fail)")
    .WithOpenApi();

app.MapPost("/orders/{orderId:guid}/cancel", async (IPublisher publisher, Guid orderId) =>
    {
        await publisher.PublishAsync(new OrderCancelled { OrderId = orderId, CancelledTime = DateTime.UtcNow });
        return "Order cancellation requested";
    })
    .WithTags("Orders")
    .WithSummary("Cancel an order directly")
    .WithOpenApi();

app.MapPost("/orders/{orderId:guid}/reactivate", async (IPublisher publisher, Guid orderId) =>
    {
        await publisher.PublishAsync(new OrderReactivated { OrderId = orderId });
        return "Order reactivation requested";
    })
    .WithTags("Orders")
    .WithSummary("Reactivate a cancelled order")
    .WithOpenApi();

app.MapGet("/orders/{orderId:guid}/stats", async (IRequester<GetOrderStats> requester, Guid orderId,
        CancellationToken token) =>
    {
        var result = await requester.RequestAsync<OrderStatsResponse>(new GetOrderStats { OrderId = orderId }, token);
        return result;
    })
    .WithTags("Orders")
    .WithSummary("Query order stats (During(OrderCreated, OrderCancelled))")
    .WithOpenApi();

app.MapGet("/orders/{orderId:guid}/summary", async (IRequester<GetOrderSummary> requester, Guid orderId,
        CancellationToken token) =>
    {
        var result =
            await requester.RequestAsync<OrderSummaryResponse>(new GetOrderSummary { OrderId = orderId }, token);
        return result;
    })
    .WithTags("Orders")
    .WithSummary("Query order summary from any state (DuringAny)")
    .WithOpenApi();

// ----- InventoryReservationStateMachine -----

app.MapPost("/inventory/reserve", async (IPublisher publisher, Guid orderId, string sku, int quantity) =>
    {
        await publisher.PublishAsync(new ReserveInventory { OrderId = orderId, Sku = sku, Quantity = quantity });
        return "Inventory reservation requested";
    })
    .WithTags("Inventory")
    .WithSummary("Reserve inventory for an order (creates the reservation instance)")
    .WithOpenApi();

app.MapPost("/inventory/release", async (IPublisher publisher, Guid orderId, string sku) =>
    {
        await publisher.PublishAsync(new ReleaseInventory { OrderId = orderId, Sku = sku });
        return "Inventory release requested";
    })
    .WithTags("Inventory")
    .WithSummary("Release a reservation (missing instance -> Fault())")
    .WithOpenApi();

app.MapPost("/inventory/confirm", async (IPublisher publisher, Guid orderId) =>
    {
        await publisher.PublishAsync(new ConfirmInventory { OrderId = orderId });
        return "Inventory confirmation requested";
    })
    .WithTags("Inventory")
    .WithSummary("Confirm a reservation (missing instance -> ExecuteAsync())")
    .WithOpenApi();

app.MapPost("/rabbitmq/test-publish", async (IPublisher publisher) =>
    {
        await publisher.PublishAsync(new RabbitMqTestPublisher
            { TestData = $"Test at: {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss}" });
        return "RabbitMq test publish";
    })
    .WithTags("Rabbitmq")
    .WithOpenApi();

app.MapPost("/rabbitmq/test-request", async (IRequester<RabbitMqTestRequest> requester) =>
    {
        var result = await requester.RequestAsync<RabbitMqTestResponse>(new RabbitMqTestRequest
            { TestData = $"Test request at: {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss}" });
        return result;
    })
    .WithTags("Rabbitmq")
    .WithOpenApi();

app.Run();