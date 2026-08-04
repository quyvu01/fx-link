using System.Data;
using System.Reflection;
using FxLink.Abstractions;
using FxLink.Extensions;
using FxLink.RabbitMq.Extensions;
using FxLink.StateMachine.EntityFrameworkCore.Extensions;
using FxLink.StateMachine.EntityFrameworkCore.Registries;
using FxLink.StateMachine.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using StateMachine.Databases;
using StateMachine.Dtos.Inventory;
using StateMachine.StateMachines.Inventory;
using StateMachine.Tests;
using StateMachine.Tests.Events;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FxLink StateMachine Sample",
        Version = "v1",
        Description = "StateMachine service: InventoryReservationStateMachine, the single state " +
                      "machine in this sample. Covers the full EventOperator DSL under Pessimistic " +
                      "concurrency (advisory-lock correlation, every OnMissingInstance variant, " +
                      "requests, schedules, activities, and branching)."
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

builder.Services.AddDbContextPool<StateMachineDbContext>(options =>
{
    options.UseNpgsql("Host=localhost;Username=postgres;Password=Abcd@2021;Database=FxLinkStateMachine", b =>
    {
        b.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name);
        b.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
    });
});

builder.Services.AddFxLink(opts =>
{
    opts.AddConsumersFromAssemblies(typeof(Program).Assembly);

    opts.AddConsumerDefinitionsFromAssemblies(typeof(Program).Assembly);

    opts.AddRabbitMq(config => { config.Host("localhost", "fxlink"); });
    opts.UseRabbitMqDelayScheduler();

    opts.AddStateMachines(c =>
    {
        c.AddActivitiesFromAssemblies(typeof(Program).Assembly);

        c.Of<InventoryReservationStateMachine>(cfg =>
        {
            cfg.EntityFrameworkRepository(config =>
            {
                config.SetIsolationLevel(IsolationLevel.ReadCommitted);
                config.UseConcurrencyMode(x => x.Pessimistic(SqlDialect.PostgreSql));
                config.DbContextFactory(sp => sp.GetRequiredService<StateMachineDbContext>());
            });
        });
        c.Of<SagaStateMachine>(cfg => { cfg.InMemoryRepository(); });
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// ----- InventoryReservationStateMachine -----

app.MapPost("/inventory/reserve", async (IPublisher publisher, Guid orderId, string sku, int quantity) =>
    {
        await publisher.PublishAsync(new ReserveInventory { OrderId = orderId, Sku = sku, Quantity = quantity });
        return "Inventory reservation requested";
    })
    .WithTags("Inventory")
    .WithSummary("Reserve inventory for an order (creates the reservation instance)")
    .WithOpenApi();

app.MapPost("/inventory/release", async (IPublisher publisher, Guid orderId, string sku, int quantity) =>
    {
        await publisher.PublishAsync(new ReleaseInventory { OrderId = orderId, Sku = sku, Quantity = quantity });
        return "Inventory release requested";
    })
    .WithTags("Inventory")
    .WithSummary("Release a reservation (missing instance -> Fault(); quantity >= reserved -> full " +
                 "release and Complete(), otherwise a partial release)")
    .WithOpenApi();

app.MapPost("/inventory/confirm", async (IPublisher publisher, Guid orderId) =>
    {
        await publisher.PublishAsync(new ConfirmInventory { OrderId = orderId });
        return "Inventory confirmation requested";
    })
    .WithTags("Inventory")
    .WithSummary("Confirm a reservation (missing instance -> ExecuteAsync()). Triggers a " +
                 "Request/RequestAsync to WarehouseConsumer; sku \"OUT_OF_STOCK\" cancels the " +
                 "reservation, sku \"FAIL\" exercises the Failed path.")
    .WithOpenApi();

app.MapPost("/inventory/cancel-schedule", async (IPublisher publisher, Guid orderId) =>
    {
        await publisher.PublishAsync(new CancelSchedule { OrderId = orderId });
        return "Inventory Schedule cancelled!";
    })
    .WithTags("Inventory")
    .WithSummary("Cancel the pending inventory schedule token for an order (missing instance -> Discard())")
    .WithOpenApi();

app.MapPost("/inventory/{orderId:guid}/adjust-stock", async (IPublisher publisher, Guid orderId, int newQuantity) =>
    {
        await publisher.PublishAsync(new AdjustStock { OrderId = orderId, NewQuantity = newQuantity });
        return "Stock adjustment requested";
    })
    .WithTags("Inventory")
    .WithSummary("Adjust the reserved quantity (IfAsync guard + message-typed Activity)")
    .WithOpenApi();

app.MapGet("/inventory/{orderId:guid}/stats", async (IRequester<GetReservationStats> requester, Guid orderId,
        CancellationToken token) =>
    {
        var result = await requester.RequestAsync<ReservationStatsResponse>(
            new GetReservationStats { OrderId = orderId },
            token);
        return result;
    })
    .WithTags("Inventory")
    .WithSummary("Query reservation stats (During(Reserved, Confirmed))")
    .WithOpenApi();

app.MapGet("/inventory/{orderId:guid}/summary", async (IRequester<GetReservationSummary> requester, Guid orderId,
        CancellationToken token) =>
    {
        var result = await requester.RequestAsync<ReservationSummaryResponse>(
            new GetReservationSummary { OrderId = orderId },
            token);
        return result;
    })
    .WithTags("Inventory")
    .WithSummary("Query reservation summary from any state (DuringAny)")
    .WithOpenApi();

app.MapPost("/test-request-reply", async (IPublisher publisher, CancellationToken token) =>
    {
        await publisher.PublishAsync<IInitTest>(new { Name = "SomeName" }, token);
        return "Publisher Request Reply";
    })
    .WithTags("Inventory")
    .WithSummary("Query reservation summary from any state (DuringAny)")
    .WithOpenApi();

app.Run();